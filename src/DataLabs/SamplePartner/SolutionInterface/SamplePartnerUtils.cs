using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
using System.Text;

namespace SamplePartnerNuget.SolutionInterface
{
    public class SamplePartnerUtils
    {
        public const string ProviderAndResourceType = "/providers/Microsoft.TestPartner/TestSolution/";
        private const string AlphaNumericString = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        internal static string GenerateOutputId(string parentId)
        {
            StringBuilder sb = new StringBuilder(6);
            Random random = new Random();

            for (int i = 0; i < 6; i++)
            {
                sb.Append(AlphaNumericString[random.Next(AlphaNumericString.Length)]);
            }

            return $"{parentId}{ProviderAndResourceType}{sb}";
        }

        internal static EventGridNotification<NotificationDataV3<GenericResource>> CloneEventGridNotificationWithNewId(
            EventGridNotification<NotificationDataV3<GenericResource>> parentEventGrid, int numReturnResources)
        {
            NotificationDataV3<GenericResource> newDataV3 = CloneArnV3WithNewId(parentEventGrid.Data, numReturnResources);
            return new EventGridNotification<NotificationDataV3<GenericResource>>(
                id: parentEventGrid.Id,
                topic: parentEventGrid.Topic,
                subject: parentEventGrid.Subject,
                eventType: parentEventGrid.EventType,
                eventTime: parentEventGrid.EventTime,
                data: newDataV3);
        }

        private static NotificationDataV3<GenericResource> CloneArnV3WithNewId(NotificationDataV3<GenericResource> parent, int numReturnResources)
        {
            var parentResourceData = parent.Resources[0];
            var parentArmResource = parentResourceData.ArmResource;

            Guid? correlationId = parentResourceData.CorrelationId != null ? Guid.Parse(parentResourceData.CorrelationId) : null;
            Guid? homeTenantId = parentResourceData.HomeTenantId != null ? Guid.Parse(parentResourceData.HomeTenantId) : null;
            Guid? resourceHomeTenantId = parentResourceData.ResourceHomeTenantId != null ? Guid.Parse(parentResourceData.ResourceHomeTenantId) : null;
            var sourceResourceId = parentResourceData.SourceResourceId;
            var eventTime = parentResourceData.ResourceEventTime;
            var statusCode = parentResourceData.StatusCode;

            var newResources = new List<NotificationResourceDataV3<GenericResource>>(numReturnResources);

            for (int i = 0; i < numReturnResources; i++)
            {
                var newArmResource = new GenericResource(parentArmResource);
                newArmResource.Id = GenerateOutputId(parentArmResource.Id);

                var newResourceData = new NotificationResourceDataV3<GenericResource>(
                correlationId ?? Guid.NewGuid(),
                newArmResource,
                parentResourceData.ApiVersion,
                eventTime ?? DateTimeOffset.UtcNow,
                homeTenantId,
                resourceHomeTenantId,
                sourceResourceId,
                statusCode);

                newResources.Add(newResourceData);
            }

            return new NotificationDataV3<GenericResource>(
                publisherInfo: parent.PublisherInfo,
                resources: newResources,
                correlationId: null,
                resourceLocation: parent.ResourceLocation,
                frontdoorLocation: parent.FrontdoorLocation,
                homeTenantId: homeTenantId,
                resourceHomeTenantId: resourceHomeTenantId,
                apiVersion: parent.ApiVersion,
                additionalBatchProperties: null,
                dataBoundary: null);
        }
    }
}
