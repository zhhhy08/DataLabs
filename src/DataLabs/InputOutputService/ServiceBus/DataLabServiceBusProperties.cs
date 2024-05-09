namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;

    [ExcludeFromCodeCoverage]
    public readonly struct DataLabServiceBusProperties
    {
        public readonly string DataFormat;
        public readonly string ActivityId;
        public readonly string ParentDifferentTraceId;
        public readonly string CorrelationId;
        public readonly string EventType;
        public readonly string ResourceId;
        public readonly string TenantId;
        public readonly string ResourceLocation;
        public readonly string RegionName;
        public readonly int? RetryCount;
        public readonly DateTimeOffset EventTime;
        public readonly DateTimeOffset TopActivityStartTime;
        public readonly DateTimeOffset FirstEnqueuedTime;
        public readonly DateTimeOffset FirstPickedUpTime;
        public readonly long? PartnerSpentTime;
        public readonly bool? HasInput;
        public readonly bool? HasOutput;
        public readonly bool? HasSourceOfTruthConflict;
        public readonly bool? HasSuccessInputCacheWrite;
        public readonly bool? SingleInlineResource;
        public readonly bool? HasCompressed;

        public DataLabServiceBusProperties(
            string dataFormat,
            string activityId,
            string parentDifferentTraceId,
            string correlationId,
            string eventType,
            string resourceId,
            string tenantId,
            string resourceLocation,
            string regionName,
            int? retryCount,
            DateTimeOffset eventTime,
            DateTimeOffset topActivityStartTime,
            DateTimeOffset firstEnqueuedTime,
            DateTimeOffset firstPickedUpTime,
            long? partnerSpentTime,
            bool? hasInput,
            bool? hasOutput,
            bool? hasSourceOfTruthConflict,
            bool? hasSuccessInputCacheWrite,
            bool? singleInlineResource,
            bool? hasCompressed)
        {
            DataFormat = dataFormat;
            ActivityId = activityId;
            ParentDifferentTraceId = parentDifferentTraceId;
            CorrelationId = correlationId;
            EventType = eventType;
            ResourceId = resourceId;
            TenantId = tenantId;
            ResourceLocation = resourceLocation;
            RegionName = regionName;
            RetryCount = retryCount;
            EventTime = eventTime;
            TopActivityStartTime = topActivityStartTime;
            FirstEnqueuedTime = firstEnqueuedTime;
            FirstPickedUpTime = firstPickedUpTime;
            PartnerSpentTime = partnerSpentTime;
            HasInput = hasInput;
            HasOutput = hasOutput;
            HasSourceOfTruthConflict = hasSourceOfTruthConflict;
            HasSuccessInputCacheWrite = hasSuccessInputCacheWrite;
            SingleInlineResource = singleInlineResource;
            HasCompressed = hasCompressed;
        }

        public static DataLabServiceBusProperties Create(ServiceBusReceivedMessage message)
        {
            string dataFormat = null;
            string activityId = null;
            string parentDifferentTraceId = null;
            string correlationId = null;
            string eventType = null;
            string resourceId = null;
            string tenantId = null;
            string resourceLocation = null;
            string regionName = null;
            int? retryCount = null;
            DateTimeOffset eventTime = default;
            DateTimeOffset topActivityStartTime = default;
            DateTimeOffset firstEnqueuedTime = default;
            DateTimeOffset firstPickedUpTime = default;
            long? partnerSpentTime = null;
            bool? hasInput = null;
            bool? hasOutput = null;
            bool? hasSourceOfTruthConflict = null;
            bool? hasSuccessInputCacheWrite = null;
            bool? singleInlineResource = null;
            bool? hasCompressed = null;

            // DataFormat
            if (message.ApplicationProperties.TryGetValue(SolutionConstants.DataFormat, out var outVal))
            {
                var outStr = outVal?.ToString();
                dataFormat = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // ActivityId 
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_ActivityId, out outVal))
            {
                var outStr = outVal?.ToString();
                activityId = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // Different parent TraceId. For example. such as input -> multiResponses, this traceId is input task's traceId
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_ParentTraceId, out outVal))
            {
                var outStr = outVal?.ToString();
                parentDifferentTraceId = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // CorrrelationId
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_CorrrelationId, out outVal))
            {
                var outStr = outVal?.ToString();
                correlationId = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // EventType
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_EventType, out outVal))
            {
                var outStr = outVal?.ToString();
                eventType = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // ResourceId
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_Resource_Id, out outVal))
            {
                var outStr = outVal?.ToString();
                resourceId = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // TenantId
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_Tenant_Id, out outVal))
            {
                var outStr = outVal?.ToString();
                tenantId = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // ResourceLocation
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_ResourceLocation, out outVal))
            {
                var outStr = outVal?.ToString();
                resourceLocation = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // string
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_RegionName, out outVal))
            {
                var outStr = outVal?.ToString();
                regionName = string.IsNullOrWhiteSpace(outStr) ? null : outStr;
            }

            // eventTime
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_EventTime, out outVal))
            {
                eventTime = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(outVal));
            }

            // ActivityStartTime
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_TopActivityStartTime, out outVal))
            {
                topActivityStartTime = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(outVal));
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_RetryCount, out outVal))
            {
                retryCount = Convert.ToInt32(outVal);
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_First_EnqueuedTime, out outVal))
            {
                firstEnqueuedTime = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(outVal));
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_First_PickedUpTime, out outVal))
            {
                firstPickedUpTime = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(outVal));
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Partner_SpentTime, out outVal))
            {
                partnerSpentTime = Convert.ToInt64(outVal);
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_HasInput, out outVal))
            {
                hasInput = Convert.ToBoolean(outVal);
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_HasOutput, out outVal))
            {
                hasOutput = Convert.ToBoolean(outVal);
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_SourceOfTruthConflict, out outVal))
            {
                hasSourceOfTruthConflict = Convert.ToBoolean(outVal);
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_SuccessInputCacheWrite, out outVal))
            {
                hasSuccessInputCacheWrite = Convert.ToBoolean(outVal);
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_SingleResource, out outVal))
            {
                singleInlineResource = Convert.ToBoolean(outVal);
            }

            // This is from Retry Queue
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_HasCompressed, out outVal))
            {
                hasCompressed = Convert.ToBoolean(outVal);
            }

            return new DataLabServiceBusProperties(
                dataFormat: dataFormat,
                activityId: activityId,
                parentDifferentTraceId: parentDifferentTraceId,
                correlationId: correlationId,
                eventType: eventType,
                resourceId: resourceId,
                tenantId: tenantId,
                resourceLocation: resourceLocation,
                regionName: regionName,
                retryCount: retryCount,
                eventTime: eventTime,
                topActivityStartTime: topActivityStartTime,
                firstEnqueuedTime: firstEnqueuedTime,
                firstPickedUpTime: firstPickedUpTime,
                partnerSpentTime: partnerSpentTime,
                hasInput: hasInput,
                hasOutput: hasOutput,
                hasSourceOfTruthConflict: hasSourceOfTruthConflict,
                hasSuccessInputCacheWrite: hasSuccessInputCacheWrite,
                singleInlineResource: singleInlineResource,
                hasCompressed: hasCompressed);
        }

        public void ToLog(OpenTelemetryActivityWrapper activity)
        {
            if (activity == null)
            {
                return;
            }

            activity.SetTag(InputOutputConstants.ServiceBus_Property_DataFormat_Log, DataFormat);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_ActivityId_Log, ActivityId);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_ParentTraceId_Log, ParentDifferentTraceId);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_CorrelationId_Log, CorrelationId);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_EventType_Log, EventType);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_ResourceId_Log, ResourceId);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_TenantId_Log, TenantId);

            if (EventTime != default)
            {
                activity.SetTag(InputOutputConstants.ServiceBus_Property_EventTime_Log, EventTime.ToUnixTimeMilliseconds());
            }

            if (TopActivityStartTime != default)
            {
                activity.SetTag(InputOutputConstants.ServiceBus_Property_TopActivityStartTime_Log, TopActivityStartTime.ToUnixTimeMilliseconds());
            }

            activity.SetTag(InputOutputConstants.ServiceBus_Property_RetryCount_Log, RetryCount);

            if (FirstEnqueuedTime != default)
            {
                activity.SetTag(InputOutputConstants.ServiceBus_Property_FirstEnqueuedTime_Log, FirstEnqueuedTime.ToUnixTimeMilliseconds());
            }

            if (FirstPickedUpTime != default)
            {
                activity.SetTag(InputOutputConstants.ServiceBus_Property_FirstPickedUpTime_Log, FirstPickedUpTime.ToUnixTimeMilliseconds());
            }

            if (FirstPickedUpTime != default)
            {
                activity.SetTag(InputOutputConstants.ServiceBus_Property_FirstPickedUpTime_Log, FirstPickedUpTime.ToUnixTimeMilliseconds());
            }

            activity.SetTag(InputOutputConstants.ServiceBus_Property_PartnerSpentTime_Log, PartnerSpentTime);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_HasInput_Log, HasInput);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_HasOutput_Log, HasOutput);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_HasSourceOfTruthConflict_Log, HasSourceOfTruthConflict);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_HasSuccessInputCacheWrite_Log, HasSuccessInputCacheWrite);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_SingleInlineResource_Log, SingleInlineResource);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_Compressed_Log, HasCompressed);
            activity.SetTag(InputOutputConstants.ServiceBus_Property_RegionName, RegionName);

        }
    }
}
