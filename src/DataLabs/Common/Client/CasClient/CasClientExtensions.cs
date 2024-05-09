namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;

    public static class CasClientExtensions
    {
        public static void AddCasClient(this IServiceCollection services)
        {
            if (CasClient.NeedToCreateDefaultCasClient(ConfigMapUtil.Configuration))
            {
                services.TryAddSingleton<ICasClient>(sp => CasClient.CreateCasClientFromDataLabConfig(ConfigMapUtil.Configuration));
            }
            else
            {
                services.TryAddSingleton<ICasClient>(sp => NoOpCasClient.Instance);
            }
        }
    }
}
