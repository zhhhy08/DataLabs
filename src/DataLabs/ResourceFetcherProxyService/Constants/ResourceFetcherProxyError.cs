namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Constants
{
    public enum ResourceFetcherProxyError
    {
        NOT_ALLOWED_TYPE,
        THROTTLE_EXIST_IN_CACHE,
        CANCELLATION_REQUESTED,
        PROXY_FLOW_TIME_OUT,
        LAST_CLIENT_TIME_OUT,
        INTERNAL_EXCEPTION,
        CLIENT_RESPONSE_CODE,
        NOTFOUND_ENTRY_EXIST_IN_CACHE
    }
}
