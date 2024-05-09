namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;

    public class DataLabsARMAdminSuccessResponse
    {
        public readonly string? Resource;
        public readonly DateTimeOffset OutputTimestamp;

        public DataLabsARMAdminSuccessResponse(string? resource, DateTimeOffset outputTimestamp)
        {
            Resource = resource;
            OutputTimestamp = outputTimestamp;
        }
    }
}