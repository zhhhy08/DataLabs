namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;

    public static class ResourceCacheClientExtensions
    {
        public static IServiceCollection AddIOResourceCacheClient(this IServiceCollection services)
        {
            services.TryAddSingleton<IConnectionMultiplexerWrapperFactory, ConnectionMultiplexerWrapperFactory>();
            services.TryAddSingleton<ICacheClient, IOCacheClient>();
            services.TryAddSingleton<ICacheTTLManager, CacheTTLManager>();
            services.TryAddSingleton<IResourceCacheClient, ResourceCacheClient>();
            return services;
        }
    }
}