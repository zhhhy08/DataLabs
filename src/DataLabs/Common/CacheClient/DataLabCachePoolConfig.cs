namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient.SelectionStrategy;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public class DataLabCachePoolConfig
    {
        public string CacheName { get; }
        public bool ReadEnabled { get; } = true;
        public bool WriteEnabled { get; } = true;
        public int NodeCount { get; }
        public int Port { get; }
        public int StartOffset { get; }
        public CacheNodeSelectionMode NodeSelectionMode { get; } = CacheNodeSelectionMode.JumpHash;
        
        //{{- printf "CacheName=%s;ReadEnabled=%s;WriteEnabled=%s;NodeCount=%s;Port=%s;StartOffset=%s;NodeSelectionMode=%s" $cacheName $readEnabled $writeEnabled $nodeCount $port $startOffset $nodeSelectionMode }}
        public DataLabCachePoolConfig(IDictionary<string, string> keyValuePairs)
        {
            GuardHelper.ArgumentNotNullOrEmpty(keyValuePairs);

            if (keyValuePairs.TryGetValue(nameof(CacheName), out var mapValue))
            {
                CacheName = mapValue;
            }
            GuardHelper.ArgumentNotNullOrEmpty(CacheName);

            if (keyValuePairs.TryGetValue(nameof(ReadEnabled), out mapValue))
            {
                ReadEnabled = bool.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(WriteEnabled), out mapValue))
            {
                WriteEnabled = bool.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(NodeCount), out mapValue))
            {
                NodeCount = int.Parse(mapValue);
            }
            GuardHelper.IsArgumentPositive(NodeCount);

            if (keyValuePairs.TryGetValue(nameof(Port), out mapValue))
            {
                Port = int.Parse(mapValue);
            }
            GuardHelper.IsArgumentPositive(Port);

            if (keyValuePairs.TryGetValue(nameof(StartOffset), out mapValue))
            {
                StartOffset = int.Parse(mapValue);
            }
            GuardHelper.ArgumentConstraintCheck(StartOffset >= 0 && StartOffset < NodeCount);

            if (keyValuePairs.TryGetValue(nameof(NodeSelectionMode), out mapValue))
            {
                NodeSelectionMode = StringEnumCache.GetEnumIgnoreCase<CacheNodeSelectionMode>(mapValue);
            }
        }

        public override string ToString()
        {
            return $"CacheName={CacheName};ReadEnabled={ReadEnabled};WriteEnabled={WriteEnabled};NodeCount={NodeCount};Port={Port};StartOffset={StartOffset};NodeSelectionMode={NodeSelectionMode}";
        }
    }

    public class DataLabCachePoolConfigKeys
    {
        public required string CachePoolDomainConfigKey { get; init; }
        public required string CachePoolConfigKey { get; init; }
        public required string CachePoolConnectionsOptionConfigKey { get; init; }
        public required string CachePoolNodeReplicationMappingConfigKey { get; init; }

        public static DataLabCachePoolConfigKeys CreateDataLabCachePoolConfigKeys(string configPrefix, int cachePoolIndex) // 0 based index
        {
            return new DataLabCachePoolConfigKeys
            {
                CachePoolDomainConfigKey = configPrefix + SolutionConstants.CachePoolDomain,
                CachePoolConfigKey = GetConfigName(configPrefix + SolutionConstants.CachePoolPrefix, cachePoolIndex),
                CachePoolConnectionsOptionConfigKey = GetConfigName(configPrefix + SolutionConstants.CachePoolConnectionsOptionPrefix, cachePoolIndex),
                CachePoolNodeReplicationMappingConfigKey = GetConfigName(configPrefix + SolutionConstants.CachePoolNodeReplicationMappingPrefix, cachePoolIndex)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetConfigName(string configName, int index)
        {
            return configName + '-' + index;
        }
    }
}
