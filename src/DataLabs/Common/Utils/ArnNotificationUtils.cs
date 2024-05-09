namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts.GenericNotificationContracts;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;

    public static class ArnNotificationUtils
    {
        #region Tracing

        private const string Move = "move";
        private const string MoveAction = "move/action";
        private const string Write = "write";
        private const string Delete = "delete";
        private const string Snapshot = "snapshot";

        private static readonly ActivityMonitorFactory ArnNotificationUtilsConvertArnNotificaitonV3ToResourceOperation =
            new ActivityMonitorFactory("ArnNotificationUtils.ConvertArnNotificaitonV3ToResourceOperation");

        #endregion

        public static IList<ResourceOperationBase> ConvertArnNotificaitonV3ToResourceOperation(
            EventGridNotification<NotificationDataV3<GenericResource>> eventGridNotification)
        {
            using var monitor = ArnNotificationUtilsConvertArnNotificaitonV3ToResourceOperation.ToMonitor();
            monitor.OnStart(false);

            try
            {
                monitor.Activity["EventType"] = eventGridNotification.EventType;
                monitor.Activity["ResourceCount"] = eventGridNotification.Data.Resources.Count;

                List<ResourceOperationBase> result = new(eventGridNotification.Data.Resources.Count); 
                foreach (var resource in eventGridNotification.Data.Resources)
                {
                    result.Add(ConvertNotificaitonResourceDataV3ToResourceOperation(resource, eventGridNotification));
                }

                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public static ResourceOperationBase ConvertNotificaitonResourceDataV3ToResourceOperation(
            NotificationResourceDataV3<GenericResource> notificaitonResourceData,
            EventGridNotification<NotificationDataV3<GenericResource>> eventGridNotification)
        {
            var genericResource = notificaitonResourceData.ArmResource;
            var genericResourceArn = new GenericResourceArn(
                location: genericResource?.Location ?? eventGridNotification.Data.ResourceLocation,
                id: genericResource?.Id ?? notificaitonResourceData.ResourceId,
                name: genericResource?.Name,
                type: genericResource?.Type,
                tags: genericResource?.Tags,
                plan: genericResource?.Plan,
                properties: genericResource?.Properties,
                kind: genericResource?.Kind,
                managedBy: genericResource?.ManagedBy,
                sku: genericResource?.Sku,
                identity: genericResource?.Identity,
                zones: genericResource?.Zones,
                systemData: genericResource?.SystemData,
                extendedLocation: genericResource?.ExtendedLocation,
                displayName: genericResource?.DisplayName,
                apiVersion: genericResource?.ApiVersion);
            var apiVersion = notificaitonResourceData.ApiVersion ?? eventGridNotification.Data.ApiVersion;
            var correlationId = notificaitonResourceData.CorrelationId ?? eventGridNotification.Id;
            var homeTenantId = notificaitonResourceData.HomeTenantId ?? notificaitonResourceData.ResourceHomeTenantId ?? eventGridNotification.Data.HomeTenantId ?? eventGridNotification.Data.ResourceHomeTenantId;
            var eventType = eventGridNotification.EventType;
            var guid = Guid.TryParse(correlationId, out var cid) && cid != Guid.Empty ? cid : Guid.NewGuid();
            var sourceResourceId = notificaitonResourceData.SourceResourceId;

            apiVersion = string.IsNullOrEmpty(apiVersion) ? "NotDefined" : apiVersion;

            ResourceAction action = ResourceAction.Undefined;

            if (eventType.EndsWith(Write, StringComparison.OrdinalIgnoreCase))
            {
                action = ResourceAction.Write;
            }
            else if (eventType.EndsWith(Delete, StringComparison.OrdinalIgnoreCase))
            {
                action = ResourceAction.Delete;
            }
            else if (eventType.EndsWith(Move, StringComparison.OrdinalIgnoreCase))
            {
                action = ResourceAction.Move;
            }
            else if (eventType.EndsWith(MoveAction, StringComparison.OrdinalIgnoreCase))
            {
                action = ResourceAction.Move;
            }
            else if (eventType.EndsWith(Snapshot, StringComparison.OrdinalIgnoreCase))
            {
                action = ResourceAction.Snapshot;
            }

            // set property of public string ResourceLocation { get; set; } in ResourceOperationBase
            var additionalResourceProperties = notificaitonResourceData.AdditionalResourceProperties;

            return action == ResourceAction.Snapshot
                ? new ResourceSnapshot(
                    genericResourceArn,
                    eventGridNotification.EventTime,
                    apiVersion,
                    guid,
                    null,
                    null,
                    homeTenantId: homeTenantId,
                    frontdoorLocation: eventGridNotification.Data.FrontdoorLocation,
                    statusCode: notificaitonResourceData.StatusCode,
                    additionalProperties: additionalResourceProperties == null ? null : JToken.FromObject(additionalResourceProperties))
                : new ResourceChange(
                    genericResourceArn,
                    action,
                    eventGridNotification.EventTime,
                    apiVersion,
                    guid,
                    null,
                    null,
                    sourceResourceId: sourceResourceId,
                    homeTenantId: homeTenantId,
                    frontdoorLocation: eventGridNotification.Data.FrontdoorLocation,
                    statusCode: notificaitonResourceData.StatusCode,
                    additionalProperties: additionalResourceProperties == null ? null : JToken.FromObject(additionalResourceProperties));
        }

    }
}
