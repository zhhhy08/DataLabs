namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub
{
    using global::Azure.Messaging.EventHubs;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    
    [ExcludeFromCodeCoverage]
    public readonly struct DataLabEventHubProperties
    {
        public readonly string DataFormat;
        public readonly string EventType;
        public readonly string CorrelationId;
        public readonly string ResourceId;
        public readonly string TenantId;
        public readonly string ResourceLocation;
        public readonly DateTimeOffset EventTime;
        public readonly int? NumResources;
        public readonly bool? HasURL;
        public readonly bool? HasCompressed;

        public DataLabEventHubProperties(
            string dataFormat,
            string eventType,
            string correlationId,
            string resourceId,
            string tenantId,
            string resourceLocation,
            DateTimeOffset eventTime,
            int? numResources,
            bool? hasURL,
            bool? hasCompressed)
        {
            DataFormat = dataFormat;
            EventType = eventType;
            CorrelationId = correlationId;
            ResourceId = resourceId;
            TenantId = tenantId;
            ResourceLocation = resourceLocation;
            EventTime = eventTime;
            NumResources = numResources;
            HasURL = hasURL;
            HasCompressed = hasCompressed;
        }

        public static DataLabEventHubProperties Create(EventData eventData)
        {
            string dataFormat = null;
            string eventType = null;
            string correlationId = null;
            string resourceId = null;
            string tenantId = null;
            string resourceLocation = null;
            DateTimeOffset eventTime = default;
            int? numResources = null;
            bool? hasURL = null;
            bool? hasCompressed = null;

            // DataFormat
            if (eventData.Properties.TryGetValue(SolutionConstants.DataFormat, out var outVal))
            {
                var outStr = outVal?.ToString();
                dataFormat = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // EventType
            if (eventData.Properties.TryGetValue(InputOutputConstants.EventHub_Property_EventType, out outVal))
            {
                var outStr = outVal?.ToString();
                eventType = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // CorrrelationId
            if (eventData.Properties.TryGetValue(InputOutputConstants.EventHub_Property_CorrelationId, out outVal))
            {
                var outStr = outVal?.ToString();
                correlationId = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // ResourceId
            if (eventData.Properties.TryGetValue(InputOutputConstants.EventHub_Property_ResourceId, out outVal))
            {
                var outStr = outVal?.ToString();
                resourceId = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // TenantId
            if (eventData.Properties.TryGetValue(InputOutputConstants.EventHub_Property_TenantId, out outVal))
            {
                var outStr = outVal?.ToString();
                tenantId = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }
            if (resourceId != null && tenantId == null)
            {
                // Based on disucssion with Jing
                // If there is resourceId but no tenantId
                // we can assume that there is no tenantId. So we can avoid unnecessary deserialization
                tenantId = InputOutputConstants.EmptyField;
            }

            // resourceLocation
            if (eventData.Properties.TryGetValue(InputOutputConstants.EventHub_Property_ResourceLocation, out outVal))
            {
                var outStr = outVal?.ToString();
                resourceLocation = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }
            if (resourceId != null && resourceLocation == null)
            {
                // Similar to TenantId
                // we can assume that there is no resourceLocation. So we can avoid unnecessary deserialization
                resourceLocation = InputOutputConstants.EmptyField;
            }

            // NumResources
            if (eventData.Properties.TryGetValue(InputOutputConstants.EventHub_Property_NumResources, out outVal))
            {
                numResources = Convert.ToInt32(outVal);
            }

            // eventTime
            if (eventData.Properties.TryGetValue(InputOutputConstants.EventHub_Property_EventTime, out outVal))
            {
                eventTime = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(outVal));
            }

            // HasURL
            if (eventData.Properties.TryGetValue(InputOutputConstants.EventHub_Property_hasURL, out outVal))
            {
                hasURL = Convert.ToBoolean(outVal);
            }

            // HasCompressed
            if (eventData.Properties.TryGetValue(InputOutputConstants.EventHub_Property_Compressed, out outVal))
            {
                hasCompressed = Convert.ToBoolean(outVal);
            }

            return new DataLabEventHubProperties(
                dataFormat: dataFormat,
                eventType: eventType,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId,
                resourceLocation: resourceLocation,
                eventTime: eventTime,
                numResources: numResources,
                hasURL: hasURL,
                hasCompressed: hasCompressed);
        }

        public void ToLog(OpenTelemetryActivityWrapper activity)
        {
            if (activity == null)
            {
                return;
            }

            activity.SetTag(InputOutputConstants.EventHub_Property_DataFormat_Log, DataFormat);
            activity.SetTag(InputOutputConstants.EventHub_Property_EventType_Log, EventType);
            activity.SetTag(InputOutputConstants.EventHub_Property_CorrelationId_Log, CorrelationId);
            activity.SetTag(InputOutputConstants.EventHub_Property_ResourceId_Log, ResourceId);
            activity.SetTag(InputOutputConstants.EventHub_Property_TenantId_Log, TenantId);
            activity.SetTag(InputOutputConstants.EventHub_Property_ResourceLocation_Log, ResourceLocation);

            if (EventTime != default)
            {
                activity.SetTag(InputOutputConstants.EventHub_Property_EventTime_Log, EventTime.ToUnixTimeMilliseconds());
            }
            
            activity.SetTag(InputOutputConstants.EventHub_Property_NumResources_Log, NumResources);
            activity.SetTag(InputOutputConstants.EventHub_Property_hasURL_Log, HasURL);
            activity.SetTag(InputOutputConstants.EventHub_Property_Compressed_Log, HasCompressed);
        }
    }
}
