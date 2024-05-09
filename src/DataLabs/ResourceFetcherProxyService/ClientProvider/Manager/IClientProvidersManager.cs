namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Manager
{
    using System.Collections.ObjectModel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArmThrottle;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient;

    internal interface IClientProvidersManager
    {
        public ReadOnlyDictionary<string, ClientProviderList<IRFProxyGetResourceClient>> GetResourceAllowedTypesMap { get; }
        public ReadOnlyDictionary<string, ClientProviderList<IARMClient>> CallARMGenericRequestAllowedTypesMap { get; }
        public ReadOnlyDictionary<string, ClientProviderList<IQFDClient>> GetCollectionAllowedTypesMap { get; }
        public ReadOnlyDictionary<string, ClientProviderList<IARMAdminClient>> GetManifestConfigAllowedTypesMap { get; }
        public ReadOnlyDictionary<string, ClientProviderList<IARMAdminClient>> GetConfigSpecsAllowedTypesMap { get; }
        public ReadOnlyDictionary<string, ClientProviderList<ICasClient>> GetCasResponseAllowedTypesMap { get; }
        public ReadOnlyDictionary<string, ClientProviderList<IQFDClient>> GetIdMappingAllowedTypesMap { get; }

        public IArmThrottleManager ArmThrottleManager { get; }
    }
}
