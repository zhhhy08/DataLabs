namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class CacheClient : ICacheClient
    {
        private static readonly ActivityMonitorFactory CacheClientGetCollectionValuesAsync =
            new("CacheClient.GetCollectionValuesAsync", useDataLabsEndpoint: true);

        public bool CacheEnabled => _cacheClientExecutor.CacheEnabled;
        internal CacheClientExecutor CacheClientExecutor => _cacheClientExecutor;

        private readonly CacheClientExecutor _cacheClientExecutor;

        public CacheClient(
            DataLabsCacheType dataLabsCacheType,
            IConfiguration configuration,
            IConnectionMultiplexerWrapperFactory connectionMultiplexerWrapperFactory,
            bool preCreateConnections)
        {
            _cacheClientExecutor = new CacheClientExecutor(
                dataLabsCacheType: dataLabsCacheType,
                configuration: configuration,
                connectionMultiplexerWrapperFactory: connectionMultiplexerWrapperFactory,
                preCreateConnections: preCreateConnections);
        }

        public async Task<string?[]> GetCollectionValuesAsync(string key, long rangeStart, long rangeEnd, int prefixBytesToRemove, bool useMget, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return [];
            }

            using var monitor = CacheClientGetCollectionValuesAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);
                monitor.Activity[SolutionConstants.CacheKey] = key;
                monitor.Activity[SolutionConstants.RangeStart] = rangeStart;
                monitor.Activity[SolutionConstants.RangeEnd] = rangeEnd;
                monitor.Activity[SolutionConstants.PrefixBytesToRemove] = prefixBytesToRemove;

                var keys = await SortedSetRangeByRankAsync(key, rangeStart, rangeEnd, cancellationToken);

                monitor.Activity[SolutionConstants.CacheResult] = keys?.Length;

                if (keys == null || keys.Length == 0)
                {
                    monitor.OnCompleted();
                    return [];
                }

                string[] result = new string[keys.Length];

                if (useMget)
                {
                    IList<string> keyList = [.. keys];
                    monitor.Activity["UseMget"] = true;
                    var values = await MGetValuesAsync(keyList, cancellationToken);
                    if (values == null || values.Count == 0)
                    {
                        monitor.Activity["MgetReturnedNull"] = true;
                        monitor.OnCompleted();
                        return [];
                    }

                    // TODO: This is for debugging purpose, remove it later
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i] == null)
                        {
                            monitor.Activity[$"Mget null for {keys[i]}"] = true;
                        }
                    }

                    Parallel.For(0, values.Count, i =>
                    {
                        if (values[i] == null)
                        {
                            return;
                        }

                        result[i] = Encoding.UTF8.GetString(values[i]!, prefixBytesToRemove, values[i]!.Length - prefixBytesToRemove);
                    });

                    monitor.OnCompleted();
                    return result;
                }

                for (int i = 0; i < keys.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var res = await GetValueAsync(keys[i]!, cancellationToken: cancellationToken);
                    if (res == null || res.Length == 0)
                    {
                        continue;
                    }
                    result[i] = Encoding.UTF8.GetString(res, prefixBytesToRemove, res.Length - prefixBytesToRemove);
                }

                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<byte[]?> GetValueAsync(string key, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return null;
            }

            var clientResult = await _cacheClientExecutor.GetValueAsync(key, readQuorum: 1, cancellationToken).ConfigureAwait(false);
            return ReturnReadResult(clientResult);
        }

        public async Task<List<byte[]?>?> MGetValuesAsync(IList<string> keys, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return null;
            }

            GuardHelper.ArgumentNotNullOrEmpty(keys);

            var clientResults = await _cacheClientExecutor.MGetValuesAsync(keys, readQuorum: 1, cancellationToken).ConfigureAwait(false);
            return ReturnReadMultipleResult(keys, clientResults);
        }

        public async Task<bool> DeleteKeyAsync(string key, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return false;
            }

            var clientResult = await _cacheClientExecutor.DeleteKeyAsync(key, cancellationToken).ConfigureAwait(false);
            return ReturnWriteResult(clientResult);
        }

        public async Task<bool> SetKeyExpireAsync(string key, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return false;
            }

            var clientResult = await _cacheClientExecutor.SetKeyExpireAsync(key, expiry, cancellationToken).ConfigureAwait(false);
            return ReturnWriteResult(clientResult);
        }

        public Task<bool> SetValueAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return Task.FromResult(false);
            }
            return SetValueWithExpiryAsync(key, readOnlyBytes, null, cancellationToken);
        }

        public Task<bool> SetValueAsync(string key, string value, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return Task.FromResult(false);
            }
            return SetValueWithExpiryAsync(key, new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(value)), null, cancellationToken);
        }

        public Task<bool> SetValueIfGreaterThanAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long greaterThanValue, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return Task.FromResult(false);
            }
            return SetValueIfGreaterThanWithExpiryAsync(key, readOnlyBytes, greaterThanValue, null, cancellationToken);
        }

        public Task<bool> SetValueIfMatchAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long matchValue, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return Task.FromResult(false);
            }
            return SetValueIfMatchWithExpiryAsync(key, readOnlyBytes, matchValue, null, cancellationToken);
        }

        public async Task<bool> SetValueIfGreaterThanWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long greaterThanValue, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return false;
            }

            var clientResult = await _cacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(key, readOnlyBytes, greaterThanValue, expiry, cancellationToken).ConfigureAwait(false);
            return ReturnWriteResult(clientResult);
        }

        public async Task<bool> SetValueIfMatchWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long matchValue, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return false;
            }

            var clientResult = await _cacheClientExecutor.SetValueIfMatchWithExpiryAsync(key, readOnlyBytes, matchValue, expiry, cancellationToken).ConfigureAwait(false);
            return ReturnWriteResult(clientResult);
        }

        public async Task<bool> SetValueWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return false;
            }

            var clientResult = await _cacheClientExecutor.SetValueWithExpiryAsync(key, readOnlyBytes, expiry, cancellationToken).ConfigureAwait(false);
            return ReturnWriteResult(clientResult);
        }

        public async Task<bool> SetValueWithExpiryAsync(string key, string value, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return false;
            }

            var clientResult = await _cacheClientExecutor.SetValueWithExpiryAsync(key, value, expiry, cancellationToken).ConfigureAwait(false);
            return ReturnWriteResult(clientResult);
        }

        public async Task<bool> SortedSetAddAsync(string key, string member, double score, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return false;
            }

            var clientResult = await _cacheClientExecutor.SortedSetAddAsync(key, member, score, cancellationToken).ConfigureAwait(false);
            return ReturnWriteResult(clientResult);
        }

        public async Task<bool> SortedSetRemoveAsync(string key, string member, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return false;
            }

            var clientResult = await _cacheClientExecutor.SortedSetRemoveAsync(key, member, cancellationToken).ConfigureAwait(false);
            return ReturnWriteResult(clientResult);
        }

        public async Task<long> SortedSetRemoveRangeByScoreAsync(string key, double minScore, double maxScore, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return 0;
            }

            var clientResult = await _cacheClientExecutor.SortedSetRemoveRangeByScoreAsync(key, minScore, maxScore, cancellationToken).ConfigureAwait(false);
            return ReturnWriteResult(clientResult);
        }

        public async Task<long> SortedSetLengthAsync(string key, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return 0;
            }

            // TODO
            // Support readQuorum?
            var clientResult = await _cacheClientExecutor.SortedSetLengthAsync(key, readQuorum: 1, cancellationToken).ConfigureAwait(false);
            return ReturnReadResult(clientResult);
        }

        public async Task<string?[]> SortedSetRangeByRankAsync(string key, long rangeStart, long rangeEnd, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return Array.Empty<string>();
            }

            // TODO
            // Support readQuorum?
            var clientResult = await _cacheClientExecutor.SortedSetRangeByRankAsync(key, rangeStart, rangeEnd, readQuorum: 1, cancellationToken).ConfigureAwait(false);
            return ReturnReadResult(clientResult);
        }

        public async Task<double?> SortedSetScoreAsync(string key, string member, CancellationToken cancellationToken)
        {
            if (!CacheEnabled)
            {
                return null;
            }

            // TODO
            // Support readQuorum?
            var clientResult = await _cacheClientExecutor.SortedSetScoreAsync(key, member, readQuorum: 1, cancellationToken).ConfigureAwait(false);
            return ReturnReadResult(clientResult);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T ReturnReadResult<T>(CacheClientReadResult<T>? clientResult)
        {
            if (clientResult != null)
            {
                if (clientResult.HasSuccess)
                {
                    return clientResult.SuccessNodeResults![0].Result;
                }
                else if (clientResult.HasFailed)
                {
                    throw new CacheClientReadException<T>("CacheClient failed to read from replicas", clientResult);
                }
            }

            return default!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static List<T?>? ReturnReadMultipleResult<T>(
            IList<string> keys,
            List<(int indexInClientResult, CacheClientReadResult<List<T?>>?)>? clientResults)
        {
            if (clientResults == null || clientResults.Count == 0)
            {
                return null;
            }

            GuardHelper.ArgumentConstraintCheck(keys.Count == clientResults.Count, "keys.Count == clientResults.Count");

            var finalResults = new List<T?>(keys.Count);

            for (int i = 0; i < keys.Count; i++)
            {
                var (indexInClientResult, clientResult) = clientResults[i];
                if (indexInClientResult >= 0 && clientResult?.HasSuccess == true)
                {
                    var resultList = clientResult.SuccessNodeResults![0].Result;
                    if (resultList != null)
                    {
                        finalResults.Add(resultList[indexInClientResult]);
                        continue;
                    }
                }

                finalResults.Add(default);
            }

            return finalResults;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T ReturnWriteResult<T>(CacheClientWriteResult<T>? clientResult)
        {
            if (clientResult != null)
            {
                if (clientResult.FailedNodeResults?.Count > 0)
                {
                    throw new CacheClientWriteException<T>("CacheClient failed to write to all replicas", clientResult);
                }
                else if (clientResult.SuccessNodeResults?.Count > 0)
                {
                    return clientResult.SuccessNodeResults[0].Result;
                }
            }

            return default!;
        }

        // Below two method is deprecated and will be removed in future
        public Task<DateTimeOffset> GetLastCheckPointTimeAsync(int replicaIndex, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string?> SendCheckPointAsync(int replicaIndex, bool backgroundSave, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
