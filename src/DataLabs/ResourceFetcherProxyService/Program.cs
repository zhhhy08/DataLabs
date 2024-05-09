namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SecretProviderManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Manager;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.OutputSourceOfTruth;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Monitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Services;
    
    public class Program
    {
        public static async Task Main(string[] args)
        {
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
            Tracer.CreateDataLabsTracerProvider(ResourceFetcherProxyMetricProvider.ResourceFetcherProxyTraceSource);

            // 4. Initialize Meter
            MetricLogger.CreateDataLabsMeterProvider(ResourceFetcherProxyMetricProvider.ResourceFetcherProxyServiceMeter);

            // 5. Initialize SecretProviderManager
            var secretProviderManager = SecretProviderManager.Instance;

            int initializeServiceTimeout = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.InitializeServiceTimeoutInSec, 5 * 60); // 5 min
            int randomDelayInSec = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.InitializeServiceRandomDelayInSec, 2); // 2 sec

            // We don't expect neighbor's noise at this time because we do one after one rolling update.
            // However just in case (like uninstall/install) or something else, 
            // To reduce possible neighbor's noise during deployment just in case, Let's put some random Delay here
            Random random = new(Guid.NewGuid().GetHashCode());
            int sleepMSec = random.Next(randomDelayInSec * 1000);
            if (sleepMSec > 0)
            {
                await Task.Delay(sleepMSec).ConfigureAwait(false);
            }

            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.CancelAfter(TimeSpan.FromSeconds(initializeServiceTimeout));
            var cancellationToken = cancellationSource.Token;

            // Initialize RegionConfigManager
            RegionConfigManager.Initialize(ConfigMapUtil.Configuration, cancellationToken);

            // Add Controllers
            builder.Services.AddControllers();

            // Add ARMClient
            builder.Services.AddARMClient();

            // Add ARMAdminClient
            builder.Services.AddARMAdminClient();

            // Add QFDClient (query front door client)
            builder.Services.AddQFDClient();

            // Add CasClient
            builder.Services.AddCasClient();

            // Add ResourceFetcherClient
            builder.Services.AddResourceFetcherClient();

            // Add RFProxyCacheClient
            builder.Services.AddRFProxyCacheClient();

            // Add RFProxyOutputSourceOfTruthClient
            builder.Services.AddRFProxyOutputSourceOfTruthClient();

            // Add ClientProvidersManager
            builder.Services.AddClientProvidersManager();

            // Add Grpc Service
            builder.Services.AddGrpc();
            builder.Services.AddGrpcHealthChecks()
                            .AddCheck("SimpleAliveCheck", () => HealthCheckResult.Healthy());

            // Set ProxyService as SingleTon so that each grpc call will not create instance every time
            builder.Services.AddSingleton<ProxyService>();

            var app = builder.Build();

            // Let's try to get Clients/ProxyService from DI container so that we can validate if it is properly initialized during startup
            // Otherwise, it will throw exception so that we will know it is not initialized properly during startup
            var armClient = app.Services.GetService<IARMClient>();
            var armAdminClient = app.Services.GetService<IARMAdminClient>();
            var qfdClient = app.Services.GetService<IQFDClient>();
            var casClient = app.Services.GetService<ICasClient>();
            var resourceFetcherClient = app.Services.GetService<IResourceFetcherClient>();
            var rfProxyCacheClient = app.Services.GetService<IRFProxyCacheClient>();
            var rfProxyOutputSourceOfTruthClient = app.Services.GetService<IRFProxyOutputSourceOfTruthClient>();
            var clientProvidersManager = app.Services.GetService<IClientProvidersManager>();
            var proxyService = app.Services.GetService<ProxyService>();

            // Configure the HTTP request pipeline.
            app.MapGrpcService<ProxyService>();
            app.MapGrpcHealthChecksService();
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            app.MapControllers();
            app.Run();
        }
    }
}