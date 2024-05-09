namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.ResourceFetcher
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Cache;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient;

    internal class ResourceFetcherArmClientProvider : IClientProvider<IARMClient>, IClientProvider<IRFProxyGetResourceClient>
    {
        public ClientProviderType ProviderType => ClientProviderType.ResourceFetcher_Arm;
        public string? ApiVersion { get; }
        public CacheClientProvider? CacheProvider { get; }

        IARMClient IClientProvider<IARMClient>.Client => _resourceFetcherClient;
        IRFProxyGetResourceClient IClientProvider<IRFProxyGetResourceClient>.Client => _rfProxyResourceFetcherArmGetResourceClient;

        private readonly IResourceFetcherClient _resourceFetcherClient;
        private readonly RFProxyResourceFetcherArmGetResourceClient _rfProxyResourceFetcherArmGetResourceClient;

        public ResourceFetcherArmClientProvider(
            IResourceFetcherClient resourceFetcherClient,
            string? apiVersion, CacheClientProvider? cacheClientProvider)
        {
            _resourceFetcherClient = resourceFetcherClient;
            _rfProxyResourceFetcherArmGetResourceClient = RFProxyResourceFetcherArmGetResourceClient.Create(_resourceFetcherClient);
            ApiVersion = apiVersion;
            CacheProvider = cacheClientProvider;
        }
    }
}
