namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;

    public class DataLabsCasSuccessResponse
    {
        public string? Resource;
        public DateTimeOffset OutputTimestamp;

        public DataLabsCasSuccessResponse(string? resource, DateTimeOffset outputTimestamp)
        {
            this.Resource = resource;
            this.OutputTimestamp = outputTimestamp;
        }
    }
}
