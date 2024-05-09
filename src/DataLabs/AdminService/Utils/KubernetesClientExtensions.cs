using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils
{
    public static class KubernetesClientExtensions
    {
        public static void AddKubernetesClient(this IServiceCollection services)
        {
            services.TryAddSingleton<IKubernetesProvider, KubernetesProvider>();
            services.TryAddSingleton<IKubernetesObjectTransformUtils, KubernetesObjectTransformUtils>();
        }
    }
}
