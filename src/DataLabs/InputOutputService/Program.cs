namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService
{
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputOutputService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;

    [ExcludeFromCodeCoverage]
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // This is IOService specific setting to track duration
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var builder = WebApplication.CreateBuilder(args);

            // 1. Initialize Logger first so that other can use logger
            using var loggerFactory = DataLabLoggerFactory.GetLoggerFactory();

            // 2.  Initialize ConfigMapUtil so that other can use configuration
            ConfigMapUtil.Initialize(builder.Configuration);
            SolutionUtils.InitializeProgram(ConfigMapUtil.Configuration, minWorkerThreads: 1000, minCompletionThreads: 1000);

            // Add IConfigurationWithCallBack service DI
            builder.Services.AddSingleton<IConfiguration>(ConfigMapUtil.Configuration);
            builder.Services.AddSingleton<IConfigurationWithCallBack>(ConfigMapUtil.Configuration);

            // 3. Initialize Tracer
            Tracer.CreateDataLabsTracerProvider(IOServiceOpenTelemetry.IOServiceTraceSource);

            // 4. Initialize Meter
            MetricLogger.CreateDataLabsMeterProvider(IOServiceOpenTelemetry.IOServiceMeter);

            // Add Grpc Service for Health Check
            builder.Services.AddGrpc();
            builder.Services.AddGrpcHealthChecks()
                            .AddCheck("SimpleAliveCheck", () => HealthCheckResult.Healthy()); // TODO, check some deadlocks or other issues

            // Add Controllers
            builder.Services.AddControllers();

            var serviceCollection = builder.Services.AddHostedService<SolutionInputOutputService>();

            bool publishOutputToARN = ConfigMapUtil.Configuration.GetValue<bool>(InputOutputConstants.PublishOutputToArn, false);
            if (publishOutputToARN)
            {
                serviceCollection = serviceCollection.AddArnNotificationClientProvider();
            }

            serviceCollection = serviceCollection
                .AddIOResourceCacheClient()
                .AddPartnerBlobClient()
                .AddRawInputChannelManager()
                .AddInputChannelManager()
                .AddInputCacheChannelManager()
                .AddPartnerChannelManager()
                .AddSourceOfTruthChannelManager()
                .AddSubJobChannelManager()
                .AddOutputCacheChannelManager()
                .AddOutputChannelManager()
                .AddBlobPayloadRoutingChannelManager()
                .AddRetryChannelManager()
                .AddPoisonChannelManager();

            var app = builder.Build();

            var serviceProvider = app.Services;

            // Initialize the InputOutputService
            await SolutionInputOutputService.InitializeServiceAsync(serviceProvider).ConfigureAwait(false);

            // Configure the HTTP request pipeline.
            app.MapGrpcHealthChecksService();
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            app.MapControllers();
            app.Run();
        }
    }
}
