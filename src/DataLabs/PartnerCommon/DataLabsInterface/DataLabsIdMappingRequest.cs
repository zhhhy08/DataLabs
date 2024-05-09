namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;

    public class DataLabsIdMappingRequest
    {
        public string TraceId;
        public int RetryCount;
        public string? CorrelationId;
        public readonly DateTimeOffset RequestTime;
        public string ResourceType;
        public IdMappingRequestBody IdMappingRequestBody;

        public DataLabsIdMappingRequest(
          string traceId,
          int retryCount,
          string? correlationId,
          string resourceType,
          IdMappingRequestBody idMappingRequestBody)
        {
            this.TraceId = traceId;
            this.RetryCount = retryCount;
            this.CorrelationId = correlationId;
            this.RequestTime = DateTimeOffset.UtcNow;
            this.ResourceType = resourceType;
            this.IdMappingRequestBody = idMappingRequestBody;
        }
    }
}
