namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class HashBlobNameProvider : IBlobNameProvider
    {
        public (uint, ulong, ulong) CalculateHash(string resourceId, string? tenantId)
        {
            var bytes = BlobUtils.GetBytesForHash(resourceId, tenantId);
            var hash1 = HashUtils.Murmur32(bytes);
            var hash2 = HashUtils.MurmurHash3x128(bytes); 
            return (hash1, hash2.Item1, hash2.Item2);
        }

        public string GetBlobName(string resourceId, string? tenantId, uint hash1, ulong hash2, ulong hash3)
        {
            return hash3.ToString("x16") + hash2.ToString("x16") + hash1.ToString("x8");
        }
    }
}
