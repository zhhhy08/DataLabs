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
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientTimeOutManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Monitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Utils;

    [ApiController]
    public class QFDClientController : ControllerBase
    {
        private static readonly ActivityMonitorFactory QFDClientControllerGetPacificResourceAsync =
            new ("QFDClientController.GetPacificResourceAsync");

        private static readonly ActivityMonitorFactory QFDClientControllerGetPacificCollectionAsync =
          new ("QFDClientController.GetPacificCollectionAsync");

        private static readonly ActivityMonitorFactory QFDClientControllerGetIdMappingsAsync =
          new("QFDClientController.GetIdMappingsAsync");

        private readonly QFDClientTimeOutManager _qfdClientTimeOutManager;
        private readonly IPartnerAuthorizeManager _partnerAuthorizeManager;
        private readonly IQFDClient _qfdClient;

        public QFDClientController(IPartnerAuthorizeManager partnerAuthorizeManager, IQFDClient qfdClient, IConfiguration configuration)
        {
            _qfdClientTimeOutManager = QFDClientTimeOutManager.Create(configuration);
            _partnerAuthorizeManager = partnerAuthorizeManager;
            _qfdClient = qfdClient;
        }

        [HttpGet(SolutionConstants.ResourceFetcher_QfdGetPacificResourceRoute)]
        public async Task<IActionResult> GetPacificResourceAsync(
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
            var callMethod = nameof(GetPacificResourceAsync);
            var activityMonitorFactory = QFDClientControllerGetPacificResourceAsync;
            var activityName = ResourceFetcherConstants.ActivityName_GetPacificResource;

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
                if (!partnerAuthorizeConfig.QfdAllowedResourceTypeApiVersionMap.TryGetValue(resourceType, out var defaultApiVersion))
                {
                    ResourceFetcherMetricProvider.AddRequestErrorCounter(partnerName, callMethod, retryFlowCount, ResourceFetcherAuthError.NOT_ALLOWED_TYPE);

                    activity.SetStatus(ActivityStatusCode.Error, "NotAllowedType: " + resourceType);

                    activity.ExportToActivityMonitor(monitor.Activity);
                    monitor.OnError(ResourceFetcherConstants.NotAllowedTypeException);

                    return ResourceFetcherUtils.ForbidResult(); ;
                }

                // If apiVersion is not provided, use default version
                if (string.IsNullOrWhiteSpace(apiVersion))
                {
                    apiVersion = defaultApiVersion;
                }

                // Log ApiVersion
                activity.SetTag(SolutionConstants.ApiVersion, apiVersion);

                // Get TimeOut per resource Type
                var timeOut = _qfdClientTimeOutManager.GetQFDCallTimeOut(callMethod: callMethod, retryFlowCount: retryFlowCount);
                activity.SetTag(SolutionConstants.TimeOutValue, timeOut);

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                HttpResponseMessage clientResponseMessage;
                try
                {
                    var responseMessage = await _qfdClient.GetPacificResourceAsync(
                        resourceId: resourceId,
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
                        component: nameof(QFDClient),
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

        [HttpGet(SolutionConstants.ResourceFetcher_QfdGetPacificCollectionRoute)]
        public async Task<IActionResult> GetPacificCollectionAsync(
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
            var callMethod = nameof(GetPacificCollectionAsync);
            var activityMonitorFactory = QFDClientControllerGetPacificCollectionAsync;
            var activityName = ResourceFetcherConstants.ActivityName_GetPacificCollection;

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
                var resourceType = ArmUtils.GetResourceTypeForCollectionCall(resourceId);
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
                if (!partnerAuthorizeConfig.QfdAllowedResourceTypeApiVersionMap.TryGetValue(resourceType, out var defaultApiVersion))
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
                    apiVersion = defaultApiVersion;
                }

                // Log ApiVersion
                activity.SetTag(SolutionConstants.ApiVersion, apiVersion);

                // Get TimeOut per resource Type
                var timeOut = _qfdClientTimeOutManager.GetQFDCallTimeOut(callMethod: callMethod, retryFlowCount: retryFlowCount);
                activity.SetTag(SolutionConstants.TimeOutValue, timeOut);

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                HttpResponseMessage clientResponseMessage;
                try
                {
                    var responseMessage = await _qfdClient.GetPacificCollectionAsync(
                        resourceId: resourceId,
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
                        component: nameof(QFDClient),
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

        [HttpPost(SolutionConstants.ResourceFetcher_QfdGetPacificIdMappingsRoute)]
        public async Task<IActionResult> GetPacificIdMappingsAsync(
            [FromHeader(Name = CommonHttpHeaders.DataLabs_PartnerName)] string partnerName,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_OpenTelemetry_ActivityId)] string? parentActivityId,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_InputCorrelationId)] string? inputCorrelationId,
            [FromHeader(Name = CommonHttpHeaders.DataLabs_RetryFlowCount)] int retryFlowCount,
            [FromHeader(Name = CommonHttpHeaders.ClientRequestId)] string? clientRequestId,
            [FromQuery(Name = SolutionConstants.DL_APIVERSION)] string? apiVersion,
            [FromBody] IdMappingRequestBody idMappingRequestBody,
            CancellationToken cancellationToken)
        {
            var callMethod = nameof(GetPacificIdMappingsAsync);
            var activityMonitorFactory = QFDClientControllerGetIdMappingsAsync;
            var activityName = ResourceFetcherConstants.ActivityName_GetIdMappings;

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
                activity.SetTag(SolutionConstants.CorrelationId, inputCorrelationId);

                monitor.OnStart(false);

                if (string.IsNullOrWhiteSpace(partnerName) || idMappingRequestBody == null || idMappingRequestBody.AliasResourceIds.IsNullOrEmpty())
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
                if (!partnerAuthorizeConfig.IdMappingAllowedCallApiVersionMap.TryGetValue(callMethod, out var defaultApiVersion))
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
                var timeOut = _qfdClientTimeOutManager.GetQFDCallTimeOut(callMethod: callMethod, retryFlowCount: retryFlowCount);
                activity.SetTag(SolutionConstants.TimeOutValue, timeOut);

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                HttpResponseMessage clientResponseMessage;
                try
                {
                    var responseMessage = await _qfdClient.GetPacificIdMappingsAsync(
                        idMappingRequestBody: idMappingRequestBody,
                        correlationId: inputCorrelationId,
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
                        component: nameof(QFDClient),
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
