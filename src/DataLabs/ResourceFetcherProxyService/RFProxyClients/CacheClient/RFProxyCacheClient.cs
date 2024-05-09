namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.CacheClient
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Monitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Utils;

    internal class RFProxyCacheClient : IRFProxyCacheClient
    {
        private static readonly ILogger<RFProxyCacheClient> Logger = DataLabLoggerFactory.CreateLogger<RFProxyCacheClient>();

        private static readonly ActivityMonitorFactory RFProxyCacheClientGetResourceAsync =
            new("RFProxyCacheClient.GetResourceAsync");

        private static readonly ActivityMonitorFactory RFProxyCacheClientGetRFProxyResourceAsync =
            new("RFProxyCacheClient.GetRFProxyResourceAsync");

        private static readonly ActivityMonitorFactory RFProxyCacheClientGetManifestConfigAsync =
            new("RFProxyCacheClient.GetManifestConfigAsync");

        private static readonly ActivityMonitorFactory RFProxyCacheClientGetConfigSpecsAsync =
            new("RFProxyCacheClient.GetConfigSpecsAsync");

        private static readonly ActivityMonitorFactory RFProxyCacheClientGetPacificResourceAsync =
            new("RFProxyCacheClient.GetPacificResourceAsync");

        private static readonly ActivityMonitorFactory RFProxyCacheClientGetPacificCollectionAsync =
            new("RFProxyCacheClient.GetPacificCollectionAsync");

        private static readonly ActivityMonitorFactory RFProxyCacheClientGetCasCapacityCheckAsync =
            new("RFProxyCacheClient.GetCasCapacityCheckAsync");

        private static readonly HttpResponseMessage NotImplementedHttpResponseMessage = new(HttpStatusCode.NotImplemented);

        public bool IsCacheEnabled => _useCacheInProxy && ResourceCacheClient != null && ResourceCacheClient.CacheEnabled;
        public IResourceCacheClient ResourceCacheClient { get; }

        private bool _useCacheInProxy;

        public RFProxyCacheClient(IResourceCacheClient resourceCacheClient, IConfiguration configuration)
        {
            ResourceCacheClient = resourceCacheClient;
            _useCacheInProxy = configuration.GetValueWithCallBack<bool>(SolutionConstants.UseCacheInResourceFetcherProxy, UpdateUseCacheInProxy, true);
        }

        public async Task<HttpResponseMessage> GetResourceAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            bool useResourceGraph,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            if (!_useCacheInProxy)
            {
                return NotImplementedHttpResponseMessage;
            }

            var callMethod = nameof(GetResourceAsync);
            using var monitor = RFProxyCacheClientGetResourceAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                monitor.Activity[SolutionConstants.ResourceId] = resourceId;
                monitor.Activity[SolutionConstants.TenantId] = tenantId;

                var resourceType = ArmUtils.GetResourceType(resourceId);

                var rfProxyHttpResponseMessage = await GetResourceFromCacheAsync(
                    cacheKey: resourceId,
                    tenantId: tenantId,
                    typeDimensionValue: resourceType,
                    activity: monitor.Activity,
                    callMethod: callMethod,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                monitor.OnCompleted();
                return rfProxyHttpResponseMessage;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<HttpResponseMessage> GetRFProxyResourceAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            string? regionName,
            int retryFlowCount,
            CancellationToken cancellationToken)
        {
            if (!_useCacheInProxy)
            {
                return NotImplementedHttpResponseMessage;
            }

            var callMethod = nameof(GetRFProxyResourceAsync);
            using var monitor = RFProxyCacheClientGetRFProxyResourceAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                monitor.Activity[SolutionConstants.ResourceId] = resourceId;
                monitor.Activity[SolutionConstants.TenantId] = tenantId;

                var resourceType = ArmUtils.GetResourceType(resourceId);

                var rfProxyHttpResponseMessage = await GetResourceFromCacheAsync(
                    cacheKey: resourceId,
                    tenantId: tenantId,
                    typeDimensionValue: resourceType,
                    activity: monitor.Activity,
                    callMethod: callMethod,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                monitor.OnCompleted();
                return rfProxyHttpResponseMessage;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<HttpResponseMessage> GetManifestConfigAsync(
            string manifestProvider,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            if (!_useCacheInProxy)
            {
                return NotImplementedHttpResponseMessage;
            }

            var callMethod = nameof(GetManifestConfigAsync);
            using var monitor = RFProxyCacheClientGetManifestConfigAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                var cacheKey = ResourceProxyClient.GetCacheKeyForManifestConfig(manifestProvider: manifestProvider);
                monitor.Activity[SolutionConstants.CacheKey] = cacheKey;
                monitor.Activity[SolutionConstants.ManifestProvider] = manifestProvider;

                var rfProxyHttpResponseMessage = await GetResourceFromCacheAsync(
                    cacheKey: cacheKey,
                    tenantId: null,
                    typeDimensionValue: manifestProvider,
                    activity: monitor.Activity,
                    callMethod: callMethod,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                monitor.OnCompleted();
                return rfProxyHttpResponseMessage;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<HttpResponseMessage> GetConfigSpecsAsync(
            string apiExtension,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            if (!_useCacheInProxy)
            {
                return NotImplementedHttpResponseMessage;
            }

            var callMethod = nameof(GetConfigSpecsAsync);
            using var monitor = RFProxyCacheClientGetConfigSpecsAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                var cacheKey = ResourceProxyClient.GetCacheKeyForConfigSpecs(apiExtension: apiExtension);
                monitor.Activity[SolutionConstants.CacheKey] = cacheKey;
                monitor.Activity[SolutionConstants.ApiExtension] = apiExtension;

                var rfProxyHttpResponseMessage = await GetResourceFromCacheAsync(
                    cacheKey: cacheKey,
                    tenantId: null,
                    typeDimensionValue: apiExtension,
                    activity: monitor.Activity,
                    callMethod: callMethod,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                monitor.OnCompleted();
                return rfProxyHttpResponseMessage;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<HttpResponseMessage> GetPacificResourceAsync(string resourceId, string? tenantId, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            if (!_useCacheInProxy)
            {
                return NotImplementedHttpResponseMessage;
            }

            var callMethod = nameof(GetPacificResourceAsync);
            using var monitor = RFProxyCacheClientGetPacificResourceAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                monitor.Activity[SolutionConstants.ResourceId] = resourceId;
                monitor.Activity[SolutionConstants.TenantId] = tenantId;

                var resourceType = ArmUtils.GetResourceType(resourceId);

                var rfProxyHttpResponseMessage = await GetResourceFromCacheAsync(
                    cacheKey: resourceId,
                    tenantId: tenantId,
                    typeDimensionValue: resourceType,
                    activity: monitor.Activity,
                    callMethod: callMethod,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                monitor.OnCompleted();
                return rfProxyHttpResponseMessage;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<HttpResponseMessage> GetPacificCollectionAsync(string resourceId, string? tenantId, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            if (!_useCacheInProxy)
            {
                return NotImplementedHttpResponseMessage;
            }

            var callMethod = nameof(GetPacificCollectionAsync);
            using var monitor = RFProxyCacheClientGetPacificCollectionAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                monitor.Activity[SolutionConstants.ResourceId] = resourceId;
                monitor.Activity[SolutionConstants.TenantId] = tenantId;

                var resourceType = ArmUtils.GetResourceType(resourceId);

                var rfProxyHttpResponseMessage = await GetResourceFromCacheAsync(
                    cacheKey: resourceId,
                    tenantId: tenantId,
                    typeDimensionValue: resourceType,
                    activity: monitor.Activity,
                    callMethod: callMethod,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                monitor.OnCompleted();
                return rfProxyHttpResponseMessage;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public Task<HttpResponseMessage> GetPacificIdMappingsAsync(IdMappingRequestBody idMappingRequestBody, string? correlationId, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<HttpResponseMessage> GetGenericRestApiAsync(string uriPath, IEnumerable<KeyValuePair<string, string>>? parameters, string? tenantId, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            return Task.FromResult(NotImplementedHttpResponseMessage);
        }

        public async Task<HttpResponseMessage> GetCasCapacityCheckAsync(CasRequestBody casRequestBody, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            if (!_useCacheInProxy)
            {
                return NotImplementedHttpResponseMessage;
            }

            var callMethod = nameof(GetCasCapacityCheckAsync);
            using var monitor = RFProxyCacheClientGetCasCapacityCheckAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                var cacheKey = ResourceProxyClient.GetCacheKeyForCas(casRequestBody.SubscriptionId, casRequestBody.Provider);
                monitor.Activity[SolutionConstants.CacheKey] = cacheKey;

                var rfProxyHttpResponseMessage = await GetResourceFromCacheAsync(
                    cacheKey: cacheKey,
                    tenantId: null,
                    typeDimensionValue: cacheKey,
                    activity: monitor.Activity,
                    callMethod: callMethod,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                monitor.OnCompleted();
                return rfProxyHttpResponseMessage;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        private async Task<RFProxyHttpResponseMessage> GetResourceFromCacheAsync(
            string cacheKey,
            string? tenantId,
            string? typeDimensionValue,
            IActivity activity,
            string callMethod,
            CancellationToken cancellationToken)
        {
            activity[SolutionConstants.CacheKey] = cacheKey;

            var cacheResult = await ResourceCacheClient.GetResourceAsync(
                resourceId: cacheKey,
                tenantId: tenantId,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            RFProxyHttpResponseMessage rfProxyHttpResponseMessage;
            if (cacheResult.Found)
            {
                // Cache Hit Metric
                ResourceFetcherProxyMetricProvider.AddRFProxyCacheClientCacheHitCounter(
                    callMethod: callMethod, typeDimensionValue: typeDimensionValue, cacheDataFormat: cacheResult.DataFormat);

                rfProxyHttpResponseMessage = new RFProxyHttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ReadOnlyMemoryHttpContent(cacheResult.Content)
                };

                rfProxyHttpResponseMessage.DataETag = cacheResult.Etag;
                rfProxyHttpResponseMessage.DataFormat = cacheResult.DataFormat;
                rfProxyHttpResponseMessage.DataTimeStamp = cacheResult.DataTimeStamp;
                rfProxyHttpResponseMessage.InsertionTimeStamp = cacheResult.InsertionTimeStamp;

                activity[SolutionConstants.ResourceSize] = cacheResult.Content.Length;
                activity[SolutionConstants.ETag] = rfProxyHttpResponseMessage.DataETag;
                activity[SolutionConstants.DataFormat] = rfProxyHttpResponseMessage.DataFormat.FastEnumToString();
                activity[SolutionConstants.DataTimeStamp] = rfProxyHttpResponseMessage.DataTimeStamp;
                activity[SolutionConstants.InsertionTimeStamp] = rfProxyHttpResponseMessage.InsertionTimeStamp;
            }
            else
            {
                // Cache Miss Metric
                ResourceFetcherProxyMetricProvider.AddRFProxyCacheClientMissCounter(callMethod: callMethod, typeDimensionValue: typeDimensionValue);

                rfProxyHttpResponseMessage = new RFProxyHttpResponseMessage(HttpStatusCode.NotFound);
            }

            activity[SolutionConstants.HttpStatusCode] = (int)rfProxyHttpResponseMessage.StatusCode;
            return rfProxyHttpResponseMessage;
        }

        public void Dispose()
        {
        }

        private Task UpdateUseCacheInProxy(bool newValue)
        {
            var oldValue = _useCacheInProxy;
            if (oldValue != newValue)
            {
                _useCacheInProxy = newValue;
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.UseCacheInResourceFetcherProxy, oldValue, newValue);
            }
            return Task.CompletedTask;
        }
    }
}
