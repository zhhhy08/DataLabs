namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System;
    using System.Collections.Generic;

    public sealed class DataLabsARMGenericRequest
    {
        public readonly string TraceId;
        public readonly int RetryCount;
        public readonly string? CorrelationId;
        public readonly string URIPath; // /providers/Microsoft.Authorization/policySetDefinitions
        public readonly string? TenantId;
        public readonly IDictionary<string, string?>? QueryParams;
        public readonly DateTimeOffset RequestTime;

        public DataLabsARMGenericRequest(
            string traceId,
            int retryCount,
            string? correlationId,
            string uriPath,
            IDictionary<string, string?>? queryParams,
            string? tenantId)
        {
            TraceId = traceId;
            RetryCount = retryCount;
            CorrelationId = correlationId;
            URIPath = uriPath;
            TenantId = tenantId;
            QueryParams = queryParams;
            RequestTime = DateTimeOffset.UtcNow;
        }
    }
}
