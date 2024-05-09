namespace SkuService.Main.Services
{
    using System.Linq;
    using System.Runtime.CompilerServices;
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
    using SkuService.Main.Pipelines;

    public class SkuSolutionService : IDataLabsInterface
    {
        private static readonly ActivityMonitorFactory GetResponsesAsyncMonitorFactory = new("SkuSolutionService.GetResponsesAsync");
        private readonly IServiceCollection serviceCollection;
        public const string PartnerActivitySourceName = "SkuPartialSyncService";
        public const string SkuLogTable = "SkuLogs";

        public SkuSolutionService()
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
            using var monitor = GetResponsesAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            SkuSolutionMetricProvider.RequestsMetricReport(PartnerActivitySourceName, request.InputResource.EventType);
            var recvTime = DateTimeOffset.UtcNow;
            var resourceValidator = ServiceRegistrations.ServiceProvider.GetService<InputResourceValidator>();
            GuardHelper.ArgumentNotNull(resourceValidator, nameof(resourceValidator));
            var inputResource = request.InputResource.Data.Resources?.First();
            if (!await resourceValidator.IsProcessingRequiredForInputResourceAsync(request, cancellationToken))
            {
                var emptySuccessResponse = new DataLabsARNV3SuccessResponse(
                                            null,
                                            DateTimeOffset.UtcNow,
                                            null);
                yield return new DataLabsARNV3Response(
                DateTimeOffset.UtcNow,
                            inputResource!.CorrelationId,
                            emptySuccessResponse,
                            null,
                            null);
                monitor.OnCompleted();
                yield break;
            }

            var pipeline = ServiceRegistrations.ServiceProvider.GetService<IDataPipeline<DataLabsARNV3Request, DataLabsARNV3Response>>();
            GuardHelper.ArgumentNotNull(pipeline, nameof(pipeline));
            IAsyncEnumerator<DataLabsARNV3Response>? enumerator = null;
            if (Constants.SubscriptionResources.Contains(inputResource!.ArmResource.Type))
            {
                enumerator = pipeline.GetResourcesForSingleSubscriptionAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
            }
            else if (request.InputResource.Id.StartsWith(Constants.Subjob, StringComparison.OrdinalIgnoreCase))
            {
                enumerator = pipeline.GetResourcesForSubjobsAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
            }
            else
            {
                enumerator = pipeline.GetSubJobsAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
            }

            DataLabsARNV3Response? errorResponse = null;
            DataLabsARNV3Response output = null!;
            while (true)
            {
                var iterationTime = DateTimeOffset.UtcNow;
                try
                {
                    if (!await enumerator.MoveNextAsync(cancellationToken))
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
