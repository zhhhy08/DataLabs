namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;

    public static class QFDClientExtensions
    {
        public static void AddQFDClient(this IServiceCollection services)
        {
            if (QFDClient.NeedToCreateDefaultQFDClient(ConfigMapUtil.Configuration))
            {
                services.TryAddSingleton<IQFDClient>(sp => QFDClient.CreateQFDClientFromDataLabConfig(ConfigMapUtil.Configuration));
            }
            else
            {
                services.TryAddSingleton<IQFDClient>(sp => NoOpQFDClient.Instance);
            }
        }
    }
}
