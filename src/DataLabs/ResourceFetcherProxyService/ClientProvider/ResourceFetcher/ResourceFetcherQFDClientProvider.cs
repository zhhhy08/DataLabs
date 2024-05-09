namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.ResourceFetcher
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Cache;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient;

    internal class ResourceFetcherQfdClientProvider : IClientProvider<IQFDClient>, IClientProvider<IRFProxyGetResourceClient>
    {
        public ClientProviderType ProviderType => ClientProviderType.ResourceFetcher_Qfd;
        public string? ApiVersion { get; }
        public CacheClientProvider? CacheProvider { get; }

        IQFDClient IClientProvider<IQFDClient>.Client => _resourceFetcherClient;
        IRFProxyGetResourceClient IClientProvider<IRFProxyGetResourceClient>.Client => _rfProxyResourceFetcherQfdGetResourceClient;

        private readonly IResourceFetcherClient _resourceFetcherClient;
        private readonly RFProxyResourceFetcherQfdGetResourceClient _rfProxyResourceFetcherQfdGetResourceClient;

        public ResourceFetcherQfdClientProvider(
            IResourceFetcherClient resourceFetcherClient,
            string? apiVersion, 
            CacheClientProvider? cacheClientProvider)
        {
            _resourceFetcherClient = resourceFetcherClient;
            _rfProxyResourceFetcherQfdGetResourceClient = RFProxyResourceFetcherQfdGetResourceClient.Create(_resourceFetcherClient);
            ApiVersion = apiVersion;
            CacheProvider = cacheClientProvider;
        }
    }
}
