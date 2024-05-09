namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;

    [ExcludeFromCodeCoverage]
    public class ARNRawInputMessage : AbstractInputMessage<ARNRawNotification>
    {
        private static readonly ActivityMonitorFactory ARNRawInputMessageEventHubMessageParseError = new ("ARNRawInputMessage.EventHubMessageParseError");

        public override DateTimeOffset EventTime { get; set; }
        public override string CorrelationId { get; set; }
        public override string ResourceId { get; set; } // RawMessage doesn't know ResourceId. RawMessage is either batched or blob

        public string EventType { get; set; }

        private string _tenantId;
        private string _resourceLocation;

        public string TenantId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_tenantId))
                {
                    return _tenantId.Equals(InputOutputConstants.EmptyField) ? null : _tenantId;
                }
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

        public override SolutionDataFormat DataFormat => SolutionDataFormat.ARN;
        public override Func<BinaryData, bool, ARNRawNotification> Deserializer
        {
            get
            {
                return NoDeserialize ? null : ARNRawNotification.ARNDeserializer;
            }
        }
        public override Func<ARNRawNotification, BinaryData> Serializer => ARNRawNotification.ARNSerializer;
        protected override Counter<long> DeserializerCounter => IOServiceOpenTelemetry.ARNRawInputDeserializerCounter;

        public bool NoDeserialize { get; set; }

        public ARNRawInputMessage(
            BinaryData serializedData,
            string rawInputCorrelationId,
            DateTimeOffset eventTime,
            string eventType,
            string tenantId,
            string resourceLocation,
            bool hasCompressed)
        {
            SerializedData = serializedData;
            CorrelationId = string.IsNullOrWhiteSpace(rawInputCorrelationId) ? null : rawInputCorrelationId;
            EventTime = eventTime;
            EventType = string.IsNullOrWhiteSpace(eventType) ? null : eventType;
            TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
            ResourceLocation = string.IsNullOrWhiteSpace(resourceLocation) ? null : resourceLocation;
            HasCompressed = hasCompressed;
        }

        public override void AddCommonTags(OpenTelemetryActivityWrapper taskActivity)
        {
            if (taskActivity == null)
            {
                return;
            }

            // Don't try to deserialize RawInputMessage
            //   because RawInputMessge should be used as PoisionMessage
            //   when inputBinary has issue during deserialization
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
            taskActivity.SetTag(SolutionConstants.Compressed, HasCompressed);
        }

        protected override void FillInfoWithDeserializedObject(ARNRawNotification deserializedObject)
        {
            if (deserializedObject == null)
            {
                return;
            }

            // check if it is single resource
            if (deserializedObject.NotificationDataV3s?.Length == 1)
            {
                var notification = deserializedObject.NotificationDataV3s[0];
                var tenantId = ArmUtils.GetTenantId(notification);
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    TenantId = tenantId;
                }
                var eventTime = ArmUtils.GetEventTime(notification);
                if (eventTime != default)
                {
                    EventTime = eventTime;
                }

                if (notification.Data?.Resources?.Count == 1)
                {
                    var singleResource = notification.Data.Resources[0];
                    ResourceId = singleResource.ResourceId;

                    if (!string.IsNullOrWhiteSpace(singleResource.CorrelationId))
                    {
                        CorrelationId = singleResource.CorrelationId;
                    }
                }

                // If we still don't have correlation Id, Let's set it from EventGrid Id
                if (string.IsNullOrWhiteSpace(CorrelationId))
                {
                    CorrelationId = notification.Id;
                }

                if (!string.IsNullOrWhiteSpace(notification.EventType))
                {
                    EventType = notification.EventType;
                }
            }

            return;
        }

        public override void AddRetryProperties(ref TagList tagList)
        {
            // Add Data Format
            tagList.Add(SolutionConstants.DataFormat, DataFormat.FastEnumToString()); // string

            if (!string.IsNullOrWhiteSpace(CorrelationId))
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_CorrrelationId, CorrelationId); // string
            }

            if (!string.IsNullOrWhiteSpace(EventType))
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_EventType, EventType); // string
            }

            if (!string.IsNullOrWhiteSpace(_tenantId))
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_Tenant_Id, _tenantId); // string
            }

            if (!string.IsNullOrWhiteSpace(_resourceLocation))
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_ResourceLocation, _resourceLocation); // string
            }

            if (EventTime != default)
            {
                tagList.Add(InputOutputConstants.PropertyTag_Input_EventTime, EventTime.ToUnixTimeMilliseconds()); // long
            }

            if (HasCompressed)
            {
                // Add only when there is compressed
                tagList.Add(InputOutputConstants.PropertyTag_Input_HasCompressed, HasCompressed); // bool
            }
        }

        public static ARNSingleInputMessage TryConvertToSingleMessage(
            ARNRawInputMessage rawInputMessage, 
            OpenTelemetryActivityWrapper taskActivity)
        {
            if (rawInputMessage.DeserializedObject?.NotificationDataV3s?.Length == 1)
            {
                var notification = rawInputMessage.DeserializedObject.NotificationDataV3s[0];
                if (notification.Data?.Resources?.Count == 1)
                {
                    return ARNSingleInputMessage.CreateSingleInputMessage(
                        notification,
                        rawInputMessage.SerializedData,
                        taskActivity);
                }
            }
            return null;
        }

        public static ARNRawInputMessage CreateRawInputMessage(
            BinaryData binaryData,
            string rawInputCorrelationId,
            DateTimeOffset eventTime,
            string eventType,
            string tenantId,
            string resourceLocation,
            bool deserialize,
            bool hasCompressed,
            OpenTelemetryActivityWrapper taskActivity)
        {
            var inputMessage = new ARNRawInputMessage(
                serializedData: binaryData,
                rawInputCorrelationId: rawInputCorrelationId,
                eventTime: eventTime,
                eventType: eventType,
                tenantId: tenantId,
                resourceLocation: resourceLocation,
                hasCompressed: hasCompressed);

            if (deserialize)
            {
                try
                {
                    // Try to deserialize
                    var deserializedObject = inputMessage.DeserializedObject;
                    var notifications = deserializedObject.NotificationDataV3s;

                    // shortcut for one resource
                    if (notifications == null || notifications.Length == 0)
                    {
                        // something wrong, impossible
                        throw new Exception("Empty Raw Notification");
                    }

                    SolutionLoggingUtils.LogRawARNV3Notification(notifications, taskActivity);
                }
                catch (Exception ex)
                {
                    using var monitor = ARNRawInputMessageEventHubMessageParseError.ToMonitor();

                    // Let's dump original binary with string
                    try
                    {
                        monitor.Activity[SolutionConstants.Message] = binaryData.ToString();
                    }
                    catch (Exception)
                    {
                        // ignore
                    }

                    monitor.OnError(ex);

                    throw;
                }
            }

            return inputMessage;

        }

        public static ARNRawInputMessage CreateRawInputMessage(ServiceBusReceivedMessage message)
        {
            // This is from Retry Queue
            string rawInputCorrelationId = null;
            string eventType = null;
            string tenantId = null;
            string resourceLocation = null;
            DateTimeOffset eventTime = default;
            var hasCompressed = false;

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_CorrrelationId, out var outVal))
            {
                var outStr = outVal?.ToString();
                rawInputCorrelationId = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_EventType, out outVal))
            {
                var outStr = outVal?.ToString();
                eventType = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_Tenant_Id, out outVal))
            {
                var outStr = outVal?.ToString();
                tenantId = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_ResourceLocation, out outVal))
            {
                var outStr = outVal?.ToString();
                resourceLocation = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_EventTime, out outVal))
            {
                eventTime = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(outVal));
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_HasCompressed, out outVal) && outVal is bool v)
            {
                hasCompressed = (bool)v;
            }

            var inputMessage = new ARNRawInputMessage(
                serializedData: message.Body,
                rawInputCorrelationId: rawInputCorrelationId,
                eventTime: eventTime,
                eventType: eventType,
                tenantId: tenantId,
                resourceLocation: resourceLocation,
                hasCompressed: hasCompressed);

            return inputMessage;
        }
    }
}
