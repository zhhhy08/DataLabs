namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;

    public static class ResourceFetcherClientExtensions
    {
        public static void AddResourceFetcherClient(this IServiceCollection services)
        {
            if (ResourceFetcherClient.NeedToCreateDefaultResourceFetcherClient(ConfigMapUtil.Configuration))
            {
                services.TryAddSingleton<IResourceFetcherClient>(sp => ResourceFetcherClient.CreateResourceFetcherClientFromDataLabConfig(ConfigMapUtil.Configuration));
            }
            else
            {
                services.TryAddSingleton<IResourceFetcherClient>(sp => NoOpResourceFetcherClient.Instance);
            }
        }
    }
}
