namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService
{
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AADAuth;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SecretProviderManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Middlewares;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Monitoring;

    public class Program
    {
        public static void Main(string[] args)
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
            Tracer.CreateDataLabsTracerProvider(ResourceFetcherMetricProvider.ResourceFetcherTraceSource);

            // 4. Initialize Meter
            MetricLogger.CreateDataLabsMeterProvider(ResourceFetcherMetricProvider.ResourceFetcherServiceMeter);

            // 5. Initialize SecretProviderManager
            var secretProviderManager = SecretProviderManager.Instance;

            // Add ARMClient
            builder.Services.AddARMClient();

            // Add ARMAdminClient
            builder.Services.AddARMAdminClient();

            // Add QFDClient (query front door client)
            builder.Services.AddQFDClient();

            // Add CasClient
            builder.Services.AddCasClient();

            if (MonitoringConstants.IsLocalDevelopment)
            {
                // Local Development
                builder.Services.AddSingleton<IAADTokenAuthenticator, NoOpAADTokenAuthenticator>();
            }
            else
            {
                // Add AADTokenAuthenticator
                builder.Services.AddAADTokenAuthenticator();
            }
                
            // Add PartnerAuthorizeManager
            builder.Services.AddPartnerAuthorizeManager();

            // Add services to the container.
            builder.Services.AddControllers();

            var app = builder.Build();

            // Let's try to get Clients and other services from DI container so that we can validate if it is properly initialized during startup
            // Otherwise, it will throw exception so that we will know it is not initialized properly during startup
            var armClient = app.Services.GetService<IARMClient>();
            var armAdminClient = app.Services.GetService<IARMAdminClient>();
            var qfdClient = app.Services.GetService<IQFDClient>();
            var casClient = app.Services.GetService<ICasClient>();
            var aadTokenAuthenticator = app.Services.GetService<IAADTokenAuthenticator>();
            var partnerAuthorizeManager = app.Services.GetService<IPartnerAuthorizeManager>();

            // TODO, enable https with certificate
            //app.UseHsts();
            //app.UseHttpsRedirection();

            // For AAD Authentication/Authorization
            app.UseAADTokenAuthMiddleware();
            
            app.MapControllers();
            app.Run();
        }
    }
}