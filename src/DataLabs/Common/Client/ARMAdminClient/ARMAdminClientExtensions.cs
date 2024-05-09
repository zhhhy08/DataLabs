namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;

    public static class ARMAdminClientExtensions
    {
        public static void AddARMAdminClient(this IServiceCollection services)
        {
            if (ARMAdminClient.NeedToCreateDefaultARMAdminClient(ConfigMapUtil.Configuration))
            {
                services.TryAddSingleton<IARMAdminClient>(sp => ARMAdminClient.CreateARMAdminClientFromDataLabConfig(ConfigMapUtil.Configuration));
            }
            else
            {
                services.TryAddSingleton<IARMAdminClient>(sp => NoOpARMAdminClient.Instance);
            }
        }
    }
}
