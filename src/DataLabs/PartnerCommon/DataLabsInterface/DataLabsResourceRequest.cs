namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;

    public class DataLabsResourceRequest
    {
        public readonly string TraceId;
        public readonly int RetryCount;
        public readonly string? CorrelationId;
        public readonly string ResourceId;
        public readonly string TenantId;
        public readonly string? RegionName;
        public readonly DateTimeOffset RequestTime;

        public DataLabsResourceRequest(
            string traceId,
            int retryCount,
            string? correlationId,
            string resourceId,
            string tenantId,
            string? regionName = null)
        {
            TraceId = traceId;
            RetryCount = retryCount;
            CorrelationId = correlationId;
            ResourceId = resourceId;
            TenantId = tenantId;
            RegionName = regionName;
            RequestTime = DateTimeOffset.UtcNow;
        }
    }
}
