namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager
{
    using System;
    using System.Runtime.CompilerServices;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class CacheClientProviderConfig : IClientProviderConfig
    {
        public string AllowedTypeName { get; }
        public ClientProviderType ProviderType { get; }
        public string? ApiVersion => null;

        public TimeSpan? ReadTTL { get; private set; }
        public bool ReadTTLFromTTLManager { get; }

        public bool WriteEnabled { get; }
        public TimeSpan? WriteTTL { get; private set; }
        public bool WriteTTLFromTTLManager { get; }

        public bool AddNotFound { get; }
        public TimeSpan? AddNotFoundWriteTTL { get; private set; }
        public bool AddNotFoundTTLFromTTLManager { get; }

        public bool HasSourceOfTruthProvider { get; }

        public CacheClientProviderConfig(
            string allowedTypeName,
            bool hasSourceOfTruthProvider,
            TimeSpan? readTTL,
            bool writeEnabled,
            TimeSpan? writeTTL,
            bool addNotFound,
            TimeSpan? addNotFoundWriteTTL,
            ICacheTTLManager cacheTTLManager)
        {
            AllowedTypeName = allowedTypeName;
            ProviderType = ClientProviderType.Cache;
            HasSourceOfTruthProvider = hasSourceOfTruthProvider;

            ReadTTL = readTTL;

            WriteEnabled = writeEnabled;
            if (WriteEnabled)
            {
                if (HasSourceOfTruthProvider)
                {
                    throw new NotSupportedException("Cache Write in Resource Proxy is not supported with Output SourceOfTruth");
                }

                WriteTTL = writeTTL;
                if (WriteTTL == null)
                {
                    // If WriteTTL is still null, Let's get it from cacheTTLManager
                    WriteTTL = cacheTTLManager.GetCacheTTL(resourceType: allowedTypeName, inputType: !HasSourceOfTruthProvider);
                    WriteTTLFromTTLManager = true;
                }

                // If readTTL is null, let's set readTTL with writeTTL
                if (ReadTTL == null)
                {
                    ReadTTL = WriteTTL;
                    ReadTTLFromTTLManager = WriteTTLFromTTLManager;
                }
            }

            if (ReadTTL == null)
            {
                // If ReadTTL is still null, Let's get it from cacheTTLManager
                ReadTTL = cacheTTLManager.GetCacheTTL(resourceType: allowedTypeName, inputType: !HasSourceOfTruthProvider);
                ReadTTLFromTTLManager = true;
            }

            AddNotFound = addNotFound;
            if (AddNotFound)
            {
                AddNotFoundWriteTTL = addNotFoundWriteTTL;
                // AddNotFoundWriteTTL is null, let's set AddNotFoundWriteTTL with writeTTL
                if (AddNotFoundWriteTTL == null && WriteEnabled && !WriteTTLFromTTLManager)
                {
                    AddNotFoundWriteTTL = WriteTTL;
                }

                if (AddNotFoundWriteTTL == null)
                {
                    // If AddNotFoundWriteTTL is still null, Let's get it from cacheTTLManager
                    AddNotFoundWriteTTL = cacheTTLManager.GetCacheTTLForNotFoundEntry(AllowedTypeName);
                    AddNotFoundTTLFromTTLManager = true;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCacheEntryExpired(ResourceCacheDataFormat cacheDataFormat, long? insertionTime, IActivity? activity)
        {
            // We support read time TTL based on configMap.
            // so even if cache entry itself exists in cache node. we can consider it as expired if it's read time TTL is expired.
            if (insertionTime == null || insertionTime <= 0)
            {
                // we can apply only if insertionTime is available.
                return false;
            }

            // Let's update cache with given response
            var readTTL = cacheDataFormat == ResourceCacheDataFormat.NotFoundEntry ? AddNotFoundWriteTTL : ReadTTL;    
            var ttl = readTTL == null ? 0 : readTTL.Value.TotalMilliseconds;
            if (ttl > 0)
            {
                var elapsedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - insertionTime.Value;
                if (activity != null)
                {
                    activity[SolutionConstants.ResourceCacheDataFormat] = cacheDataFormat.FastEnumToString();
                    activity[SolutionConstants.ReadTTL] = ttl;
                    activity[SolutionConstants.InsertionTime] = insertionTime.Value;
                    activity[SolutionConstants.ElapsedTime] = elapsedTime;
                }

                return elapsedTime > ttl;
            }

            return false;
        }

        public void UpdateCacheTTLManager(ICacheTTLManager cacheTTLManager)
        {
            if (ReadTTLFromTTLManager)
            {
                ReadTTL = cacheTTLManager.GetCacheTTL(resourceType: AllowedTypeName, inputType: !HasSourceOfTruthProvider);
            }
            if (WriteTTLFromTTLManager)
            {
                WriteTTL = cacheTTLManager.GetCacheTTL(resourceType: AllowedTypeName, inputType: !HasSourceOfTruthProvider);
            }
            if (AddNotFoundTTLFromTTLManager)
            {
                AddNotFoundWriteTTL = cacheTTLManager.GetCacheTTLForNotFoundEntry(AllowedTypeName);
            }
        }
    }
}
