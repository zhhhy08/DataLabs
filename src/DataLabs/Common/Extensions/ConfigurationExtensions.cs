namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;

    public static class ConfigurationExtensions
    {
        public static T? GetValueWithCallBack<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(this IConfiguration configuration,
            string key, Func<T, Task> callback, T defaultValue, bool allowMultiCallBacks = true) where T : notnull
        {
            var configurationWithCallBack = configuration as IConfigurationWithCallBack;
            if (configurationWithCallBack != null)
            {
                return configurationWithCallBack.GetValueWithCallBack<T>(key, callback, defaultValue, allowMultiCallBacks);
            }
            else
            {
                return configuration.GetValue(key, defaultValue);
            }
        }
    }
}