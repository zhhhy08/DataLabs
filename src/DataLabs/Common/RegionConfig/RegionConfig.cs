namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient;

    public class RegionConfig
    {
        public required string RegionLocationName;

        public required string sourceOfTruthStorageAccountNames;

        public required IOutputBlobClient? outputBlobClient;
    } 
}
