namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient
{
    public interface IBlobNameProvider
    {
        public (uint, ulong, ulong) CalculateHash(string resourceId, string? tenantId);
        public string GetBlobName(string resourceId, string? tenantId, uint hash1, ulong hash2, ulong hash3);
    }
}
