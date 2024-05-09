namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Controllers
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientTimeOutManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Monitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Utils;

    [ApiController]
    public class ARMClientController : ControllerBase
    {
        private static readonly ActivityMonitorFactory ARMClientControllerGetResourceAsync =
            new ("ARMClientController.GetResourceAsync");

        private static readonly ActivityMonitorFactory ARMClientControllerGetGenericRestApiAsync =
          new ("ARMClientController.GetGenericRestApiAsync");

        private readonly ArmClientTimeOutManager _armClientTimeOutManager;
        private readonly IPartnerAuthorizeManager _partnerAuthorizeManager;
        private readonly IARMClient _armClient;

        public ARMClientController(IPartnerAuthorizeManager partnerAuthorizeManager, IARMClient armClient, IConfiguration configuration)
        {
            _armClientTimeOutManager = ArmClientTimeOutManager.Create(configuration);
            _partnerAuthorizeManager = partnerAuthorizeManager;
            _armClient = armClient;
        }

        [HttpGet(SolutionConstants.ResourceFetcher_ArmGetResourceRoute)]
        public async Task<IActionResult> GetResourceAsync(
            [FromHeader(Name = CommonHttpHeaders.DataLabs_PartnerName)] string partnerName,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_OpenTelemetry_ActivityId)] string? parentActivityId,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_InputCorrelationId)] string? inputCorrelationId,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_RetryFlowCount)] int retryFlowCount,
            [FromHeader(Name = CommonHttpHeaders.ClientRequestId)] string? clientRequestId,
            [FromQuery(Name = SolutionConstants.DL_RESOURCEID)] string resourceId,
            [FromQuery(Name = SolutionConstants.DL_TENANTID)] string? tenantId,
            [FromQuery(Name = SolutionConstants.DL_APIVERSION)] string? apiVersion,
            CancellationToken cancellationToken)
        {
            var callMethod = nameof(GetResourceAsync);
            var activityMonitorFactory = ARMClientControllerGetResourceAsync;
            var activityName = ResourceFetcherConstants.ActivityName_GetResource;


            // MVC controll does URL decodes automatically when it comes from URL Query Param ("FromQuery")
            // we don't need to call explicit URL decoding for parameters from Query
            using var activity = new OpenTelemetryActivityWrapper(
                source: ResourceFetcherMetricProvider.ResourceFetcherActivitySource,
                name: activityName,
                kind: ActivityKind.Server,
                parentId: parentActivityId);

            using var monitor = activityMonitorFactory.ToMonitor(component: ResourceFetcherConstants.ResourceFetcherService);

            try
            {
                // Details logs will be saved in activityMonitor and Opentelemtry Activity will have only short/important tags
                // So call activity.ExportToActivityMonitor(monitor.Activity) before activityMonitor returns

                // Set correlationId and Resource Id
                ResourceFetcherUtils.SetCorrelationIdAndResourceId(
                    correlationId: inputCorrelationId,
                    resourceIdColumnValue: resourceId,
                    otelActivity: activity,
                    activity: monitor.Activity);

                activity.SetTag(SolutionConstants.PartnerName, partnerName);
                activity.SetTag(SolutionConstants.RetryCount, retryFlowCount);
                activity.SetTag(SolutionConstants.ClientRequestId, clientRequestId);
                activity.SetTag(SolutionConstants.ResourceId, resourceId);
                activity.SetTag(SolutionConstants.TenantId, tenantId);

                monitor.OnStart(false);

                if (string.IsNullOrWhiteSpace(partnerName) || string.IsNullOrWhiteSpace(resourceId))
                {
                    ResourceFetcherMetricProvider.AddRequestErrorCounter(partnerName, callMethod, retryFlowCount, ResourceFetcherAuthError.BAD_REQUEST);

                    activity.SetStatus(ActivityStatusCode.Error, "BadRequest");

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(ResourceFetcherConstants.BadRequestException);
                    
                    return ResourceFetcherUtils.BadRequestResult();
                }

                // No tenantId null check because TenantId might be optional due to global resource
                var resourceType = ArmUtils.GetResourceType(resourceId);
                activity.SetTag(SolutionConstants.ResourceType, resourceType);

                if (string.IsNullOrWhiteSpace(resourceType))
                {
                    ResourceFetcherMetricProvider.AddRequestErrorCounter(partnerName, callMethod, retryFlowCount, ResourceFetcherAuthError.BAD_REQUEST);

                    activity.SetStatus(ActivityStatusCode.Error, "BadRequest");

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(ResourceFetcherConstants.BadRequestException);

                    return ResourceFetcherUtils.BadRequestResult();
                }

                // Validate
                var partnerAuthorizeConfig = _partnerAuthorizeManager.GetPartnerAuthorizeConfig(partnerName);
                if (partnerAuthorizeConfig == null)
                {
                    // this should not happen because it should be already validated in AADTokenAuthMiddleWare
                    // but just in case
                    ResourceFetcherMetricProvider.AddRequestErrorCounter(partnerName, callMethod, retryFlowCount, ResourceFetcherAuthError.NOT_ALLOWED_PARTNER);

                    activity.SetStatus(ActivityStatusCode.Error, "NotAllowedPartner");

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(ResourceFetcherConstants.NotAllowedPartnerException);

                    return ResourceFetcherUtils.ForbidResult();
                }

                // Check if resourceType is allowed
                if (!partnerAuthorizeConfig.ArmAllowedResourceTypeApiVersionMap.TryGetValue(resourceType, out var armGetResourceParams))
                {
                    ResourceFetcherMetricProvider.AddRequestErrorCounter(partnerName, callMethod, retryFlowCount, ResourceFetcherAuthError.NOT_ALLOWED_TYPE);

                    activity.SetStatus(ActivityStatusCode.Error, "NotAllowedType: " + resourceType);

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(ResourceFetcherConstants.NotAllowedTypeException);

                    return ResourceFetcherUtils.ForbidResult();
                }

                // If apiVersion is not provided, use default version
                if (string.IsNullOrWhiteSpace(apiVersion))
                {
                    apiVersion = armGetResourceParams.ApiVersion;
                }

                // Log ApiVersion
                activity.SetTag(SolutionConstants.ApiVersion, apiVersion);

                // Get TimeOut per resource Type
                var timeOut = _armClientTimeOutManager.GetResourceTypeTimeOut(resourceType: resourceType, retryFlowCount: retryFlowCount);
                activity.SetTag(SolutionConstants.TimeOutValue, timeOut);

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                HttpResponseMessage clientResponseMessage;
                try
                {
                    var responseMessage = await _armClient.GetResourceAsync(
                        resourceId: resourceId,
                        tenantId: tenantId,
                        apiVersion: apiVersion,
                        useResourceGraph: armGetResourceParams.UseResourceGraph,
                        clientRequestId: clientRequestId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    clientResponseMessage = responseMessage;
                }
                catch (Exception clientEx)
                {
                    return ResourceFetcherUtils.SetResourceFetcherException(
                        partnerName: partnerName,
                        callMethod: callMethod,
                        retryFlowCount: retryFlowCount,
                        exception: clientEx,
                        component: nameof(ARMClient),
                        activity: activity,
                        monitor: monitor);
                }

                using (clientResponseMessage)
                {
                    // Copy HttpResponseMessage from client
                    return await ResourceFetcherUtils.CopyClientResponseAsync(
                        clientResponseMessage: clientResponseMessage,
                        targetResponse: Response,
                        partnerName: partnerName,
                        retryFlowCount: retryFlowCount,
                        callMethod: callMethod,
                        activity: activity,
                        monitor: monitor,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                return ResourceFetcherUtils.SetResourceFetcherException(
                    partnerName: partnerName,
                    callMethod: callMethod,
                    retryFlowCount: retryFlowCount,
                    exception: ex,
                    component: ResourceFetcherConstants.ResourceFetcherService,
                    activity: activity,
                    monitor: monitor);
            }
        }

        [HttpGet(SolutionConstants.ResourceFetcher_ArmGetGenericRestApiRoute)]
        public async Task<IActionResult> GetGenericRestApiAsync(
            [FromHeader(Name = CommonHttpHeaders.DataLabs_PartnerName)] string partnerName,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_OpenTelemetry_ActivityId)] string? parentActivityId,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_InputCorrelationId)] string? inputCorrelationId,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_RetryFlowCount)] int retryFlowCount,
            [FromHeader(Name = CommonHttpHeaders.ClientRequestId)] string? clientRequestId,
            [FromQuery(Name = SolutionConstants.DL_URIPATH)] string uriPath,
            [FromQuery(Name = SolutionConstants.DL_PARAMETERS)] string? concatenatedParameters,
            [FromQuery(Name = SolutionConstants.DL_TENANTID)] string? tenantId,
            [FromQuery(Name = SolutionConstants.DL_APIVERSION)] string? apiVersion,
            CancellationToken cancellationToken)
        {
            var callMethod = nameof(GetGenericRestApiAsync);
            var activityMonitorFactory = ARMClientControllerGetGenericRestApiAsync;
            var activityName = ResourceFetcherConstants.ActivityName_GetGenericRestApi;

            // MVC controll does URL decodes automatically when it comes from URL Query Param ("FromQuery")
            // we don't need to call explicit URL decoding for parameters from Query
            using var activity = new OpenTelemetryActivityWrapper(
                source: ResourceFetcherMetricProvider.ResourceFetcherActivitySource,
                name: activityName,
                kind: ActivityKind.Server,
                parentId: parentActivityId);

            using var monitor = activityMonitorFactory.ToMonitor(component: ResourceFetcherConstants.ResourceFetcherService);

            try
            {
                // Details logs will be saved in activityMonitor and Opentelemtry Activity will have only short/important tags
                // So call activity.ExportToActivityMonitor(monitor.Activity) before activityMonitor returns

                // Set correlationId and Resource Id
                ResourceFetcherUtils.SetCorrelationIdAndResourceId(
                    correlationId: inputCorrelationId,
                    resourceIdColumnValue: uriPath,
                    otelActivity: activity,
                    activity: monitor.Activity);

                activity.SetTag(SolutionConstants.PartnerName, partnerName);
                activity.SetTag(SolutionConstants.RetryCount, retryFlowCount);
                activity.SetTag(SolutionConstants.ClientRequestId, clientRequestId);

                activity.SetTag(SolutionConstants.TenantId, tenantId);
                activity.SetTag(SolutionConstants.URIPath, uriPath);
                
                monitor.OnStart(false);

                if (string.IsNullOrWhiteSpace(partnerName) || string.IsNullOrWhiteSpace(uriPath))
                {
                    ResourceFetcherMetricProvider.AddRequestErrorCounter(partnerName, callMethod, retryFlowCount, ResourceFetcherAuthError.BAD_REQUEST);

                    activity.SetStatus(ActivityStatusCode.Error, "BadRequest");

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(ResourceFetcherConstants.BadRequestException);

                    return ResourceFetcherUtils.BadRequestResult();
                }

                // Validate
                var partnerAuthorizeConfig = _partnerAuthorizeManager.GetPartnerAuthorizeConfig(partnerName);
                if (partnerAuthorizeConfig == null)
                {
                    // this should not happen because it should be already validated in AADTokenAuthMiddleWare
                    // but just in case
                    ResourceFetcherMetricProvider.AddRequestErrorCounter(partnerName, callMethod, retryFlowCount, ResourceFetcherAuthError.NOT_ALLOWED_PARTNER);

                    activity.SetStatus(ActivityStatusCode.Error, "NotAllowedPartner");

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(ResourceFetcherConstants.NotAllowedPartnerException);

                    return ResourceFetcherUtils.ForbidResult();
                }

                // Check if URIPath is allowed
                if (!partnerAuthorizeConfig.ArmAllowedGenericURIPathApiVersionMap.TryGetValue(uriPath, out var defaultApiVersion))
                {
                    ResourceFetcherMetricProvider.AddRequestErrorCounter(partnerName, callMethod, retryFlowCount, ResourceFetcherAuthError.NOT_ALLOWED_TYPE);

                    activity.SetStatus(ActivityStatusCode.Error, "NotAllowedType: " + uriPath);

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(ResourceFetcherConstants.NotAllowedTypeException);

                    return ResourceFetcherUtils.ForbidResult();
                }

                // If apiVersion is not provided, use default version
                if (string.IsNullOrWhiteSpace(apiVersion))
                {
                    apiVersion = defaultApiVersion;
                }

                // Log ApiVersion
                activity.SetTag(SolutionConstants.ApiVersion, apiVersion);

                // Get TimeOut per URI Path
                var timeOut = _armClientTimeOutManager.GetGenericApiTimeOut(urlPath: uriPath, retryFlowCount: retryFlowCount);
                activity.SetTag(SolutionConstants.TimeOutValue, timeOut);

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                KeyPairList<string,string> parameters = default;

                if (!string.IsNullOrWhiteSpace(concatenatedParameters))
                {
                    ResourceFetcherUtils.ParseQueryString(concatenatedParameters, ref parameters);
                }

                HttpResponseMessage clientResponseMessage;
                try
                {
                    var responseMessage = await _armClient.GetGenericRestApiAsync(
                        uriPath: uriPath,
                        parameters: parameters.Count == 0 ? null : parameters,
                        tenantId: tenantId,
                        apiVersion: apiVersion,
                        clientRequestId: clientRequestId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    clientResponseMessage = responseMessage;
                }
                catch (Exception clientEx)
                {
                    return ResourceFetcherUtils.SetResourceFetcherException(
                        partnerName: partnerName,
                        callMethod: callMethod,
                        retryFlowCount: retryFlowCount,
                        exception: clientEx,
                        component: nameof(ARMClient),
                        activity: activity,
                        monitor: monitor);
                }

                using (clientResponseMessage)
                {
                    // Copy HttpResponseMessage from client
                    return await ResourceFetcherUtils.CopyClientResponseAsync(
                        clientResponseMessage: clientResponseMessage,
                        targetResponse: Response,
                        partnerName: partnerName,
                        retryFlowCount: retryFlowCount,
                        callMethod: callMethod,
                        activity: activity,
                        monitor: monitor,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                return ResourceFetcherUtils.SetResourceFetcherException(
                    partnerName: partnerName,
                    callMethod: callMethod,
                    retryFlowCount: retryFlowCount,
                    exception: ex,
                    component: ResourceFetcherConstants.ResourceFetcherService,
                    activity: activity,
                    monitor: monitor);
            }
        }
    }
}