namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Constants
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;

    public static class ResourceFetcherProxyConstants
    {
        public static readonly NotFoundEntryExistInCacheException NotFoundEntryExistInCacheException = new("NotFound Entry Exists In Cache");
        public static readonly NotAllowedTypeException NotAllowedTypeException = new("Not Allowed Type");
        public static readonly ThrottleExistInCacheException ThrottleExistInCacheException = new("Throttle Exists In Cache");
        public static readonly NotValidResponseStatusCodeException NotValidResponseStatusCodeException = new("Not Valid Response StatusCode");

        public const string ActivityName_GetResource = "ResourceFetcherProxy.GetResource";
        public const string ActivityName_GetARMGenericResource = "ResourceFetcherProxy.GetARMGenericResource";
        public const string ActivityName_GetCas = "ResourceFetcherProxy.GetCas";
        public const string ActivityName_GetManifestConfig = "ResourceFetcherProxy.GetManifestConfig";
        public const string ActivityName_GetConfigSpecs = "ResourceFetcherProxy.GetConfigSpecs";
        public const string ActivityName_GetCollection = "ResourceFetcherProxy.GetCollection";
        public const string ActivityName_GetIdMappings = "ResourceFetcherProxy.GetIdMappings";
    }

}

