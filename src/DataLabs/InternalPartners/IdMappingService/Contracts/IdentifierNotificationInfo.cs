namespace Microsoft.WindowsAzure.IdMappingService.Services.Contracts
{
    using System;
    using System.Collections.Generic;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.Enums;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.IdMappingService.Services.Telemetry;
    using Microsoft.WindowsAzure.IdMappingService.Services.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;

    public class IdentifierNotificationInfo
    {
        #region Fields

        /// <summary>
        /// Azure region of original arm resource
        /// </summary>
        public string Location { get; set; }

        public string ArmCorrelationId { get; set; }

        /// <summary>
        /// EventType from input EventGridNotification
        /// </summary>
        public string EventType { get; set;  }

        /// <summary>
        /// HomeTenantId from input NotificationData, should be populated by ARN, but may be null for resources in newly created subscriptions
        /// </summary>
        public string HomeTenantId { get; set; }

        /// <summary>
        /// ResourceHomeTenantId from input NotificationData, should be populated by ARN, but may be null for resources in newly created subscriptions
        /// </summary>
        public string ResourceHomeTenantId { get; set; }

        /// <summary>
        /// ResourceIdentifiers extracted from resource properties. Null for delete events
        /// </summary>
        public ResourceIdentifiers ResourceIdentifiers { get; set; }

        /// <summary>
        /// CreatedTime of the original resource, not the identifier resource. Null if not ResourceSystemProperties of original resource notification.
        /// </summary>
        public DateTimeOffset? CreatedTime { get; set; }

        /// <summary>
        /// ModifiedTime of the original resource, not the identifier resource. Null if not ResourceSystemProperties of original resource notification.
        /// </summary>
        public DateTimeOffset? ModifiedTime { get; set; }

        #endregion

        #region Tracing

        private static readonly ActivityMonitorFactory IdentifierNotificationInfoToArnNotification = new("IdentifierNotificationInfo.ToArnNotification");

        #endregion

        public IdentifierNotificationInfo(string location, string armCorrelationId, string eventType, string homeTenantId, string resourceHomeTenantId, ResourceIdentifiers resourceIdentifiers, DateTimeOffset? createdTime = null, DateTimeOffset? modifiedTime = null)
        {
            GuardHelper.ArgumentNotNullOrEmpty(location, nameof(location));
            GuardHelper.ArgumentNotNullOrEmpty(armCorrelationId, nameof(armCorrelationId));
            GuardHelper.ArgumentNotNullOrEmpty(eventType, nameof(eventType));
            GuardHelper.ArgumentConstraintCheck(eventType.EndsWith(IdMappingConstants.DeleteEventSuffix, StringComparison.OrdinalIgnoreCase) || resourceIdentifiers.Identifiers != null, "Identifiers can only be null for delete events");

            Location = location;
            ArmCorrelationId = armCorrelationId;
            EventType = eventType;
            HomeTenantId = homeTenantId;
            ResourceHomeTenantId = resourceHomeTenantId;
            ResourceIdentifiers = resourceIdentifiers;
            CreatedTime = createdTime;
            ModifiedTime = modifiedTime;
        }

        public NotificationDataV3<GenericResource> ToArnNotification(IActivity parentActivity)
        {
            using var monitor = IdentifierNotificationInfoToArnNotification.ToMonitor(parentActivity);
            monitor.OnStart();

            try 
            {
                DateTimeOffset dateTimeOffset = this.ResourceIdentifiers.ResourceUpdateTimestamp.HasValue ? this.ResourceIdentifiers.ResourceUpdateTimestamp.Value : DateTimeOffset.Now;
                Guid? homeTenantIdGuid = string.IsNullOrEmpty(this.HomeTenantId) ? null : new Guid(this.HomeTenantId);
                Guid? resourceHomeTenantIdGuid = string.IsNullOrEmpty(this.ResourceHomeTenantId) ? null : new Guid(this.ResourceHomeTenantId);
                var correlationId = Guid.NewGuid();

                var eventAction = this.EventType.EndsWith(IdMappingConstants.DeleteEventSuffix, StringComparison.OrdinalIgnoreCase) ? Activity.Delete : Activity.Update;
                var resourceSystemProperties = new ResourceSystemProperties(this.CreatedTime, this.ModifiedTime, eventAction);

                monitor.Activity.Properties["correlationId"] = correlationId;
                monitor.Activity.Properties["inputCorrelationId"] = this.ArmCorrelationId;
                monitor.Activity.Properties["resourceId"] = this.ResourceIdentifiers.ArmResourceId;
                monitor.Activity.Properties["eventAction"] = eventAction.ToString();

                var data = new NotificationResourceDataV3<GenericResource>(
                    correlationId: correlationId,
                    armResource: GetGenericResource(),
                    apiVersion: IdMappingConstants.IdentifierApiVersion,
                    eventTime: dateTimeOffset,
                    resourceSystemProperties: resourceSystemProperties,
                    homeTenantId: homeTenantIdGuid,
                    resourceHomeTenantId: resourceHomeTenantIdGuid
                );

                var notificationDataV3 = new NotificationDataV3<GenericResource>(
                    publisherInfo: IdMappingConstants.IdentifierPublisherInfo,
                    resources: new List<NotificationResourceDataV3<GenericResource>> { data },
                    correlationId: correlationId,
                    resourceLocation: this.Location,
                    homeTenantId: homeTenantIdGuid,
                    resourceHomeTenantId: resourceHomeTenantIdGuid
                );

                monitor.OnCompleted();
                return notificationDataV3;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }
       
        private GenericResource GetGenericResource()
        {
            return new GenericResource(
                id: GetIdentifierResourceId(),
                name: IdMappingConstants.DefaultResourceName,
                type: IdMappingConstants.IdentifierResourceType,
                location: this.Location,
                tags: null,
                plan: null,
                properties: this.ResourceIdentifiers,
                kind: null,
                managedBy: null,
                sku: null,
                identity: null,
                zones: null,
                systemData: null,
                extendedLocation: null,
                apiVersion: IdMappingConstants.IdentifierApiVersion
           );
        }

        private string GetIdentifierResourceId()
        {
            return $"{this.ResourceIdentifiers.ArmResourceId}/providers/{IdMappingConstants.IdentifierResourceType}/{IdMappingConstants.DefaultResourceName}";
        }
        
        internal static string GetIdentifierEventType(string eventType)
        {
            var eventTypeAction = ArmNotificationUtils.ParseEventType(eventType).Action;
            return $"{IdMappingConstants.IdentifierResourceType}/{eventTypeAction}";
        }
    }
}
