namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net;
    using System.Runtime.CompilerServices;
    using Azure;
    using Grpc.Core;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Constants;

    internal static class ResourceFetcherProxyMetricProvider
    {
        public const string ResourceFetcherProxyTraceSource = "ARG.DataLabs.ResourceFetcherProxyService";
        public const string ResourceFetcherProxyServiceMeter = ResourceFetcherProxyTraceSource;
        public const string ServiceMeterVersion = "1.0";

        internal static readonly ActivitySource ResourceFetcherProxyActivitySource = new(ResourceFetcherProxyTraceSource);
        internal static readonly Meter ResourceFetcherProxyMeter = new(ResourceFetcherProxyServiceMeter, ServiceMeterVersion);

        public const string RFProxySuccessRequestCounterName = "RFProxySuccessRequestCounter";
        public const string RFProxyFailedRequestCounterName = "RFProxyFailedRequestCounter";
        public const string RFProxyCacheClientCounterName = "RFProxyCacheCounter";
        public const string RFProxyClientToRecvMetricName = "RFProxyClientToRecvMetric";
        public const string ARMRemainingSubscriptionReadsMetricName = "ARMRemainingSubscriptionReads";

        private static readonly Counter<long> RFProxySuccessRequestCounter =
            ResourceFetcherProxyMeter.CreateCounter<long>(RFProxySuccessRequestCounterName);

        private static readonly Counter<long> RFProxyFailedRequestCounter =
            ResourceFetcherProxyMeter.CreateCounter<long>(RFProxyFailedRequestCounterName);

        private static readonly Counter<long> RFProxyCacheClientCounter =
            ResourceFetcherProxyMeter.CreateCounter<long>(RFProxyCacheClientCounterName);

        private static readonly Histogram<int> RFProxyClientToRecvMetric =
            ResourceFetcherProxyMeter.CreateHistogram<int>(RFProxyClientToRecvMetricName);

        /// <summary>
        /// Remaining ARM Read calls for a subscription (expected dimension is resourceType though 
        /// since subscription dimension will be too heavy for Geneva)
        /// Temporarily this is used for monitoring the remaining ARM read calls migration in ARM side
        /// </summary>
        private static readonly Histogram<long> ARMRemainingSubscriptionReadsMetric = 
            ResourceFetcherProxyMeter.CreateHistogram<long>(ARMRemainingSubscriptionReadsMetricName);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RecordARMRemainingSubscriptionReadsMetric(long remainingSubscriptionReads, HttpStatusCode httpStatusCode, string? resourceType)
        {
            ARMRemainingSubscriptionReadsMetric.Record(remainingSubscriptionReads,
                new KeyValuePair<string, object?>(SolutionConstants.HttpStatusCode, httpStatusCode.FastEnumToString()),
                new KeyValuePair<string, object?>(SolutionConstants.ResourceType, resourceType ?? SolutionConstants.MissingResourceType)); ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddRFProxyCacheClientCacheHitCounter(
            string callMethod, 
            string? typeDimensionValue, 
            ResourceCacheDataFormat cacheDataFormat)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(SolutionConstants.Type, typeDimensionValue ?? string.Empty);
            dimensions.Add(SolutionConstants.DataFormat, cacheDataFormat.FastEnumToString());
            dimensions.Add(SolutionConstants.CacheHit, true);
            RFProxyCacheClientCounter.Add(1, dimensions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddRFProxyCacheClientMissCounter(string callMethod, string? typeDimensionValue)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(SolutionConstants.Type, typeDimensionValue ?? string.Empty);
            dimensions.Add(SolutionConstants.DataFormat, "NONE");
            dimensions.Add(SolutionConstants.CacheHit, false);
            RFProxyCacheClientCounter.Add(1, dimensions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddRequestErrorCounter(
            string callMethod,
            int retryFlowCount,
            string? typeDimensionValue,
            int httpStatusCode,
            string providerTypeName,
            ResourceFetcherProxyError proxyError)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(MonitoringConstants.RetryCountDimension, retryFlowCount);
            dimensions.Add(SolutionConstants.Type, typeDimensionValue ?? string.Empty);
            dimensions.Add(SolutionConstants.HttpStatusCode, httpStatusCode);
            dimensions.Add(SolutionConstants.ProviderType, providerTypeName);
            dimensions.Add(SolutionConstants.ErrorType, proxyError.FastEnumToString());
            RFProxyFailedRequestCounter.Add(1, dimensions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddRequestSuccessCounter(
            string callMethod,
            int retryFlowCount,
            string? typeDimensionValue,
            string providerTypeName)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(MonitoringConstants.RetryCountDimension, retryFlowCount);
            dimensions.Add(SolutionConstants.Type, typeDimensionValue ?? string.Empty);
            dimensions.Add(SolutionConstants.ProviderType, providerTypeName);
            RFProxySuccessRequestCounter.Add(1, dimensions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddRFProxyClientToRecvMetric(
            string callMethod,
            long requestEpochTime,
            ServerCallContext context)
        {
            var recvEpochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var clientToServerTime = (int)(recvEpochTime - requestEpochTime);
            if (clientToServerTime <= 0)
            {
                clientToServerTime = 1;
            }

            var clientIp = context.Peer;
            var serverIp = MonitoringConstants.POD_IP;

            RFProxyClientToRecvMetric.Record(clientToServerTime,
                new KeyValuePair<string, object?>(SolutionConstants.CallMethod, callMethod),
                new KeyValuePair<string, object?>(SolutionConstants.ClientIP, clientIp),
                new KeyValuePair<string, object?>(SolutionConstants.ServerIP, serverIp));
        }
    }
}