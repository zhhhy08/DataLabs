namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN
{
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;

    [ExcludeFromCodeCoverage]
    public class ARNSingleInputMessage : AbstractInputMessage<ARNNotification>
    {
        private string _correlationId;
        private string _eventType;
        private string _eventAction;
        private string _subscriptionId;
        private string _resourceType;
        private string _resourceId;
        private string _tenantId;
        private string _resourceLocation;
        private DateTimeOffset _eventTime;

        /* 
         * Notice: Try to avoid calling eventTime field unless it is really necessary because it will deserialize binary
         * For example. please don't call EventTime only for logging 
         * Currently EvenTime is used only in InputCache to store up-to-date cache
         * If we need to use EventTime for other purpose, 
         *  // TODO, Work with Jing to get the meta data from EventHub Property
         */
        public override DateTimeOffset EventTime
        {
            get
            {
                if (_eventTime == default)
                {
                    _ = DeserializedObject;
                }
                return _eventTime;
            }
            set
            {
                _eventTime = value;
            }
        }

        public override string CorrelationId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_correlationId))
                {
                    return _correlationId;
                }
                _ = DeserializedObject;
                return _correlationId;
            }
            set
            {
                if (value != null)
                {
                    _correlationId = value;
                }
            }
        }

        public string EventType
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_eventType))
                {
                    return _eventType;
                }
                _ = DeserializedObject;
                return _eventType;
            }
            set
            {
                if (value != null)
                {
                    _eventType = value;
                }
            }
        }

        public string EventAction
        {
            get
            {
                if (_eventAction != null)
                {
                    return _eventAction;
                }
                var eventType = EventType;
                _eventAction = ArmUtils.GetAction(eventType);
                return _eventAction;
            }
            set
            {
                if (value != null)
                {
                    _eventAction = value;
                }
            }
        }

        public string TenantId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_tenantId))
                {
                    return _tenantId.Equals(InputOutputConstants.EmptyField) ? null : _tenantId;
                }
                _ = DeserializedObject;
                return _tenantId;
            }
            set
            {
                if (value != null)
                {
                    _tenantId = value;
                }
            }
        }

        public string ResourceLocation
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_resourceLocation))
                {
                    return _resourceLocation.Equals(InputOutputConstants.EmptyField) ? null : _resourceLocation;
                }
                _ = DeserializedObject;
                return _resourceLocation;
            }
            set
            {
                if (value != null)
                {
                    _resourceLocation = value;
                }
            }
        }

        public override string ResourceId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_resourceId))
                {
                    return _resourceId;
                }
                _ = DeserializedObject;
                return _resourceId;
            }
            set
            {
                if (value != null)
                {
                    _resourceId = value;
                }
            }
        }

        public string ResourceType
        {
            get
            {
                if (_resourceType != null)
                {
                    return _resourceType;
                }
                if (!string.IsNullOrWhiteSpace(ResourceId))
                {
                    _resourceType = ArmUtils.GetResourceType(ResourceId) ?? string.Empty; // To avoid repeating parsing
                }
                return _resourceType;
            }
            set
            {
                if (value != null)
                {
                    _resourceType = value;
                }
            }
        }

        public string SubscriptionId
        {
            get
            {
                if (_subscriptionId != null)
                {
                    return _subscriptionId;
                }
                if (!string.IsNullOrWhiteSpace(ResourceId))
                {
                    // Some notification doesn't have subscriptionId
                    _subscriptionId = ResourceId.GetSubscriptionIdOrNull() ?? string.Empty; // To avoid repeating parsing
                }
                return string.IsNullOrWhiteSpace(_subscriptionId) ? null : _subscriptionId;
            }
            set
            {
                if (value != null)
                {
                    _subscriptionId = value;
                }
            }
        }

        public override SolutionDataFormat DataFormat => SolutionDataFormat.ARN;
        public override Func<BinaryData, bool, ARNNotification> Deserializer => ARNNotification.ARNDeserializer;
        public override Func<ARNNotification, BinaryData> Serializer => ARNNotification.ARNSerializer;
        protected override Counter<long> DeserializerCounter => IOServiceOpenTelemetry.ARNSingleInputDeserializerCounter;

        public ARNSingleInputMessage(
            BinaryData serializedData,
            ARNNotification deserializedObject,
            string correlationId,
            string eventType,
            string resourceId,
            string tenantId,
            string resourceLocation,
            DateTimeOffset eventTime,
            bool hasCompressed)
        {
            SerializedData = serializedData;
            DeserializedObject = deserializedObject;
            EventType = string.IsNullOrWhiteSpace(eventType) ? null : eventType;
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
            ResourceId = string.IsNullOrWhiteSpace(resourceId) ? null : resourceId;
            TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
            ResourceLocation = string.IsNullOrWhiteSpace(resourceLocation) ? null : resourceLocation;
            EventTime = eventTime;
            HasCompressed = hasCompressed;

            // If there is information from deserializedObject, use it
            if (deserializedObject != null)
            {
                FillInfoWithDeserializedObject(deserializedObject);
            }
        }

        public override void AddCommonTags(OpenTelemetryActivityWrapper taskActivity)
        {
            if (taskActivity == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(CorrelationId))
            {
                taskActivity.InputCorrelationId = CorrelationId;
            }
            if (!string.IsNullOrWhiteSpace(ResourceId))
            {
                taskActivity.InputResourceId = ResourceId;
            }
            if (!string.IsNullOrWhiteSpace(EventType))
            {
                taskActivity.EventType = EventType;
            }
            if (EventTime != default)
            {
                taskActivity.SetTag(SolutionConstants.EventTime, EventTime);
            }

            taskActivity.SetTag(SolutionConstants.TenantId, TenantId);
            taskActivity.SetTag(SolutionConstants.ResourceLocation, ResourceLocation);
            taskActivity.SetTag(SolutionConstants.InputResourceType, ResourceType);
            taskActivity.SetTag(SolutionConstants.Compressed, HasCompressed);
        }

        protected override void FillInfoWithDeserializedObject(ARNNotification deserializedObject)
        {
            if (deserializedObject == null)
            {
                return;
            }

            var arnV3Notification = deserializedObject.NotificationDataV3;
            var eventV3 = arnV3Notification.Data;
            var tenantId = ArmUtils.GetTenantId(arnV3Notification);
          
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                TenantId = tenantId;
            }

            if (!string.IsNullOrWhiteSpace(eventV3?.ResourceLocation))
            {
                ResourceLocation = eventV3.ResourceLocation;
            }

            var eventTime = ArmUtils.GetEventTime(arnV3Notification);
            if (eventTime != default)
            {
                EventTime = eventTime;
            }

            if (!string.IsNullOrWhiteSpace(arnV3Notification.EventType))
            {
                EventType = arnV3Notification.EventType;
            }

            if (eventV3.Resources?.Count > 0)
            {
                if (eventV3.Resources?.Count > 1)
                {
                    throw new InvalidOperationException("ARNSingleInputMessage should have at most only one resource");
                }

                var resource = eventV3.Resources[0];

                // Set correlation Id with Resource CorrelationId
                if (!string.IsNullOrWhiteSpace(resource.CorrelationId))
                {
                    _correlationId = resource.CorrelationId;
                }
                // For some reason, there is no resource correlation Id, let's set it with eventGridId
                if (string.IsNullOrWhiteSpace(_correlationId))
                {
                    // If we still don't have correlation Id, Let's set it from EventGrid Id
                    _correlationId = arnV3Notification.Id;
                }

                ResourceId = resource.ResourceId;
                ResourceType = ArmUtils.GetResourceType(resource.ResourceId);
            }
        }

        public override void AddRetryProperties(ref TagList tagList)
        {
            // Add Data Format
            tagList.Add(SolutionConstants.DataFormat, DataFormat.FastEnumToString()); // string
            tagList.Add(InputOutputConstants.PropertyTag_SingleResource, true); // bool

            if (!string.IsNullOrWhiteSpace(_correlationId))
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_CorrrelationId, _correlationId); // string
            }

            if (!string.IsNullOrWhiteSpace(_eventType))
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_EventType, _eventType); // string
            }

            if (_eventTime != default)
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_EventTime, _eventTime.ToUnixTimeMilliseconds()); // long
            }

            if (!string.IsNullOrWhiteSpace(_tenantId))
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_Tenant_Id, _tenantId); // string
            }

            if (!string.IsNullOrWhiteSpace(_resourceLocation))
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_ResourceLocation, _resourceLocation); // string
            }

            if (!string.IsNullOrWhiteSpace(_resourceId))
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_Resource_Id, _resourceId); // string
            }

            if (HasCompressed)
            {
                // Add only when there is compressed
                tagList.Add(InputOutputConstants.PropertyTag_Input_HasCompressed, HasCompressed); // bool
            }
        }

        public static ARNSingleInputMessage CreateSingleInputMessage(
            EventData eventData, 
            OpenTelemetryActivityWrapper taskActivity,
            in DataLabEventHubProperties dataLabEventHubProperties)
        {
            var inputMessage = new ARNSingleInputMessage(
                serializedData: eventData.Data,
                deserializedObject: null,
                correlationId: dataLabEventHubProperties.CorrelationId,
                eventType: dataLabEventHubProperties.EventType,
                resourceId: dataLabEventHubProperties.ResourceId,
                tenantId: dataLabEventHubProperties.TenantId,
                resourceLocation: dataLabEventHubProperties.ResourceLocation,
                eventTime: dataLabEventHubProperties.EventTime,
                hasCompressed: dataLabEventHubProperties.HasCompressed ?? false);

            inputMessage.AddCommonTags(taskActivity);
            return inputMessage;
        }

        public static ARNSingleInputMessage CreateSingleInputMessage(
            EventGridNotification<NotificationDataV3<GenericResource>> singleResourceEventGridEvent, 
            BinaryData binaryData, 
            OpenTelemetryActivityWrapper taskActivity)
        {
            // From ARN message
            // This is usually from blob URI based notification
            GuardHelper.IsArgumentEqual(1, singleResourceEventGridEvent.Data.Resources.Count);

            var singleNotification = new ARNNotification()
            {
                NotificationDataV3 = singleResourceEventGridEvent
            };

            var inputMessage = new ARNSingleInputMessage(
                serializedData: binaryData,
                deserializedObject: singleNotification,
                correlationId: null,
                eventType: null,
                resourceId: null,
                tenantId: null,
                resourceLocation: null,
                eventTime: default,
                hasCompressed: false);

            inputMessage.AddCommonTags(taskActivity);
            return inputMessage;
        }

        public static ARNSingleInputMessage CreateSingleInputMessage(
            ServiceBusReceivedMessage message, 
            OpenTelemetryActivityWrapper taskActivity,
            in DataLabServiceBusProperties dataLabServiceBusProperties)
        {
            var inputMessage = new ARNSingleInputMessage(
                serializedData: message.Body,
                deserializedObject: null,
                correlationId: dataLabServiceBusProperties.CorrelationId,
                eventType: dataLabServiceBusProperties.EventType,
                resourceId: dataLabServiceBusProperties.ResourceId,
                tenantId: dataLabServiceBusProperties.TenantId,
                resourceLocation: dataLabServiceBusProperties.ResourceLocation,
                eventTime: dataLabServiceBusProperties.EventTime,
                hasCompressed: dataLabServiceBusProperties.HasCompressed ?? false);

            inputMessage.AddCommonTags(taskActivity);
            return inputMessage;
        }
    }
}
