namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN
{
    using System;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;

    public class ARNRawNotification
    {
        public EventGridNotification<NotificationDataV3<GenericResource>>[] NotificationDataV3s { get; set; }

        public static ARNRawNotification ARNDeserializer(BinaryData binaryData, bool isCompressed)
        {
            // TODO
            // consider V5 later
            var eventList = SerializationHelper.DeserializeArnV3Notification(binaryData, isCompressed);
            if (eventList == null || eventList.Length == 0)
            {
                return null;
            }

            return new ARNRawNotification()
            {
                NotificationDataV3s = eventList
            };
        }

        public static BinaryData ARNSerializer(ARNRawNotification notification)
        {
            if (notification.NotificationDataV3s == null)
            {
                return null;
            }

            var memory = SerializationHelper.SerializeToMemory(notification.NotificationDataV3s, false);
            return new BinaryData(memory);
        }
    }
}
