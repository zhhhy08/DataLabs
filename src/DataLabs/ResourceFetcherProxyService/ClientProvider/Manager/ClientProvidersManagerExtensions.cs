namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Manager
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;

    internal static class ClientProvidersManagerExtensions
    {
        public static void AddClientProvidersManager(this IServiceCollection services)
        {
            services.TryAddSingleton<ICacheTTLManager, CacheTTLManager>();
            services.TryAddSingleton<IResourceProxyAllowedTypesConfigManager, ResourceProxyAllowedTypesConfigManager>();
            services.TryAddSingleton<IClientProvidersManager, ClientProvidersManager>();
        }
    }
}
