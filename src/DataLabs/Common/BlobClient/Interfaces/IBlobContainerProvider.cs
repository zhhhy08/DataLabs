namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient
{
    using global:: Azure.Storage.Blobs;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBlobContainerProvider
    {
        public ValueTask<BlobContainerClient> GetBlobContainerClientAsync(string resourceId, string? tenantId, string blobName, uint hash1, ulong hash2, ulong hash3, CancellationToken cancellationToken);
    }
}
