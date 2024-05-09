namespace SkuService.FullSync.Services
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using SkuService.Common;
    using SkuService.Common.Extensions;
    using SkuService.Common.Telemetry;
    using SkuService.Common.Utilities;
    using SkuService.Main;
    using SkuService.Main.Pipelines;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    public class SkuFullSyncService : IDataLabsInterface
    {
        private readonly IServiceCollection serviceCollection;
        private static readonly ActivityMonitorFactory GetResponsesAsyncMonitorFactory = new("SkuFullSyncService.GetResponsesAsync");
        public const string SkuLogTable = "SkuLogs";
        public const string PartnerActivitySourceName = "SkuFullSyncService";

        /// <summary>
        /// 
        /// </summary>
        public SkuFullSyncService()
        {
            serviceCollection = new ServiceCollection();
        }
        public List<string> GetMeterNames()
        {
            return new List<string> { SkuSolutionMetricProvider.SkuServiceMeter };
        }

        public Task<DataLabsARNV3Response> GetResponseAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async IAsyncEnumerable<DataLabsARNV3Response> GetResponsesAsync(DataLabsARNV3Request request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var recvTime = DateTimeOffset.UtcNow;
            using var monitor = GetResponsesAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            SkuSolutionMetricProvider.RequestsMetricReport(PartnerActivitySourceName, request.InputResource.EventType);
            var pipeline = ServiceRegistrations.ServiceProvider.GetService<IDataPipeline<DataLabsARNV3Request, DataLabsARNV3Response>>();
            GuardHelper.ArgumentNotNull(pipeline, nameof(pipeline));
            var inputResource = request.InputResource.Data.Resources?.First();
            IAsyncEnumerator<DataLabsARNV3Response>? enumerator = null;

            if (request.InputResource.Id.StartsWith(Constants.Subjob, StringComparison.OrdinalIgnoreCase))
            {
                enumerator = pipeline.GetResourcesForSubjobsAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
            }
            else
            {
                enumerator = pipeline.GetSubJobsForFullSyncAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
            }

            DataLabsARNV3Response? errorResponse = null;
            DataLabsARNV3Response output = null!;
            while (true)
            {
                var iterationTime = DateTimeOffset.UtcNow;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }

                    output = enumerator.Current;
                    errorResponse = null;
                }
                catch (Exception e)
                {
                    monitor.OnError(e);
                    errorResponse = NotificationUtils.BuildDataLabsErrorResponse(request, e);
                    monitor.OnStart();
                }

                if (errorResponse != null)
                {
                    SkuSolutionMetricProvider.FailedResponseMetricReport(PartnerActivitySourceName);
                    yield return errorResponse;
                }
                else
                {
                    SkuSolutionMetricProvider.SuccessResponseMetricReport(PartnerActivitySourceName);
                    SkuSolutionMetricProvider.SuccessfulResponseRequestDurationMetric.Record((long)(DateTimeOffset.UtcNow - iterationTime).TotalMilliseconds,
                        new KeyValuePair<string, object?>(SkuSolutionMetricProvider.ServiceNameDimension, PartnerActivitySourceName));
                    yield return output;
                }
            }

            SkuSolutionMetricProvider.CompleteResponseRequestDurationMetric.Record((long)(DateTimeOffset.UtcNow - recvTime).TotalMilliseconds,
                        new KeyValuePair<string, object?>(SkuSolutionMetricProvider.ServiceNameDimension, PartnerActivitySourceName));
            monitor.OnCompleted();
        }

        public List<string> GetTraceSourceNames()
        {
            return new List<string> { PartnerActivitySourceName };
        }

        public void SetConfiguration(IConfigurationWithCallBack configurationWithCallBack)
        {
            serviceCollection.AddSingleton(configurationWithCallBack);
        }

        public void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            serviceCollection.AddSingleton(loggerFactory);
        }

        public void SetResourceProxyClient(IResourceProxyClient resourceProxyClient)
        {
            serviceCollection.AddSingleton(resourceProxyClient);
            ServiceRegistrations.InitializeServiceProvider(serviceCollection, PartnerActivitySourceName);
            var adminService = ServiceRegistrations.ServiceProvider.GetService<ArmAdminConfigBackgroundService>();
            GuardHelper.ArgumentNotNull(adminService, nameof(adminService));
            new TaskFactory().StartNew(async () => await adminService.ExecuteAsync());
        }

        public Dictionary<string, string> GetLoggerTableNames()
        {
            return new Dictionary<string, string>
            {
                { "SkuLogTable", SkuLogTable }
            };
        }

        public List<string> GetCustomerMeterNames()
        {
            return new List<string> { SkuSolutionMetricProvider.SkuServiceMeter };
        }

        public void SetCacheClient(ICacheClient? cacheClient)
        {
            GuardHelper.ArgumentNotNull(cacheClient, nameof(cacheClient));
            serviceCollection.AddSingleton(cacheClient);
        }
    }
}
