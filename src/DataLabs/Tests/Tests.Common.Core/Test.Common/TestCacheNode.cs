namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;

    [ExcludeFromCodeCoverage]
    public class TestCacheNode
    {
        public Dictionary<string, byte[]> _cache = new Dictionary<string, byte[]>();
        public int NumSetKeyExpireAsync;
        public int NumSetValueAsync;
        public int NumSetValueIfGreaterThanAsync;
        public int NumSetValueIfMatchAsync;
        public int NumSortedSetAddAsync;
        public int NumSortedSetRangeByRankAsync;
        public int NumSortedSetRemoveAsync;
        public int NumSortedSetScoreAsync;
        public int NumSortedSetRemoveRangeByScoreAsync;
        public int NumSortedSetLengthAsync;
        public int NumGetCall;
        public int NumDeleteCall;
        public int NumGetCollectionValuesAsync;

        public bool ReturnInsertError;
        public bool ReturnGetError;

        public string GetLastSaveTime()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        }

        public string SaveCheckPoint(bool bgsave)
        {
            return bgsave ? "Background save started" : "Save are finished";
        }

        public bool DeleteKeyAsync(string key)
        {
            bool result;
            lock (this)
            {
                NumDeleteCall++;
                result = _cache.Remove(key);
            }
            return result;
        }

        public bool SetKeyExpireAsync(string key, TimeSpan? expiry)
        {
            NumSetKeyExpireAsync++;
            return true;
        }

        public string[] GetCollectionValuesAsync(string key, long rangeStart, long rangeEnd, int prefixBytesToRemove)
        {
            NumGetCollectionValuesAsync++;
            return Array.Empty<string>();
        }

        public byte[]? GetValueAsync(string key)
        {
            byte[] result = null;
            lock (this)
            {
                if (ReturnGetError)
                {
                    throw new Exception("Cache Get Failed");
                }

                NumGetCall++;
                result = _cache.TryGetValue(key, out byte[] value) ? value : null;
            }
            return result;
        }

        public bool SetValueAsync(string key, ReadOnlyMemory<byte> readOnlyBytes)
        {
            lock (this)
            {
                if (ReturnInsertError)
                {
                    throw new Exception("Cache Insert Failed");
                }

                NumSetValueAsync++;
                _cache[key] = readOnlyBytes.ToArray();
            }

            return true;
        }

        public string SetValueIfGreaterThanAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long greaterThanValue)
        {
            lock (this)
            {
                NumSetValueIfGreaterThanAsync++;
                _cache[key] = readOnlyBytes.ToArray();
            }
            return CacheClientExecutor.CACHE_RESULT_OK;
        }

        public string SetValueIfMatchAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long matchValue)
        {
            lock (this)
            {
                NumSetValueIfMatchAsync++;
                _cache[key] = readOnlyBytes.ToArray();
            }

            return CacheClientExecutor.CACHE_RESULT_OK;
        }

        public string SortedSetAddAsync(string key, string member, double score)
        {
            lock (this)
            {
                NumSortedSetAddAsync++;
            }

            return CacheClientExecutor.CACHE_RESULT_OK;
        }

        public string[] SortedSetRangeByRankAsync(string key, long rangeStart, long rangeEnd)
        {
            lock (this)
            {
                NumSortedSetRangeByRankAsync++;
            }

            return Array.Empty<string>();
        }

        public string SortedSetRemoveAsync(string key, string member)
        {
            lock (this)
            {
                NumSortedSetRemoveAsync++;
            }

            return CacheClientExecutor.CACHE_RESULT_OK;
        }

        public double SortedSetScoreAsync(string key, string member)
        {
            lock (this)
            {
                NumSortedSetScoreAsync++;
            }
            return 1;
        }

        public long SortedSetRemoveRangeByScoreAsync(string key, double minScore, double maxScore)
        {
            lock (this)
            {
                NumSortedSetRemoveRangeByScoreAsync++;
            }
            return 1;
        }

        public long SortedSetLengthAsync(string key)
        {
            lock (this)
            {
                NumSortedSetLengthAsync++;
            }
            return (long)_cache.Count;
        }

        public void Clear()
        {
            _cache.Clear();

            NumSetKeyExpireAsync = 0;
            NumSetValueAsync = 0;
            NumSetValueIfGreaterThanAsync = 0;
            NumSetValueIfMatchAsync = 0;
            NumSortedSetAddAsync = 0;
            NumSortedSetRangeByRankAsync = 0;
            NumSortedSetRemoveAsync = 0;
            NumSortedSetScoreAsync = 0;
            NumSortedSetRemoveRangeByScoreAsync = 0;
            NumSortedSetLengthAsync = 0;
            NumGetCall = 0;
            NumDeleteCall = 0;
            NumGetCollectionValuesAsync = 0;
            ReturnInsertError = false;
            ReturnGetError = false;
        }
    }
}