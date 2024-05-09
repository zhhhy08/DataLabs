namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IResourceCacheClient
    {
        public bool CacheEnabled { get; }
        public ICacheTTLManager CacheTTLManager { get; }

        public Task<bool> SetResourceAsync(string resourceId, string? tenantId, ResourceCacheDataFormat dataFormat, ReadOnlyMemory<byte> resource, long timeStamp, string? etag, TimeSpan? expiry, CancellationToken cancellationToken);
        public Task<bool> SetResourceIfGreaterThanAsync(string resourceId, string? tenantId, ResourceCacheDataFormat dataFormat, ReadOnlyMemory<byte> resource, long timeStamp, string? etag, TimeSpan? expiry, CancellationToken cancellationToken);
        public Task<bool> SetNotFoundResourceAsync(string resourceId, string? tenantId, TimeSpan? expiry, CancellationToken cancellationToken);
        public Task<ResourceCacheResult> GetResourceAsync(string resourceId, string? tenantId, CancellationToken cancellationToken);
        public Task<bool> DeleteResourceAsync(string resourceId, string? tenantId, CancellationToken cancellationToken);
        public Task<bool> SetLongValueAsync(string key, long value, TimeSpan? expiry, CancellationToken cancellationToken);
        public Task<long?> GetLongValueAsync(string key, CancellationToken cancellationToken);
        public string GetCacheKey(string resourceId, string? tenantId);
    }
}