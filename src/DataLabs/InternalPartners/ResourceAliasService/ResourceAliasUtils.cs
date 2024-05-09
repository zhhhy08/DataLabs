namespace ResourceAliasService
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using static ResourceAliasService.ResourceAliasSolutionService;

    public static class ResourceAliasUtils
    {
        public static NotificationResourceDataV3<GenericResource> CloneNotificationResourceDataV3WithNewResourceId(NotificationResourceDataV3<GenericResource> parentResourceData, string newResourceId, SystemMetadata systemMetadata)
        {
            GuardHelper.ArgumentNotNull(parentResourceData);
            GuardHelper.ArgumentNotNullOrEmpty(newResourceId);
            GuardHelper.ArgumentNotNull(systemMetadata);

            var parentArmResource = parentResourceData.ArmResource;
            parentArmResource.Id = newResourceId;

            var correlationId = parentResourceData.CorrelationId != null ? Guid.Parse(parentResourceData.CorrelationId) : Guid.NewGuid();
            Guid? homeTenantId = null; // null here as per idmapping team's request
            Guid? resourceHomeTenantId = null; // null here as per idmapping team's request
            var sourceResourceId = parentResourceData.SourceResourceId;
            var eventTime = parentResourceData.ResourceEventTime;
            var statusCode = parentResourceData.StatusCode;
            var additionalResourceProperties = parentResourceData.AdditionalResourceProperties ?? new Dictionary<string, object>();
            additionalResourceProperties["system"] = systemMetadata;

            return new NotificationResourceDataV3<GenericResource>(
                correlationId,
                parentArmResource,
                parentResourceData.ApiVersion,
                eventTime ?? DateTimeOffset.UtcNow,
                homeTenantId,
                resourceHomeTenantId,
                sourceResourceId,
                statusCode,
                additionalResourceProperties,
                parentResourceData.ResourceSystemProperties
            );
        }

        public static NotificationDataV3<GenericResource> CloneNotificationDataV3WithNewResources(NotificationDataV3<GenericResource> parentNotificationData, IEnumerable<NotificationResourceDataV3<GenericResource>> newResources)
        {
            GuardHelper.ArgumentNotNull(parentNotificationData);
            GuardHelper.ArgumentNotNull(newResources);

            return new NotificationDataV3<GenericResource>(
                publisherInfo: parentNotificationData.PublisherInfo,
                resources: newResources.ToList(),
                correlationId: null,
                resourceLocation: parentNotificationData.ResourceLocation,
                frontdoorLocation: parentNotificationData.FrontdoorLocation,
                homeTenantId: null, // null here as per idmapping team's request
                resourceHomeTenantId: null, // null here as per idmapping team's request
                apiVersion: parentNotificationData.ApiVersion,
                additionalBatchProperties: parentNotificationData.AdditionalBatchProperties,
                dataBoundary: parentNotificationData.DataBoundary
            );
        }
    }
}
