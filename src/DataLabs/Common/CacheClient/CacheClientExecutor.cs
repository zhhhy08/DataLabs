namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using StackExchange.Redis;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    public class CacheClientExecutor
    {
        private static readonly ILogger<CacheClientExecutor> Logger = DataLabLoggerFactory.CreateLogger<CacheClientExecutor>();

        // Activity Monitor

        private static readonly ActivityMonitorFactory CacheClientGetLastCheckPointTimeAsync =
            new ("CacheClient.GetLastCheckPointTimeAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSendCheckPointAsync =
            new ("CacheClient.SendCheckPointAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSetKeyExpireAsync =
            new ("CacheClient.SetKeyExpireAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSendKeyExpireAsyncError =
            new ("CacheClient.SendKeyExpireAsyncError", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientDeleteKeyAsync =
            new ("CacheClient.DeleteKeyAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSetValueWithExpiryAsync =
            new ("CacheClient.SetValueWithExpiryAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSetValueIfMatchWithExpiryAsync =
            new ("CacheClient.SetValueIfMatchWithExpiryAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSetValueIfGreaterThanWithExpiryAsync =
            new ("CacheClient.SetValueIfGreaterThanWithExpiryAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientGetValueAsync =
            new ("CacheClient.GetValueAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientMGetValuesAsync =
            new ("CacheClient.MGetValuesAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientMGetValuesFromCacheNodeAsync =
            new("CacheClient.MGetValuesFromCacheNodeAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSendMGetKeysListAsync =
            new("CacheClient.SendMGetKeysListAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSortedSetAddAsync =
            new ("CacheClient.SortedSetAddAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSortedSetRemoveAsync =
            new ("CacheClient.SortedSetRemoveAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSortedSetRangeByRankAsync =
            new ("CacheClient.SortedSetRangeByRankAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSortedSetScoreAsync =
            new ("CacheClient.SortedSetScoreAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSortedSetRemoveRangeByScoreAsync =
            new ("CacheClient.SortedSetRemoveRangeByScoreAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory CacheClientSortedSetLengthAsync = 
            new ("CacheClient.GetSortedSetCountAsync", useDataLabsEndpoint: true);

        public const string CACHE_RESULT_OK = "OK";

        public bool CacheEnabled => _dataLabCachePoolsManager != null;
        private readonly DataLabCachePoolsManager? _dataLabCachePoolsManager;

        private readonly string _cacheMaxRetryToReplicasConfigKey;
        private readonly string _cacheMGetMaxBatchSizeConfigKey;
        
        private int _cacheMaxRetryToReplicas;
        private int _cacheMGetMaxBatchSize;

        private delegate Task<TResult> DelegateWithParams<TKey, TParamType, TResult>(DataLabCacheNode cacheNode, TKey key, params TParamType[] args);
        
        public CacheClientExecutor(
            DataLabsCacheType dataLabsCacheType, 
            IConfiguration configuration, 
            IConnectionMultiplexerWrapperFactory connectionMultiplexerWrapperFactory, 
            bool preCreateConnections)
        {
            var isPartnerCache = dataLabsCacheType == DataLabsCacheType.PARTNER_CACHE;
            var configPrefix = isPartnerCache ? SolutionConstants.PARTNER_CACHE_PREFIX : "";

            var cacheNumPoolConfig = configPrefix + SolutionConstants.CacheNumPools;
            var numCachePools = configuration.GetValue<int>(cacheNumPoolConfig);

            _cacheMaxRetryToReplicasConfigKey = configPrefix + SolutionConstants.CacheMaxRetryToReplicas;
            _cacheMaxRetryToReplicas = configuration.GetValueWithCallBack<int>(_cacheMaxRetryToReplicasConfigKey, UpdateCacheMaxRetryToReplicas, 5);
            GuardHelper.IsArgumentNonNegative(_cacheMaxRetryToReplicas);

            _cacheMGetMaxBatchSizeConfigKey = configPrefix + SolutionConstants.CacheMGetMaxBatchSize;
            _cacheMGetMaxBatchSize = configuration.GetValueWithCallBack<int>(_cacheMGetMaxBatchSizeConfigKey, UpdateCacheMGetMaxBatchSize, 100);
            GuardHelper.IsArgumentPositive(_cacheMGetMaxBatchSize);

            _dataLabCachePoolsManager = numCachePools == 0 ? null : 
                new DataLabCachePoolsManager(
                    configPrefix:  configPrefix, 
                    configuration: configuration, 
                    connectionMultiplexerWrapperFactory: connectionMultiplexerWrapperFactory, 
                    preCreateConnections: preCreateConnections);
        }

        public Task<CacheClientReadResult<byte[]?>?> GetValueAsync(string key, int readQuorum, CancellationToken cancellationToken)
        {
            var methodName = nameof(GetValueAsync);

            if (!CacheEnabled)
            {
                return Task.FromResult<CacheClientReadResult<byte[]?>?>(null);
            }

            return GetCacheResultAsync<object, byte[]?>(
                methodName: methodName,
                key: key,
                readQuorum: readQuorum,
                func: GetValueAsync,
                cancellationToken: cancellationToken);
        }

        public async Task<List<(int indexInClientResult, CacheClientReadResult<List<byte[]?>>?)>?> 
            MGetValuesAsync(IList<string> keys, int readQuorum, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                Logger.LogError("Cache is not enabled");
                return null;
            }

            GuardHelper.ArgumentNotNullOrEmpty(keys);

            using var monitor = CacheClientMGetValuesAsync.ToMonitor();
            
            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.NumOfKeys] = keys.Count;

                var mgetNodeToKeysMap = new MGetNodeToKeysMap(_cacheMGetMaxBatchSize);

                foreach (var key in keys)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }
                    else
                    {
                        var selectedNodes = GetReadCacheNodes(key);
                        if (selectedNodes.Count == 0)
                        {
                            continue;
                        }

                        var numNodes = selectedNodes.Count;
                        var selectedIndex = PickCacheNodeId(numNodes);
                        mgetNodeToKeysMap.AddKey(selectedNodes, selectedIndex, key);
                    }
                }

                var batchResults = new List<(List<string>, CacheClientReadResult<List<byte[]?>>?)>(mgetNodeToKeysMap.NumTotalBatches());

                await SendMGetAsync(readQuorum: readQuorum,
                    mgetNodeToKeysMap: mgetNodeToKeysMap,
                    batchResults: batchResults,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var keyResultMap = new Dictionary<string, (int, CacheClientReadResult<List<byte[]?>>?)>(keys.Count);

                foreach (var (batchKeys, clientResult) in batchResults)
                {
                    if (batchKeys?.Count > 0)
                    {
                        var hasSuccess = clientResult?.HasSuccess == true;

                        for (int i = 0; i < batchKeys.Count; i++)
                        {
                            var key = batchKeys[i];
                            int index = hasSuccess ? i : -1;
                            keyResultMap[key] = (index, clientResult);
                        }
                    }
                }

                var results = new List<(int, CacheClientReadResult<List<byte[]?>>?)>(keys.Count);
                for (int i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    if (keyResultMap.TryGetValue(key, out var result))
                    {
                        results.Add(result);
                    }
                    else
                    {
                        results.Add((-1, null));
                    }
                }

                monitor.OnCompleted();
                return results;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<CacheClientWriteResult<bool>?> DeleteKeyAsync(string key, CancellationToken cancellationToken)
        {
            var methodName = nameof(DeleteKeyAsync);

            if (!CacheEnabled)
            {
                return null;
            }

            var selectedNodes = GetWriteCacheNodes(key);
            if (selectedNodes.Count == 0)
            {
                return null;
            }

            var numNodes = selectedNodes.Count;
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

            // Write to all replicas
            Task<bool>[] tasksToRun = new Task<bool>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                tasksToRun[i] = DeleteKeyAsync(cacheNode: selectedNodes[i], key: key);
            }

            try
            {
                await Task.WhenAll(tasksToRun).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            return CreateCacheClientWriteResult(
                tasksToRun: tasksToRun,
                selectedNodes: selectedNodes,
                methodName: methodName,
                startStopWatchTimeStamp: startStopWatchTimeStamp);
        }

        public async Task<CacheClientWriteResult<bool>?> SetKeyExpireAsync(string key, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            var methodName = nameof(SetKeyExpireAsync);

            if (!CacheEnabled)
            {
                return null;
            }

            var selectedNodes = GetWriteCacheNodes(key);
            if (selectedNodes.Count == 0)
            {
                return null;
            }

            var numNodes = selectedNodes.Count;
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

            // Write to all replicas
            Task<bool>[] tasksToRun = new Task<bool>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                tasksToRun[i] = SetKeyExpireAsync(cacheNode: selectedNodes[i], key: key, expiry: expiry);
            }

            try
            {
                await Task.WhenAll(tasksToRun).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            return CreateCacheClientWriteResult(
                tasksToRun: tasksToRun,
                selectedNodes: selectedNodes,
                methodName: methodName,
                startStopWatchTimeStamp: startStopWatchTimeStamp);
        }

        public async Task<CacheClientWriteResult<bool>?> SetValueIfGreaterThanWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long greaterThanValue, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            var methodName = nameof(SetValueIfGreaterThanWithExpiryAsync);

            if (!CacheEnabled)
            {
                return null;
            }

            var selectedNodes = GetWriteCacheNodes(key);
            if (selectedNodes.Count == 0)
            {
                return null;
            }

            var numNodes = selectedNodes.Count;
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

            // Write to all replicas
            // TODO memory optimization for often new Task<bool>[]
            Task<bool>[] tasksToRun = new Task<bool>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                tasksToRun[i] = SetValueIfGreaterThanWithExpiryAsync(cacheNode: selectedNodes[i], key: key, readOnlyBytes: readOnlyBytes, greaterThanValue: greaterThanValue, expiry: expiry);
            }

            try
            {
                await Task.WhenAll(tasksToRun).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            return CreateCacheClientWriteResult(
                tasksToRun: tasksToRun,
                selectedNodes: selectedNodes,
                methodName: methodName,
                startStopWatchTimeStamp: startStopWatchTimeStamp);
        }

        public async Task<CacheClientWriteResult<bool>?> SetValueIfMatchWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long matchValue, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            var methodName = nameof(SetValueIfMatchWithExpiryAsync);

            if (!CacheEnabled)
            {
                return null;
            }

            var selectedNodes = GetWriteCacheNodes(key);
            if (selectedNodes.Count == 0)
            {
                return null;
            }

            var numNodes = selectedNodes.Count;
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

            // Write to all replicas
            Task<bool>[] tasksToRun = new Task<bool>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                tasksToRun[i] = SetValueIfMatchWithExpiryAsync(cacheNode: selectedNodes[i], key: key, readOnlyBytes: readOnlyBytes, matchValue: matchValue, expiry: expiry);
            }

            try
            {
                await Task.WhenAll(tasksToRun).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            return CreateCacheClientWriteResult(
                tasksToRun: tasksToRun,
                selectedNodes: selectedNodes,
                methodName: methodName,
                startStopWatchTimeStamp: startStopWatchTimeStamp);
        }

        public async Task<CacheClientWriteResult<bool>?> SetValueWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            var methodName = nameof(SetValueWithExpiryAsync);

            if (!CacheEnabled)
            {
                return null;
            }

            var selectedNodes = GetWriteCacheNodes(key);
            if (selectedNodes.Count == 0)
            {
                return null;
            }

            var numNodes = selectedNodes.Count;
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

            // Write to all replicas
            Task<bool>[] tasksToRun = new Task<bool>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                tasksToRun[i] = SetValueWithExpiryAsync(cacheNode: selectedNodes[i], key: key, readOnlyBytes: readOnlyBytes, expiry: expiry);
            }

            try
            {
                await Task.WhenAll(tasksToRun).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            return CreateCacheClientWriteResult(
                tasksToRun: tasksToRun,
                selectedNodes: selectedNodes,
                methodName: methodName,
                startStopWatchTimeStamp: startStopWatchTimeStamp);
        }

        public async Task<CacheClientWriteResult<bool>?> SetValueWithExpiryAsync(string key, string value, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            var methodName = nameof(SetValueWithExpiryAsync);

            if (!CacheEnabled)
            {
                return null;
            }

            var selectedNodes = GetWriteCacheNodes(key);
            if (selectedNodes.Count == 0)
            {
                return null;
            }

            var numNodes = selectedNodes.Count;
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

            // Write to all replicas
            Task<bool>[] tasksToRun = new Task<bool>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                tasksToRun[i] = SetValueWithExpiryAsync(cacheNode: selectedNodes[i], key: key, value: value, expiry: expiry);
            }

            try
            {
                await Task.WhenAll(tasksToRun).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            return CreateCacheClientWriteResult(
                tasksToRun: tasksToRun,
                selectedNodes: selectedNodes,
                methodName: methodName,
                startStopWatchTimeStamp: startStopWatchTimeStamp);
        }

        public async Task<CacheClientWriteResult<bool>?> SortedSetAddAsync(string key, string member, double score, CancellationToken cancellationToken)
        {
            var methodName = nameof(SortedSetAddAsync);

            if (!CacheEnabled)
            {
                return null;
            }

            var selectedNodes = GetWriteCacheNodes(key);
            if (selectedNodes.Count == 0)
            {
                return null;
            }

            var numNodes = selectedNodes.Count;
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

            // Write to all replicas
            Task<bool>[] tasksToRun = new Task<bool>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                tasksToRun[i] = SortedSetAddAsync(cacheNode: selectedNodes[i], key: key, member: member, score: score);
            }

            try
            {
                await Task.WhenAll(tasksToRun).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            return CreateCacheClientWriteResult(
                tasksToRun: tasksToRun,
                selectedNodes: selectedNodes,
                methodName: methodName,
                startStopWatchTimeStamp: startStopWatchTimeStamp);
        }

        public async Task<CacheClientWriteResult<bool>?> SortedSetRemoveAsync(string key, string member, CancellationToken cancellationToken)
        {
            var methodName = nameof(SortedSetRemoveAsync);

            if (!CacheEnabled)
            {
                return null;
            }

            var selectedNodes = GetWriteCacheNodes(key);
            if (selectedNodes.Count == 0)
            {
                return null;
            }

            var numNodes = selectedNodes.Count;
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

            // Write to all replicas
            Task<bool>[] tasksToRun = new Task<bool>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                tasksToRun[i] = SortedSetRemoveAsync(cacheNode: selectedNodes[i], key: key, member: member);
            }

            try
            {
                await Task.WhenAll(tasksToRun).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            return CreateCacheClientWriteResult(
                tasksToRun: tasksToRun,
                selectedNodes: selectedNodes,
                methodName: methodName,
                startStopWatchTimeStamp: startStopWatchTimeStamp);
        }

        public async Task<CacheClientWriteResult<long>?> SortedSetRemoveRangeByScoreAsync(string key, double minScore, double maxScore, CancellationToken cancellationToken)
        {
            var methodName = nameof(SortedSetRemoveRangeByScoreAsync);

            if (!CacheEnabled)
            {
                return null;
            }

            var selectedNodes = GetWriteCacheNodes(key);
            if (selectedNodes.Count == 0)
            {
                return null;
            }

            var numNodes = selectedNodes.Count;
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

            // Write to all replicas
            Task<long>[] tasksToRun = new Task<long>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                tasksToRun[i] = SortedSetRemoveRangeByScoreAsync(cacheNode: selectedNodes[i], key, minScore, maxScore);
            }

            try
            {
                await Task.WhenAll(tasksToRun).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            return CreateCacheClientWriteResult(
                tasksToRun: tasksToRun,
                selectedNodes: selectedNodes,
                methodName: methodName,
                startStopWatchTimeStamp: startStopWatchTimeStamp);
        }

        public Task<CacheClientReadResult<long>?> SortedSetLengthAsync(string key, int readQuorum, CancellationToken cancellationToken)
        {
            var methodName = nameof(SortedSetLengthAsync);

            if (!CacheEnabled)
            {
                return Task.FromResult<CacheClientReadResult<long>?>(null);
            }

            return GetCacheResultAsync<object, long>(
                methodName: methodName,
                key: key,
                readQuorum: readQuorum,
                func: SortedSetLengthAsync,
                cancellationToken: cancellationToken);
        }

        public Task<CacheClientReadResult<string?[]>?> SortedSetRangeByRankAsync(string key, long rangeStart, long rangeEnd, int readQuorum, CancellationToken cancellationToken)
        {
            var methodName = nameof(SortedSetRangeByRankAsync);

            if (!CacheEnabled)
            {
                return Task.FromResult<CacheClientReadResult<string?[]>?>(null);
            }

            return GetCacheResultAsync<long, string?[]>(
                methodName: methodName,
                key: key,
                readQuorum: readQuorum,
                func: null,
                cancellationToken: cancellationToken, 
                delegateWithParams: SortedSetRangeByRankAsync, 
                rangeStart, rangeEnd);
        }

        public Task<CacheClientReadResult<double?>?> SortedSetScoreAsync(string key, string member, int readQuorum, CancellationToken cancellationToken)
        {
            var methodName = nameof(SortedSetScoreAsync);

            if (!CacheEnabled)
            {
                return Task.FromResult<CacheClientReadResult<double?>?>(null);
            }

            return GetCacheResultAsync<string, double?>(
               methodName: methodName,
               key: key,
               readQuorum: readQuorum,
               func: null,
               cancellationToken: cancellationToken,
               delegateWithParams: SortedSetScoreAsync,
               member);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<DataLabCacheNode> GetReadCacheNodes(string key)
        {
            var keyHash = GetKeyHash(key);
            return _dataLabCachePoolsManager!.GetReadCacheNodes(keyHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<DataLabCacheNode> GetWriteCacheNodes(string key)
        {
            var keyHash = GetKeyHash(key);
            return _dataLabCachePoolsManager!.GetWriteCacheNodes(keyHash);
        }

        private Task UpdateCacheMaxRetryToReplicas(int newValue)
        {
            if (newValue < 0)
            {
                Logger.LogError("{config} must be equal or larger than 0", _cacheMaxRetryToReplicasConfigKey);
                return Task.CompletedTask;
            }

            var oldValue = _cacheMaxRetryToReplicas;
            if (oldValue != newValue)
            {
                if (Interlocked.CompareExchange(ref _cacheMaxRetryToReplicas, newValue, oldValue) == oldValue)
                {
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", _cacheMaxRetryToReplicasConfigKey, oldValue, newValue);
                }
            }

            return Task.CompletedTask;
        }

        private Task UpdateCacheMGetMaxBatchSize(int newValue)
        {
            if (newValue <= 0)
            {
                Logger.LogError("{config} must be larger than 0", _cacheMGetMaxBatchSizeConfigKey);
                return Task.CompletedTask;
            }

            var oldValue = _cacheMGetMaxBatchSize;
            if (oldValue != newValue)
            {
                if (Interlocked.CompareExchange(ref _cacheMGetMaxBatchSize, newValue, oldValue) == oldValue)
                {
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", _cacheMGetMaxBatchSizeConfigKey, oldValue, newValue);
                }
            }

            return Task.CompletedTask;
        }
       
        #region static methods

        public static async Task<DateTimeOffset> GetLastCheckPointTimeAsync(DataLabCacheNode cacheNode)
        {
            using var monitor = CacheClientGetLastCheckPointTimeAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;

                var multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();
                var resp = await db.ExecuteAsync("LASTSAVE").IgnoreContext();

                var respString = resp?.ToString();
                monitor.Activity["Response"] = respString;
                if (string.IsNullOrWhiteSpace(respString))
                {
                    monitor.OnCompleted();
                    return default;
                }

                // If it is not expected unitTimeSeconds
                // Let's throw exception
                var unitTimeSec = int.Parse(respString);
                var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(unitTimeSec);
                monitor.Activity["DateTime"] = dateTimeOffset.ToString(); ;

                monitor.OnCompleted();
                return dateTimeOffset;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public static async Task<string?> SendCheckPointAsync(DataLabCacheNode cacheNode, bool backgroundSave)
        {
            using var monitor = CacheClientSendCheckPointAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity["BackGroundSave"] = backgroundSave;

                var multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();
                string? respString = null;

                if (backgroundSave)
                {
                    var resp = await db.ExecuteAsync("BGSAVE").IgnoreContext();
                    respString = resp?.ToString();
                    monitor.Activity["BGSAVE"] = respString;
                }
                else
                {
                    var resp = await db.ExecuteAsync("SAVE").IgnoreContext();
                    respString = resp?.ToString();
                    monitor.Activity["SAVE"] = respString;
                }

                monitor.OnCompleted();
                return respString;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public static async Task<bool> SetKeyExpireAsync(DataLabCacheNode cacheNode, string key, TimeSpan? expiry)
        {
            using var monitor = CacheClientSetKeyExpireAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;
                monitor.Activity[SolutionConstants.CacheExpiry] = !expiry.HasValue ? "NoExpiry" : expiry.Value.ToString();

                var multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();
                bool result = await db.KeyExpireAsync(key, expiry).ConfigureAwait(false);

                monitor.Activity[SolutionConstants.CacheResult] = result;
                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public static async Task<bool> DeleteKeyAsync(DataLabCacheNode cacheNode, string key)
        {
            using var monitor = CacheClientDeleteKeyAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;

                var multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();
                bool result = await db.KeyDeleteAsync(key).ConfigureAwait(false);

                monitor.Activity[SolutionConstants.CacheResult] = result.ToString();
                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public static async Task<bool> SetValueWithExpiryAsync(DataLabCacheNode cacheNode, string key, ReadOnlyMemory<byte> readOnlyBytes, TimeSpan? expiry)
        {
            using var monitor = CacheClientSetValueWithExpiryAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;
            var keyExpireSetKv = CacheClientMetricProvider.KeyExpireSetNullPair;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;
                monitor.Activity[SolutionConstants.CacheExpiry] = !expiry.HasValue ? "NoExpiry" : expiry.Value.ToString();

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                bool result = await db.StringSetAsync(key, readOnlyBytes).ConfigureAwait(false);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;
                monitor.Activity[SolutionConstants.CacheResult] = result;

                // Unfortunately Garnet doesn't honor expiry parameter in stackExchange's stringSet.
                // we need to call it separately
                if (result && expiry.HasValue && expiry.Value != default)
                {
                    var keyExpireResult = await SendKeyExpireAsync(cacheNode.CacheNodeName, db, key, expiry, monitor.Activity).ConfigureAwait(false);
                    keyExpireSetKv = keyExpireResult ? CacheClientMetricProvider.KeyExpireSetSuccessPair : CacheClientMetricProvider.KeyExpireSetFailPair;
                }

                CacheClientMetricProvider.CacheClientSetValueWithExpiryMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, result),
                    keyExpireSetKv,
                    MonitoringConstants.GetSuccessDimension(true));
                
                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientSetValueWithExpiryMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, false),
                        keyExpireSetKv,
                        MonitoringConstants.GetSuccessDimension(false));
                }

                throw;
            }
        }

        public static Task<bool> SetValueAsync(DataLabCacheNode cacheNode, string key, ReadOnlyMemory<byte> readOnlyBytes)
        {
            return SetValueWithExpiryAsync(cacheNode, key, readOnlyBytes, null);
        }

        public static Task<bool> SetValueAsync(DataLabCacheNode cacheNode, string key, string value)
        {
            return SetValueWithExpiryAsync(cacheNode, key, new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(value)), null);
        }

        public static Task<bool> SetValueWithExpiryAsync(DataLabCacheNode cacheNode, string key, string value, TimeSpan? expiry)
        {
            return SetValueWithExpiryAsync(cacheNode, key, new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(value)), expiry);
        }

        public static async Task<bool> SetValueIfMatchWithExpiryAsync(DataLabCacheNode cacheNode, string key, ReadOnlyMemory<byte> readOnlyBytes, long matchValue, TimeSpan? expiry)
        {
            using var monitor = CacheClientSetValueIfMatchWithExpiryAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;
            var keyExpireSetKv = CacheClientMetricProvider.KeyExpireSetNullPair;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;
                monitor.Activity[SolutionConstants.CacheExpiry] = !expiry.HasValue ? "NoExpiry" : expiry.Value.ToString();

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                var matchValueBytes = BitConverter.GetBytes(matchValue);

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                var resultWrapper = await db.ExecuteAsync("SETIFPM", key, readOnlyBytes, matchValueBytes).ConfigureAwait(false);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;

                bool result = CheckOkResponse(resultWrapper);
                monitor.Activity[SolutionConstants.CacheResult] = result;

                if (result)
                {
                    // Unfortunately Garnet doesn't honor expiry parameter in stackExchange's stringSet.
                    // we need to call it separately
                    if (expiry.HasValue && expiry.Value != default)
                    {
                        var keyExpireResult = await SendKeyExpireAsync(cacheNode.CacheNodeName, db, key, expiry, monitor.Activity).ConfigureAwait(false);
                        keyExpireSetKv = keyExpireResult ? CacheClientMetricProvider.KeyExpireSetSuccessPair : CacheClientMetricProvider.KeyExpireSetFailPair;
                    }
                }
                else
                {
                    // Unexpected result
                    monitor.Activity[SolutionConstants.CacheRawResult] = resultWrapper?.ToString();
                }

                CacheClientMetricProvider.CacheClientSetValueIfMatchWithExpiryMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, result),
                    keyExpireSetKv,
                    MonitoringConstants.GetSuccessDimension(true));

                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientSetValueIfMatchWithExpiryMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, false),
                        keyExpireSetKv,
                        MonitoringConstants.GetSuccessDimension(false));
                }

                throw;
            }
        }

        public static Task<bool> SetValueIfMatchAsync(DataLabCacheNode cacheNode, string key, ReadOnlyMemory<byte> readOnlyBytes, long matchValue)
        {
            return SetValueIfMatchWithExpiryAsync(cacheNode, key, readOnlyBytes, matchValue, null);
        }

        public static Task<bool> SetValueIfGreaterThanAsync(DataLabCacheNode cacheNode, string key, ReadOnlyMemory<byte> readOnlyBytes, long greaterThanValue)
        {
            return SetValueIfGreaterThanWithExpiryAsync(cacheNode, key, readOnlyBytes, greaterThanValue, null);
        }

        public static async Task<bool> SetValueIfGreaterThanWithExpiryAsync(DataLabCacheNode cacheNode, string key, ReadOnlyMemory<byte> readOnlyBytes, long greaterThanValue, TimeSpan? expiry)
        {
            using var monitor = CacheClientSetValueIfGreaterThanWithExpiryAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;
            var keyExpireSetKv = CacheClientMetricProvider.KeyExpireSetNullPair;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;
                monitor.Activity[SolutionConstants.CacheExpiry] = !expiry.HasValue ? "NoExpiry" : expiry.Value.ToString();

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                var timeStampBytes = BitConverter.GetBytes(greaterThanValue);

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                var resultWrapper = await db.ExecuteAsync("SETWPIFPGT", key, readOnlyBytes, timeStampBytes).ConfigureAwait(false);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;

                bool result = CheckOkResponse(resultWrapper);
                monitor.Activity[SolutionConstants.CacheResult] = result;

                if (result)
                {
                    // Unfortunately Garnet doesn't honor expiry parameter in stackExchange's stringSet.
                    // we need to call it separately
                    if (expiry.HasValue && expiry.Value != default)
                    {
                        var keyExpireResult = await SendKeyExpireAsync(cacheNode.CacheNodeName, db, key, expiry, monitor.Activity).ConfigureAwait(false);
                        keyExpireSetKv = keyExpireResult ? CacheClientMetricProvider.KeyExpireSetSuccessPair : CacheClientMetricProvider.KeyExpireSetFailPair;
                    }
                }
                else
                {
                    // Unexpected result
                    monitor.Activity[SolutionConstants.CacheRawResult] = resultWrapper?.ToString();
                }

                CacheClientMetricProvider.CacheClientSetValueIfGreaterThanWithExpiryMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, result),
                    keyExpireSetKv,
                    MonitoringConstants.GetSuccessDimension(true));

                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientSetValueIfGreaterThanWithExpiryMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, false),
                        keyExpireSetKv,
                        MonitoringConstants.GetSuccessDimension(false));
                }
                
                throw;
            }
        }

        public static async Task<byte[]?> GetValueAsync(DataLabCacheNode cacheNode, string key)
        {
            using var monitor = CacheClientGetValueAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                byte[]? value = await db.StringGetAsync(key).ConfigureAwait(false);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;

                var found = value?.Length > 0;
                monitor.Activity[SolutionConstants.ResourceFound] = found;

                CacheClientMetricProvider.CacheClientGetValueMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    found ? CacheClientMetricProvider.CacheFoundPair : CacheClientMetricProvider.CacheNotFoundPair,
                    MonitoringConstants.GetSuccessDimension(true));

                monitor.OnCompleted();
                return !found ? null : value;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientGetValueMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        CacheClientMetricProvider.CacheNotFoundPair,
                        MonitoringConstants.GetSuccessDimension(false));
                }
                throw;
            }
        }

        public static async Task<List<byte[]?>> MGetValuesFromCacheNodeAsync(DataLabCacheNode cacheNode, IList<string> keys)
        {
            GuardHelper.ArgumentNotNullOrEmpty(keys);

            using var monitor = CacheClientMGetValuesFromCacheNodeAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.NumOfKeys] = keys.Count;

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                
                RedisKey[] redisKeys = new RedisKey[keys.Count];
                for (int i = 0; i < keys.Count; i++)
                {
                    redisKeys[i] = keys[i];
                }

                RedisValue[] values = await db.StringGetAsync(redisKeys).ConfigureAwait(false);

                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;

                // Result
                var results = new List<byte[]?>(keys.Count);
                int numFounds = 0;
                for (int i = 0; i < values.Length; i++)
                {
                    var value = values[i];
                    
                    // Check if value is nil
                    if (value == RedisValue.Null)
                    {
                        results.Add(null);
                    }
                    else
                    {
                        results.Add(value);
                        numFounds++;
                    }
                }

                var numNotFounds = keys.Count - numFounds;

                monitor.Activity[SolutionConstants.NumResourceNotFound] = numNotFounds;
                monitor.Activity[SolutionConstants.ResourceFound] = numFounds;

                CacheClientMetricProvider.CacheClientMGetValuesMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    MonitoringConstants.GetSuccessDimension(true));

                CacheClientMetricProvider.CacheClientMGetValuesNumKeysMetric.Record(keys.Count,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    MonitoringConstants.GetSuccessDimension(true));
           
                CacheClientMetricProvider.CacheClientMGetValuesFoundCounter.Add(numFounds,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    CacheClientMetricProvider.CacheFoundPair,
                    MonitoringConstants.GetSuccessDimension(true));

                CacheClientMetricProvider.CacheClientMGetValuesFoundCounter.Add(numNotFounds,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    CacheClientMetricProvider.CacheNotFoundPair,
                    MonitoringConstants.GetSuccessDimension(true));

                monitor.OnCompleted();
                return results;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientMGetValuesMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        MonitoringConstants.GetSuccessDimension(false));

                    CacheClientMetricProvider.CacheClientMGetValuesNumKeysMetric.Record(keys.Count,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        MonitoringConstants.GetSuccessDimension(false));

                    CacheClientMetricProvider.CacheClientMGetValuesFoundCounter.Add(keys.Count,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        CacheClientMetricProvider.CacheNotFoundPair,
                        MonitoringConstants.GetSuccessDimension(false));
                }
                throw;
            }
        }

        public static async Task<bool> SortedSetAddAsync(DataLabCacheNode cacheNode, string key, string member, double score)
        {
            using var monitor = CacheClientSortedSetAddAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;
                monitor.Activity[SolutionConstants.Member] = member;
                monitor.Activity[SolutionConstants.Score] = score;

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                var result = await db.SortedSetAddAsync(key, member, score).ConfigureAwait(false);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;
                monitor.Activity[SolutionConstants.CacheResult] = result;

                CacheClientMetricProvider.CacheClientSortedSetAddMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, result),
                    MonitoringConstants.GetSuccessDimension(true));

                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientSortedSetAddMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, false),
                        MonitoringConstants.GetSuccessDimension(false));
                }

                throw;
            }
        }

        public static async Task<bool> SortedSetRemoveAsync(DataLabCacheNode cacheNode, string key, string member)
        {
            using var monitor = CacheClientSortedSetRemoveAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;
                monitor.Activity[SolutionConstants.Member] = member;

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                var result = await db.SortedSetRemoveAsync(key, member).ConfigureAwait(false);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;
                monitor.Activity[SolutionConstants.CacheResult] = result;

                CacheClientMetricProvider.CacheClientSortedSetRemoveMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, result),
                    MonitoringConstants.GetSuccessDimension(true));

                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientSortedSetRemoveMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, false),
                        MonitoringConstants.GetSuccessDimension(false));
                }

                throw;
            }
        }

        public static Task<string?[]> SortedSetRangeByRankAsync(DataLabCacheNode cacheNode, string key, params long[] args)
        {
            GuardHelper.ArgumentConstraintCheck(args.Length == 2);
            return SortedSetRangeByRankAsync(cacheNode, key, args[0], args[1]);
        }

        public static async Task<string?[]> SortedSetRangeByRankAsync(DataLabCacheNode cacheNode, string key, long rangeStart, long rangeEnd)
        {
            using var monitor = CacheClientSortedSetRangeByRankAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;
                monitor.Activity[SolutionConstants.RangeStart] = rangeStart;
                monitor.Activity[SolutionConstants.RangeEnd] = rangeEnd;

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                var result = await db.SortedSetRangeByRankAsync(key, rangeStart, rangeEnd).ConfigureAwait(false);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;
                monitor.Activity[SolutionConstants.CacheResult] = result.Length;

                CacheClientMetricProvider.CacheClientSortedSetRangeByRankMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, result.Length > 0),
                    MonitoringConstants.GetSuccessDimension(true));

                monitor.OnCompleted();
                return result.ToStringArray();
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientSortedSetRangeByRankMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, false),
                        MonitoringConstants.GetSuccessDimension(false));
                }

                throw;
            }
        }

        public static Task<double?> SortedSetScoreAsync(DataLabCacheNode cacheNode, string key, params string[] args)
        {
            GuardHelper.ArgumentConstraintCheck(args.Length == 1);
            return SortedSetScoreAsync(cacheNode, key, args[0]);
        }

        public static async Task<double?> SortedSetScoreAsync(DataLabCacheNode cacheNode, string key, string member)
        {
            using var monitor = CacheClientSortedSetScoreAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;
                monitor.Activity[SolutionConstants.Member] = member;

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                double? result = await db.SortedSetScoreAsync(key, member).ConfigureAwait(false);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;
                monitor.Activity[SolutionConstants.CacheResult] = result;

                CacheClientMetricProvider.CacheClientSortedSetScoreMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, result != null),
                    MonitoringConstants.GetSuccessDimension(true));

                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientSortedSetScoreMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, false),
                        MonitoringConstants.GetSuccessDimension(false));
                }
                
                throw;
            }
        }

        public static async Task<long> SortedSetRemoveRangeByScoreAsync(DataLabCacheNode cacheNode, string key, double minScore, double maxScore)
        {
            using var monitor = CacheClientSortedSetRemoveRangeByScoreAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;
                monitor.Activity[SolutionConstants.MinScore] = minScore;
                monitor.Activity[SolutionConstants.MaxScore] = maxScore;

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                var result = await db.SortedSetRemoveRangeByScoreAsync(key, minScore, maxScore);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;
                monitor.Activity[SolutionConstants.CacheResult] = result;

                CacheClientMetricProvider.CacheClientSortedSetRemoveRangeByScoreMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, result > 0),
                    MonitoringConstants.GetSuccessDimension(true));

                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientSortedSetRemoveRangeByScoreMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, false),
                        MonitoringConstants.GetSuccessDimension(false));
                }

                throw;
            }
        }

        public static async Task<long> SortedSetLengthAsync(DataLabCacheNode cacheNode, string key)
        {
            using var monitor = CacheClientSortedSetLengthAsync.ToMonitor();

            long startStopWatchTimeStamp = 0;
            IConnectionMultiplexer? multiplexer = null;

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNode.CacheNodeName;
                monitor.Activity[SolutionConstants.CacheKey] = key;

                multiplexer = await GetConnectionMultiplexerAsync(cacheNode, monitor.Activity, default).ConfigureAwait(false);
                var db = multiplexer.GetDatabase();

                startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                var result = await db.SortedSetLengthAsync(key);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                monitor.Activity[SolutionConstants.CacheCommandElapsed] = elapsed;
                monitor.Activity[SolutionConstants.CacheResult] = result;

                CacheClientMetricProvider.CacheClientSortedSetLengthMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, result > 0),
                    MonitoringConstants.GetSuccessDimension(true));

                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                monitor.OnError(ex);

                if (multiplexer != null)
                {
                    CacheClientMetricProvider.CacheClientSortedSetLengthMetric.Record(elapsed,
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNode.CacheNodeName),
                        new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheResultDimension, false),
                        MonitoringConstants.GetSuccessDimension(false));
                }
                
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static ulong GetKeyHash(string key)
        {
            var bytes = Encoding.UTF8.GetBytes(key);
            return HashUtils.MurmurHash3x64(bytes);
        }

        private static async Task<bool> SendKeyExpireAsync(string cacheNodeName, IDatabase db, string key, TimeSpan? expiry, IActivity activity)
        {
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

            try
            {
                var keyExpireResult = await db.KeyExpireAsync(key, expiry).ConfigureAwait(false);
                var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;

                CacheClientMetricProvider.CacheClientSendKeyExpireMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNodeName),
                    MonitoringConstants.GetSuccessDimension(true));

                activity[SolutionConstants.CacheExpiryCommandElapsed] = elapsed;
                activity[SolutionConstants.CacheExpiryResult] = keyExpireResult;

                return keyExpireResult;
            }
            catch (Exception ex)
            {
                int elapsed = startStopWatchTimeStamp > 0 ? (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds : 0;

                // Let's not throw exception when KeyExpireAsync fails because anyway Garnet will compact old data and we will compare the old data before returning in our side as well
                // KeyExpire is not critical issue in our case
                using var monitor = CacheClientSendKeyExpireAsyncError.ToMonitor();
                monitor.Activity[SolutionConstants.CacheNodeName] = cacheNodeName;
                monitor.OnError(ex);
                
                CacheClientMetricProvider.CacheClientSendKeyExpireMetric.Record(elapsed,
                    new KeyValuePair<string, object?>(CacheClientMetricProvider.CacheNodeDimension, cacheNodeName),
                    MonitoringConstants.GetSuccessDimension(false));

                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ValueTask<IConnectionMultiplexer> GetConnectionMultiplexerAsync(
            DataLabCacheNode cacheNode,
            IActivity? activity,
            CancellationToken cancellationToken)
        {
            var multiplexerWrapper = cacheNode.PickConnectionMultiplexerWrapper();
            return multiplexerWrapper.CreateConnectionMultiplexerAsync(activity, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckOkResponse(RedisResult cacheResult)
        {
            return !cacheResult.IsNull &&
                cacheResult.Resp2Type == ResultType.SimpleString &&
                CACHE_RESULT_OK.EqualsInsensitively((string?)cacheResult);
        }

        private Task<CacheClientReadResult<TResult>?> GetCacheResultAsync<TParamType, TResult>(
            string methodName,
            string key,
            int readQuorum,
            Func<DataLabCacheNode, string, Task<TResult>>? func, // This is most frequent/primary function such as GetValue
            CancellationToken cancellationToken,
            DelegateWithParams<string, TParamType, TResult>? delegateWithParams = null, // This is for delegate with params
            params TParamType[] args)
        {
            if (!CacheEnabled)
            {
                return Task.FromResult<CacheClientReadResult<TResult>?>(null);
            }

            var selectedNodes = GetReadCacheNodes(key);
            if (selectedNodes.Count == 0)
            {
                return Task.FromResult<CacheClientReadResult<TResult>?>(null);
            }

            var numNodes = selectedNodes.Count;
            var selectedIndex = PickCacheNodeId(numNodes);

            return GetCacheResultAsync(
                methodName: methodName,
                histogramMetric: CacheClientMetricProvider.CacheClientReadCallMetric,
                key: key,
                readQuorum: readQuorum,
                selectedNodes: selectedNodes,
                selectedIndex: selectedIndex,
                func: func,
                cancellationToken: cancellationToken,
                delegateWithParams: delegateWithParams,
                args: args);
        }

        private Task<CacheClientReadResult<List<TResult>>?> GetCacheMultiResultAsync<TParamType, TResult>(
            string methodName,
            IList<string> keys,
            int readQuorum,
            List<DataLabCacheNode> selectedNodes,
            int selectedIndex,
            Func<DataLabCacheNode, IList<string>, Task<List<TResult>>> func,
            CancellationToken cancellationToken,
            DelegateWithParams<IList<string>, TParamType, List<TResult>>? delegateWithParams = null, // This is for delegate with params
            params TParamType[] args)
        {
            if (!CacheEnabled || selectedNodes == null || selectedNodes.Count == 0)
            {
                return Task.FromResult<CacheClientReadResult<List<TResult>>?>(null);
            }

            return GetCacheResultAsync(
              methodName: methodName,
              histogramMetric: CacheClientMetricProvider.CacheClientReadMultiResultCallMetric,
              key: keys,
              readQuorum: readQuorum,
              selectedNodes: selectedNodes,
              selectedIndex: selectedIndex,
              func: func,
              cancellationToken: cancellationToken,
              delegateWithParams: delegateWithParams,
              args: args);
        }

        private async Task<CacheClientReadResult<TResult>?> GetCacheResultAsync<TKey, TParamType, TResult>(
            string methodName,
            Histogram<int> histogramMetric,
            TKey key,
            int readQuorum,
            List<DataLabCacheNode> selectedNodes,
            int selectedIndex,
            Func<DataLabCacheNode, TKey, Task<TResult>>? func, // This is most frequent/primary function such as GetValue
            CancellationToken cancellationToken,
            DelegateWithParams<TKey, TParamType, TResult>? delegateWithParams = null, // This is for delegate with params, let's use predefine delegate to avoid unnecessary lambda creation
            params TParamType[] args)
        {
            if (!CacheEnabled || selectedNodes == null || selectedNodes.Count == 0)
            {
                return null;
            }

            GuardHelper.IsArgumentPositive(readQuorum);
            GuardHelper.ArgumentConstraintCheck((func != null && delegateWithParams == null) || (func == null && delegateWithParams != null)); // either one, not both

            selectedIndex %= selectedNodes.Count;
            var numNodes = selectedNodes.Count;
            readQuorum = Math.Min(readQuorum, numNodes);
            int numTry = 1 + Math.Min(_cacheMaxRetryToReplicas, numNodes - 1);

            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();
            var cacheClientReadResult = new CacheClientReadResult<TResult>(selectedNodes, readQuorum);

            if (readQuorum > 1)
            {
                Task<TResult>[] tasksToRun = new Task<TResult>[readQuorum];
                for (int i = 0; i < readQuorum; i++)
                {
                    var indexInNodes = (selectedIndex + i) % numNodes;
                    var cacheNode = selectedNodes[indexInNodes];
                    if (func != null)
                    {
                        tasksToRun[i] = func(cacheNode, key);
                    }
                    else
                    {
                        tasksToRun[i] = delegateWithParams!(cacheNode, key, args);
                    }
                }

                try
                {
                    await Task.WhenAll(tasksToRun).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                List<DataLabCacheNode>? otherRemainingNodes = null;
                int numOtherNodes = numNodes - readQuorum;

                for (int i = 0; i < readQuorum; i++)
                {
                    var task = tasksToRun[i];
                    var indexInNodes = (selectedIndex + i) % numNodes;
                    var cacheNode = selectedNodes[indexInNodes];

                    var cacheException = task.Exception?.GetFirstInnerException(); // when exception exists, call task.Result will throw exception. we have to check exception first
                    var cacheResult = (cacheException == null && task.Status == TaskStatus.RanToCompletion) ? task.Result : default;

                    AddCacheClientReadResult(cacheResult, cacheException, cacheNode, cacheClientReadResult!);

                    if (cacheException != null && numOtherNodes > 0)
                    {
                        if (otherRemainingNodes == null)
                        {
                            otherRemainingNodes = new List<DataLabCacheNode>(numOtherNodes);
                            int startOtherNodeIndex = (selectedIndex + readQuorum);
                            for (int otherIndex = 0; otherIndex < numOtherNodes; otherIndex++)
                            {
                                var otherNode = selectedNodes[(startOtherNodeIndex + otherIndex) % numNodes];
                                otherRemainingNodes.Add(otherNode);
                            }
                        }

                        // We still have some nodes to try
                        if (otherRemainingNodes.Count > 0)
                        {
                            // We still have some nodes to try
                            cancellationToken.ThrowIfCancellationRequested();

                            // Pick otherNode randomly
                            var tryOtherIndex = otherRemainingNodes.Count == 1 ? 0 : Random.Shared.Next(0, otherRemainingNodes.Count);
                            var tryOtherCacheNode = otherRemainingNodes[tryOtherIndex];

                            try
                            {
                                var retriedResult = func != null ?
                                    await func(tryOtherCacheNode, key).ConfigureAwait(false) :
                                    await delegateWithParams!(tryOtherCacheNode, key, args).ConfigureAwait(false);

                                AddCacheClientReadResult(retriedResult, null, tryOtherCacheNode, cacheClientReadResult);
                            }
                            catch (Exception retriedEx)
                            {
                                AddCacheClientReadResult(default, retriedEx, tryOtherCacheNode, cacheClientReadResult!);
                            }
                            finally
                            {
                                otherRemainingNodes.RemoveAt(tryOtherIndex);
                            }
                        }
                    }
                }
            }
            else
            {
                var tryNodes = selectedNodes;
                var tryIndex = selectedIndex;

                for (int i = 0; i < numTry; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var tryCacheNode = tryNodes[tryIndex % tryNodes.Count]!;

                    try
                    {
                        var cacheResult = func != null ?
                            await func(tryCacheNode, key).ConfigureAwait(false) :
                            await delegateWithParams!(tryCacheNode, key, args).ConfigureAwait(false);

                        AddCacheClientReadResult(cacheResult, null, tryCacheNode, cacheClientReadResult);
                        break;
                    }
                    catch (Exception cacheException)
                    {
                        AddCacheClientReadResult(default, cacheException, tryCacheNode, cacheClientReadResult!);

                        if (tryNodes.Count > 1 && i < numTry - 1)
                        {
                            // Cache Node failed, don't try to just next node because it will cause bias
                            // Let's pick another random node
                            if (ReferenceEquals(tryNodes, selectedNodes))
                            {
                                // To avoid possible issue, let's copy original selectedNodes to tryNodes when node failed
                                tryNodes = new List<DataLabCacheNode>(selectedNodes);
                            }

                            // Delete failed Node
                            tryNodes.RemoveAt(tryIndex % tryNodes.Count);

                            if (tryNodes.Count == 1)
                            {
                                tryIndex = 0;
                            }
                            else
                            {
                                tryIndex = Random.Shared.Next(0, tryNodes.Count);
                            }
                        }
                    }
                }
            }

            var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
            CacheClientMetricProvider.RecordCacheClientReadCallMetric(
                histogramMetric: histogramMetric,
                elapsed: elapsed,
                lastTriedCacheNodeName:
                    cacheClientReadResult.HasSuccess ?
                    cacheClientReadResult.LastSuccessCacheNode?.CacheNodeName :
                    cacheClientReadResult.LastFailedCacheNode?.CacheNodeName,
                methodName: methodName,
                numTriedNodes:
                    cacheClientReadResult.SuccessNodeCount + cacheClientReadResult.FailedNodeCount,
                success: cacheClientReadResult.HasSuccess);

            return cacheClientReadResult;
        }

        private static CacheClientWriteResult<T> CreateCacheClientWriteResult<T>(
            Task<T>[] tasksToRun,
            List<DataLabCacheNode> selectedNodes,
            string methodName,
            long startStopWatchTimeStamp)
        {
            var cacheClientWriteResult = new CacheClientWriteResult<T>(selectedNodes);

            var numTasks = tasksToRun.Length;
            for (int i = 0; i < numTasks; i++)
            {
                var task = tasksToRun[i];
                var cacheNode = selectedNodes[i];
                if (task.Exception == null && task.Status == TaskStatus.RanToCompletion)
                {
                    var cacheClientNodeResult = new CacheClientNodeResult<T>()
                    {
                        CacheNode = cacheNode,
                        Result = task.Result,
                        IsSuccess = true
                    };

                    cacheClientWriteResult.AddSuccessNode(cacheClientNodeResult);
                }
                else
                {
                    var cacheClientNodeResult = new CacheClientNodeResult<T>()
                    {
                        CacheNode = cacheNode,
                        Result = default!,
                        Exception = task.Exception?.GetFirstInnerException(),
                        IsSuccess = false
                    };

                    cacheClientWriteResult.AddFailedNode(cacheClientNodeResult);
                }
            }

            var elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
            CacheClientMetricProvider.RecordCacheClientWriteCallMetric(
                elapsed: elapsed, 
                methodName: methodName, 
                numSuccessCount: cacheClientWriteResult.SuccessNodeCount, 
                numFailedCount: cacheClientWriteResult.FailedNodeCount);

            return cacheClientWriteResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PickCacheNodeId(int nodeCount)
        {
            if (nodeCount == 1)
            {
                return 0;
            }

            // For now, pick random
            return Random.Shared.Next(0, nodeCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddCacheClientReadResult<TResult>(
            TResult result,
            Exception? exception,
            DataLabCacheNode cacheNode,
            CacheClientReadResult<TResult> cacheClientReadResult)
        {
            if (exception == null)
            {
                var cacheClientNodeResult = new CacheClientNodeResult<TResult>()
                {
                    CacheNode = cacheNode,
                    Result = result,
                    IsSuccess = true
                };

                cacheClientReadResult.AddSuccessNode(cacheClientNodeResult);
            }
            else
            {
                var cacheClientNodeResult = new CacheClientNodeResult<TResult>()
                {
                    CacheNode = cacheNode,
                    Result = default!,
                    Exception = exception,
                    IsSuccess = false
                };

                cacheClientReadResult.AddFailedNode(cacheClientNodeResult);
            }
        }

        #endregion

        #region MGet Related Codes

        private class MGetNodeToKeysMap
        {
            public readonly Dictionary<DataLabCacheNode, KeysList> _nodeToKeysMap;
            
            private readonly int _maxBatchSize;

            public MGetNodeToKeysMap(int maxBatchSize)
            {
                _nodeToKeysMap = new Dictionary<DataLabCacheNode, KeysList>();
                _maxBatchSize = maxBatchSize;
            }

            public void AddKey(List<DataLabCacheNode> selectedNodes, int selectedIndex, string key)
            {
                var cacheNode = selectedNodes[selectedIndex];
                if (!_nodeToKeysMap.TryGetValue(cacheNode, out var keysList))
                {
                    keysList = new KeysList(_maxBatchSize, selectedNodes, selectedIndex);
                    _nodeToKeysMap.Add(cacheNode, keysList);
                }
                keysList.Add(key);
            }

            public int NumTotalBatches()
            {
                int numBatches = 0;
                foreach (var keysList in _nodeToKeysMap.Values)
                {
                    numBatches += keysList.BatchCount;
                }
                return numBatches;
            }
        }

        private class KeysList
        {
            public int KeyCount => _count;
            public int BatchCount => _multilist == null ? 1 : _multilist.Count;
            public LinkedList<List<string>>? _multilist;
            public List<string> _last;
            public readonly int _maxBatchSize;
            public readonly List<DataLabCacheNode> _selectedCacheNodes;
            public readonly int _selectedIndex;

            private int _count;

            public KeysList(int maxBatchSize, List<DataLabCacheNode> selectedCacheNodes, int selectedIndex)
            {
                _maxBatchSize = maxBatchSize;
                _last = new List<string>(Math.Min(maxBatchSize, 100));
                _selectedCacheNodes = selectedCacheNodes;
                _selectedIndex = selectedIndex;
            }

            public void Add(string key)
            {
                _count++;

                if (_last.Count >= _maxBatchSize)
                {
                    // We have more than maxBatchSize
                    // Let's create linked list
                    if (_multilist == null)
                    {
                        // since _multilist is delayed create
                        // when we create linked list, we have to add first _last to linked list
                        _multilist = new LinkedList<List<string>>();
                        _multilist.AddLast(_last);
                    }

                    _last = new List<string>(Math.Min(_maxBatchSize, 100));
                    _multilist.AddLast(_last);
                }

                _last.Add(key);
            }
        }

        private async Task SendMGetAsync(
            int readQuorum,
            MGetNodeToKeysMap mgetNodeToKeysMap,
            List<(List<string>, CacheClientReadResult<List<byte[]?>>?)> batchResults,
            CancellationToken cancellationToken)
        {
            Task[] tasksToRun = new Task[mgetNodeToKeysMap._nodeToKeysMap.Count];
            int taskIndex = 0;

            foreach (var kv in mgetNodeToKeysMap._nodeToKeysMap)
            {
                tasksToRun[taskIndex++] = SendMGetKeysListAsync(
                    readQuorum: readQuorum, 
                    keysList: kv.Value, 
                    batchResults: batchResults,
                    cancellationToken: cancellationToken);
            }

            await Task.WhenAll(tasksToRun).ConfigureAwait(false);
        }

        private async Task SendMGetKeysListAsync(
            int readQuorum,
            KeysList keysList,
            List<(List<string>, CacheClientReadResult<List<byte[]?>>?)> batchResults,
            CancellationToken cancellationToken)
        {
            using var monitor = CacheClientSendMGetKeysListAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.NumOfKeys] = keysList.KeyCount;
                monitor.Activity[SolutionConstants.NumBatches] = keysList.BatchCount;

                var methodName = nameof(MGetValuesFromCacheNodeAsync);

                if (keysList._multilist != null)
                {
                    // Since this is going to the same cacheNode
                    // Let's send one batch after one batch for the same cacheNode
                    // This will reduce the number of connections
                    foreach (var keys in keysList._multilist)
                    {
                        var result = await GetCacheMultiResultAsync<object, byte[]?>(
                            methodName: methodName,
                            keys: keys,
                            readQuorum: readQuorum,
                            selectedNodes: keysList._selectedCacheNodes,
                            selectedIndex: keysList._selectedIndex,
                            func: MGetValuesFromCacheNodeAsync,
                            cancellationToken: cancellationToken,
                            delegateWithParams: null).ConfigureAwait(false);

                        lock(batchResults)
                        {
                            batchResults.Add((keys, result));
                        }
                    }
                }
                else
                {
                    var keys = keysList._last;
                    var result = await GetCacheMultiResultAsync<object, byte[]?>(
                        methodName: methodName,
                        keys: keys,
                        readQuorum: readQuorum,
                        selectedNodes: keysList._selectedCacheNodes,
                        selectedIndex: keysList._selectedIndex,
                        func: MGetValuesFromCacheNodeAsync,
                        cancellationToken: cancellationToken,
                        delegateWithParams: null).ConfigureAwait(false);

                    lock (batchResults)
                    {
                        batchResults.Add((keys, result));
                    }
                }

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                // In MGET, we don't throw exception because we can still get the result of other keys
                monitor.OnError(ex);
            }
        }

        #endregion
    }
}
