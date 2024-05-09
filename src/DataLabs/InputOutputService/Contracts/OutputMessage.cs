namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using StackExchange.Redis;

    public class OutputMessage
    {
        public IIOEventTaskContext ParentIOEventTaskContext { get; }
        public CancellationToken TaskCancellationToken => ParentIOEventTaskContext.TaskCancellationToken;
        public ActivityContext ParentActivityContext => ParentIOEventTaskContext.EventTaskActivity.Context;

        public BinaryData GetOutputMessage() => Data;
        public int GetOutputMessageSize() => Data?.ToMemory().Length ?? 0;

        public SolutionDataFormat OutputFormat { get; }

        // serialized binary data for output message
        public BinaryData Data { get; }

        // correlationId to track this message across entire flow. e.g) ARM Correlation Id
        public string CorrelationId { get; }

        // Output Resource Id (e.g. ARM ID)
        public string ResourceId { get; }

        public string TenantId { get; }

        public string ResourceType { get; }

        public string EventType { get; }

        public string ResourceLocation { get; }

        public long OutputTimeStamp { get; }

        public string ETag { get; set; }

        // Additional Properties for this Message
        public IDictionary<string, string> RespProperties { get; set; }

        public OutputMessage(
            SolutionDataFormat outputFormat, 
            BinaryData data, 
            string correlationId, 
            string resourceId, 
            string tenantId,
            string eventType,
            string resourceLocation,
            string etag,
            long outputTimeStamp,
            IDictionary<string, string> respProperties,
            IIOEventTaskContext parentIOEventTaskContext)
        {
            OutputFormat = outputFormat;
            Data = data;
            CorrelationId = correlationId;
            ResourceId = resourceId;
            TenantId = tenantId;
            EventType = eventType;
            ResourceType = ArmUtils.GetResourceType(resourceId);
            ResourceLocation = resourceLocation;
            ETag = string.IsNullOrEmpty(etag) ? null : etag;
            OutputTimeStamp = outputTimeStamp;
            RespProperties = respProperties;
            ParentIOEventTaskContext = parentIOEventTaskContext;
        }           

        public void AddRetryProperties(ref TagList tagList)
        {
            // Add Data Format
            tagList.Add(InputOutputConstants.PropertyTag_Output_ResourceFormat, OutputFormat.FastEnumToString()); // string

            if (CorrelationId != null)
            {
                tagList.Add(InputOutputConstants.PropertyTag_Output_CorrrelationId, CorrelationId); // string
            }
            if (ResourceId != null)
            {
                tagList.Add(InputOutputConstants.PropertyTag_Output_Resource_Id, ResourceId); // string
            }
            if (TenantId != null)
            {
                tagList.Add(InputOutputConstants.PropertyTag_Output_Tenant_Id, TenantId); // string
            }
            if (EventType != null)
            {
                tagList.Add(InputOutputConstants.PropertyTag_Output_EventType, EventType); // string
            }
            if (ETag != null)
            {
                tagList.Add(InputOutputConstants.PropertyTag_Output_ETag, ETag); // string
            }
            if (OutputTimeStamp > 0)
            {
                tagList.Add(InputOutputConstants.PropertyTag_Output_TimeStamp, OutputTimeStamp); // long
            }

            /*
            if (RespProperties?.Count > 0) {
                foreach(var item in RespProperties)
                {
                    if (!eventData.ApplicationProperties.TryAdd(item.Key, item.Value))
                    {
                        // duplicated key. should not happen
                        Logger.LogCritical("ResponseProperty Key is duplicated with reserved name: {name}", item.Key);
                    }
                }
            }
            */
        }

        /*
         * Notice!!!
         * This method should match with above AddRetryProperties
         * When you add new property for retry inside AddRetryProperties, you MUST add the property in below method
         */
        public static OutputMessage CreateOutputMessage(ServiceBusReceivedMessage message, IIOEventTaskContext parentIOEventTaskContext)
        {
            var resourceFormat = SolutionDataFormat.ARN;
            string correlationId = null;
            string resourceId = null;
            string tenantId = null;
            string eventType = null;
            string resourceLocation = null;
            long outputTimeStamp = 0;
            string etag = null;

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Output_ResourceFormat, out object outVal) && outVal is string)
            {
                resourceFormat = StringEnumCache.GetEnumIgnoreCase<SolutionDataFormat>((string)outVal);
            }

            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Output_CorrrelationId, out outVal))
            {
                correlationId = outVal?.ToString();
            }
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Output_Resource_Id, out outVal))
            {
                resourceId = outVal?.ToString();
            }
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Output_Tenant_Id, out outVal))
            {
                tenantId = outVal?.ToString();
            }
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Output_EventType, out outVal))
            {
                eventType = outVal?.ToString();
            }
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Output_ResourceLocation, out outVal))
            {
                resourceLocation = outVal?.ToString();
            }
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Output_ETag, out outVal))
            {
                etag = outVal?.ToString();
            }
            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Output_TimeStamp, out outVal))
            {
                outputTimeStamp = Convert.ToInt64(outVal);
            }

            return new OutputMessage(
                outputFormat: resourceFormat,
                data: message.Body,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId,
                eventType: eventType,
                resourceLocation: resourceLocation,
                etag: etag,
                outputTimeStamp: outputTimeStamp,
                respProperties: null,
                parentIOEventTaskContext: parentIOEventTaskContext);
        }
    }
}
