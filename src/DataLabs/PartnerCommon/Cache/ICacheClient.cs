namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICacheClient
    {
        public bool CacheEnabled { get; }

        public Task<DateTimeOffset> GetLastCheckPointTimeAsync(int replicaIndex, CancellationToken cancellationToken);
        public Task<string?> SendCheckPointAsync(int replicaIndex, bool backgroundSave, CancellationToken cancellationToken);

        public Task<bool> SetKeyExpireAsync(string key, TimeSpan? expiry, CancellationToken cancellationToken);
        public Task<bool> DeleteKeyAsync(string key, CancellationToken cancellationToken);

        public Task<bool> SetValueAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, CancellationToken cancellationToken);
        public Task<bool> SetValueWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, TimeSpan? expiry, CancellationToken cancellationToken);

        public Task<bool> SetValueAsync(string key, string value, CancellationToken cancellationToken);
        public Task<bool> SetValueWithExpiryAsync(string key, string value, TimeSpan? expiry, CancellationToken cancellationToken);

        public Task<bool> SetValueIfMatchAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long matchValue, CancellationToken cancellationToken);
        public Task<bool> SetValueIfMatchWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long matchValue, TimeSpan? expiry, CancellationToken cancellationToken);

        public Task<bool> SetValueIfGreaterThanAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long greaterThanValue, CancellationToken cancellationToken);
        public Task<bool> SetValueIfGreaterThanWithExpiryAsync(string key, ReadOnlyMemory<byte> readOnlyBytes, long greaterThanValue, TimeSpan? expiry, CancellationToken cancellationToken);

        public Task<byte[]?> GetValueAsync(string key, CancellationToken cancellationToken);
        public Task<List<byte[]?>?> MGetValuesAsync(IList<string> keys, CancellationToken cancellationToken);

        public Task<bool> SortedSetAddAsync(string key, string member, double score, CancellationToken cancellationToken);

        public Task<bool> SortedSetRemoveAsync(string key, string member, CancellationToken cancellationToken);

        public Task<string?[]> SortedSetRangeByRankAsync(string key, long rangeStart, long rangeEnd, CancellationToken cancellationToken);

        public Task<double?> SortedSetScoreAsync(string key, string member, CancellationToken cancellationToken);

        public Task<long> SortedSetLengthAsync(string key, CancellationToken cancellationToken);

        public Task<long> SortedSetRemoveRangeByScoreAsync(string key, double minScore, double maxScore, CancellationToken cancellationToken);

        public Task<string?[]> GetCollectionValuesAsync(string key, long rangeStart, long rangeEnd, int prefixBytesToRemove, bool useMget, CancellationToken cancellationToken);

    }
}
