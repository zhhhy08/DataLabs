namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using System;
    using System.Collections.Generic;

    public class DataLabsARNV3Request
    {
        public readonly DateTimeOffset RequestTime;
        public readonly string TraceId;
        public readonly int RetryCount;
        public readonly string? CorrelationId;
        public readonly EventGridNotification<NotificationDataV3<GenericResource>> InputResource;
        public readonly IDictionary<string, string>? Attributes;
        public readonly string RegionName;


        public DataLabsARNV3Request(
            DateTimeOffset requestTime,
            string traceId,
            int retryCount,
            string? correlationId,
            EventGridNotification<NotificationDataV3<GenericResource>> inputResource,
            IDictionary<string, string>? reqAttributes) : this(requestTime, traceId, retryCount, correlationId, inputResource, reqAttributes, string.Empty)
        {   
        }

        public DataLabsARNV3Request(
            DateTimeOffset requestTime,
            string traceId,
            int retryCount,
            string? correlationId,
            EventGridNotification<NotificationDataV3<GenericResource>> inputResource,
            IDictionary<string, string>? reqAttributes,
            string regionName) 
        {
            RequestTime = requestTime;
            TraceId = traceId;
            RetryCount = retryCount;
            CorrelationId = correlationId;
            InputResource = inputResource;
            Attributes = reqAttributes;
            RegionName = regionName;
        }
    }
}
