namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;

    public class DataLabsARMGenericSuccessResponse
    {
        public string? Response;
        public DateTimeOffset OutputTimestamp;

        public DataLabsARMGenericSuccessResponse(string? response, DateTimeOffset outputTimestamp)
        {
            Response = response;
            OutputTimestamp = outputTimestamp;
        }
    }
}
