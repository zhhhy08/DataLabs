namespace Microsoft.Azure.ARMDataInsights.ArmDataCacheService.IngestionData
{
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net;
    using Microsoft.Azure.ARMDataInsights.ArmDataCacheService.Enums;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Newtonsoft.Json.Linq;

    internal class SubscriptionsIngestion
    {
        /* OpenTelemetry Trace */
        public const string PartnerActivitySourceName = "ArmDataSubscriptionsIngestion";
        public static readonly ActivitySource PartnerActivitySource = new ActivitySource(PartnerActivitySourceName);
        private static readonly ActivityMonitorFactory IngestSubscriptionIdAsyncMonitor = new("SubscriptionsIngestion.IngestSubscriptionIdAsync");

        /* OpenTelemetry Metric */
        public const string PartnerMeterName = "ArmDataSubscriptionsIngestion";
        public static readonly Meter PartnerMeter = new(PartnerMeterName, "1.0");
        private static readonly Histogram<int> DurationMetric = PartnerMeter.CreateHistogram<int>("Duration");
        private static readonly Counter<long> RequestCounter = PartnerMeter.CreateCounter<long>("Request");

        public const string LoggerTable = "ArmDataCacheIngest";

        private static int subscriptionBuckets = 2; //make it configurable

        public static async Task<DataLabsARNV3Response> IngestSubscriptionIdAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            var monitor = IngestSubscriptionIdAsyncMonitor.ToMonitor();
            monitor.OnStart();

            // Add Request Counter
            RequestCounter.Add(1);

            // Measure duration
            var stopWatchStartTime = Stopwatch.GetTimestamp();

            try
            {
                DataLabsErrorResponse? dataLabsErrorResponse = null;

                var resource = request.InputResource.Data.Resources[0];

                var subscriptionInternalProp = resource.ArmResource;
                var state = ((JObject)subscriptionInternalProp.Properties)[Constants.State]?.ToString();

                monitor.Activity.Properties[Constants.State] = state;

                var subscription = resource.ResourceId.GetSubscriptionId();
                var key = SkuService.Common.Utilities.Constants.SubscriptionsCacheKeyPrefix + Math.Abs(subscription.GetHashCode() % subscriptionBuckets);

                var existingScore = await ArmDataCacheIngestionService.CacheClient!.SortedSetScoreAsync(key, subscription, cancellationToken);

                if (state.EqualsOrdinalInsensitively(Constants.Deleted))
                {
                    monitor.Activity.Properties[Constants.Action] = Constants.Delete;
                    if (existingScore != null)
                    {
                        var success = await ArmDataCacheIngestionService.CacheClient.SortedSetRemoveAsync(key, subscription, cancellationToken);
                        if (!success)
                        {
                            dataLabsErrorResponse = new DataLabsErrorResponse(DataLabsErrorType.RETRY, request.RetryCount * 1000, HttpStatusCode.InternalServerError.ToString(), "Subscription Removal Failed", ArmDataCacheServiceComponents.SubscriptionIngestion.FastEnumToString());
                        }
                    }
                }
                else
                {
                    monitor.Activity.Properties[Constants.Action] = Constants.Ingest;

                    TimeSpan timeSpan = DateTime.UtcNow - Constants.offSetStartTime;
                    double timeStampScore = timeSpan.TotalMinutes;

                    if (existingScore == null)
                    {
                        var success = await ArmDataCacheIngestionService.CacheClient.SortedSetAddAsync(key, subscription, timeStampScore, cancellationToken);
                        if (!success)
                        {
                            dataLabsErrorResponse = new DataLabsErrorResponse(DataLabsErrorType.RETRY, request.RetryCount * 1000, HttpStatusCode.InternalServerError.ToString(), "Subscription Ingestion Failed", ArmDataCacheServiceComponents.SubscriptionIngestion.FastEnumToString());
                        }
                    }
                }
                var duration = Stopwatch.GetElapsedTime(stopWatchStartTime).TotalMilliseconds;

                // Report Duration Metric
                DurationMetric.Record((int)duration);
                monitor.OnCompleted();
                if (dataLabsErrorResponse != null)
                {
                    return new DataLabsARNV3Response(
                                DateTimeOffset.UtcNow,
                                resource.CorrelationId,
                                null,
                                dataLabsErrorResponse,
                                null);
                }
                var successResponse = new DataLabsARNV3SuccessResponse(
                                            null,
                                            DateTimeOffset.UtcNow,
                                            null);

                return new DataLabsARNV3Response(
                            DateTimeOffset.UtcNow,
                            resource.CorrelationId,
                            successResponse,
                            null,
                            null);
            }
            catch (Exception e)
            {
                monitor.OnError(e);
                throw;
            }
        }
    }
}
