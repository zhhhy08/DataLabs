namespace Microsoft.Azure.ARMDataInsights.ArmDataCacheService.IngestionData
{
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.ARMDataInsights.ArmDataCacheService.Enums;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Newtonsoft.Json;
    using SKuUtilities = SkuService.Common.Utilities;

    internal class GlobalSkuIngestion
    {
        /* OpenTelemetry Trace */
        public const string PartnerActivitySourceName = "ArmDataGlobalSkuIngestion";
        public static readonly ActivitySource PartnerActivitySource = new ActivitySource(PartnerActivitySourceName);
        private static readonly ActivityMonitorFactory IngestGlobalSkuAsyncMonitorFactory = new("GlobalSkuIngestion.IngestGlobalSkuAsync");
        /* OpenTelemetry Metric */
        public const string PartnerMeterName = "ArmDataGlobalSkuIngestion";
        public static readonly Meter PartnerMeter = new(PartnerMeterName, "1.0");
        private static readonly Histogram<int> DurationMetric = PartnerMeter.CreateHistogram<int>("Duration");
        private static readonly Counter<long> RequestCounter = PartnerMeter.CreateCounter<long>("Request");

        public static async Task<DataLabsARNV3Response> IngestGlobalSkuAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            using var monitor = IngestGlobalSkuAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            // Add Request Counter
            RequestCounter.Add(1);
            DataLabsErrorResponse? dataLabsErrorResponse = null;

            // Measure duration
            var stopWatchStartTime = Stopwatch.GetTimestamp();
            try
            {
                TimeSpan timeSpan = DateTime.UtcNow - Constants.offSetStartTime;
                double timeStampScore = timeSpan.TotalMinutes;
                var resource = request.InputResource.Data.Resources[0];
                var resourceId = resource.ResourceId;
                var rp = resourceId.GetResourceNamespace(true);
                GuardHelper.ArgumentNotNull(rp);
                bool success;

                var skuData = JsonConvert.SerializeObject(resource.ArmResource);
                var existingData = await ArmDataCacheIngestionService.CacheClient!.GetValueAsync(resourceId, cancellationToken);

                // remove eventTimePrefix and compare
                if (existingData == null || !skuData.Equals(Encoding.UTF8.GetString(existingData, SKuUtilities.Constants.EventTimeBytes, existingData.Length - SKuUtilities.Constants.EventTimeBytes)))
                {
                    if (existingData == null)
                    {
                        monitor.Activity.Properties["ExistingDataNull"] = "true";
                    }
                    else
                    {
                        monitor.Activity.Properties["ExistingData"] = Encoding.UTF8.GetString(existingData, SKuUtilities.Constants.EventTimeBytes, existingData.Length - SKuUtilities.Constants.EventTimeBytes);
                        monitor.Activity.Properties["NewData"] = skuData;
                    }
                    DateTimeOffset? eventTime = resource.ResourceEventTime != default ? resource.ResourceEventTime : DateTimeOffset.UtcNow;
                    // save value with eventTime prefix
                    success = await ArmDataCacheIngestionService.CacheClient.SetValueIfGreaterThanWithExpiryAsync(resourceId, new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(skuData)), eventTime!.Value.ToUnixTimeMilliseconds(), TimeSpan.FromHours(60), cancellationToken);
                    monitor.Activity.Properties["SetValueIfGreaterThanWithExpiryAsync"] = success;
                }
                else
                {
                    success = await ArmDataCacheIngestionService.CacheClient.SetKeyExpireAsync(resourceId, TimeSpan.FromHours(60), cancellationToken);
                    monitor.Activity.Properties["SetKeyExpireAsync"] = success;
                }

                if (success)
                {
                    // Cache the RP. Consider checking its presence before adding?
                    var rpAddResult = await ArmDataCacheIngestionService.CacheClient.SortedSetAddAsync(SolutionConstants.ResourceProvidersKey, rp, timeStampScore, cancellationToken);
                    monitor.Activity.Properties["RPSortedSetAddAsync"] = rpAddResult;
                    var existingScore = await ArmDataCacheIngestionService.CacheClient.SortedSetScoreAsync(rp, resourceId, cancellationToken);
                    monitor.Activity.Properties["ExistingScore"] = existingScore;
                    if (existingScore == null)
                    {
                        success = await ArmDataCacheIngestionService.CacheClient.SortedSetAddAsync(rp, resourceId, timeStampScore, cancellationToken);
                        monitor.Activity.Properties["SortedSetAddAsync"] = success;
                    }


                    if (!success)
                    {
                        dataLabsErrorResponse = GetDataLabsErrorResponse(request);
                    }
                }
                else
                {
                    dataLabsErrorResponse = GetDataLabsErrorResponse(request);
                }

                monitor.Activity.Properties[Constants.Success] = success;
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

        private static DataLabsErrorResponse GetDataLabsErrorResponse(DataLabsARNV3Request request)
        {
            return new DataLabsErrorResponse(DataLabsErrorType.RETRY, request.RetryCount * 1000, HttpStatusCode.InternalServerError.ToString(), "GlobalSku Ingestion Failed", ArmDataCacheServiceComponents.GlobalSKUIngestion.FastEnumToString());
        }
    }
}
