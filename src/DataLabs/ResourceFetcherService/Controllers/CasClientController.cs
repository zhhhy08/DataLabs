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
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientTimeOutManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Monitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Utils;

    [ApiController]
    public class CasClientController : ControllerBase
    {
        private static readonly ActivityMonitorFactory CasClientControllerGetCasCapacityCheckAsync =
            new ("CasClientController.GetCasCapacityCheckAsync");

        private readonly CasClientTimeOutManager _casClientTimeOutManager;
        private readonly IPartnerAuthorizeManager _partnerAuthorizeManager;
        private readonly ICasClient _casClient;

        public CasClientController(IPartnerAuthorizeManager partnerAuthorizeManager, ICasClient casClient, IConfiguration configuration)
        {
            _casClientTimeOutManager = CasClientTimeOutManager.Create(configuration);
            _partnerAuthorizeManager = partnerAuthorizeManager;
            _casClient = casClient;
        }

        [HttpPost(SolutionConstants.ResourceFetcher_CASGetCasCapacityCheckRoute)]
        public async Task<IActionResult> GetCasCapacityCheckAsync(
            [FromHeader(Name = CommonHttpHeaders.DataLabs_PartnerName)] string partnerName,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_OpenTelemetry_ActivityId)] string? parentActivityId,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_InputCorrelationId)] string? inputCorrelationId,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_RetryFlowCount)] int retryFlowCount,
            [FromHeader(Name = CommonHttpHeaders.ClientRequestId)] string? clientRequestId,
            [FromQuery(Name = SolutionConstants.DL_APIVERSION)] string? apiVersion,
            [FromBody] CasRequestBody casRequestBody,
            CancellationToken cancellationToken)
        {
            var callMethod = nameof(GetCasCapacityCheckAsync);
            var activityMonitorFactory = CasClientControllerGetCasCapacityCheckAsync;
            var activityName = ResourceFetcherConstants.ActivityName_GetCasCapacityCheck;

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
                    resourceIdColumnValue: null,
                    otelActivity: activity,
                    activity: monitor.Activity);

                activity.SetTag(SolutionConstants.PartnerName, partnerName);
                activity.SetTag(SolutionConstants.RetryCount, retryFlowCount);
                activity.SetTag(SolutionConstants.ClientRequestId, clientRequestId);

                monitor.OnStart(false);

                if (string.IsNullOrWhiteSpace(partnerName) || casRequestBody == null)
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

                // Check if call is allowed
                if (!partnerAuthorizeConfig.CasAllowedCallApiVersionMap.TryGetValue(callMethod, out var defaultApiVersion))
                {
                    ResourceFetcherMetricProvider.AddRequestErrorCounter(partnerName, callMethod, retryFlowCount, ResourceFetcherAuthError.NOT_ALLOWED_TYPE);

                    activity.SetStatus(ActivityStatusCode.Error, "NotAllowedType: " + callMethod);

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

                // Get TimeOut per resource Type
                var timeOut = _casClientTimeOutManager.GetCasCallTimeOut(callMethod: callMethod, retryFlowCount: retryFlowCount);
                activity.SetTag(SolutionConstants.TimeOutValue, timeOut);

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                HttpResponseMessage clientResponseMessage;
                try
                {
                    var responseMessage = await _casClient.GetCasCapacityCheckAsync(
                        casRequestBody: casRequestBody,
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
                        component: nameof(CasClient),
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
