namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using System.Runtime.CompilerServices;
    using System.Text;

    public class SolutionLoggingUtils
    {
        // If possible, Let's try not to use Regex any matching

        private const string SigFromBlobUri = "&sig=";
        private const string SigMaskedInBlobUri = "&sig=MASKED";
        private const string SigEndMark = "&";

        public static string HideSigFromBlobUri(string stringWithBlobUri)
        {
            GuardHelper.ArgumentNotNull(stringWithBlobUri);
            
            // Based on log, most of time sig appears in the end. 
            // Let's search from back
            var idx = stringWithBlobUri.LastIndexOf(SigFromBlobUri);
            if (idx < 0)
            {
                return stringWithBlobUri;
            }else
            {
                var stringBuilder = new StringBuilder(stringWithBlobUri.Length);
                // find end Idx
                var endIdx = stringWithBlobUri.IndexOf(SigEndMark, idx + SigFromBlobUri.Length);
                if (idx > 0)
                {
                    stringBuilder.Append(stringWithBlobUri, 0, idx);
                }
                stringBuilder.Append(SigMaskedInBlobUri);

                if (endIdx > 0)
                {
                    stringBuilder.Append(stringWithBlobUri, endIdx, stringWithBlobUri.Length - endIdx);
                }

                return stringBuilder.ToString();
            }
        }

        public static void LogARNV3Notification(
            EventGridNotification<NotificationDataV3<GenericResource>> notification,
            OpenTelemetryActivityWrapper? taskActivity,
            string? suffix)
        {
            if (taskActivity == null)
            {
                return;
            }

            var eventId = notification.Id;
            var topic = notification.Topic;
            var subject = notification.Subject;
            var eventType = notification.EventType;
            var eventTime = notification.EventTime.ToString("o");
            var resources = notification.Data?.Resources;
            var resourceLocation = notification.Data?.ResourceLocation;
            var publisherInfo = notification.Data?.PublisherInfo;
            var homeTenantId = notification.Data?.HomeTenantId;
            var resourceHomeTenantId = notification.Data?.ResourceHomeTenantId;
            var apiVersion = notification.Data?.ApiVersion;
            var numResources = resources == null ? 0 : resources.Count;

            var eventIdKey = SolutionConstants.EventId + suffix;
            var topicKey = SolutionConstants.Topic + suffix;
            var subjectKey = SolutionConstants.Subject + suffix;
            var eventTypeKey = SolutionConstants.EventType + suffix;
            var eventTimeKey = SolutionConstants.EventTime + suffix;
            var numResourcesKey = SolutionConstants.NumResources + suffix;
            var resourceLocationKey = SolutionConstants.ResourceLocation + suffix;
            var publisherInfoKey = SolutionConstants.PublisherInfo + suffix;
            var homeTenantIdKey = SolutionConstants.HomeTenantId + suffix;
            var resourceHomeTenantIdKey = SolutionConstants.ResourceHomeTenantId + suffix;
            var apiVersionKey = SolutionConstants.ApiVersion + suffix;

            taskActivity.SetTag(eventIdKey, eventId);
            taskActivity.SetTag(topicKey, topic);
            taskActivity.SetTag(subjectKey, subject);
            taskActivity.SetTag(eventTypeKey, eventType);
            taskActivity.SetTag(eventTimeKey, eventTime);
            taskActivity.SetTag(numResourcesKey, numResources);
            taskActivity.SetTag(resourceLocationKey, resourceLocation);
            taskActivity.SetTag(publisherInfoKey, publisherInfo);
            taskActivity.SetTag(homeTenantIdKey, homeTenantId);
            taskActivity.SetTag(resourceHomeTenantIdKey, resourceHomeTenantId);
            taskActivity.SetTag(apiVersionKey, apiVersion);

            var blobInfo = notification.Data?.ResourcesBlobInfo;
            if (blobInfo != null)
            {
                // Blob
                var maskedBlobUri = HideSigFromBlobUri(blobInfo.BlobUri);

                var blobURIKey = SolutionConstants.BlobURI + suffix;
                var blobSizeKey = SolutionConstants.BlobSize + suffix;

                taskActivity.SetTag(blobURIKey, maskedBlobUri);
                taskActivity.SetTag(blobSizeKey, blobInfo.BlobSize);
            }
        }

        public static void LogRawARNV3Notification(
            EventGridNotification<NotificationDataV3<GenericResource>>[] notifications,
            OpenTelemetryActivityWrapper? taskActivity)
        {
            if (taskActivity == null)
            {
                return;
            }

            taskActivity.SetTag(SolutionConstants.NumOfEventGridNotifications, notifications.Length);
            
            if (notifications.Length == 1)
            {
                LogARNV3Notification(notifications[0], taskActivity, null);
                return;
            }

            for (int i = 0; i < notifications.Length; i++)
            {
                var notification = notifications[i];
                LogARNV3Notification(notification, taskActivity, (i + 1).ToString());
            }
        }
    }
}
