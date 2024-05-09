namespace SkuService.Common.Telemetry
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using System.Runtime.CompilerServices;

    [ExcludeFromCodeCoverage]
    public class SkuSolutionMetricProvider
    {
        public const string ServiceNameDimension = "ServiceName";
        public const string ResourceIdDimension = "ResourceId";
        public const string SkuServiceMeter = "SkuSolutionService";
        public const string CacheKeyDimension = "CacheKey";
        public const string DatasetDimension = "Dataset";
        public const string RequestTypeDimension = "RequestType";

        internal static readonly Meter SkuSolutionMeter = new(SkuServiceMeter, "1.0");

        internal static readonly Counter<long> SuccessResponseMetric = SkuSolutionMeter.CreateCounter<long>("SuccessResponse", description: "Count of successful responses outputted from Sku solution");
        internal static readonly Counter<long> RequestsMetric = SkuSolutionMeter.CreateCounter<long>("Requests", description: "Count of requests to Sku solution");
        internal static readonly Counter<long> FailedResponseMetric = SkuSolutionMeter.CreateCounter<long>("FailedResponse", description: "Count of failed responses outputted from Sku solution");
        public static readonly Histogram<long> SuccessfulResponseRequestDurationMetric = SkuSolutionMeter.CreateHistogram<long>("SuccessfulResponseRequestDuration", "ms", description: "Time from Sku solution receiving request to returning a successful response via stream.");
        public static readonly Histogram<long> CompleteResponseRequestDurationMetric = SkuSolutionMeter.CreateHistogram<long>("CompleteResponseRequestDuration", "ms", description: "Time from Sku solution receiving request to returning all responses via stream.");
        internal static readonly Counter<long> PartnerCacheSuccessResponseMetric = SkuSolutionMeter.CreateCounter<long>("PartnerCacheSuccessResponse", description: "Count of successful responses from partner cache");
        internal static readonly Counter<long> PartnerCacheMissedResponseMetric = SkuSolutionMeter.CreateCounter<long>("PartnerCacheMissedResponse", description: "Count of cache miss responses from partner cache");
        internal static readonly Counter<long> ResourceProxyResourceMissedMetric = SkuSolutionMeter.CreateCounter<long>("ResourceProxyResourceMissed", description: "Count of resource miss responses from resource proxy");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PartnerCacheMissedResponseMetricReport(string serviceName, string key)
        {
            PartnerCacheMissedResponseMetric.Add(1,
                new KeyValuePair<string, object?>(ServiceNameDimension, serviceName),
                new KeyValuePair<string, object?>(CacheKeyDimension, key));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SuccessResponseMetricReport(string serviceName)
        {
            SuccessResponseMetric.Add(1,
                               new KeyValuePair<string, object?>(ServiceNameDimension, serviceName));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FailedResponseMetricReport(string serviceName)
        {
            FailedResponseMetric.Add(1,
                               new KeyValuePair<string, object?>(ServiceNameDimension, serviceName));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResourceProxyResourceMissedMetricReport(string serviceName, string dataset, string resourceId)
        {
            ResourceProxyResourceMissedMetric.Add(1,
                new KeyValuePair<string, object?>(ServiceNameDimension, serviceName),
                new KeyValuePair<string, object?>(DatasetDimension, dataset),
                new KeyValuePair<string, object?>(ResourceIdDimension, resourceId));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RequestsMetricReport(string serviceName, string requestType)
        {
            RequestsMetric.Add(1,
                               new KeyValuePair<string, object?>(ServiceNameDimension, serviceName),
                               new KeyValuePair<string, object?>(RequestTypeDimension, requestType));
        }
    }
}
