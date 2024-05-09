namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientCertificateRestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class ARMAdminClient : IARMAdminClient
    {
        private static readonly ActivityMonitorFactory ARMAdminClientGetManifestConfigAsync = new ("ARMAdminClient.GetManifestConfigAsync");
        private static readonly ActivityMonitorFactory ARMAdminClientGetConfigSpecsAsync = new ("ARMAdminClient.GetConfigSpecsAsync");

        public static readonly string UserAgent = RestClient.CreateUserAgent("ARMAdminClient");

        // Metric
        private const string ARMAdminGetManifestConfigMetric = "ARMAdminGetManifestConfigMetric";
        private const string ARMAdminGetConfigSpecsMetric = "ARMAdminGetConfigSpecsMetric";
        private static readonly Histogram<long> ARMAdminGetManifestConfigMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(ARMAdminGetManifestConfigMetric);
        private static readonly Histogram<long> ARMAdminGetConfigSpecsMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(ARMAdminGetConfigSpecsMetric);

        private readonly IRestClient _restClient;
        private readonly ARMAdminClientOptions _clientOptions;

        private volatile bool _disposed;

        public static bool NeedToCreateDefaultARMAdminClient(IConfiguration configuration)
        {
            var endPoints = configuration.GetValue<string>(SolutionConstants.ArmAdminEndpoints).ConvertToSet(caseSensitive: false);
            var certificateName = configuration.GetValue<string>(SolutionConstants.ArmAdminCertificateName);
            return endPoints?.Count > 0 && certificateName?.Length > 0;
        }

        /* Default ARMAdminClient should be registered as singleTon in service Collection */
        public static ARMAdminClient CreateARMAdminClientFromDataLabConfig(IConfiguration configuration)
        {
            // Using default configuration in DataLabs
            var certificateName = configuration.GetValue<string>(SolutionConstants.ArmAdminCertificateName);
            GuardHelper.ArgumentNotNullOrEmpty(certificateName, SolutionConstants.ArmAdminCertificateName);

            var endPointSelector = new EndPointSelector(SolutionConstants.ArmAdminEndpoints, SolutionConstants.ArmAdminBackupEndpoints, configuration);

            var clientOptions = new ARMAdminClientOptions(configuration)
            {
                EndPointSelector = endPointSelector,
            };

            return new ARMAdminClient(
                certificateName: certificateName,
                clientOptions: clientOptions);
        }

        public ARMAdminClient(string certificateName, ARMAdminClientOptions clientOptions)
        {
            GuardHelper.ArgumentNotNull(clientOptions.EndPointSelector);

            _clientOptions = clientOptions;

            _restClient = new ClientCertificateRestClient(
                certificateName: certificateName,
                restClientOptions: _clientOptions);
        }

        public async Task<HttpResponseMessage> GetManifestConfigAsync(
            string manifestProvider,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            using var monitor = ARMAdminClientGetManifestConfigAsync.ToMonitor();

            long startTimeStamp = 0;

            try
            {
                monitor.OnStart(false);
                
                GuardHelper.ArgumentNotNullOrEmpty(manifestProvider, nameof(manifestProvider));

                var encodedManifestProvider = Uri.EscapeDataString(manifestProvider);

                var stringBuilder = new StringBuilder(512)
                  .Append("/providers/")
                  .Append(encodedManifestProvider)
                  .Append("?api-version=")
                  .Append(apiVersion);

                var requestUri = stringBuilder.ToString();
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await _restClient.CallRestApiAsync(
                  endPointSelector: _clientOptions.EndPointSelector,
                  requestUri: requestUri,
                  httpMethod: HttpMethod.Get,
                  accessToken: null, // accessToken is not necessary for ARMAdmin
                  headers: null,
                  jsonRequestContent: (string?)null,
                  clientRequestId: clientRequestId,
                  skipUriPathLogging: false,
                  cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                ARMAdminGetManifestConfigMetricDuration.Record(restClientDuration, tagList);

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
                    tagList.Add(MonitoringConstants.GetSuccessDimension(false));
                    ARMAdminGetManifestConfigMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        public async Task<HttpResponseMessage> GetConfigSpecsAsync(
            string apiExtension,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            using var monitor = ARMAdminClientGetConfigSpecsAsync.ToMonitor();

            long startTimeStamp = 0;

            try
            {
                monitor.OnStart(false);

                var stringBuilder = new StringBuilder(512)
                    .Append("/configspecs");

                if (!string.IsNullOrEmpty(apiExtension))
                {
                    var encodedApiExtension = Uri.EscapeDataString(apiExtension);
                    if (encodedApiExtension[0] != '/')
                    {
                        stringBuilder.Append('/');
                    }
                    stringBuilder.Append(encodedApiExtension);
                }

                stringBuilder
                    .Append("?api-version=")
                    .Append(apiVersion);

                var requestUri = stringBuilder.ToString();

                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await _restClient.CallRestApiAsync(
                  endPointSelector: _clientOptions.EndPointSelector,
                  requestUri: requestUri,
                  httpMethod: HttpMethod.Get,
                  accessToken: null, // accessToken is not necessary for ARMAdmin
                  headers: null,
                  jsonRequestContent: (string?)null,
                  clientRequestId: clientRequestId,
                  skipUriPathLogging: false,
                  cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                ARMAdminGetConfigSpecsMetricDuration.Record(restClientDuration, tagList);

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
                    tagList.Add(MonitoringConstants.GetSuccessDimension(false));
                    ARMAdminGetConfigSpecsMetricDuration.Record(restClientDuration, tagList);
                }

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
