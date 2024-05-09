namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Monitoring
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;

    internal static class ResourceFetcherMetricProvider
    {
        public const string ResourceFetcherTraceSource = "ARG.DataLabs.ResourceFetcherService";
        public const string ResourceFetcherServiceMeter = ResourceFetcherTraceSource;
        public const string ServiceMeterVersion = "1.0";

        internal static readonly ActivitySource ResourceFetcherActivitySource = new(ResourceFetcherTraceSource);
        internal static readonly Meter ResourceFetcherMeter = new (ResourceFetcherServiceMeter, ServiceMeterVersion);

        public const string ResourceFetcherMiddleWareAuthCounterName = "ResourceFetcherMiddleWareAuthCounter";
        public const string ResourceFetcherRequestErrorCounterName = "ResourceFetcherRequestErrorCounter";
        public const string ResourceFetcherResponseCounterName = "ResourceFetcherResponseCounter";

        private static readonly Counter<long> ResourceFetcherMiddleWareAuthCounter = 
            ResourceFetcherMeter.CreateCounter<long>(ResourceFetcherMiddleWareAuthCounterName);

        private static readonly Counter<long> ResourceFetcherRequestErrorCounter =
            ResourceFetcherMeter.CreateCounter<long>(ResourceFetcherRequestErrorCounterName);

        private static readonly Counter<long> ResourceFetcherResponseCounter =
            ResourceFetcherMeter.CreateCounter<long>(ResourceFetcherResponseCounterName);

        internal static void AddMiddleWareAuthErrorCounter(string? partnerName, ResourceFetcherAuthError errorType)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.PartnerName, partnerName);
            dimensions.Add(SolutionConstants.AuthResult, errorType.FastEnumToString());
            dimensions.Add(MonitoringConstants.GetSuccessDimension(false));
            ResourceFetcherMiddleWareAuthCounter.Add(1, dimensions);
        }

        internal static void AddMiddleWareAuthSuccessCounter(string? partnerName, string authType)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.PartnerName, partnerName);
            dimensions.Add(SolutionConstants.AuthResult, authType);
            dimensions.Add(MonitoringConstants.GetSuccessDimension(true));
            ResourceFetcherMiddleWareAuthCounter.Add(1, dimensions);
        }

        internal static void AddRequestErrorCounter(string? partnerName, string callMethod, int retryFlowCount, ResourceFetcherAuthError errorType)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.PartnerName, partnerName);
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(SolutionConstants.ErrorType, errorType.FastEnumToString());
            dimensions.Add(MonitoringConstants.RetryCountDimension, retryFlowCount);
            ResourceFetcherRequestErrorCounter.Add(1, dimensions);
        }

        internal static void AddRequestErrorCounter(string? partnerName, string callMethod, int retryFlowCount, Exception ex)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.PartnerName, partnerName);
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(SolutionConstants.ErrorType, ex?.GetType().Name ?? "INTERNAL_ERROR");
            dimensions.Add(MonitoringConstants.RetryCountDimension, retryFlowCount);
            ResourceFetcherRequestErrorCounter.Add(1, dimensions);
        }

        internal static void AddClientResponseCounter(string? partnerName, string callMethod, int retryFlowCount, HttpStatusCode statusCode)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.PartnerName, partnerName);
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(SolutionConstants.HttpStatusCode, (int)statusCode);
            dimensions.Add(MonitoringConstants.RetryCountDimension, retryFlowCount);
            ResourceFetcherResponseCounter.Add(1, dimensions);
        }
    }
}
