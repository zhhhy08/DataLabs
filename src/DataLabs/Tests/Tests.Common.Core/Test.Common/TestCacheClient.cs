namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;

    [ExcludeFromCodeCoverage]
    public class TestCacheClient : ICacheClient
    {
        public Dictionary<string, byte[]> _cache = new Dictionary<string, byte[]>();
        public int NumSetValueWithExpiryAsync;
        public int NumSetValueAsync;
        public int NumSetValueIfGreaterThanAsync;
        public int NumSetValueIfGreaterThanWithExpiryAsync;
        public int NumSetValueIfMatchAsync;
        public int NumSetValueIfMatchWithExpiryAsync;
        public int NumSortedSetAddAsync;
        public int NumSortedSetRangeByRankAsync;
        public int NumSortedSetRemoveAsync;
        public int NumSortedSetScoreAsync;
        
        public int NumGetCall;
        public int NumDeleteCall;
        public int ReturnInsertErrorAfterNum;

        public bool CacheEnabled => true;

        public Task<bool> DeleteKeyAsync(string key, CancellationToken cancellationToken)
        {
            bool result;
            lock(this)
            {
                NumDeleteCall++;
                result = _cache.Remove(key);
            }
            return Task.FromResult(result);
        }

        public Task<bool> SetKeyExpireAsync(string key, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            // TODO if we need to test this scenario later
            return Task.FromResult(true);
        }

        public Task<string[]> GetCollectionValuesAsync(string key, long rangeStart, long rangeEnd, int prefixBytesToRemove, bool useMget, CancellationToken cancellationToken)
        {
            return Task.FromResult(Array.Empty<string>());
        }

        public Task<byte[]?> GetValueAsync(string key, CancellationToken cancellationToken)
        {
            byte[] result;
            lock (this)
            {
                NumGetCall++;
                result = _cache.TryGetValue(key, out byte[] value) ? value : null;
            }
            return Task.FromResult(result);
        }

        public async Task<bool> SetValueWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSetValueWithExpiryAsync++;

                if (ReturnInsertErrorAfterNum > 0 && ReturnInsertErrorAfterNum == NumSetValueWithExpiryAsync)
                {
                    throw new Exception("Cache Insert Failed");
                }
                _cache[key] = readOnlyBytes.ToArray();
            }

            await SetKeyExpireAsync(key, expiry, cancellationToken).ConfigureAwait(false);
            return true;
        }

        public Task<bool> SetValueAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSetValueAsync++;

                if (ReturnInsertErrorAfterNum > 0 && ReturnInsertErrorAfterNum == NumSetValueAsync)
                {
                    throw new Exception("Cache Insert Failed");
                }
                _cache[key] = readOnlyBytes.ToArray();
            }
            
            return Task.FromResult(true);
        }

        public async Task<bool> SetValueWithExpiryAsync(string key, string value, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSetValueWithExpiryAsync++;
                _cache[key] = Encoding.UTF8.GetBytes(value);
            }
            
            await SetKeyExpireAsync(key, expiry, cancellationToken).ConfigureAwait(false);
            return true;
        }

        public Task<bool> SetValueAsync(string key, string value, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSetValueAsync++;
                _cache[key] = Encoding.UTF8.GetBytes(value);
            }
            return Task.FromResult(true);
        }

        public Task<bool> SetValueIfGreaterThanAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long greaterThanValue, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSetValueIfGreaterThanAsync++;
                _cache[key] = readOnlyBytes.ToArray();
            }
            return Task.FromResult(true);
        }

        public async Task<bool> SetValueIfGreaterThanWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long greaterThanValue, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSetValueIfGreaterThanWithExpiryAsync++;
                _cache[key] = readOnlyBytes.ToArray();
            }
            
            await SetKeyExpireAsync(key, expiry, cancellationToken).ConfigureAwait(false);
            return true;
        }

        public Task<bool> SetValueIfMatchAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long matchValue, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSetValueIfMatchAsync++;
                _cache[key] = readOnlyBytes.ToArray();
            }
            
            return Task.FromResult(true);
        }

        public async Task<bool> SetValueIfMatchWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long matchValue, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSetValueIfMatchWithExpiryAsync++;
                _cache[key] = readOnlyBytes.ToArray();
            }
            
            await SetKeyExpireAsync(key, expiry, cancellationToken).ConfigureAwait(false);
            return true;
        }

        public Task<bool> SortedSetAddAsync(string key, string member, double score, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSortedSetAddAsync++;
            }
            
            return Task.FromResult(true);
        }

        public Task<string[]> SortedSetRangeByRankAsync(string key, long rangeStart, long rangeEnd, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSortedSetRangeByRankAsync++;
            }
            
            return Task.FromResult(Array.Empty<string>());
        }

        public Task<bool> SortedSetRemoveAsync(string key, string member, CancellationToken cancellationToken)
        {
            lock(this)
            {
                NumSortedSetRemoveAsync++;
            }
            
            return Task.FromResult(true);
        }

        public Task<double?> SortedSetScoreAsync(string key, string member, CancellationToken cancellationToken)
        {
            lock(this) {
                NumSortedSetScoreAsync++;
            }
            
            double? value = 1;
            return Task.FromResult(value);
        }

        public Task<long> SortedSetRemoveRangeByScoreAsync(string key, double minScore, double maxScore, CancellationToken cancellationToken)
        {
            return Task.FromResult(1L);
        }

        public void Clear()
        {
            _cache.Clear();

            NumSetValueWithExpiryAsync = 0;
            NumSetValueAsync = 0;
            NumSetValueIfGreaterThanAsync = 0;
            NumSetValueIfGreaterThanWithExpiryAsync = 0;
            NumSetValueIfMatchAsync = 0;
            NumSetValueIfMatchWithExpiryAsync = 0;
            NumSortedSetAddAsync = 0;
            NumSortedSetRangeByRankAsync = 0;
            NumSortedSetRemoveAsync = 0;
            NumSortedSetScoreAsync = 0;

            NumGetCall = 0;
            ReturnInsertErrorAfterNum = 0;
        }

        public Task<long> SortedSetLengthAsync(string key, CancellationToken cancellationToken)
        {
            lock (this)
            {
                NumGetCall++;
            }
            return Task.FromResult((long)_cache.Count);
        }

        public Task<string?> SendCheckPointAsync(int replicaIndex, bool backgroundSave, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<DateTimeOffset> GetLastCheckPointTimeAsync(int replicaIndex, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<List<byte[]>> MGetValuesAsync(IList<string> keys, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}