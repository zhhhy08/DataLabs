namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants
{
    public static class CommonHttpHeaders
    {
        /* Headers between Resource Fetcher Client and Resource Fetcher Services */

        // ResourceFetcherProxyClient -> Resource Fetcher Request Headers
        public const string DataLabs_OpenTelemetry_ActivityId = "x-dl-otel-activity-id";
        public const string DataLabs_InputCorrelationId = "x-dl-input-correlation-id";
        public const string DataLabs_RetryFlowCount = "x-dl-retryflow_count";
        public const string DataLabs_PartnerName = "x-dl-partner-name";
        
        // Resource Fetcher -> Resource Fetcher Client Response Header
        public const string DataLabs_AuthError = "x-dl-auth-error";
        public const string DataLabs_Source_Header_Prefix = "x-dl-sh-";
        public const string DataLabs_Source_StatusCode = "x-dl-src-status-code";

        /*
         * x-ms-client-request-id and x-ms-client-request-id is unqiue per each resource fetcher proxy to resource fetcher call. 
         * They will be generated in Resource Fetcher Proxy and propogates to Resource Fetcher Service
         * x-ms-client-request-id is recognized in azure service like ARM for tracking purpose.
         * x-ms-correlation-request-id is recognized in QFD
         * Just in case, we will set to both headers
         * 
         * We will generate separate Id(GUID) to track each externa call because input correlation Id is not changed during retry.
         * We are still able to correlate the input correlation Id and each external correlation Id based on trace Id
         */
        public const string ClientRequestId = "x-ms-client-request-id";
        public const string CorrelationRequestId = "x-ms-correlation-request-id";

        public const string AuthorizationDstsV2 = "Authorization-DstsV2";
        public const string Authorization = "Authorization";

        public const string ARMRemainingSubscriptionReads = "x-ms-ratelimit-remaining-subscription-reads";

        // Pacific Headers
        public const string PacificUserQuotaRemaining = "x-ms-user-quota-remaining";
        public const string PacificRequestDuration = "x-ms-resource-graph-request-duration";
        public const string PacificSnapshotTimeStamp = "x-ms-arg-snapshot-timestamp";
    }
}
