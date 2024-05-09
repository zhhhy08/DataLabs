namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using StackExchange.Redis;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Runtime.CompilerServices;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    
    public class CacheClientMetricProvider
    {
        // Metric Names
        public const string CacheClientReadCallMetricName = "CacheClientReadCall";
        public const string CacheClientWriteCallMetricName = "CacheClientWriteCall";
        public const string CacheClientReadMultiResultCallMetricName = "CacheClientReadMultiResultCall";

        public const string CacheClientSendKeyExpireMetricName = "CacheClientSendKeyExpire";
        public const string CacheClientSetValueWithExpiryMetricName = "CacheClientSetValueWithExpiry";
        public const string CacheClientSetValueIfMatchWithExpiryMetricName = "CacheClientSetValueIfMatchWithExpiry";
        public const string CacheClientSetValueIfGreaterThanWithExpiryMetricName = "CacheClientSetValueIfGreaterThanWithExpiry";
        public const string CacheClientGetValueMetricName = "CacheClientGetValue";
        
        public const string CacheClientMGetValuesMetricName = "CacheClientMGetValues";
        public const string CacheClientMGetValuesNumKeysMetricName = "CacheClientMGetValuesNumKeys";
        public const string CacheClientMGetValuesFoundCounterName = "CacheClientMGetValuesFoundCounter";

        public const string CacheClientSortedSetAddMetricName = "CacheClientSortedSetAdd";
        public const string CacheClientSortedSetRemoveMetricName = "CacheClientSortedSetRemove";
        public const string CacheClientSortedSetRangeByRankMetricName = "CacheClientSortedSetRangeByRank";
        public const string CacheClientSortedSetScoreMetricName = "CacheClientSortedSetScore";
        public const string CacheClientSortedSetRemoveRangeByScoreMetricName = "CacheClientSortedSetRemoveRangeByScore";
        public const string CacheClientSortedSetLengthMetricName = "CacheClientSortedSetLength";

        public const string CacheClientConnectionCreationCounterName = "CacheClientConnectionCreationCounter";
        public const string CacheClientConnectionFailedEventCounterName = "CacheClientConnectionFailedEventCounter";
        public const string CacheClientConnectionRestoredEventCounterName = "CacheClientConnectionRestoredEventCounter";

        public const string CacheNameDimension = "CacheName";
        public const string CacheNodeDimension = "CacheNode";
        public const string CacheFoundDimension = "CacheFound";
        public const string CacheKeyExpireSetDimension = "KeyExpireSet";
        public const string FailureTypeDimension = "FailureType";
        public const string CacheResultDimension = "CacheResult";

        public const string MethodDimension = "Method";
        public const string LastTriedCacheNodeDimension = "LastTriedCacheNode";
        public const string TriedNodeCountDimension = "TriedNodeCount";
        public const string NumSuccessCountDimension = "NumSuccessCount";
        public const string NumFailedCountDimension = "NumFailedCount";

        public static readonly KeyValuePair<string, object?> CacheFoundPair = new(CacheFoundDimension, true);
        public static readonly KeyValuePair<string, object?> CacheNotFoundPair = new(CacheFoundDimension, false);
        public static readonly KeyValuePair<string, object?> KeyExpireSetSuccessPair = new(CacheKeyExpireSetDimension, true);
        public static readonly KeyValuePair<string, object?> KeyExpireSetFailPair = new(CacheKeyExpireSetDimension, false);
        public static readonly KeyValuePair<string, object?> KeyExpireSetNullPair = new(CacheKeyExpireSetDimension, null);

        public static readonly Histogram<int> CacheClientSendKeyExpireMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientSendKeyExpireMetricName);
        public static readonly Histogram<int> CacheClientSetValueWithExpiryMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientSetValueWithExpiryMetricName);
        public static readonly Histogram<int> CacheClientSetValueIfMatchWithExpiryMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientSetValueIfMatchWithExpiryMetricName);
        public static readonly Histogram<int> CacheClientSetValueIfGreaterThanWithExpiryMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientSetValueIfGreaterThanWithExpiryMetricName);
        public static readonly Histogram<int> CacheClientGetValueMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientGetValueMetricName);
        public static readonly Histogram<int> CacheClientSortedSetAddMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientSortedSetAddMetricName);
        public static readonly Histogram<int> CacheClientSortedSetRemoveMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientSortedSetRemoveMetricName);
        public static readonly Histogram<int> CacheClientSortedSetRangeByRankMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientSortedSetRangeByRankMetricName);
        public static readonly Histogram<int> CacheClientSortedSetScoreMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientSortedSetScoreMetricName);
        public static readonly Histogram<int> CacheClientSortedSetRemoveRangeByScoreMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientSortedSetRemoveRangeByScoreMetricName);
        public static readonly Histogram<int> CacheClientSortedSetLengthMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientSortedSetLengthMetricName);

        public static readonly Histogram<int> CacheClientMGetValuesMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientMGetValuesMetricName);
        public static readonly Histogram<int> CacheClientMGetValuesNumKeysMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientMGetValuesNumKeysMetricName);
        public static readonly Counter<long> CacheClientMGetValuesFoundCounter = MetricLogger.CommonMeter.CreateCounter<long>(CacheClientMGetValuesFoundCounterName);

        public static readonly Histogram<int> CacheClientWriteCallMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientWriteCallMetricName);
        public static readonly Histogram<int> CacheClientReadCallMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientReadCallMetricName);
        public static readonly Histogram<int> CacheClientReadMultiResultCallMetric = MetricLogger.CommonMeter.CreateHistogram<int>(CacheClientReadMultiResultCallMetricName);

        private static readonly Counter<long> CacheClientConnectionCreationCounter = MetricLogger.CommonMeter.CreateCounter<long>(CacheClientConnectionCreationCounterName);
        private static readonly Counter<long> CacheClientConnectionFailedEventCounter = MetricLogger.CommonMeter.CreateCounter<long>(CacheClientConnectionFailedEventCounterName);
        private static readonly Counter<long> CacheClientConnectionRestoredEventCounter = MetricLogger.CommonMeter.CreateCounter<long>(CacheClientConnectionRestoredEventCounterName);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddConnectionCreationErrorCounter(string cacheName, string cacheNodeName)
        {
            CacheClientConnectionCreationCounter.Add(1,
                MonitoringConstants.GetSuccessDimension(false),
                new KeyValuePair<string, object?>(CacheNameDimension, cacheName),
                new KeyValuePair<string, object?>(CacheNodeDimension, cacheNodeName));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddConnectionCreationSuccessCounter(string cacheName, string cacheNodeName)
        {
            CacheClientConnectionCreationCounter.Add(1,
                MonitoringConstants.GetSuccessDimension(true),
                new KeyValuePair<string, object?>(CacheNameDimension, cacheName),
                new KeyValuePair<string, object?>(CacheNodeDimension, cacheNodeName));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddConnectionFailedEvent(string cacheName, string cacheNodeName, ConnectionFailedEventArgs args)
        {
            CacheClientConnectionFailedEventCounter.Add(1,
                new KeyValuePair<string, object?>(CacheNameDimension, cacheName),
                new KeyValuePair<string, object?>(CacheNodeDimension, cacheNodeName),
                new KeyValuePair<string, object?>(FailureTypeDimension, args.FailureType));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddConnectionRestoredEvent(string cacheName, string cacheNodeName, ConnectionFailedEventArgs args)
        {
            CacheClientConnectionRestoredEventCounter.Add(1,
                new KeyValuePair<string, object?>(CacheNameDimension, cacheName),
                new KeyValuePair<string, object?>(CacheNodeDimension, cacheNodeName),
                new KeyValuePair<string, object?>(FailureTypeDimension, args.FailureType));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecordCacheClientReadCallMetric(
            Histogram<int> histogramMetric,
            int elapsed, 
            string? lastTriedCacheNodeName, 
            string methodName, 
            int numTriedNodes, 
            bool success)
        {
            histogramMetric.Record(elapsed,
                new KeyValuePair<string, object?>(LastTriedCacheNodeDimension, lastTriedCacheNodeName),
                new KeyValuePair<string, object?>(MethodDimension, methodName),
                new KeyValuePair<string, object?>(TriedNodeCountDimension, numTriedNodes),
                MonitoringConstants.GetSuccessDimension(success));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecordCacheClientWriteCallMetric(
            int elapsed, 
            string methodName, 
            int numSuccessCount, 
            int numFailedCount)
        {
            CacheClientWriteCallMetric.Record(elapsed,
                new KeyValuePair<string, object?>(MethodDimension, methodName),
                new KeyValuePair<string, object?>(NumSuccessCountDimension, numSuccessCount),
                new KeyValuePair<string, object?>(NumFailedCountDimension, numFailedCount),
                MonitoringConstants.GetSuccessDimension(numFailedCount == 0));
        }
    }
}