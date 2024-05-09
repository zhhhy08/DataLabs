namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    public static class PartnerAuthorizeManagerExtensions
    {
        public static void AddPartnerAuthorizeManager(this IServiceCollection services)
        {
            services.TryAddSingleton<IPartnerAuthorizeManager, PartnerAuthorizeManager>();
        }
    }
}
