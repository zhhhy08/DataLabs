namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.ResourceFetcher
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Cache;

    internal class ResourceFetcherArmAdminClientProvider : ClientProvider<IARMAdminClient>
    {
        public ResourceFetcherArmAdminClientProvider(
            IResourceFetcherClient resourceFetcherClient, 
            string? apiVersion, 
            CacheClientProvider? cacheClientProvider) : base(
                ClientProviderType.ResourceFetcher_ArmAdmin,
                resourceFetcherClient, 
                apiVersion, 
                cacheClientProvider)
        {
        }
    }
}
