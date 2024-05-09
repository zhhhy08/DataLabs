namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;

    public class DataLabsARNV3SuccessResponse
    {
        public EventGridNotification<NotificationDataV3<GenericResource>>? Resource;
        public DateTimeOffset OutputTimestamp;
        public string? ETag;

        public DataLabsARNV3SuccessResponse(
            EventGridNotification<NotificationDataV3<GenericResource>>? resource,
            DateTimeOffset outputTimestamp,
            string? eTag)
        {
            Resource = resource;
            OutputTimestamp = outputTimestamp;
            ETag = eTag;
        }
    }
}
