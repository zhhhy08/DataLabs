namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient
{
    public interface IStorageAccountSelector
    {
        public IBlobContainerProvider GetBlobContainerProvider(string resourceId, string? tenantId, uint hash1, ulong hash2, ulong hash3);
    }
}
