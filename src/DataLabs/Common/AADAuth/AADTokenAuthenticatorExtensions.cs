namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AADAuth
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;

    public static class AADTokenAuthenticatorExtensions
    {
        public static void AddAADTokenAuthenticator(this IServiceCollection services)
        {
            services.TryAddSingleton<IAADTokenAuthenticator>(sp => AADTokenAuthenticator.CreateAADTokenAuthenticatorFromDataLabConfig(ConfigMapUtil.Configuration));
        }
    }
}
