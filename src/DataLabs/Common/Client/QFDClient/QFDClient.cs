namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.DstsClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;

    public class QFDClient : IQFDClient
    {
        private static readonly ActivityMonitorFactory QFDClientGetPacificResourceAsync = new("QFDClient.GetPacificResourceAsync");
        private static readonly ActivityMonitorFactory QFDClientGetPacificCollectionAsync = new("QFDClient.GetPacificCollectionAsync");
        private static readonly ActivityMonitorFactory QFDClientGetPacificIdMappingAsync = new("QFDClient.GetPacificIdMappingAsync");

        public static readonly string UserAgent = RestClient.CreateUserAgent("QueryFrontDoorClient");

        // Metric
        private const string QFDGetPacificResourceMetric = "QFDGetPacificResourceMetric";
        private const string QFDGetPacificCollectionMetric = "QFDGetPacificCollectionMetric";
        private const string QFDGetPacificIdMappingMetric = "QFDGetPacificIdMappingMetric";
        private static readonly Histogram<long> QFDGetPacificResourceMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(QFDGetPacificResourceMetric);
        private static readonly Histogram<long> QFDGetPacificCollectionMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(QFDGetPacificCollectionMetric);
        private static readonly Histogram<long> QFDGetPacificIdMappingMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(QFDGetPacificIdMappingMetric);

        private readonly QFDClientOptions _clientOptions;
        private readonly IRestClient _restClient;

        private volatile bool _disposed;

        public static bool NeedToCreateDefaultQFDClient(IConfiguration configuration)
        {
            var endPoints = configuration.GetValue<string>(SolutionConstants.QfdEndpoints).ConvertToSet(caseSensitive: false);
            var certificateName = configuration.GetValue<string>(SolutionConstants.QfdDstsCertificateName);
            return endPoints?.Count > 0 && certificateName?.Length > 0;
        }

        /* Default QFDClient should be registered as singleTon in service Collection */
        public static QFDClient CreateQFDClientFromDataLabConfig(IConfiguration configuration)
        {
            // Using default configuration in DataLabs
            var configNames = new DstsConfigNames()
            {
                ConfigNameForDstsClientId = SolutionConstants.QfdDstsClientId,
                ConfigNameForDstsServerId = SolutionConstants.QfdDstsServerId,
                ConfigNameForClientHome = SolutionConstants.QfdDstsClientHome,
                ConfigNameForServerHome = SolutionConstants.QfdDstsServerHome,
                ConfigNameForServerRealm = SolutionConstants.QfdDstsServerRealm,
                ConfigNameForCertificateName = SolutionConstants.QfdDstsCertificateName,
                ConfigNameForSkipServerCertificateValidation = SolutionConstants.QfdDstsSkipServerCertificateValidation
            };

            var endPointSelector = new EndPointSelector(SolutionConstants.QfdEndpoints, SolutionConstants.QfdBackupEndpoints, configuration);

            var clientOptions = new QFDClientOptions(configuration)
            {
                EndPointSelector = endPointSelector,
            };

            return new QFDClient(
                configNames: configNames,
                clientOptions: clientOptions,
                configuration: configuration);
        }

        public QFDClient(DstsConfigNames configNames, QFDClientOptions clientOptions, IConfiguration configuration)
        {
            GuardHelper.ArgumentNotNull(clientOptions.EndPointSelector);

            _clientOptions = clientOptions;

            var dstsConfigValues = DstsConfigValues.CreateDstsConfigValues(configNames, configuration);

            _restClient = new DstsClient(
                dstsConfigValues: dstsConfigValues,
                restClientOptions: clientOptions);
        }

        public async Task<HttpResponseMessage> GetPacificResourceAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            // TODO: Looks like current Pacific Get Resource doesn't use tenant Id but need to double check */
            using var monitor = QFDClientGetPacificResourceAsync.ToMonitor();

            long startTimeStamp = 0;
            string? resourceType = null;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(resourceId, nameof(resourceId));
                GuardHelper.ArgumentNotNullOrEmpty(apiVersion, nameof(apiVersion));

                resourceType = ArmUtils.GetResourceType(resourceId);

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await GetInternalPacificResourceAsync(
                    resourceId: resourceId,
                    apiVersion: apiVersion,
                    clientRequestId: clientRequestId,
                    activity: monitor.Activity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(SolutionConstants.ResourceType, resourceType);
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                QFDGetPacificResourceMetricDuration.Record(restClientDuration, tagList);

                monitor.OnCompleted();
                return response;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);

                if (startTimeStamp > 0)
                {
                    long endTimestamp = Stopwatch.GetTimestamp();
                    var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                    TagList tagList = default;
                    tagList.Add(SolutionConstants.HttpStatusCode, SolutionUtils.GetExceptionTypeSimpleName(ex));
                    tagList.Add(SolutionConstants.ResourceType, resourceType);
                    tagList.Add(MonitoringConstants.GetSuccessDimension(false));
                    QFDGetPacificResourceMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        public async Task<HttpResponseMessage> GetPacificCollectionAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            // TODO: Looks like current Pacific Get Resource doesn't use tenant Id but need to double check */
            using var monitor = QFDClientGetPacificCollectionAsync.ToMonitor();

            long startTimeStamp = 0;
            string? resourceType = null;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(resourceId, nameof(resourceId));
                GuardHelper.ArgumentNotNullOrEmpty(apiVersion, nameof(apiVersion));

                resourceType = ArmUtils.GetResourceType(resourceId);

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await GetInternalPacificResourceAsync(
                    resourceId: resourceId,
                    apiVersion: apiVersion,
                    clientRequestId: clientRequestId,
                    activity: monitor.Activity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(SolutionConstants.ResourceType, resourceType);
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                QFDGetPacificCollectionMetricDuration.Record(restClientDuration, tagList);

                monitor.OnCompleted();
                return response;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);

                if (startTimeStamp > 0)
                {
                    long endTimestamp = Stopwatch.GetTimestamp();
                    var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                    TagList tagList = default;
                    tagList.Add(SolutionConstants.HttpStatusCode, SolutionUtils.GetExceptionTypeSimpleName(ex));
                    tagList.Add(SolutionConstants.ResourceType, resourceType);
                    tagList.Add(MonitoringConstants.GetSuccessDimension(false));
                    QFDGetPacificCollectionMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        public async Task<HttpResponseMessage> GetPacificIdMappingsAsync(
            IdMappingRequestBody idMappingRequestBody,
            string? correlationId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            // TODO: Looks like current Pacific Get Resource doesn't use tenant Id but need to double check */
            using var monitor = QFDClientGetPacificIdMappingAsync.ToMonitor();

            long startTimeStamp = 0;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNull(idMappingRequestBody, nameof(idMappingRequestBody));
                GuardHelper.ArgumentNotNullOrEmpty(apiVersion, nameof(apiVersion));

                startTimeStamp = Stopwatch.GetTimestamp();

                var requestUri = $"IdMapping/resolveAliases?api-version={apiVersion}";
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                var response = await _restClient.CallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Post,
                    accessToken: null, // accessToken will be inserted through DstsV2TokenGenerationHandler
                    headers: null,
                    jsonRequestContent: idMappingRequestBody,
                    clientRequestId: clientRequestId,
                    skipUriPathLogging: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(SolutionConstants.CorrelationId, correlationId);
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                QFDGetPacificIdMappingMetricDuration.Record(restClientDuration, tagList);

                monitor.OnCompleted();
                return response;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);

                if (startTimeStamp > 0)
                {
                    long endTimestamp = Stopwatch.GetTimestamp();
                    var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                    TagList tagList = default;
                    tagList.Add(SolutionConstants.HttpStatusCode, SolutionUtils.GetExceptionTypeSimpleName(ex));
                    tagList.Add(SolutionConstants.CorrelationId, correlationId);
                    tagList.Add(MonitoringConstants.GetSuccessDimension(false));
                    QFDGetPacificIdMappingMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        private async Task<HttpResponseMessage> GetInternalPacificResourceAsync(
            string resourceId,
            string apiVersion,
            string? clientRequestId,
            IActivity activity,
            CancellationToken cancellationToken)
        {
            var requestUri = $"{resourceId}?api-version={apiVersion}";
            activity[SolutionConstants.RequestURI] = requestUri;

            var response = await _restClient.CallRestApiAsync(
                endPointSelector: _clientOptions.EndPointSelector,
                requestUri: requestUri,
                httpMethod: HttpMethod.Get,
                accessToken: null, // accessToken will be inserted through DstsV2TokenGenerationHandler
                headers: null,
                jsonRequestContent: (string?)null,
                clientRequestId: clientRequestId,
                skipUriPathLogging: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _restClient.Dispose();
            }
        }
    }
}
