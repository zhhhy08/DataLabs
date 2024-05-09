namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Constants
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;

    public static class ResourceFetcherConstants
    {
        internal static readonly NotAllowedTypeException NotAllowedTypeException = new(ResourceFetcherAuthError.NOT_ALLOWED_TYPE.FastEnumToString());
        internal static readonly NotAllowedPartnerException NotAllowedPartnerException = new(ResourceFetcherAuthError.NOT_ALLOWED_PARTNER.FastEnumToString());
        internal static readonly NotAllowedClientIdException NotAllowedClientIdException = new(ResourceFetcherAuthError.NOT_ALLOWED_CLIENT_ID.FastEnumToString());
        internal static readonly ResourceFetcherBadRequestException BadRequestException = new(ResourceFetcherAuthError.BAD_REQUEST.FastEnumToString());

        public const string ResourceFetcherService = "ResourceFetcherService";

        public const string ActivityName_AADTokenAuthMiddleware = "ResourceFetcher.AADTokenAuthMiddleware";
        public const string ActivityName_GetResource = "ResourceFetcher.GetResource";
        public const string ActivityName_GetGenericRestApi = "ResourceFetcher.GetGenericRestApi";
        public const string ActivityName_GetManifestConfig = "ResourceFetcher.GetManifestConfig";
        public const string ActivityName_GetConfigSpecs = "ResourceFetcher.GetConfigSpecs";
        public const string ActivityName_GetPacificResource = "ResourceFetcher.GetPacificResource";
        public const string ActivityName_GetPacificCollection = "ResourceFetcher.GetPacificCollection";
        public const string ActivityName_GetIdMappings = "ResourceFetcher.GetIdMappings";
        public const string ActivityName_GetCasCapacityCheck = "ResourceFetcher.GetCasCapacityCheck";
    }
}
