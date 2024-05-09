namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.CacheClient
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;

    internal static class RFProxyCacheClientExtensions
    {
        public static void AddRFProxyCacheClient(this IServiceCollection services)
        {
            services.AddIOResourceCacheClient();
            services.TryAddSingleton<IRFProxyCacheClient, RFProxyCacheClient>();
        }
    }
}
