namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;

    public class DataLabsConfigSpecsRequest
    {
        public readonly string TraceId;
        public readonly int RetryCount;
        public readonly string? CorrelationId;
        public readonly DateTimeOffset RequestTime;
        public readonly string ApiExtension;

        public DataLabsConfigSpecsRequest(
            string traceId,
            int retryCount,
            string? correlationId,
            string apiExtension)
        {
            TraceId = traceId;
            RetryCount = retryCount;
            CorrelationId = correlationId;
            RequestTime = DateTimeOffset.UtcNow;
            ApiExtension = apiExtension;
        }
    }
}
