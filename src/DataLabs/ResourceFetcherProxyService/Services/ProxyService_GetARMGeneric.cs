namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Services
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Monitoring;
    
    internal partial class ProxyService : ResourceProxyService.ResourceProxyServiceBase
    {
        private static readonly ActivityMonitorFactory ProxyServiceGetARMGenericResource = new("ProxyService.GetARMGenericResource");

        public override async Task<ResourceResponse> GetARMGenericResource(
            ARMGenericRequest request,
            ServerCallContext context)
        {
            var callMethod = nameof(GetARMGenericResource);
            var activityMonitorFactory = ProxyServiceGetARMGenericResource;
            var activityName = ResourceFetcherProxyConstants.ActivityName_GetARMGenericResource;

            using var activity = new OpenTelemetryActivityWrapper(
                source: ResourceFetcherProxyMetricProvider.ResourceFetcherProxyActivitySource,
                name: activityName,
                kind: ActivityKind.Server,
                parentId: request.TraceId);

            var scenario = ParseScenario(request.ReqAttributes);
            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: SolutionConstants.ResourceFetcherProxyService);

            int retryCount = request.RetryCount;
            string? correlationId = request.CorrelationId;
            string? typeDimensionValue = request.UriPath;

            try
            {
                ResourceFetcherProxyMetricProvider.AddRFProxyClientToRecvMetric(callMethod, request.RequestEpochTime, context);

                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(request.UriPath);

                activity.SetTag(SolutionConstants.URIPath, request.UriPath);
                activity.SetTag(SolutionConstants.TenantId, request.TenantId);

                // For detail params, let's add it to activityMonitor
                monitor.Activity.LogCollectionAndCount(SolutionConstants.QueryParams, request.QueryParams);

                string? resourceIdColumnValue = request.UriPath;
                string allowedType = request.UriPath;
                string? subscriptionId = null;
                string? tenantId = request.TenantId;
                string? resourceType = null;
                string? cacheKey = null; // no cache for now for ARMGenericResource
                var defaultDataFormat = ResourceCacheDataFormat.ARM;

                return await ExecuteAsync(
                    allowedTypesMap: _clientProvidersManager.CallARMGenericRequestAllowedTypesMap,
                    clientDelegateFunc: ExecuteGetGenericRestApiAsync,
                    deletegateFuncArg: request,
                    clientTimeOutDelegate: GetGenericRestApiTimeOut,
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

        private static Task<HttpResponseMessage> ExecuteGetGenericRestApiAsync(
            IARMClient client,
            object arg,
            string? apiVersion,
            CancellationToken cancellationToken)
        {
            var request = (ARMGenericRequest)arg;
            return client.GetGenericRestApiAsync(
                uriPath: request.UriPath,
                parameters: request.QueryParams,
                tenantId: request.TenantId,
                apiVersion: apiVersion ?? string.Empty,
                clientRequestId: null,
                cancellationToken: cancellationToken);
        }

        private static TimeSpan? GetGenericRestApiTimeOut(ClientProviderType providerType, string callMethod, string allowedType, int retryFlowCount)
        {
            if (providerType == ClientProviderType.Arm)
            {
                return _armClientTimeOutManager.GetGenericApiTimeOut(urlPath: allowedType, retryFlowCount: retryFlowCount);
            }
            return null;
        }
    }
}