namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    public static class ArnNotificationClientExtensions
    {
        [ExcludeFromCodeCoverage]
        public static IServiceCollection AddArnNotificationClientProvider(this IServiceCollection services)
        {
            services.TryAddSingleton<IArnNotificationClient, ArnNotificationClient>();
            return services;
        }
    }
}
