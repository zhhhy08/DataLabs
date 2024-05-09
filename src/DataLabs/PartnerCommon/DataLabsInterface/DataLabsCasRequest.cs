namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;

    public class DataLabsCasRequest
    {
        public string TraceId;
        public int RetryCount;
        public string? CorrelationId;
        public readonly DateTimeOffset RequestTime;
        public CasRequestBody casRequestBody;

        public DataLabsCasRequest(
          string traceId,
          int retryCount,
          string? correlationId,
          CasRequestBody casRequestBody)
        {
            this.TraceId = traceId;
            this.RetryCount = retryCount;
            this.CorrelationId = correlationId;
            this.RequestTime = DateTimeOffset.UtcNow;
            this.casRequestBody = casRequestBody;
        }
    }
}
