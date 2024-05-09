namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Cache;

    internal class ClientProvider<T> : IClientProvider<T>
    {
        public ClientProviderType ProviderType { get; }
        public T Client { get; }
        public string? ApiVersion { get; }
        public CacheClientProvider? CacheProvider { get; }

        public ClientProvider(
            ClientProviderType providerType,
            T client, 
            string? apiVersion, 
            CacheClientProvider? cacheClientProvider)
        {
            ProviderType = providerType;
            Client = client;
            ApiVersion = apiVersion;
            CacheProvider = cacheClientProvider;
        }
    }
}
