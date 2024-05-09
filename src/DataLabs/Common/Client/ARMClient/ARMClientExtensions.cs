namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;

    public static class ARMClientExtensions
    {
        public static void AddARMClient(this IServiceCollection services)
        {
            if (ARMClient.NeedToCreateDefaultARMClient(ConfigMapUtil.Configuration))
            {
                services.TryAddSingleton<IARMClient>(sp => ARMClient.CreateARMClientFromDataLabConfig(ConfigMapUtil.Configuration));
            }
            else
            {
                services.TryAddSingleton<IARMClient>(sp => NoOpARMClient.Instance);
            }
        }
    }
}
