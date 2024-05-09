namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN
{
    using System;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;

    public class ARNNotification
    {
        public EventGridNotification<NotificationDataV3<GenericResource>> NotificationDataV3 { get; set; }

        public static ARNNotification ARNDeserializer(BinaryData binaryData, bool isCompressed)
        {
            // TODO
            // consider V5 later
            var eventList = SerializationHelper.DeserializeArnV3Notification(binaryData, isCompressed);
            if (eventList == null || eventList.Length == 0)
            {
                return null;
            }

            return new ARNNotification()
            {
                NotificationDataV3 = eventList[0]
            };
        }

        public static BinaryData ARNSerializer(ARNNotification notification)
        {
            if (notification.NotificationDataV3 == null)
            {
                return null;
            }

            var memory = SerializationHelper.SerializeToMemory(notification.NotificationDataV3, false);
            return new BinaryData(memory);
        }
    }
}
