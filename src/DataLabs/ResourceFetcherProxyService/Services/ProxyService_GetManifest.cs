namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Services
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Monitoring;

    internal partial class ProxyService : ResourceProxyService.ResourceProxyServiceBase
    {
        private static readonly ActivityMonitorFactory ProxyServiceGetManifestConfig = 
            new("ProxyService.GetManifestConfig");
        
        public override async Task<ResourceResponse> GetManifestConfig(
            ManifestRequest request,
            ServerCallContext context)
        {
            var callMethod = nameof(GetManifestConfig);
            var activityMonitorFactory = ProxyServiceGetManifestConfig;
            var activityName = ResourceFetcherProxyConstants.ActivityName_GetManifestConfig;

            using var activity = new OpenTelemetryActivityWrapper(
                source: ResourceFetcherProxyMetricProvider.ResourceFetcherProxyActivitySource,
                name: activityName,
                kind: ActivityKind.Server,
                parentId: request.TraceId);

            var scenario = ParseScenario(request.ReqAttributes);
            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: SolutionConstants.ResourceFetcherProxyService);

            int retryCount = request.RetryCount;
            string? correlationId = request.CorrelationId;
            string? typeDimensionValue = request.ManifestProvider;

            try
            {
                ResourceFetcherProxyMetricProvider.AddRFProxyClientToRecvMetric(callMethod, request.RequestEpochTime, context);

                monitor.OnStart(false);

                activity.SetTag(SolutionConstants.ManifestProvider, request.ManifestProvider);

                string? resourceIdColumnValue = request.ManifestProvider;
                string allowedType = ClientProviderConfigList.AllAllowedSymbol; // For now, for GetManifestConfig, we only support allow or not allow scenario
                string? subscriptionId = null;
                string? tenantId = null;
                string? resourceType = null;
                string? cacheKey = ResourceProxyClient.GetCacheKeyForManifestConfig(request.ManifestProvider);
                var defaultDataFormat = ResourceCacheDataFormat.ARMAdmin;

                return await ExecuteAsync(
                    allowedTypesMap: _clientProvidersManager.GetManifestConfigAllowedTypesMap,
                    clientDelegateFunc: ExecuteGetManifestConfigAsync,
                    deletegateFuncArg: request,
                    clientTimeOutDelegate: GetManifestConfigTimeOut,
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

        private static Task<HttpResponseMessage> ExecuteGetManifestConfigAsync(
            IARMAdminClient client,
            object arg,
            string? apiVersion,
            CancellationToken cancellationToken)
        {
            var request = (ManifestRequest)arg;
            return client.GetManifestConfigAsync(
                manifestProvider: request.ManifestProvider,
                apiVersion: apiVersion ?? string.Empty,
                clientRequestId: null, 
                cancellationToken: cancellationToken);
        }

        private static TimeSpan? GetManifestConfigTimeOut(ClientProviderType providerType, string callMethod, string allowedType, int retryFlowCount)
        {
            if (providerType == ClientProviderType.ArmAdmin)
            {
                return _armAdminClientTimeOutManager.GetAdminCallTimeOut(callMethod: callMethod, retryFlowCount: retryFlowCount);
            }
            return null;
        }
    }
}