namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Cache;

    internal interface IClientProvider<T>
    {
        public ClientProviderType ProviderType { get; }
        public T Client { get; }
        public string? ApiVersion { get; }
        public CacheClientProvider? CacheProvider { get; }
    }
}
