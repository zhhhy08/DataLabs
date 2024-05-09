namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PodHealth;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class DataLabCachePoolsManager
    {
        private static readonly ILogger<DataLabCachePoolsManager> Logger = DataLabLoggerFactory.CreateLogger<DataLabCachePoolsManager>();

        public DataLabCachePool[] CachePools => _cachePools;

        private readonly DataLabCachePool[] _cachePools;
        private PodHealthManager _poolHealthManager = new("CachePoolHealth");
        private PodHealthManager _podHealthManager = new("CachePodHealth");

        private HashSet<string> _currentDenyPoolList;
        private HashSet<string> _currentDenyPodList;

        private readonly object _updateLock = new();

        public DataLabCachePoolsManager(
            string configPrefix, 
            IConfiguration configuration, 
            IConnectionMultiplexerWrapperFactory connectionMultiplexerWrapperFactory, 
            bool preCreateConnections)
        {
            var cacheNumPoolConfig = configPrefix + SolutionConstants.CacheNumPools;
            var numCachePools = configuration.GetValue<int>(cacheNumPoolConfig);
            GuardHelper.IsArgumentPositive(numCachePools);

            _cachePools = new DataLabCachePool[numCachePools];

            for (int i = 0; i < numCachePools; i++)
            {
                var configKeys = DataLabCachePoolConfigKeys.CreateDataLabCachePoolConfigKeys(configPrefix: configPrefix, cachePoolIndex: i);
                _cachePools[i] = new DataLabCachePool(configKeys, configuration: configuration, connectionMultiplexerWrapperFactory: connectionMultiplexerWrapperFactory, preCreateConnections: preCreateConnections);
            }

            _poolHealthManager = new PodHealthManager("CachePoolHealth", SolutionConstants.CachePoolDenyList);
            _currentDenyPoolList = _poolHealthManager.DenyListedNodes;

            _podHealthManager = new PodHealthManager("CachePodHealth", SolutionConstants.CachePodDenyList);
            _currentDenyPodList = _podHealthManager.DenyListedNodes;
        }

        public List<DataLabCacheNode> GetReadCacheNodes(ulong keyHash)
        {
            return GetCacheNodes(keyHash, writeMode: false);
        }

        public List<DataLabCacheNode> GetWriteCacheNodes(ulong keyHash)
        {
            return GetCacheNodes(keyHash, writeMode: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<DataLabCacheNode> GetCacheNodes(ulong keyHash, bool writeMode)
        {
            if (!ReferenceEquals(_currentDenyPodList, _podHealthManager.DenyListedNodes))
            {
                // DenyPodList has been updated
                UpdateDenyListedPods();
            }

            if (!ReferenceEquals(_currentDenyPoolList, _poolHealthManager.DenyListedNodes))
            {
                // DenyPoolList has been updated
                UpdateDenyListedPools();
            }

            var selectedNodes = new List<DataLabCacheNode>(4);

            DataLabCachePool? prevDataLabCachePool = null;
            int nodeIndexWithoutStartOffsetInPrevPool = -1;
            var cachePools = _cachePools;

            for (int i = 0; i < cachePools.Length; i++)
            {
                var cachePool = cachePools[i];
                var cachePoolEnabled = writeMode ? cachePool.IsWriteEnabled : cachePool.IsReadEnabled;
                if (!cachePoolEnabled)
                {
                    // Pool is not allowed
                    continue;
                }

                var cacheNodes = cachePool.CacheNodes;
                var cachePoolConfig = cachePool.CachePoolConfig;

                // Find Selected Node
                int selectedNodeIndex;
                if (prevDataLabCachePool?.HasSameNumNodesAndSelectionStrategy(cachePoolConfig) == true)
                {
                    // Current Pool has same Node Count and Node Selection Strategy as Previous Pool
                    // We don't need to recalculate the selected node
                    selectedNodeIndex = (nodeIndexWithoutStartOffsetInPrevPool + cachePoolConfig.StartOffset) % cacheNodes.Length;
                }
                else
                {
                    // Current Pool is first pool or has different Node Count or Node Selection Strategy
                    selectedNodeIndex = cachePool.SelectNode(keyHash).CacheNodeIndex;

                    // Each Cache Pool could have startOffset
                    // Let's adjust the selectedNodeIndex based on startOffset before saving it
                    prevDataLabCachePool = cachePool;
                    nodeIndexWithoutStartOffsetInPrevPool = GetSelectedNodeIndexWithoutStartOffset(cachePool, selectedNodeIndex);
                }

                selectedNodeIndex %= cacheNodes.Length;
                var cacheNode = cacheNodes[selectedNodeIndex];

                // Add Selected Node if it is enabled
                var cacheNodeEnabled = writeMode ? cacheNode.IsWriteEnabled : cacheNode.IsReadEnabled;
                if (cacheNodeEnabled)
                {
                    selectedNodes.Add(cacheNode);
                }

                // Add NodeReplicas if any
                var replicaNodeIds = cacheNode.ReplicaNodeIds;
                if (replicaNodeIds != null)
                {
                    for (int j = 0; j < replicaNodeIds.Length; j++)
                    {
                        var replicaNodeId = replicaNodeIds[j];
                        if (replicaNodeId < cacheNodes.Length)
                        {
                            var replicaNode = cacheNodes[replicaNodeId];
                            var replicaNodeEnabled = writeMode ? replicaNode.IsWriteEnabled : replicaNode.IsReadEnabled;
                            if (replicaNodeEnabled)
                            {
                                selectedNodes.Add(replicaNode);
                            }
                        }
                    }
                }
            }

            return selectedNodes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetSelectedNodeIndexWithoutStartOffset(DataLabCachePool cachePool, int selectedNodeIndex)
        {
            var cacheNodes = cachePool.CacheNodes;
            var startOffset = cachePool.CachePoolConfig.StartOffset;

            return (startOffset == 0) ? selectedNodeIndex : 
                (selectedNodeIndex - startOffset + cacheNodes.Length) % cacheNodes.Length;
        }

        private void UpdateDenyListedPods()
        {
            lock(_updateLock)
            {
                var oldDenyPodList = _currentDenyPodList;
                var oldVal = oldDenyPodList == null ? "" : string.Join(";", oldDenyPodList);
                var newDenyPodList = _podHealthManager.DenyListedNodes;
                var newVal = newDenyPodList == null ? "" : string.Join(";", newDenyPodList);
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", SolutionConstants.CachePodDenyList, oldVal, newVal);

                Interlocked.Exchange(ref _currentDenyPodList, newDenyPodList!);
                var denyPodList = _currentDenyPodList;
                var cachePools = _cachePools;

                for (int i = 0; i < cachePools.Length; i++)
                {
                    var cachePool = cachePools[i];
                    var cacheNodes = cachePool.CacheNodes;

                    for (int j = 0; j < cacheNodes.Length; j++)
                    {
                        var cacheNode = cacheNodes[j];
                        if (denyPodList?.Count > 0 && denyPodList.Contains(cacheNode.CacheNodeName))
                        {
                            cacheNode.IsDenyListed = true;
                        }
                        else
                        {
                            cacheNode.IsDenyListed = false;
                        }
                    }
                }
            }
        }

        private void UpdateDenyListedPools()
        {
            lock(_updateLock)
            {
                var oldDenyPoolList = _currentDenyPoolList;
                var oldVal = oldDenyPoolList == null ? "" : string.Join(";", oldDenyPoolList);
                var newDenyPoolList = _poolHealthManager.DenyListedNodes;
                var newVal = newDenyPoolList == null ? "" : string.Join(";", newDenyPoolList);
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", SolutionConstants.CachePoolDenyList, oldVal, newVal);

                Interlocked.Exchange(ref _currentDenyPoolList, _poolHealthManager.DenyListedNodes);
                var denyPoolList = _currentDenyPoolList;
                var cachePools = _cachePools;

                for (int i = 0; i < cachePools.Length; i++)
                {
                    var cachePool = cachePools[i];
                    if (denyPoolList?.Count > 0 && denyPoolList.Contains(cachePool.CachePoolName))
                    {
                        cachePool.IsDenyListed = true;
                    }
                    else
                    {
                        cachePool.IsDenyListed = false;
                    }
                }
            }
        }
    }
}
