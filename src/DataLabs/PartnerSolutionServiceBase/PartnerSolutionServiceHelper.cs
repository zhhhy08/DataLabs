namespace Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase.Services;

    [ExcludeFromCodeCoverage]
    public static class PartnerSolutionServiceHelper
    {
        public static void Startup(IDataLabsInterface partnerNuget, string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Initialize LoggerFactory
            DiagnosticType.SetDefaultDiagnosticEndpoint(DiagnosticEndpoint.Partner);
            InitializeLoggers(partnerNuget);

            // 2. Initialize ConfigMapUtil so that other can use configuration
            ConfigMapUtil.Initialize(builder.Configuration);
            SolutionUtils.InitializeProgram(ConfigMapUtil.Configuration, minWorkerThreads: 1000, minCompletionThreads: 1000);

            // Add IConfigurationWithCallBack service DI
            builder.Services.AddSingleton<IConfiguration>(ConfigMapUtil.Configuration);
            builder.Services.AddSingleton<IConfigurationWithCallBack>(ConfigMapUtil.Configuration);

            // 3. Initialize all Trace and Meter Configuration
            InitializeTracesAndMeters(partnerNuget);

            // 4. Add services to the container.
            builder.Services.TryAddSingleton<IConnectionMultiplexerWrapperFactory, ConnectionMultiplexerWrapperFactory>();
            builder.Services.AddResourceProxyClientProvider();
            builder.Services.AddGrpc();
            builder.Services.AddGrpcHealthChecks()
                            .AddCheck("SimpleAliveCheck", () => HealthCheckResult.Healthy());

            builder.Services.AddSingleton<IDataLabsInterface>(partnerNuget);
            builder.Services.AddSingleton<PartnerSolutionService>();

            var app = builder.Build();

            var dataLabService = app.Services.GetService<IDataLabsInterface>();

            // TODO
            // Separate ConfigMap for Partner from ConfigMap for PartnerSolutionService
            var partnerConfiguration = ConfigMapUtil.Configuration;
            dataLabService!.SetConfiguration(partnerConfiguration);

            var partnerCacheSet = false;
            if (MonitoringConstants.IS_DEDICATED_PARTNER_AKS && 
                ConfigMapUtil.Configuration.GetValue<bool>(SolutionConstants.UseIOCacheAsPartnerCache, false))
            {
                var ioCacheClient = app.Services.GetService<ICacheClient>();
                if (ioCacheClient != null && ioCacheClient.CacheEnabled)
                {
                    dataLabService!.SetCacheClient(ioCacheClient);
                    partnerCacheSet = true;
                }
            }

            if (!partnerCacheSet)
            {
                var connectionMultiplexerWrapperFactory = app.Services.GetService<IConnectionMultiplexerWrapperFactory>();
                var partnerCacheClient = new PartnerCacheClient(partnerConfiguration, connectionMultiplexerWrapperFactory!);
                if (partnerCacheClient.CacheEnabled)
                {
                    dataLabService!.SetCacheClient(partnerCacheClient);
                }
            }

            var resourceProxyClient = app.Services.GetService<IResourceProxyClient>();
            dataLabService!.SetResourceProxyClient(resourceProxyClient!);

            // Configure the HTTP request pipeline.
            app.MapGrpcService<PartnerSolutionService>();
            app.MapGrpcHealthChecksService();
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
            app.Run();
        }

        private static void InitializeLoggers(IDataLabsInterface partnerNuget)
        {
            // Initialize DataLabLoggerFactory (CreateLogger will get called from this static class)
            var dataLabsLoggerFactory = DataLabLoggerFactory.GetLoggerFactory();

            Dictionary<string, string> partnerLoggerTableNames = partnerNuget.GetLoggerTableNames();
            var partnerLoggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddPartnerMonitoringLogger(partnerLoggerTableNames);
            });
            partnerNuget.SetLoggerFactory(partnerLoggerFactory);
        }

        private static void InitializeTracesAndMeters(IDataLabsInterface partnerNuget)
        {
            // Initialize Data Labs components
            var dataLabsTracerNames = new List<string> { PartnerSolutionService.PartnerTraceAndMetricSourceName };
            var dataLabsMeterNames = new List<string> { PartnerSolutionService.PartnerTraceAndMetricSourceName };
            Tracer.CreateDataLabsTracerProvider(dataLabsTracerNames.ToArray());
            MetricLogger.CreateDataLabsMeterProvider(dataLabsMeterNames.ToArray());

            // Partner tracer names
            var partnerTracerNames = partnerNuget.GetTraceSourceNames();
            Tracer.CreatePartnerTracerProvider(partnerTracerNames == null ? new string[] { } : partnerTracerNames.ToArray());
            
            // Partner meter names
            var partnerMeterNames = partnerNuget.GetMeterNames();
            MetricLogger.CreatePartnerMeterProvider(partnerMeterNames == null ? new string[]{ } : partnerMeterNames.ToArray());

            // Customer Meter Names
            var customerMeterNames = partnerNuget.GetCustomerMeterNames();
            if (!string.IsNullOrEmpty(MonitoringConstants.MDM_CUSTOMER_ENDPOINT_VALUE))
            {
                MetricLogger.CreateCustomerMeterProvider(customerMeterNames == null ? new string[] { } : customerMeterNames.ToArray());
            }
        }
    }
}
