namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient
{
    using global::Azure.Storage.Blobs;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBlobContainerProviderFactory
    {
        public Task<IBlobContainerProvider> CreateBlobContainerProviderAsync(BlobServiceClient blobServiceClient, CancellationToken cancellationToken);
    }
}
