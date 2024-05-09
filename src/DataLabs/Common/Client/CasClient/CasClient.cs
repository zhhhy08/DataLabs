namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.DstsClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;

    [ExcludeFromCodeCoverage]
    public class CasClient : ICasClient
    {
        private static readonly ActivityMonitorFactory CasClientGetCasCapacityCheckAsync = new("CasClient.GetCasCapacityCheckAsync");

        public static readonly string UserAgent = RestClient.CreateUserAgent("CasClient");

        // Metric
        private const string CasClientGetCasCapacityCheckMetric = "CasClientGetCasCapacityCheckMetric";
        private static readonly Histogram<long> CasClientGetCasCapacityCheckMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(CasClientGetCasCapacityCheckMetric);

        private readonly IRestClient _restClient;
        private readonly CasClientOptions _clientOptions;

        private volatile bool _disposed;

        public static bool NeedToCreateDefaultCasClient(IConfiguration configuration)
        {
            var endPoints = configuration.GetValue<string>(SolutionConstants.CasEndpoints).ConvertToSet(caseSensitive: false);
            var certificateName = configuration.GetValue<string>(SolutionConstants.CasDstsCertificateName);
            return endPoints?.Count > 0 && certificateName?.Length > 0;
        }

        public static CasClient CreateCasClientFromDataLabConfig(IConfiguration configuration)
        {
            // Using default configuration in DataLabs
            var configNames = new DstsConfigNames()
            {
                ConfigNameForDstsClientId = SolutionConstants.CasDstsClientId,
                ConfigNameForDstsServerId = SolutionConstants.CasDstsServerId,
                ConfigNameForClientHome = SolutionConstants.CasDstsClientHome,
                ConfigNameForServerHome = SolutionConstants.CasDstsServerHome,
                ConfigNameForServerRealm = SolutionConstants.CasDstsServerRealm,
                ConfigNameForCertificateName = SolutionConstants.CasDstsCertificateName,
                ConfigNameForSkipServerCertificateValidation = SolutionConstants.CasDstsSkipServerCertificateValidation
            };

            var endPointSelector = new EndPointSelector(SolutionConstants.CasEndpoints, SolutionConstants.CasBackupEndpoints, configuration);

            var clientOptions = new CasClientOptions(configuration)
            {
                EndPointSelector = endPointSelector,
            };

            return new CasClient(
                configNames: configNames,
                clientOptions: clientOptions,
                configuration: configuration);
        }

        /* Default CasClient should be registered as singleTon in service Collection */
        public CasClient(DstsConfigNames configNames, CasClientOptions clientOptions, IConfiguration configuration)
        {
            GuardHelper.ArgumentNotNull(clientOptions.EndPointSelector);

            _clientOptions = clientOptions;

            var dstsConfigValues = DstsConfigValues.CreateDstsConfigValues(configNames, configuration);

            _restClient = new DstsClient(
                dstsConfigValues: dstsConfigValues,
                restClientOptions: _clientOptions);
        }

        /// <summary>
        /// Gets Cas Capacity Check output 
        /// <returns></returns>
        public async Task<HttpResponseMessage> GetCasCapacityCheckAsync(
            CasRequestBody casRequestBody,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            using var monitor = CasClientGetCasCapacityCheckAsync.ToMonitor();

            long startTimeStamp = 0;

            try
            {
                monitor.OnStart(false);

                var encodedProvider = Uri.EscapeDataString(casRequestBody.Provider);
                var requestUri = $"/subscriptions/{casRequestBody.SubscriptionId}/providers/{encodedProvider}/version/{apiVersion}/fetchCapacityRestrictions?api-version={apiVersion}";
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                startTimeStamp = Stopwatch.GetTimestamp();
                // Mandatory headers for CAS
                var headers = new[]
                {
                    new KeyValuePair<string, string>(SolutionConstants.ActivityId, monitor.Activity.ActivityId.ToString() ?? Guid.NewGuid().ToString()),
                    new KeyValuePair<string, string>(SolutionConstants.CorrelationId, monitor.Activity.CorrelationId ?? Guid.NewGuid().ToString())
                };
                var response = await _restClient.CallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Post,
                    accessToken: null, // accessToken will be inserted through DstsV2TokenGenerationHandler
                    headers: headers,
                    jsonRequestContent: casRequestBody,
                    clientRequestId: clientRequestId,
                    skipUriPathLogging: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                CasClientGetCasCapacityCheckMetricDuration.Record(restClientDuration, tagList);

                monitor.OnCompleted();
                return response;
            }
            catch (Exception ex)
            {
                if (startTimeStamp > 0)
                {
                    long endTimestamp = Stopwatch.GetTimestamp();
                    var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                    TagList tagList = default;
                    tagList.Add(SolutionConstants.HttpStatusCode, SolutionUtils.GetExceptionTypeSimpleName(ex));
                    tagList.Add(MonitoringConstants.GetSuccessDimension(false));
                    CasClientGetCasCapacityCheckMetricDuration.Record(restClientDuration, tagList);
                }

                monitor.OnError(ex);
                throw;
            }
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