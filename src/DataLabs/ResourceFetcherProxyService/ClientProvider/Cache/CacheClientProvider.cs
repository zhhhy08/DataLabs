namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Cache
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;

    internal class CacheClientProvider :
        IClientProvider<IRFProxyGetResourceClient>,
        IClientProvider<IARMClient>,
        IClientProvider<IARMAdminClient>,
        IClientProvider<IQFDClient>,
        IClientProvider<ICasClient>
    {
        private static readonly ActivityMonitorFactory CacheClientProviderAddToCacheAsync = new ("CacheClientProvider.AddToCacheAsync");
        private static readonly ActivityMonitorFactory CacheClientProviderAddNotFoundToCacheAsync = new("CacheClientProvider.AddNotFoundToCacheAsync");

        private static readonly AddToCacheFailedException AddToCacheFailedException = new ("AddToCache failed");
        private static readonly AddNotFoundToCacheFailedException AddNotFoundToCacheFailedException = new("AddNotFoundToCache failed");

        public ClientProviderType ProviderType { get; }
        public string? ApiVersion => null;
        public CacheClientProvider? CacheProvider => this;

        IRFProxyGetResourceClient IClientProvider<IRFProxyGetResourceClient>.Client => RFProxyCacheClient;
        IARMClient IClientProvider<IARMClient>.Client => RFProxyCacheClient;
        IARMAdminClient IClientProvider<IARMAdminClient>.Client => RFProxyCacheClient;
        IQFDClient IClientProvider<IQFDClient>.Client => RFProxyCacheClient;
        ICasClient IClientProvider<ICasClient>.Client => RFProxyCacheClient;

        public IRFProxyCacheClient RFProxyCacheClient { get; }
        public CacheClientProviderConfig CacheProviderConfig { get; }
        
        public CacheClientProvider(IRFProxyCacheClient rfProxyCacheClient, CacheClientProviderConfig cacheClientProviderConfig)
        {
            ProviderType = ClientProviderType.Cache;
            RFProxyCacheClient = rfProxyCacheClient;
            CacheProviderConfig = cacheClientProviderConfig;
        }

        public async Task<bool> AddToCacheAsync(
            string cacheKey,
            string? tenantId,
            string? resourceType,
            ResourceCacheDataFormat dataFormat,
            ReadOnlyMemory<byte> resource,
            long dataTimeStamp,
            string? etag,
            CancellationToken cancellationToken)
        {
            if (!RFProxyCacheClient.IsCacheEnabled || !CacheProviderConfig.WriteEnabled)
            {
                return false;
            }

            using var monitor = CacheClientProviderAddToCacheAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                // Let's update cache with given response
                var writeTTL = CacheProviderConfig.WriteTTL ?? RFProxyCacheClient.ResourceCacheClient.CacheTTLManager.GetCacheTTL(resourceType: resourceType, inputType: true);

                var cacheSuccess = await RFProxyCacheClient.ResourceCacheClient.SetResourceIfGreaterThanAsync(
                    resourceId: cacheKey,
                    tenantId: tenantId,
                    dataFormat: dataFormat,
                    resource: resource,
                    timeStamp: dataTimeStamp,
                    etag: string.IsNullOrWhiteSpace(etag) ? null : etag,
                    expiry: writeTTL,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (cacheSuccess)
                {
                    monitor.OnCompleted();
                    return true;
                }
                else
                {
                    monitor.OnError(AddToCacheFailedException);
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Even if cache update fails, no throw exception
                monitor.OnError(ex);
                return false;                
            }
        }

        public async Task<bool> AddNotFoundToCacheAsync(
            string cacheKey,
            string? tenantId,
            string? resourceType,
            CancellationToken cancellationToken)
        {
            if (!RFProxyCacheClient.IsCacheEnabled || !CacheProviderConfig.AddNotFound)
            {
                return false;
            }

            using var monitor = CacheClientProviderAddNotFoundToCacheAsync.ToMonitor();
            
            try
            {
                monitor.OnStart(false);

                var notFoundTTL = CacheProviderConfig.AddNotFoundWriteTTL ?? RFProxyCacheClient.ResourceCacheClient.CacheTTLManager.GetCacheTTLForNotFoundEntry(resourceType: resourceType);

                var cacheSuccess = await RFProxyCacheClient.ResourceCacheClient.SetNotFoundResourceAsync(
                    resourceId: cacheKey,
                    tenantId: tenantId,
                    expiry: notFoundTTL,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                monitor.Activity[SolutionConstants.AddNotFoundEntryToCache] = cacheSuccess;

                if (cacheSuccess)
                {
                    monitor.OnCompleted();
                    return true;
                }
                else
                {
                    monitor.OnError(AddNotFoundToCacheFailedException);
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Even if cache update fails, no throw exception
                monitor.OnError(ex);
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCacheEntryExpired(ResourceCacheDataFormat cacheDataFormat, long? insertionTime, IActivity? activity)
        {
            return CacheProviderConfig.IsCacheEntryExpired(cacheDataFormat: cacheDataFormat, insertionTime: insertionTime, activity: activity);
        }
    }
}
