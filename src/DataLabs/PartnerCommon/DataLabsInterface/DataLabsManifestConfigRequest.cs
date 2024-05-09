namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;

    public class DataLabsManifestConfigRequest
    {
        public readonly string TraceId;
        public readonly int RetryCount;
        public readonly string? CorrelationId;
        public readonly DateTimeOffset RequestTime;
        public readonly string ManifestProvider;

        public DataLabsManifestConfigRequest(
            string traceId,
            int retryCount,
            string? correlationId,
            string manifestProvider)
        {
            TraceId = traceId;
            RetryCount = retryCount;
            CorrelationId = correlationId;
            RequestTime = DateTimeOffset.UtcNow;
            ManifestProvider = manifestProvider;
        }
    }
}
