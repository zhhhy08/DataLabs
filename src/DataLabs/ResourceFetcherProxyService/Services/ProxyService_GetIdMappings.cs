namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Services
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Monitoring;

    internal partial class ProxyService : ResourceProxyService.ResourceProxyServiceBase
    {
        private static readonly ActivityMonitorFactory ProxyServiceGetIdMappings = 
            new("ProxyService.GetIdMappings");

        public override async Task<ResourceResponse> GetIdMappings(
            IdMappingRequest request,
            ServerCallContext context)
        {
            var callMethod = nameof(GetIdMappings);
            var activityMonitorFactory = ProxyServiceGetIdMappings;
            var activityName = ResourceFetcherProxyConstants.ActivityName_GetIdMappings;

            using var activity = new OpenTelemetryActivityWrapper(
                source: ResourceFetcherProxyMetricProvider.ResourceFetcherProxyActivitySource,
                name: activityName,
                kind: ActivityKind.Server,
                parentId: request.TraceId);

            var scenario = ParseScenario(request.ReqAttributes);
            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: SolutionConstants.ResourceFetcherProxyService);

            var resourceType = request.ResourceType;
            activity.SetTag(SolutionConstants.ResourceType, resourceType);

            int retryCount = request.RetryCount;
            string? correlationId = request.CorrelationId;
            string? typeDimensionValue = resourceType;

            try
            {
                ResourceFetcherProxyMetricProvider.AddRFProxyClientToRecvMetric(callMethod, request.RequestEpochTime, context);

                monitor.OnStart(false);

                string? resourceIdColumnValue = request.CorrelationId;
                string allowedType = ClientProviderConfigList.AllAllowedSymbol; // TODO: update if we want to restrict allowed type
                string? subscriptionId = null;
                string? tenantId = null;
                string? cacheKey = request.CorrelationId;
                var defaultDataFormat = ResourceCacheDataFormat.IdMapping;

                return await ExecuteAsync(
                    allowedTypesMap: _clientProvidersManager.GetIdMappingAllowedTypesMap,
                    clientDelegateFunc: ExecuteGetIdMappingsAsync,
                    deletegateFuncArg: request,
                    clientTimeOutDelegate: GetPacificCollectionTimeOut,
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

        private static Task<HttpResponseMessage> ExecuteGetIdMappingsAsync(
            IQFDClient client,
            object arg,
            string? apiVersion,
            CancellationToken cancellationToken)
        {
            var request = (IdMappingRequest)arg;
            var idMappingRequestBody = new IdMappingRequestBody 
            { 
                AliasResourceIds = request.IdMappingProtoRequestBody.AliasResourceIds 
            };
            return client.GetPacificIdMappingsAsync(
                idMappingRequestBody: idMappingRequestBody,
                correlationId: request.CorrelationId,
                apiVersion: apiVersion ?? string.Empty,
                clientRequestId: null,
                cancellationToken: cancellationToken);
        }
    }
}