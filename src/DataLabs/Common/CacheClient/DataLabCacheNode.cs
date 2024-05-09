namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SelectionStrategy;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using StackExchange.Redis;

    public class DataLabCacheNode : IConsistentHashingNode, IDisposable
    {
        private static readonly ILogger<DataLabCacheNode> Logger = DataLabLoggerFactory.CreateLogger<DataLabCacheNode>();

        public string DnsAddress { get; } // <CacheNodeName>.<CacheName>.<Namespace>.svc.cluster.local. e.g.) iocache-0.iocache.cache-namespace.svc.cluster.local
        public string CachePoolName { get; } // iocache
        public string CacheNodeName { get; }   // like iocache-0, iocache-1, iocache-2, etc.
        public int Port { get; } // like 3729
        public int CacheNodeIndex => _nodeIndex; // 0 based index
        public int[]? ReplicaNodeIds => _replicaNodeIds;
        public string ConsistentHashingNodeName => DnsAddress;

        public ConfigurationOptions ConfigurationOptions => _configurationOptions;
        public IConnectionMultiplexerWrapper[] ConnectionMultiplexers => _connectionMultiplexerWrappers;

        public bool IsDenyListed { get; set; }
        public bool IsWriteEnabled => !IsDenyListed && NodeWriteReady;
        public bool IsReadEnabled => IsWriteEnabled && NodeReadyReady; // Read requires writeEnabled

        // Below two flags are set by HealthCheck
        internal bool NodeWriteReady { get; set; } = true;
        internal bool NodeReadyReady { get; set; } = true;

        private IConnectionMultiplexerWrapper[] _connectionMultiplexerWrappers;
        private ConfigurationOptions _configurationOptions;
        private ISelectionStrategy<IConnectionMultiplexerWrapper, string> _connectionSelectionStrategy;
        private readonly IConnectionMultiplexerWrapperFactory _connectionMultiplexerWrapperFactory;

        private int[]? _replicaNodeIds;
        private readonly int _nodeIndex;

        private readonly object _updateLock = new();
        
        private volatile bool _disposed;

        public DataLabCacheNode(
            string cachePoolName,
            int nodeIndex,  // 0 based index
            string cacheDomain,
            int port,
            CacheConnectionOption cacheConnectionOption,
            IConnectionMultiplexerWrapperFactory connectionMultiplexerWrapperFactory)
        {
            GuardHelper.ArgumentNotNullOrEmpty(cachePoolName);
            GuardHelper.IsArgumentNonNegative(nodeIndex);
            GuardHelper.ArgumentNotNullOrEmpty(cacheDomain);
            GuardHelper.ArgumentNotNull(connectionMultiplexerWrapperFactory);

            _connectionMultiplexerWrapperFactory = connectionMultiplexerWrapperFactory;
            _nodeIndex = nodeIndex;

            CachePoolName = cachePoolName;
            CacheNodeName = GetCacheNodeName(_nodeIndex);
            DnsAddress = GetDNSAddress(_nodeIndex, cacheDomain);
            Port = port;

            GuardHelper.ArgumentNotNullOrEmpty(DnsAddress);
            GuardHelper.ArgumentConstraintCheck(port > 0);

            var numConnections = cacheConnectionOption.NumConnections;
            GuardHelper.ArgumentConstraintCheck(numConnections > 0);

            _configurationOptions = CacheConnectionOption.CreateConfigurationOptions(dnsAddress: DnsAddress, port: Port, cacheConnectionOption: cacheConnectionOption);
            _connectionSelectionStrategy = CacheConnectionOption.CreateConnectionSelectionStrategy(cacheConnectionOption.ConnectionSelectionStrategy);

            _connectionMultiplexerWrappers = new IConnectionMultiplexerWrapper[numConnections];
            for (int i = 0; i < numConnections; i++)
            {
                _connectionMultiplexerWrappers[i] = connectionMultiplexerWrapperFactory.CreateConnectionMultiplexerWrapper(this);
            }
        }

        public void SetReplicaNodeIds(IList<int>? nodeIndexes)
        {
            int[]? newReplicaNodes = null;

            if (nodeIndexes?.Count > 0)
            {
                List<int> replicaNodes = new(nodeIndexes.Count);
                for (int i = 0; i < nodeIndexes.Count; i++)
                {
                    var nodeIndex = nodeIndexes[i];
                    if (nodeIndex == _nodeIndex)
                    {
                        continue;
                    }
                    replicaNodes.Add(nodeIndex);
                }

                if (replicaNodes.Count > 0)
                {
                    newReplicaNodes = replicaNodes.ToArray();
                }
            }

            // Swap
            lock (_updateLock)
            {
                Interlocked.Exchange(ref _replicaNodeIds, newReplicaNodes);
            }
        }

        public IConnectionMultiplexerWrapper PickConnectionMultiplexerWrapper()
        {
            return _connectionSelectionStrategy.Select(_connectionMultiplexerWrappers, string.Empty);
        }

        public async Task CreateAllConnectionsAsync()
        {
            var connectionMultiplexerWrappers = _connectionMultiplexerWrappers;
            List<Task> tasks = new(connectionMultiplexerWrappers.Length);

            for (int i = 0; i < connectionMultiplexerWrappers.Length; i++)
            {
                var connectionMultiplexerWrapper = connectionMultiplexerWrappers[i];
                tasks.Add(connectionMultiplexerWrapper.CreateConnectionMultiplexerAsync(null, CancellationToken.None).AsTask());
            }
            
            await Task.WhenAll(tasks);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeOldConnections(_connectionMultiplexerWrappers);
        }

        public bool UpdateCacheConnectionOption(CacheConnectionOption cacheConnectionOption)
        {
            var newNumConnections = cacheConnectionOption.NumConnections;
            if (newNumConnections <= 0)
            {
                Logger.LogError("NumConnections should be greater than 0");
                return false;
            }
            
            var newConfigurationOptions = CacheConnectionOption.CreateConfigurationOptions(dnsAddress: DnsAddress, port: Port, cacheConnectionOption: cacheConnectionOption);
            var oldConnectionMultiplexerWrappers = _connectionMultiplexerWrappers;

            try
            {
                lock (_updateLock)
                {
                    Interlocked.Exchange(ref _configurationOptions, newConfigurationOptions);
                    Interlocked.Exchange(ref _connectionSelectionStrategy, CacheConnectionOption.CreateConnectionSelectionStrategy(cacheConnectionOption.ConnectionSelectionStrategy));

                    var newConnectionMultiplexerWrappers = new IConnectionMultiplexerWrapper[newNumConnections];
                    for (int i = 0; i < newNumConnections; i++)
                    {
                        newConnectionMultiplexerWrappers[i] = _connectionMultiplexerWrapperFactory.CreateConnectionMultiplexerWrapper(this);
                    }

                    Interlocked.Exchange(ref _connectionMultiplexerWrappers, newConnectionMultiplexerWrappers);

                    Logger.LogWarning($"UpdateCacheConnectionOption is called on {CacheNodeName}.{CachePoolName}");
            
                    // Dispose Old connections after 30 secs
                    _ = Task.Run(() => Task.Delay(TimeSpan.FromSeconds(30))
                        .ContinueWith((antecedent, info) => DisposeOldConnections((IConnectionMultiplexerWrapper[]?)info), oldConnectionMultiplexerWrappers,
                        TaskContinuationOptions.None));

                    return true;
                }
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, "Failed to update cache connection option");
                return false;
            }
        }

        public void ConnectionFailedHandler(object? sender, ConnectionFailedEventArgs e)
        {
            CacheClientMetricProvider.AddConnectionFailedEvent(CachePoolName, CacheNodeName, e);
        }

        public void ConnectionRestoredHandler(object? sender, ConnectionFailedEventArgs e)
        {
            CacheClientMetricProvider.AddConnectionRestoredEvent(CachePoolName, CacheNodeName, e);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetCacheNodeName(int replicaIndex)
        {
            return CachePoolName + '-' + replicaIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetDNSAddress(int replicaIndex, string cacheDomain)
        {
            return MonitoringConstants.IsLocalDevelopment ?
                "localhost" : GetCacheNodeName(replicaIndex) + '.' + CachePoolName + '.' + cacheDomain;
        }

        private static void DisposeOldConnections(IConnectionMultiplexerWrapper[]? oldConnectionMultiplexerWrappers)
        {
            if (oldConnectionMultiplexerWrappers == null || oldConnectionMultiplexerWrappers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < oldConnectionMultiplexerWrappers.Length; i++)
            {
                try
                {
                    var connectionMultiplexer = oldConnectionMultiplexerWrappers[i];
                    connectionMultiplexer?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to dispose old connections");
                }
            }
        }
    }
}
