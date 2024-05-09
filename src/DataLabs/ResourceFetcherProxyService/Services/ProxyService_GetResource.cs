namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Monitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient;

    internal partial class ProxyService : ResourceProxyService.ResourceProxyServiceBase
    {
        private static readonly ActivityMonitorFactory ProxyServiceGetResource = new("ProxyService.GetResource");

        public override async Task<ResourceResponse> GetResource(
            ResourceRequest request,
            ServerCallContext context)
        {
            var callMethod = nameof(GetResource);
            var activityMonitorFactory = ProxyServiceGetResource;
            var activityName = ResourceFetcherProxyConstants.ActivityName_GetResource;

            using var activity = new OpenTelemetryActivityWrapper(
                source: ResourceFetcherProxyMetricProvider.ResourceFetcherProxyActivitySource,
                name: activityName,
                kind: ActivityKind.Server,
                parentId: request.TraceId);

            var scenario = ParseScenario(request.ReqAttributes);
            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: SolutionConstants.ResourceFetcherProxyService);

            int retryCount = request.RetryCount;
            string? correlationId = request.CorrelationId;
            string? typeDimensionValue = null;

            try
            {
                ResourceFetcherProxyMetricProvider.AddRFProxyClientToRecvMetric(callMethod, request.RequestEpochTime, context);

                monitor.OnStart(false);

                var resourceType = ArmUtils.GetResourceType(request.ResourceId);
                activity.SetTag(SolutionConstants.ResourceType, resourceType);
                activity.SetTag(SolutionConstants.TenantId, request.TenantId);

                GuardHelper.ArgumentNotNullOrEmpty(request.ResourceId);
                GuardHelper.ArgumentNotNullOrEmpty(resourceType);

                typeDimensionValue = resourceType;
                string? resourceIdColumnValue = request.ResourceId;
                string allowedType = resourceType;
                string? subscriptionId = request.ResourceId.GetSubscriptionIdOrNull();
                string? tenantId = request.TenantId;
                string? cacheKey = request.ResourceId;
                var defaultDataFormat = ResourceCacheDataFormat.ARM;

                return await ExecuteAsync(
                    allowedTypesMap: _clientProvidersManager.GetResourceAllowedTypesMap,
                    clientDelegateFunc: ExecuteGetRFProxyResourceAsync,
                    deletegateFuncArg: request,
                    clientTimeOutDelegate: GetRFProxyResourceTimeOut,
                    callMethod: callMethod,
                    correlationId: correlationId,
                    resourceIdColumnValue: resourceIdColumnValue,
                    retryFlowCount: retryCount,
                    allowedType: allowedType,
                    defaultDataFormat: defaultDataFormat,
                    subscriptionId: subscriptionId,
                    tenantId: tenantId,
                    resourceType: resourceType,
                    typeDimensionValue: typeDimensionValue,
                    cacheKey: cacheKey,
                    skipCacheRead: request.SkipCacheRead,
                    skipCacheWrite: request.SkipCacheWrite,
                    otelActivity: activity,
                    monitor: monitor,
                    parentCancellationToken: context.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(
                    correlationId: correlationId,
                    callMethod: callMethod,
                    retryFlowCount: retryCount,
                    typeDimensionValue: typeDimensionValue,
                    httpStatusCode: 0,
                    providerType: ClientProviderType.None,
                    proxyError: ResourceFetcherProxyError.INTERNAL_EXCEPTION,
                    errorType: ProxyErrorType.Retry,
                    errorMessage: ex.Message,
                    retryAfter: 0,
                    exception: ex,
                    otelActivity: activity,
                    monitor: monitor);
            }
        }

        private static Task<HttpResponseMessage> ExecuteGetRFProxyResourceAsync(
            IRFProxyGetResourceClient client,
            object arg,
            string? apiVersion,
            CancellationToken cancellationToken)
        {
            var request = (ResourceRequest)arg;
            return client.GetRFProxyResourceAsync(
                resourceId: request.ResourceId,
                tenantId: request.TenantId,
                apiVersion: apiVersion ?? string.Empty,
                regionName: request.RegionName,
                retryFlowCount: request.RetryCount,
                cancellationToken: cancellationToken);
        }

        private static TimeSpan? GetRFProxyResourceTimeOut(ClientProviderType providerType, string callMethod, string allowedType, int retryFlowCount)
        {
            if (providerType == ClientProviderType.Arm)
            {
                return _armClientTimeOutManager.GetResourceTypeTimeOut(resourceType: allowedType, retryFlowCount: retryFlowCount);

            }else if (providerType == ClientProviderType.Qfd)
            {
                return _qfdClientTimeOutManager.GetQFDCallTimeOut(callMethod: callMethod, retryFlowCount: retryFlowCount);
            }
            return null;
        }
    }
}