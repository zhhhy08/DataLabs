namespace Microsoft.Azure.ARMDataInsights.ArmDataCacheService
{
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net;
    using Microsoft.Azure.ARMDataInsights.ArmDataCacheService.Enums;
    using Microsoft.Azure.ARMDataInsights.ArmDataCacheService.Extensions;
    using Microsoft.Azure.ARMDataInsights.ArmDataCacheService.IngestionData;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using SkuService.Common.Telemetry;
    using SKuUtilities = SkuService.Common.Utilities;

    public class ArmDataCacheIngestionService : IDataLabsInterface
    {
        /* OpenTelemetry Trace */
        public const string PartnerActivitySourceName = "ArmDataCacheIngestionService";
        public static readonly ActivitySource PartnerActivitySource = new ActivitySource(PartnerActivitySourceName);
        private static readonly ActivityMonitorFactory GetResponsesAsyncMonitorFactory = new("ArmDataCacheIngestionService.GetResponsesAsync");

        /* OpenTelemetry Metric */
        public const string PartnerMeterName = "ArmDataCacheIngestionService";
        public static readonly Meter PartnerMeter = new(PartnerMeterName, "1.0");
       
        public const string LoggerTable = "ArmDataCacheIngest";

        public static ICacheClient? CacheClient;

        private readonly IServiceCollection serviceCollection;
        public ArmDataCacheIngestionService()
        {
            serviceCollection = new ServiceCollection();
        }

        public async Task<DataLabsARNV3Response> GetResponseAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            using var monitor = GetResponsesAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            SkuSolutionMetricProvider.RequestsMetricReport(PartnerActivitySourceName, request.InputResource.EventType);
            var resource = request.InputResource.Data.Resources[0];
            if (resource.ArmResource.Type.EqualsOrdinalInsensitively(SKuUtilities.Constants.GlobalSkuResourceType))
            {
                return await GlobalSkuIngestion.IngestGlobalSkuAsync(request, cancellationToken);
            }
            else if(resource.ArmResource.Type.EqualsOrdinalInsensitively(SKuUtilities.Constants.SubscriptionInternalPropertiesResourceType))
            {
                return await SubscriptionsIngestion.IngestSubscriptionIdAsync(request, cancellationToken);
            }

            var errorResponse = new DataLabsErrorResponse(DataLabsErrorType.DROP, -1, HttpStatusCode.BadRequest.ToString(), "Resource type not supported", ArmDataCacheServiceComponents.ArmDataCacheService.FastEnumToString());
            var response = new DataLabsARNV3Response(
                            DateTimeOffset.UtcNow,
                            resource.CorrelationId,
                            null,
                            errorResponse,
                            null);
            monitor.OnCompleted();
            return response;
        }

        public IAsyncEnumerable<DataLabsARNV3Response> GetResponsesAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public List<string> GetTraceSourceNames()
        {
            var list = new List<string>(1);
            list.Add(PartnerActivitySourceName);
            return list;
        }

        public List<string> GetMeterNames()
        {
            var list = new List<string>(1);
            list.Add(PartnerMeterName);
            return list;
        }

        public List<string> GetCustomerMeterNames()
        {
            var list = new List<string>(1);
            list.Add(PartnerMeterName);
            return list;
        }

        public Dictionary<string, string> GetLoggerTableNames()
        {
            return new Dictionary<string, string>
            {
                [LoggerTable] = LoggerTable
            };
        }

        public void SetCacheClient(ICacheClient? cacheClient)
        {
            GuardHelper.ArgumentNotNull(cacheClient);
            serviceCollection.AddSingleton(cacheClient);
            CacheClient = cacheClient;
        }

        public void SetConfiguration(IConfigurationWithCallBack configurationWithCallBack)
        {
            GuardHelper.ArgumentNotNull(configurationWithCallBack);
            serviceCollection.AddSingleton(configurationWithCallBack);
        }

        public void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            GuardHelper.ArgumentNotNull(loggerFactory);
            serviceCollection.AddSingleton(loggerFactory);
        }

        public void SetResourceProxyClient(IResourceProxyClient resourceProxyClient)
        {
            GuardHelper.ArgumentNotNull(resourceProxyClient);
            serviceCollection.AddSingleton(resourceProxyClient);
            ServiceRegistrations.InitializeServiceProvider(serviceCollection);
            var cacheBackgroundService = ServiceRegistrations.ServiceProvider.GetService<CacheBackgroundService>();
            GuardHelper.ArgumentNotNull(cacheBackgroundService);
            new TaskFactory().StartNew(async () => await cacheBackgroundService.RemoveDeletedSubscriptionsFromCacheAsync());
        }
    }
}
