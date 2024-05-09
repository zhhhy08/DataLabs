namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.ResourceFetcher
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Cache;

    internal class ResourceFetcherCasClientProvider : ClientProvider<ICasClient>
    {
        public ResourceFetcherCasClientProvider(
            IResourceFetcherClient resourceFetcherClient, 
            string? apiVersion, 
            CacheClientProvider? cacheClientProvider) : base(
                ClientProviderType.ResourceFetcher_Cas,
                resourceFetcherClient, 
                apiVersion, 
                cacheClientProvider)
        {
        }
    }
}
