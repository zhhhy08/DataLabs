namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using System.Collections.ObjectModel;

    public interface IResourceProxyAllowedTypesConfigManager
    {
        public ICacheTTLManager CacheTTLManager { get; }
        public ReadOnlyDictionary<string, ClientProviderConfigList> GetAllowedTypesMap(ResourceProxyAllowedConfigType configType);
        public void AddUpdateListener(IResourceProxyAllowedTypesUpdateListener updateListener);
    }

    public enum ResourceProxyAllowedConfigType
    {
        // Be careful
        // The enumeration values below should follow a sequential order starting from 0, as they serve as array indices
        GetResourceAllowedTypes = 0,
        CallARMGenericRequestAllowedTypes = 1,
        GetCollectionAllowedTypes = 2,
        GetManifestConfigAllowedTypes = 3,
        GetConfigSpecsAllowedTypes = 4,
        GetCasResponseAllowedTypes = 5,
        GetIdMappingAllowedTypes = 6
    }

}