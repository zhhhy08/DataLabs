namespace SkuService.Common.Extensions
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Newtonsoft.Json;
    using SkuService.Common.Builders;
    using SkuService.Common.DataProviders;
    using SkuService.Common.Models.V1;
    using SkuService.Common.Utilities;
    using SkuService.Main.Pipelines;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    public static class ServiceRegistrations
    {
        private static IDictionary<string, string> CustomConfigDictionary = new Dictionary<string, string>();
        private static string customConfigString = string.Empty;

        public static IServiceProvider ServiceProvider { get; set; } = default!;

        public static IDictionary<string, string> GetCustomConfigDictionary => CustomConfigDictionary;

        public static void InitializeServiceProvider(IServiceCollection serviceCollection, string serviceName)
        {
            serviceCollection.AddSingleton<ISkuServiceProvider>(new SkuServiceProvider(serviceName));
            serviceCollection.AddSingleton<IRegistrationProvider, RegistrationProvider>();
            serviceCollection.AddSingleton<ISubscriptionProvider, SubscriptionDataProvider>();
            serviceCollection.AddSingleton<IRestrictionsProvider, RestrictionsProvider>();
            serviceCollection.AddSingleton<IDataBuilder<SubscriptionSkuModel>, SubscriptionSkuBuilder>();
            serviceCollection.AddSingleton<IDataPipeline<DataLabsARNV3Request, DataLabsARNV3Response>, DataPipeline>();
            serviceCollection.AddSingleton<IArmAdminDataProvider, ArmAdminDataProvider>();
            serviceCollection.AddSingleton<ArmAdminConfigBackgroundService>(); // AddHostedService doesnt seem to work here
            serviceCollection.AddSingleton<InputResourceValidator>();
            ServiceProvider = serviceCollection.BuildServiceProvider();
            InitializeCustomConfigDictionary().Wait();
        }

        private static async Task InitializeCustomConfigDictionary()
        {
            var value = ConfigMapUtil.Configuration.GetValueWithCallBack(Constants.CustomConfig, UpdateCustomConfigDictionary, string.Empty, true);
            await UpdateCustomConfigDictionary(value);
        }

        private static Task UpdateCustomConfigDictionary(string? newCustomConfigString)
        {
            if (string.IsNullOrEmpty(newCustomConfigString) || customConfigString.Equals(newCustomConfigString, StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            var oldCustomConfigDictionary = CustomConfigDictionary;
            var newCustomConfigDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(newCustomConfigString);

            if(newCustomConfigDictionary == null)
            {
                return Task.CompletedTask;
            } 

            if (Interlocked.CompareExchange(
                ref CustomConfigDictionary!,
                newCustomConfigDictionary!,
                oldCustomConfigDictionary) == oldCustomConfigDictionary)
            {
                customConfigString = newCustomConfigString;
            }

            return Task.CompletedTask;
        }

    }
}
