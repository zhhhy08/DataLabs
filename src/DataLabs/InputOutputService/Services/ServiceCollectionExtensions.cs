namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputOutputService.Services
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.BlobPayloadRoutingChannelManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.FinalChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.InputChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.InputResourceCacheChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputCacheChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PoisonChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RawInputChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RetryChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.SourceOfTruthChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.SubJobChannel;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSourceOfTruthChannelManager(this IServiceCollection services)
        {
            services.AddRetryChannelManager();
            services.TryAddSingleton<ISourceOfTruthChannelManager<ARNSingleInputMessage>, SourceOfTruthChannelManager<ARNSingleInputMessage>>();
            return services;
        }

        public static IServiceCollection AddSubJobChannelManager(this IServiceCollection services)
        {
            services.AddRetryChannelManager();
            services.TryAddSingleton<ISubJobChannelManager<ARNSingleInputMessage>, SubJobChannelManager<ARNSingleInputMessage>>();
            return services;
        }

        public static IServiceCollection AddOutputChannelManager(this IServiceCollection services)
        {
            services.AddRetryChannelManager();
            services.TryAddSingleton<IOutputChannelManager<ARNSingleInputMessage>, OutputChannelManager<ARNSingleInputMessage>>();
            return services;
        }

        public static IServiceCollection AddBlobPayloadRoutingChannelManager(this IServiceCollection services)
        {
            services.AddRetryChannelManager();
            services.TryAddSingleton<IBlobPayloadRoutingChannelManager<ARNSingleInputMessage>, BlobPayloadRoutingChannelManager>();
            return services;
        }

        public static IServiceCollection AddOutputCacheChannelManager(this IServiceCollection services)
        {
            services.AddRetryChannelManager();
            services.TryAddSingleton<IOutputCacheChannelManager, OutputCacheChannelManager>();
            return services;
        }

        public static IServiceCollection AddRawInputChannelManager(this IServiceCollection services)
        {
            services.AddInputChannelManager();
            services.TryAddSingleton<IRawInputChannelManager<ARNRawInputMessage>, RawInputChannelManager<ARNRawInputMessage>>();
            return services;
        }

        public static IServiceCollection AddInputChannelManager(this IServiceCollection services)
        {
            services.AddPartnerChannelManager();
            services.AddInputCacheChannelManager();
            services.AddRetryChannelManager();
            services.TryAddSingleton<IInputChannelManager<ARNSingleInputMessage>, InputChannelManager<ARNSingleInputMessage>>();
            return services;
        }

        public static IServiceCollection AddInputCacheChannelManager(this IServiceCollection services)
        {
            services.AddRetryChannelManager();
            services.TryAddSingleton<IInputCacheChannelManager<ARNSingleInputMessage>, InputCacheChannelManager<ARNSingleInputMessage>>();
            return services;
        }

        public static IServiceCollection AddPartnerChannelManager(this IServiceCollection services)
        {
            services.AddSourceOfTruthChannelManager();
            services.AddOutputCacheChannelManager();
            services.AddOutputChannelManager();
            services.AddRetryChannelManager();
            services.TryAddSingleton<IPartnerChannelRoutingManager, PartnerChannelRoutingManager>();
            return services;
        }

        public static IServiceCollection AddRetryChannelManager(this IServiceCollection services)
        {
            services.TryAddSingleton<IRetryChannelManager<ARNSingleInputMessage>, RetryChannelManager<ARNSingleInputMessage>>();
            services.TryAddSingleton<IRetryChannelManager<ARNRawInputMessage>, RetryChannelManager<ARNRawInputMessage>>();
            services.AddPoisonChannelManager();
            return services;
        }

        public static IServiceCollection AddPoisonChannelManager(this IServiceCollection services)
        {
            services.TryAddSingleton<IPoisonChannelManager<ARNSingleInputMessage>, PoisonChannelManager<ARNSingleInputMessage>>();
            services.TryAddSingleton<IPoisonChannelManager<ARNRawInputMessage>, PoisonChannelManager<ARNRawInputMessage>>();

            services.TryAddSingleton<IFinalChannelManager<ARNSingleInputMessage>, FinalChannelManager<ARNSingleInputMessage>>();
            services.TryAddSingleton<IFinalChannelManager<ARNRawInputMessage>, FinalChannelManager<ARNRawInputMessage>>();

            return services;
        }
    }
}
