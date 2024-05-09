namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth
{
    public enum ResourceFetcherAuthError
    {
        NO_PARTNER_IN_REQUEST,
        NO_TOKEN_IN_HEADER,
        AAD_TOKEN_EXPIRED,
        AAD_TOKEN_AUTH_FAIL,
        NOT_ALLOWED_PARTNER, // no allowed partner name in config
        NOT_ALLOWED_CLIENT_ID, // no allowed client Id in config
        NOT_ALLOWED_TYPE,
        BAD_REQUEST,
        INTERNAL_ERROR,
    }
}
