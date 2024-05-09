namespace Microsoft.Azure.ARMDataInsights.ArmDataCacheService.Extensions
{
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.ARMDataInsights.ArmDataCacheService.IngestionData;
    using Microsoft.Extensions.DependencyInjection;

    [ExcludeFromCodeCoverage]
    public static class ServiceRegistrations
    {

        public static IServiceProvider ServiceProvider { get; set; } = default!;

        public static void InitializeServiceProvider(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<CacheBackgroundService>();
            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

    }
}
