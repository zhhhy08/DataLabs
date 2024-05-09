namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient
{
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;

    [ExcludeFromCodeCoverage]
    public static class ResourceProxyClientExtensions
    {
        public static IServiceCollection AddResourceProxyClientProvider(this IServiceCollection services)
        {
            if (MonitoringConstants.IS_DEDICATED_PARTNER_AKS)
            {
                // For dedicated partner AKS, let's create cache client here
                // so that Partner can utilize shortcut for better performance (avoiding partner pod -> resource fetcher proxy pod GRPC call)
                services.AddIOResourceCacheClient();
            }
            services.TryAddSingleton<ICacheTTLManager, CacheTTLManager>();
            services.TryAddSingleton<IResourceProxyAllowedTypesConfigManager, ResourceProxyAllowedTypesConfigManager>();
            services.TryAddSingleton<IResourceProxyClient, ResourceProxyClient>();
            return services;
        }
    }
}
