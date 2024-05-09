namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.OutputSourceOfTruth
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    internal static class RFProxyOutputSourceOfTruthClientExtensions
    {
        public static void AddRFProxyOutputSourceOfTruthClient(this IServiceCollection services)
        {
            services.TryAddSingleton<IRFProxyOutputSourceOfTruthClient, RFProxyOutputSourceOfTruthClient>();
        }
    }
}
