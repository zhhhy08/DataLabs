namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient.SelectionStrategy;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class DataLabCachePool
    {
        private static readonly ILogger<DataLabCachePool> Logger = DataLabLoggerFactory.CreateLogger<DataLabCachePool>();

        public bool IsDenyListed { get; set; }
        public bool IsWriteEnabled => !IsDenyListed && _dataLabCachePoolConfig.WriteEnabled;
        public bool IsReadEnabled => IsWriteEnabled && _dataLabCachePoolConfig.ReadEnabled;  // Read requires writeEnabled

        public DataLabCachePoolConfig CachePoolConfig => _dataLabCachePoolConfig;

        public string CachePoolName => _dataLabCachePoolConfig.CacheName; // iocache1
        public string CacheDomain { get; } // cache-namespace.svc.cluster.local
        public int Port => _dataLabCachePoolConfig.Port;
        public DataLabCacheNode[] CacheNodes => _cacheNodes;

        private readonly DataLabCachePoolConfigKeys _dataLabCachePoolConfigKeys;
        private readonly IConnectionMultiplexerWrapperFactory _connectionMultiplexerWrapperFactory;

        private DataLabCachePoolConfig _dataLabCachePoolConfig;
        private DataLabCacheNode[] _cacheNodes;
        private ICacheNodeSelectionStrategy _nodeSelectionStrategy;

        private string _cacheConnectionOptionStr;
        private CacheConnectionOption _cacheConnectionOption;
        private string _nodeReplicationStr;

        private readonly object _updateLock = new();

        public DataLabCachePool(
            DataLabCachePoolConfigKeys cachePoolConfigKeys,
            IConfiguration configuration,
            IConnectionMultiplexerWrapperFactory connectionMultiplexerWrapperFactory,
            bool preCreateConnections)
        {
            _dataLabCachePoolConfigKeys = cachePoolConfigKeys;
            _connectionMultiplexerWrapperFactory = connectionMultiplexerWrapperFactory;

            // Domain
            var cacheDomain = configuration.GetValue<string>(cachePoolConfigKeys.CachePoolDomainConfigKey);
            GuardHelper.ArgumentNotNullOrEmpty(cacheDomain, cachePoolConfigKeys.CachePoolDomainConfigKey);
            CacheDomain = cacheDomain;

            // CachePoolConfig
            var cachePoolConfig = configuration.GetValueWithCallBack<string>(cachePoolConfigKeys.CachePoolConfigKey, 
                UpdateCachePoolConfig, string.Empty) ?? string.Empty;
            _dataLabCachePoolConfig = new DataLabCachePoolConfig(cachePoolConfig.ConvertToDictionary(caseSensitive: false));

            // ConnectionsOptions
            _cacheConnectionOptionStr = configuration.GetValueWithCallBack<string>(cachePoolConfigKeys.CachePoolConnectionsOptionConfigKey, 
                UpdateCacheConnectionOption, string.Empty) ?? string.Empty;
            _cacheConnectionOption = new CacheConnectionOption(_cacheConnectionOptionStr.ConvertToDictionary(caseSensitive: false));

            // NodeReplication
            _nodeReplicationStr = configuration.GetValueWithCallBack<string>(cachePoolConfigKeys.CachePoolNodeReplicationMappingConfigKey,
                UpdateNodeReplication, string.Empty) ?? string.Empty;

            var nodeCount = _dataLabCachePoolConfig.NodeCount;
            var cacheNodes = new DataLabCacheNode[nodeCount];

            for (int i = 0; i < nodeCount; i++)
            {
                cacheNodes[i] = new DataLabCacheNode(
                    cachePoolName: CachePoolName,
                    nodeIndex: i, // 0 based index
                    cacheDomain: CacheDomain,
                    port: Port,
                    cacheConnectionOption: _cacheConnectionOption,
                    connectionMultiplexerWrapperFactory: connectionMultiplexerWrapperFactory);
            }

            // SetNodeReplication
            SetNodeReplication(_nodeReplicationStr, cacheNodes);
            
            // Create NodeSelectionStrategy
            var nodeSelectionStrategy = ICacheNodeSelectionStrategy.CreateCacheNodeSelectionStrategy(cacheNodes, _dataLabCachePoolConfig);
            
            Interlocked.Exchange(ref _cacheNodes, cacheNodes);
            Interlocked.Exchange(ref _nodeSelectionStrategy, nodeSelectionStrategy);

            if (preCreateConnections)
            {
                _ = Task.Run(() => CreateCacheAllConnectionsAsync(cacheNodes));  // background task
            }
        }

        public DataLabCacheNode SelectNode(ulong keyHash)
        {
            var startOffset = _dataLabCachePoolConfig.StartOffset;
            var cacheNode = _nodeSelectionStrategy.SelectNode(keyHash);
            var nodeId = cacheNode.CacheNodeIndex;

            // Don't use cacheNode directly because cacheNode might be exchanged by UpdateCachePoolConfig (Hotconfig)
            // Instead get selected cacheNode's nodeId
            var cacheNodes = _cacheNodes;
            return cacheNodes[(nodeId + startOffset) % cacheNodes.Length];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasSameNumNodesAndSelectionStrategy(DataLabCachePoolConfig otherDataLabCachePoolConfig)
        {
            return _dataLabCachePoolConfig.NodeCount == otherDataLabCachePoolConfig.NodeCount &&
                _dataLabCachePoolConfig.NodeSelectionMode == otherDataLabCachePoolConfig.NodeSelectionMode;
        }

        private async static Task CreateCacheAllConnectionsAsync(DataLabCacheNode[] cacheNodes)
        {
            // Create Connections for one cache node at a time
            var randomStartIdx = Random.Shared.Next(0, cacheNodes.Length);
            for (int i = 0; i < cacheNodes.Length; i++)
            {
                var idx = (randomStartIdx + i) % cacheNodes.Length;
                var cacheNode = cacheNodes[idx];
                await cacheNode.CreateAllConnectionsAsync().ConfigureAwait(false);
            }
        }

        private Task UpdateNodeReplication(string newVal)
        {
            var oldVal = _nodeReplicationStr;
            if (string.IsNullOrWhiteSpace(newVal) || newVal.EqualsInsensitively(oldVal))
            {
                return Task.CompletedTask;
            }

            lock (_updateLock)
            {
                var cacheNodes = _cacheNodes;

                if (newVal.EqualsInsensitively(SolutionConstants.NoneValue))
                {
                    // Clear all old node replication
                    for (int i = 0; i < cacheNodes.Length; i++)
                    {
                        cacheNodes[i].SetReplicaNodeIds(null);
                    }
                }
                else
                {
                    SetNodeReplication(newVal, cacheNodes);
                }
            }

            Logger.LogWarning("{CachePoolName}: {config} is changed, Old: {oldVal}, New: {newVal}",
                CachePoolName, _dataLabCachePoolConfigKeys.CachePoolNodeReplicationMappingConfigKey, oldVal, newVal);
            return Task.CompletedTask;
        }

        private Task UpdateCacheConnectionOption(string newVal)
        {
            var oldVal = _cacheConnectionOptionStr;
            if (string.IsNullOrWhiteSpace(newVal) || newVal.EqualsInsensitively(oldVal))
            {
                return Task.CompletedTask;
            }

            var newCacheConnectionsOption = new CacheConnectionOption(newVal.ConvertToDictionary(caseSensitive: false));

            try
            {
                lock (_updateLock)
                {
                    if (Interlocked.CompareExchange(ref _cacheConnectionOptionStr, newVal, oldVal) == oldVal)
                    {
                        Interlocked.Exchange(ref _cacheConnectionOption, newCacheConnectionsOption);
                        Logger.LogWarning("{CachePoolName}: {config} is changed, Old: {oldVal}, New: {newVal}",
                            CachePoolName, _dataLabCachePoolConfigKeys.CachePoolConnectionsOptionConfigKey, oldVal, newVal); ;

                        var cacheNodes = _cacheNodes;
                        for (int i = 0; i < cacheNodes.Length; i++)
                        {
                            var cacheNode = cacheNodes[i];
                            if (!cacheNode.UpdateCacheConnectionOption(newCacheConnectionsOption))
                            {
                                Logger.LogError("{CachePoolName}: {cacheNodeName} Failed to update cache connection option",
                                    CachePoolName, cacheNode.CacheNodeName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{CachePoolName}: Failed to update {config}", CachePoolName, _dataLabCachePoolConfigKeys.CachePoolConnectionsOptionConfigKey);
            }

            return Task.CompletedTask;
        }

        private Task UpdateCachePoolConfig(string newVal)
        {
            if (string.IsNullOrWhiteSpace(newVal))
            {
                return Task.CompletedTask;
            }

            var oldDataLabCachePoolConfig = _dataLabCachePoolConfig;
            var newDataLabCachePoolConfig = new DataLabCachePoolConfig(newVal.ConvertToDictionary(caseSensitive: false));

            // CacheName must be same
            if (!oldDataLabCachePoolConfig.CacheName.Equals(newDataLabCachePoolConfig.CacheName))
            {
                Logger.LogError("CacheName mismatch, Old: {oldVal}, New: {newVal}",
                    oldDataLabCachePoolConfig.CacheName,
                    newDataLabCachePoolConfig.CacheName);
                return Task.CompletedTask;
            }

            // Port must be same
            if (oldDataLabCachePoolConfig.Port != newDataLabCachePoolConfig.Port)
            {
                Logger.LogError("{CachePoolName}: Port mismatch, Old: {oldVal}, New: {newVal}",
                    CachePoolName,
                    oldDataLabCachePoolConfig.Port,
                    newDataLabCachePoolConfig.Port);
                return Task.CompletedTask;
            }

            // NodeSelectionNode hotconfig support?
            // I don't think it makes sense to change NodeSelectionMode hotconfig.
            if (oldDataLabCachePoolConfig.NodeSelectionMode != newDataLabCachePoolConfig.NodeSelectionMode)
            {
                Logger.LogError("{CachePoolName}: NodeSelectionMode mismatch, Old: {oldVal}, New: {newVal}",
                    CachePoolName,
                    oldDataLabCachePoolConfig.NodeSelectionMode.FastEnumToString(),
                    newDataLabCachePoolConfig.NodeSelectionMode.FastEnumToString());
                return Task.CompletedTask;
            }

            var oldConfigString = oldDataLabCachePoolConfig.ToString();
            var newConfigString = newDataLabCachePoolConfig.ToString();

            if (oldConfigString.EqualsInsensitively(newConfigString))
            {
                return Task.CompletedTask;
            }

            lock (_updateLock)
            {
                Interlocked.Exchange(ref _dataLabCachePoolConfig, newDataLabCachePoolConfig);

                Logger.LogWarning("{CachePoolName}: {config} is changed, Old: {oldVal}, New: {newVal}",
                    CachePoolName,
                    _dataLabCachePoolConfigKeys.CachePoolConfigKey,
                    oldConfigString,
                    newConfigString);

                if (oldDataLabCachePoolConfig.NodeCount != newDataLabCachePoolConfig.NodeCount)
                {
                    var oldNodeCount = oldDataLabCachePoolConfig.NodeCount;
                    var newNodeCount = newDataLabCachePoolConfig.NodeCount;

                    Logger.LogWarning("{CachePoolName}: NodeCount is changed, Old: {oldVal}, New: {newVal}",
                        CachePoolName, oldNodeCount, newNodeCount);

                    var cacheConnectionOption = _cacheConnectionOption;
                    var nodeReplicationStr = _nodeReplicationStr;

                    var oldCacheNodes = _cacheNodes;
                    var newCacheNodes = new DataLabCacheNode[newNodeCount];

                    for (int i = 0; i < newNodeCount; i++)
                    {
                        var newCacheNode = new DataLabCacheNode(
                            cachePoolName: CachePoolName,
                            nodeIndex: i, // 0 based index
                            cacheDomain: CacheDomain,
                            port: Port,
                            cacheConnectionOption: cacheConnectionOption,
                            connectionMultiplexerWrapperFactory: _connectionMultiplexerWrapperFactory);

                        if (i < oldCacheNodes.Length)
                        {
                            var oldCacheNode = oldCacheNodes[i];
                            // Get DenyListed status from old cache node
                            newCacheNode.IsDenyListed = oldCacheNode.IsDenyListed;
                        }

                        newCacheNodes[i] = newCacheNode;
                    }

                    // SetNodeReplication
                    SetNodeReplication(nodeReplicationStr, newCacheNodes);

                    // Create NodeSelectionStrategy
                    var newNodeSelectionStrategy = ICacheNodeSelectionStrategy.CreateCacheNodeSelectionStrategy(newCacheNodes, newDataLabCachePoolConfig);

                    // Set New Cache Nodes and NodeSelectionStrategy
                    Interlocked.Exchange(ref _cacheNodes, newCacheNodes);
                    Interlocked.Exchange(ref _nodeSelectionStrategy, newNodeSelectionStrategy);

                    // Now we exchange cacheNodes and SelectionStrategy so that old cacheNodes can be disposed

                    // Dispose Old CacheNodes after 1 minutes
                    _ = Task.Run(() => Task.Delay(TimeSpan.FromSeconds(60))
                        .ContinueWith((antecedent, info) => DisposeOldCacheNodes((DataLabCacheNode[]?)info), oldCacheNodes,
                        TaskContinuationOptions.None));

                    return Task.CompletedTask;
                }
            }

            return Task.CompletedTask;
        }

        private static void SetNodeReplication(string? value, DataLabCacheNode[] cacheNodes)
        {
            var numNodes = cacheNodes.Length;
            var nodeReplicas = new List<int>[numNodes];

            if (!string.IsNullOrWhiteSpace(value))
            {
                foreach (var nodes in value.ConvertToList())
                {
                    var replicaSet = nodes.ConvertToIntSet(delimiter: "=").ToList();
                    if (replicaSet.Count > 1)
                    {
                        for (int i = 0; i < replicaSet.Count; i++)
                        {
                            var nodeId = replicaSet[i];
                            if (nodeId < 0 || nodeId >= numNodes)
                            {
                                throw new ArgumentException($"Invalid NodeReplication index: {nodeId}");
                            }

                            nodeReplicas[nodeId] = replicaSet;
                        }
                    }
                }
            }

            for (int i = 0; i < cacheNodes.Length; i++)
            {
                var replicaSet = nodeReplicas[i];
                cacheNodes[i].SetReplicaNodeIds(replicaSet);
            }
        }

        private static void DisposeOldCacheNodes(DataLabCacheNode[]? oldDataLabCacheNodes)
        {
            if (oldDataLabCacheNodes == null || oldDataLabCacheNodes.Length == 0)
            {
                return;
            }

            for (int i = 0; i < oldDataLabCacheNodes.Length; i++)
            {
                try
                {
                    var cacheNode = oldDataLabCacheNodes[i];
                    cacheNode?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to dispose old CacheNode");
                }
            }
        }
    }
}
