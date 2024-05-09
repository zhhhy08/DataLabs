namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;

    public class DataLabsARMSuccessResponse
    {
        public GenericResource? Resource;
        public DateTimeOffset OutputTimestamp;

        public DataLabsARMSuccessResponse(GenericResource? resource, DateTimeOffset outputTimestamp)
        {
            Resource = resource;
            OutputTimestamp = outputTimestamp;
        }
    }
}
