namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Middlewares
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AADAuth;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Monitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;

    [ExcludeFromCodeCoverage]
    internal class AADTokenAuthMiddleware
    {
        private static readonly ActivityMonitorFactory AADTokenAuthMiddlewareInvoke = new ("AADTokenAuthMiddleware.Invoke");

        private static readonly NoPartnerInRequestException _noPartnerInRequestException = new(ResourceFetcherAuthError.NO_PARTNER_IN_REQUEST.FastEnumToString());
        private static readonly NoTokenInHeaderException _noTokenInHeaderException = new(ResourceFetcherAuthError.NO_TOKEN_IN_HEADER.FastEnumToString());
        private static readonly AADTokenAuthFailException _aadTokenAuthFailException = new(ResourceFetcherAuthError.AAD_TOKEN_AUTH_FAIL.FastEnumToString());

        private readonly RequestDelegate _next;
        private readonly IAADTokenAuthenticator _aadTokenAuthenticator;
        private readonly IPartnerAuthorizeManager _partnerAuthorizeManager;

        public AADTokenAuthMiddleware(RequestDelegate next,
            IAADTokenAuthenticator aadTokenAuthenticator,
            IPartnerAuthorizeManager partnerAuthorizeManager)
        {
            GuardHelper.ArgumentNotNull(next);
            GuardHelper.ArgumentNotNull(aadTokenAuthenticator);
            GuardHelper.ArgumentNotNull(partnerAuthorizeManager);

            _next = next;
            _aadTokenAuthenticator = aadTokenAuthenticator;
            _partnerAuthorizeManager = partnerAuthorizeManager;
        }

        public Task Invoke(HttpContext context)
        {
            // There is no reason to log success cases every time in this Middleware. So we will do log only failed case
            // But metric will be always published
            
            string? partnerName = null;
            string? parentActivityId = null;
            string? inputCorrelationId = null;
            string? retryFlowCount = null;
            string? clientRequestId = null;

            var requestHeader = context.Request.Headers;

            if (requestHeader.TryGetValue(CommonHttpHeaders.DataLabs_PartnerName, out var headerValue))
            {
                partnerName = headerValue;
            }

            if (requestHeader.TryGetValue(CommonHttpHeaders.DataLabs_OpenTelemetry_ActivityId, out headerValue))
            {
                parentActivityId = headerValue;
            }

            if (requestHeader.TryGetValue(CommonHttpHeaders.DataLabs_InputCorrelationId, out headerValue))
            {
                inputCorrelationId = headerValue;
            }

            if (requestHeader.TryGetValue(CommonHttpHeaders.DataLabs_RetryFlowCount, out headerValue))
            {
                retryFlowCount = headerValue;
            }

            if (requestHeader.TryGetValue(CommonHttpHeaders.ClientRequestId, out headerValue))
            {
                clientRequestId = headerValue;
            }

            using var activity = new OpenTelemetryActivityWrapper(
                source: ResourceFetcherMetricProvider.ResourceFetcherActivitySource,
                name: ResourceFetcherConstants.ActivityName_AADTokenAuthMiddleware,
                kind: ActivityKind.Server,
                parentId: parentActivityId);

            using var monitor = AADTokenAuthMiddlewareInvoke.ToMonitor();

            try
            {
                activity.InputCorrelationId = inputCorrelationId;
                monitor.Activity.CorrelationId = inputCorrelationId;

                activity.SetTag(SolutionConstants.PartnerName, partnerName);
                activity.SetTag(SolutionConstants.RetryCount, retryFlowCount);
                activity.SetTag(SolutionConstants.ClientRequestId, clientRequestId);

                monitor.OnStart(false);
                
                if (string.IsNullOrWhiteSpace(partnerName))
                {
                    ResourceFetcherMetricProvider.AddMiddleWareAuthErrorCounter(null, ResourceFetcherAuthError.NO_PARTNER_IN_REQUEST);
                    ResourceFetcherUtils.SetAuthError(context, ResourceFetcherAuthError.NO_PARTNER_IN_REQUEST);

                    activity.SetStatus(ActivityStatusCode.Error, ResourceFetcherAuthError.NO_PARTNER_IN_REQUEST.FastEnumToString());
                    
                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(_noPartnerInRequestException);
                    return Task.CompletedTask;
                }

                // Compare Partner Name with one in config
                var partnerAuthorizeConfig = _partnerAuthorizeManager.GetPartnerAuthorizeConfig(partnerName);
                if (partnerAuthorizeConfig == null)
                {
                    ResourceFetcherMetricProvider.AddMiddleWareAuthErrorCounter(partnerName, ResourceFetcherAuthError.NOT_ALLOWED_PARTNER);
                    ResourceFetcherUtils.SetAuthError(context, ResourceFetcherAuthError.NOT_ALLOWED_PARTNER);

                    activity.SetStatus(ActivityStatusCode.Error, "NotAllowedPartner: " + partnerName);
                    
                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(ResourceFetcherConstants.NotAllowedPartnerException);
                    return Task.CompletedTask;
                }

                // Validate AAD Token
                var authHeader = context.Request.Headers[CommonHttpHeaders.Authorization];
                var token = SolutionUtils.ParseBearerAuthorizationHeader(authHeader);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ResourceFetcherMetricProvider.AddMiddleWareAuthErrorCounter(partnerName, ResourceFetcherAuthError.NO_TOKEN_IN_HEADER);
                    ResourceFetcherUtils.SetAuthError(context, ResourceFetcherAuthError.NO_TOKEN_IN_HEADER);

                    activity.SetStatus(ActivityStatusCode.Error, ResourceFetcherAuthError.NO_TOKEN_IN_HEADER.FastEnumToString());

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(_noTokenInHeaderException);
                    return Task.CompletedTask;
                }

                if (!_aadTokenAuthenticator.Authenticate(token, out var authenticatedInfo) || !authenticatedInfo.IsSuccess)
                {
                    var isTokenExpiredException = authenticatedInfo.Exception == AADTokenAuthenticator.expiredTokenException;
                    var authError = isTokenExpiredException ? ResourceFetcherAuthError.AAD_TOKEN_EXPIRED : ResourceFetcherAuthError.AAD_TOKEN_AUTH_FAIL;

                    // For security, don't include more detail(exception from AAD Authenticator) error message in http response
                    ResourceFetcherMetricProvider.AddMiddleWareAuthErrorCounter(partnerName, authError);
                    ResourceFetcherUtils.SetAuthError(context, authError);

                    activity.SetStatus(ActivityStatusCode.Error, authError.FastEnumToString());

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(authenticatedInfo.Exception ?? _aadTokenAuthFailException);
                    return Task.CompletedTask;
                }

                // AAD token is successfully authenticated
                // Now Compare Client Id with one in config
                if (string.IsNullOrWhiteSpace(authenticatedInfo.AppId) || 
                    !partnerAuthorizeConfig.ClientIds.Contains(authenticatedInfo.AppId))
                {
                    ResourceFetcherMetricProvider.AddMiddleWareAuthErrorCounter(partnerName, ResourceFetcherAuthError.NOT_ALLOWED_CLIENT_ID);
                    ResourceFetcherUtils.SetAuthError(context, ResourceFetcherAuthError.NOT_ALLOWED_CLIENT_ID);

                    activity.SetStatus(ActivityStatusCode.Error, ResourceFetcherAuthError.NOT_ALLOWED_CLIENT_ID.FastEnumToString());

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(ResourceFetcherConstants.NotAllowedClientIdException);
                    return Task.CompletedTask;
                }

                // AAD Token is fully authenticated
                ResourceFetcherMetricProvider.AddMiddleWareAuthSuccessCounter(partnerName, "AAD_TOKEN");

                activity.ExportToActivityMonitor(monitor.Activity);
                monitor.OnCompleted();

                return _next(context);
            }
            catch (Exception ex)
            {
                ResourceFetcherMetricProvider.AddMiddleWareAuthErrorCounter(partnerName, ResourceFetcherAuthError.INTERNAL_ERROR);
                ResourceFetcherUtils.SetAuthError(context, ResourceFetcherAuthError.INTERNAL_ERROR);

                activity.SetStatus(ActivityStatusCode.Error, ResourceFetcherAuthError.INTERNAL_ERROR.FastEnumToString());

                activity.ExportToActivityMonitor(monitor.Activity);
                monitor.OnError(ex);

                return Task.CompletedTask;
            }
        }
    }
}
