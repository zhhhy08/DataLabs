namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient
{
    using System;
    using System.Diagnostics.Metrics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;

    public class ResourceCacheClient : IResourceCacheClient
    {
        private static readonly ILogger<ResourceCacheClient> Logger =
            DataLabLoggerFactory.CreateLogger<ResourceCacheClient>();

        private static readonly ActivityMonitorFactory ResourceCacheClientGetResourceAsyncError =
         new("ResourceCacheClient.GetResourceAsyncError", useDataLabsEndpoint: true);

        public const string GetResourceReadQuorumMisMatchCounterName = "GetResourceReadQuorumMisMatchCounter";

        private static readonly Counter<long> GetResourceReadQuorumMisMatchCounter = 
            MetricLogger.CommonMeter.CreateCounter<long>(GetResourceReadQuorumMisMatchCounterName);

        public bool CacheEnabled => _cacheClient.CacheEnabled;
        public bool UseHashForResourceCacheKey => _useHashForResourceCacheKey;
        public ICacheTTLManager CacheTTLManager => _cacheTTLManager;

        private readonly ICacheTTLManager _cacheTTLManager;
        private readonly ICacheClient _cacheClient;
        private readonly CacheClientExecutor? _cacheClientExecutor;

        private int _cacheReadQuorum = 1;
        private bool _useHashForResourceCacheKey;

        public ResourceCacheClient(ICacheClient cacheClient, ICacheTTLManager cacheTTLManager, IConfiguration configuration)
        {
            _cacheClient = cacheClient;
            _cacheTTLManager = cacheTTLManager;
            _useHashForResourceCacheKey = configuration.GetValueWithCallBack<bool>(SolutionConstants.UseHashForResourceCacheKey, UpdateUseHashForResourceCacheKey, true);
            _cacheReadQuorum = configuration.GetValueWithCallBack<int>(SolutionConstants.ResourceCacheReadQuorum, UpdateCacheReadQuorum, 1);
            GuardHelper.IsArgumentPositive(_cacheReadQuorum);

            if (cacheClient is CacheClient dataLabCacheClient)
            {
                _cacheClientExecutor = dataLabCacheClient.CacheClientExecutor;
            }
        }

        public async Task<ResourceCacheResult> GetResourceAsync(string resourceId, string? tenantId, CancellationToken cancellationToken)
        {
            var cacheKey = GetCacheKey(resourceId, tenantId);

            if (_cacheClientExecutor != null)
            {
                var cacheClientReadResult = await _cacheClientExecutor.GetValueAsync(cacheKey, readQuorum: _cacheReadQuorum, cancellationToken).ConfigureAwait(false);
                return GetResourceCacheResult(cacheClientReadResult);
            }
            else
            {
                var value = await _cacheClient.GetValueAsync(cacheKey, cancellationToken).ConfigureAwait(false);
                if (value != null)
                {
                    try
                    {
                        return ResourceCacheUtils.DecompressCacheValue(value);
                    }
                    catch (Exception ex)
                    {
                        // this is possible when data is invalid/corrupted for several reasons
                        using var monitor = ResourceCacheClientGetResourceAsyncError.ToMonitor();
                        monitor.OnError(ex);
                    }
                }

                // Consider this as no cache entry
                return ResourceCacheResult.NoCacheEntry;
            }
        }

        public Task<bool> SetResourceAsync(string resourceId, string? tenantId, ResourceCacheDataFormat dataFormat, ReadOnlyMemory<byte> resource, long timeStamp, string? etag, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            timeStamp = timeStamp > 0 ? timeStamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var cacheKey = GetCacheKey(resourceId, tenantId);
            var cacheValue = ResourceCacheUtils.CompressCacheValue(dataFormat, resource, timeStamp, etag);
            return _cacheClient.SetValueWithExpiryAsync(cacheKey, cacheValue, expiry, cancellationToken);
        }

        public Task<bool> SetResourceIfGreaterThanAsync(string resourceId, string? tenantId, ResourceCacheDataFormat dataFormat, ReadOnlyMemory<byte> resource, long timeStamp, string? etag, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            timeStamp = timeStamp > 0 ? timeStamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var cacheKey = GetCacheKey(resourceId, tenantId);
            var cacheValue = ResourceCacheUtils.CompressCacheValue(dataFormat, resource, timeStamp, etag);

            return _cacheClient.SetValueIfGreaterThanWithExpiryAsync(cacheKey, cacheValue, timeStamp, expiry, cancellationToken);
        }

        public Task<bool> SetNotFoundResourceAsync(string resourceId, string? tenantId, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            return SetResourceIfGreaterThanAsync(resourceId: resourceId, tenantId: tenantId, dataFormat: ResourceCacheDataFormat.NotFoundEntry,
                resource: ReadOnlyMemory<byte>.Empty, timeStamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), etag: null, expiry: expiry, cancellationToken: cancellationToken);
        }

        public Task<bool> DeleteResourceAsync(string resourceId, string? tenantId, CancellationToken cancellationToken)
        {
            var cacheKey = GetCacheKey(resourceId, tenantId);
            return _cacheClient.DeleteKeyAsync(cacheKey, cancellationToken);
        }

        public Task<bool> SetLongValueAsync(string key, long value, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            GuardHelper.ArgumentNotNullOrEmpty(key, nameof(key));

            var cacheKey = key.ToLowerInvariant();
            var cacheValue = BitConverter.GetBytes(value);

            return _cacheClient.SetValueWithExpiryAsync(cacheKey, cacheValue, expiry, cancellationToken);
        }

        public async Task<long?> GetLongValueAsync(string key, CancellationToken cancellationToken)
        {
            GuardHelper.ArgumentNotNullOrEmpty(key, nameof(key));

            var cacheKey = key.ToLowerInvariant();
            var cacheValue = await _cacheClient.GetValueAsync(cacheKey, cancellationToken).ConfigureAwait(false);

            if (cacheValue?.Length > 0)
            {
                return BitConverter.ToInt64(cacheValue);
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetCacheKey(string resourceId, string? tenantId)
        {
            var key = ResourceCacheUtils.GetLowerCaseKeyWithArmId(resourceId, tenantId);
            return !_useHashForResourceCacheKey ? key : ResourceCacheUtils.GetHashKeyString(key);
        }

        private static ResourceCacheResult GetResourceCacheResult(CacheClientReadResult<byte[]?>? clientResult)
        {
            if (clientResult?.HasSuccess == true)
            {
                // check if we have read quorum or single success
                if (clientResult.SuccessNodeCount == 1)
                {
                    var successResult = clientResult.SuccessNodeResults![0].Result;
                    if (successResult != null)
                    {
                        try
                        {
                            // Success
                            return ResourceCacheUtils.DecompressCacheValue(successResult);
                        }
                        catch (Exception ex)
                        {
                            // this is possible when data is invalid/corrupted for several reasons
                            using var monitor = ResourceCacheClientGetResourceAsyncError.ToMonitor();
                            monitor.OnError(ex);
                        }
                    }

                    // Consider this as no cache entry
                    return ResourceCacheResult.NoCacheEntry;
                }
                else
                {
                    // Read Quorum
                    // Pick latest one
                    bool hasMisMatchInQuorum = false;
                    ResourceCacheResult latestCacheResult = ResourceCacheResult.NoCacheEntry;
                    for (int i = 0; i < clientResult.SuccessNodeCount; i++)
                    {
                        var successResult = clientResult.SuccessNodeResults![i].Result;
                        if (successResult != null)
                        {
                            try
                            {
                                var resourceCacheResult = ResourceCacheUtils.DecompressCacheValue(successResult);
                                if (resourceCacheResult.DataTimeStamp > latestCacheResult.DataTimeStamp)
                                {
                                    if (latestCacheResult.DataTimeStamp > 0)
                                    {
                                        // We have mis-match in quorum
                                        hasMisMatchInQuorum = true;
                                    }
                                    latestCacheResult = resourceCacheResult;
                                }
                            }
                            catch (Exception ex)
                            {
                                // this is possible when data is invalid/corrupted for several reasons
                                using var monitor = ResourceCacheClientGetResourceAsyncError.ToMonitor();
                                monitor.OnError(ex);
                            }
                        }
                    }

                    if (hasMisMatchInQuorum)
                    {
                        GetResourceReadQuorumMisMatchCounter.Add(1);
                    }

                    return latestCacheResult;
                }
            }
            else if (clientResult?.HasFailed == true)
            {
                throw new CacheClientReadException<byte[]?>("CacheClient failed to read from replicas", clientResult);
            }

            // Consider this as no cache entry
            return ResourceCacheResult.NoCacheEntry;
        }

        private Task UpdateUseHashForResourceCacheKey(bool newValue)
        {
            var oldValue = _useHashForResourceCacheKey;
            if (oldValue != newValue)
            {
                _useHashForResourceCacheKey = newValue;
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.UseHashForResourceCacheKey, oldValue, newValue);
            }
            return Task.CompletedTask;
        }

        private Task UpdateCacheReadQuorum(int newValue)
        {
            if (newValue < 1)
            {
                Logger.LogError("{config} must be equal or larger than 1", SolutionConstants.ResourceCacheReadQuorum);
                return Task.CompletedTask;
            }

            var oldValue = _cacheReadQuorum;
            if (oldValue != newValue)
            {
                if (Interlocked.CompareExchange(ref _cacheReadQuorum, newValue, oldValue) == oldValue)
                {
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                        SolutionConstants.ResourceCacheReadQuorum, oldValue, newValue);
                }
            }

            return Task.CompletedTask;
        }
    }
}
