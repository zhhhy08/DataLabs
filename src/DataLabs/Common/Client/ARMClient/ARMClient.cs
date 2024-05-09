namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AccessTokenProvider;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SecretProviderManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    
    public class ARMClient : IARMClient, ICertificateListener
    {
        private static readonly ILogger<ARMClient> Logger
            = DataLabLoggerFactory.CreateLogger<ARMClient>();
        private static readonly ActivityMonitorFactory ARMClientCertificateChangedAsync = new("ARMClient.CertificateChangedAsync");
        private static readonly ActivityMonitorFactory ARMClientGetResourceAsync = new ("ARMClient.GetResourceAsync");
        private static readonly ActivityMonitorFactory ARMClientGetGenericRestApiAsync = new("ARMClient.GetGenericRestApiAsync");

        public static readonly string UserAgent = RestClient.CreateUserAgent("ARMClient");
        private static bool _retryAfterPacific404;

        private const int TokenCacheLimitInBytes = 100 * 1024 * 1024; //An app token is about 2-3 KB in size, so 100MB can cache 30000 ~ 50000 tokens
        private const string UseResourceGraphQueryString = "&useResourceGraph=true";

        // Metric
        private const string ARMGetResourceMetric = "ARMGetResourceMetric";
        private const string ARMGetGenericRestApiMetric = "ARMGetGenericRestApiMetric";
        private static readonly Histogram<long> ARMGetResourceMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(ARMGetResourceMetric);
        private static readonly Histogram<long> ARMGetGenericRestApiMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(ARMGetGenericRestApiMetric);

        private IAccessTokenProvider _accessTokenProvider;
        private readonly IRestClient _restClient;
        private readonly ARMClientOptions _clientOptions;
        private readonly string? _certificateName;
        private readonly string[] _armResourceScopes;

        private readonly object _updateLock = new();
        private volatile bool _disposed;

        static ARMClient()
        {
            _retryAfterPacific404 = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(SolutionConstants.RetryAfterPacific404, UpdateRetryAfterPacific404, defaultValue: false);
        }

        public static bool NeedToCreateDefaultARMClient(IConfiguration configuration)
        {
            var endPoints = configuration.GetValue<string>(SolutionConstants.ARMEndpoints).ConvertToSet(caseSensitive: false);
            var certificateName = configuration.GetValue<string>(SolutionConstants.ARMFirstPartyAppCertName, string.Empty);
            var useCredentialToken = configuration.GetValue<bool>(SolutionConstants.UseCredentialTokenForArmClient, false);
            return endPoints?.Count > 0 && (!string.IsNullOrWhiteSpace(certificateName) || useCredentialToken);
        }

        /* Default ARMAClient should be registered as singleTon in service Collection */
        public static ARMClient CreateARMClientFromDataLabConfig(IConfiguration configuration)
        {
            var armTokenResource = configuration.GetValue<string>(SolutionConstants.ARMTokenResource);
            var defaultTenantId = configuration.GetValue<string>(SolutionConstants.DefaultTenantId);
            var aadAuthority = configuration.GetValue<string>(SolutionConstants.AADAuthority);

            GuardHelper.ArgumentNotNullOrEmpty(armTokenResource, SolutionConstants.ARMTokenResource);
            GuardHelper.ArgumentNotNullOrEmpty(defaultTenantId, SolutionConstants.DefaultTenantId);

            var endPointSelector = new EndPointSelector(SolutionConstants.ARMEndpoints, SolutionConstants.ARMBackupEndpoints, configuration);

            // Using default configuration in DataLabs
            var useCredentialToken = configuration.GetValue<bool>(SolutionConstants.UseCredentialTokenForArmClient, false);
            if (useCredentialToken)
            {
                // Use credential Token
                var clientOptions = new ARMClientOptions(configuration)
                {
                    EndPointSelector = endPointSelector,
                    FirstPartyAppId = null,
                    ARMTokenResource = armTokenResource,
                    AADAuthority = aadAuthority ?? string.Empty,
                    DefaultTenantId = defaultTenantId,
                };

                return new ARMClient(
                    accessTokenProvider: new DefaultAzureCredentialTokenProvider(),
                    clientOptions: clientOptions);
            }
            else
            {
                // Use certificate
                GuardHelper.ArgumentNotNullOrEmpty(aadAuthority, SolutionConstants.AADAuthority);
                
                var certificateName = configuration.GetValue<string>(SolutionConstants.ARMFirstPartyAppCertName);
                GuardHelper.ArgumentNotNullOrEmpty(certificateName, SolutionConstants.ARMFirstPartyAppCertName);

                var firstPartyAppId = configuration.GetValue<string>(SolutionConstants.ARMFirstPartyAppId);
                GuardHelper.ArgumentNotNullOrEmpty(firstPartyAppId, SolutionConstants.ARMFirstPartyAppId);

                var clientOptions = new ARMClientOptions(configuration)
                {
                    EndPointSelector = endPointSelector,
                    FirstPartyAppId = firstPartyAppId,
                    ARMTokenResource = armTokenResource,
                    AADAuthority = aadAuthority,
                    DefaultTenantId = defaultTenantId,
                };

                return new ARMClient(
                    certificateName: certificateName,
                    clientOptions: clientOptions);
            }
        }

        public ARMClient(IAccessTokenProvider accessTokenProvider, ARMClientOptions clientOptions)
        {
            GuardHelper.ArgumentNotNull(accessTokenProvider);

            GuardHelper.ArgumentNotNull(clientOptions.EndPointSelector);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.ARMTokenResource);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.DefaultTenantId);

            _accessTokenProvider = accessTokenProvider;
            _clientOptions = clientOptions;
            _certificateName = null;

            var tokenResource = clientOptions.ARMTokenResource.TrimEnd('/');
            _armResourceScopes = new[] { $"{tokenResource}/.default" };

            _restClient = new RestClient(options: _clientOptions);
        }

        public ARMClient(string certificateName, ARMClientOptions clientOptions)
        {
            GuardHelper.ArgumentNotNullOrEmpty(certificateName);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.FirstPartyAppId);

            GuardHelper.ArgumentNotNull(clientOptions.EndPointSelector);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.ARMTokenResource);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.AADAuthority);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.DefaultTenantId);
            
            _clientOptions = clientOptions;
            _certificateName = certificateName;

            var tokenResource = clientOptions.ARMTokenResource.TrimEnd('/');
            _armResourceScopes = new[] { $"{tokenResource}/.default" };

            // Get Certificate from SecretProviderManager
            // For first party App, let's not allow MultiListeners
            // I don't see any other code which we should use the firstPartyAppCertificate
            // Only single ARMClient will have to use the firstPartyAppCertificate
            var secretProviderManager = SecretProviderManager.Instance;
            var certificate = secretProviderManager.GetCertificateWithListener(
                certificateName: _certificateName,
                listener: this,
                allowMultiListeners: false);
            GuardHelper.ArgumentNotNull(certificate);

            _accessTokenProvider = new AppBasedAccessTokenProvider(
                applicationId: _clientOptions.FirstPartyAppId,
                certificate: certificate,
                aadAuthority: _clientOptions.AADAuthority,
                inMemoryCacheLimit: TokenCacheLimitInBytes);

            _restClient = new RestClient(options: _clientOptions);
        }

        public async Task<HttpResponseMessage> GetResourceAsync(
            string resourceId, 
            string? tenantId, 
            string apiVersion,
            bool useResourceGraph,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            using var monitor = ARMClientGetResourceAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(resourceId, nameof(resourceId));
                GuardHelper.ArgumentNotNullOrEmpty(apiVersion, nameof(apiVersion));

                var resourceType = ArmUtils.GetResourceType(resourceId);

                var accessToken = await _accessTokenProvider.GetAccessTokenAsync(
                    tenantId: tenantId ?? _clientOptions.DefaultTenantId,
                    scopes: _armResourceScopes,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                
                var encodedResourceId = Uri.EscapeDataString(resourceId);

                var stringBuilder = new StringBuilder(512)
                    .Append(encodedResourceId)
                    .Append("?api-version=")
                    .Append(apiVersion);

                if (useResourceGraph)
                {
                    stringBuilder.Append(UseResourceGraphQueryString);
                }

                var requestUri = stringBuilder.ToString();
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                var response = await InternalCallRestApiAsync(
                    requestUri: requestUri,
                    accessToken: accessToken,
                    resourceType: resourceType,
                    clientRequestId: clientRequestId,
                    useResourceGraph: useResourceGraph,
                    activity: monitor.Activity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var needToRetryToArm = useResourceGraph && 
                    (response.StatusCode == HttpStatusCode.UnprocessableEntity ||
                    (_retryAfterPacific404 && response.StatusCode == HttpStatusCode.NotFound)); // 422 or (retryAfterPacific404 && 404)

                if (needToRetryToArm) 
                {
                    /*
                     * From Pacific Site
                     * https://eng.ms/docs/cloud-ai-platform/azure-core/azure-management-and-platforms/control-plane-bburns/azure-resource-graph/unified-cloud-inventory-arg/pacific/questions/known-limitations-and-solutions
                     * Unprocessable Resource Request
                     * There are rare scenarios where Pacific is not able to index a resource correctly, other than the existence of the resource. In order to not sacrifice data quality, Project Pacific will refuse serving GET calls for these resources and return an error code of HTTP 422.
                     * Client Handling Of HTTP 422 Error
                     * Clients of Pacific should treat HTTP 422 as a permanent error. Clients should retry by falling back to RP (by removing useResourceGraph=true flag). Since the error is applicable specifically to Pacific, fallback to RPs should result in an E2E success
                     * */

                    // Reset useResourceGraph flag
                    useResourceGraph = false;

                    stringBuilder.Clear().Append(encodedResourceId)
                        .Append("?api-version=")
                        .Append(apiVersion);

                    requestUri = stringBuilder.ToString();
                    monitor.Activity[SolutionConstants.RequestURIAfterPacificFail] = requestUri;

                    // Send to ARM without ARGFlag
                    response = await InternalCallRestApiAsync(
                        requestUri: requestUri,
                        accessToken: accessToken,
                        resourceType: resourceType,
                        clientRequestId: clientRequestId,
                        useResourceGraph: useResourceGraph,
                        activity: monitor.Activity,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                monitor.OnCompleted();
                return response;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<HttpResponseMessage> GetGenericRestApiAsync(
            string uriPath,
            IEnumerable<KeyValuePair<string, string>>? parameters,
            string? tenantId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            using var monitor = ARMClientGetGenericRestApiAsync.ToMonitor();

            long startTimeStamp = 0;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(uriPath, nameof(uriPath));
                GuardHelper.ArgumentNotNullOrEmpty(apiVersion, nameof(apiVersion));

                var accessToken = await _accessTokenProvider.GetAccessTokenAsync(
                    tenantId: tenantId ?? _clientOptions.DefaultTenantId,
                    scopes: _armResourceScopes,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var stringBuilder = new StringBuilder(512)
                    .Append(Uri.EscapeDataString(uriPath))
                    .Append("?api-version=")
                    .Append(apiVersion);

                if (parameters != null)
                {
                    SolutionUtils.AddQueryParamString(parameters, stringBuilder);
                }

                var requestUri = stringBuilder.ToString();
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await _restClient.CallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Get,
                    accessToken: accessToken,
                    headers: null,
                    jsonRequestContent: (string?)null,
                    clientRequestId: clientRequestId,
                    skipUriPathLogging: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(SolutionConstants.URIPath, uriPath);
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                ARMGetGenericRestApiMetricDuration.Record(restClientDuration, tagList);

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
                    tagList.Add(SolutionConstants.URIPath, uriPath);
                    tagList.Add(MonitoringConstants.GetSuccessDimension(false));
                    ARMGetGenericRestApiMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        public Task CertificateChangedAsync(X509Certificate2 certificate)
        {
            // Certificate is changed
            // we have to create new accessTokenProvider
            using var monitor = ARMClientCertificateChangedAsync.ToMonitor();

            try
            {
                lock (_updateLock)
                {
                    if (_disposed)
                    {
                        // Already Disposed
                        return Task.CompletedTask;
                    }

                    monitor.OnStart(false);

                    var oldaccessTokenProvider = _accessTokenProvider;

                    var newAccessTokenProvider = new AppBasedAccessTokenProvider(
                        applicationId: _clientOptions.FirstPartyAppId!,
                        certificate: certificate,
                        aadAuthority: _clientOptions.AADAuthority,
                        inMemoryCacheLimit: TokenCacheLimitInBytes);

                    Interlocked.Exchange(ref _accessTokenProvider, newAccessTokenProvider);

                    // DO NOT log certificate content because it includes private key
                    Logger.LogWarning("{config} is changed", _certificateName);

                    // Dispose old ones
                    oldaccessTokenProvider.Dispose();
                    monitor.OnCompleted();
                }
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _restClient.Dispose();
                if (_certificateName != null)
                {
                    SecretProviderManager.Instance.RemoveListener(_certificateName, this);
                }
            }
        }

        private async Task<HttpResponseMessage> InternalCallRestApiAsync(
            string requestUri,
            string accessToken, 
            string? resourceType,
            string? clientRequestId,
            bool useResourceGraph,
            IActivity activity,
            CancellationToken cancellationToken)
        {
            var startTimeStamp = Stopwatch.GetTimestamp();
            var responseFromPacific = false;

            try
            {
                var response = await _restClient.CallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Get,
                    accessToken: accessToken,
                    headers: null,
                    jsonRequestContent: (string?)null,
                    clientRequestId: clientRequestId,
                    skipUriPathLogging: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                if (useResourceGraph)
                {
                    responseFromPacific = IsFromPacific(response, activity);
                }

                UpdateARMGetResourceMetricDuration(
                    duration: restClientDuration, statusCode: response.StatusCode.FastEnumToString(),
                    resourceType: resourceType, useResourceGraph: useResourceGraph,
                    fromPacific: responseFromPacific, isSuccess: true);

                return response;

            }
            catch (Exception ex)
            {
                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                UpdateARMGetResourceMetricDuration(
                    duration: restClientDuration, statusCode: SolutionUtils.GetExceptionTypeSimpleName(ex), 
                    resourceType: resourceType, useResourceGraph: useResourceGraph,
                    fromPacific: responseFromPacific, isSuccess: false);

                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateARMGetResourceMetricDuration(
            long duration, 
            string statusCode, 
            string? resourceType, 
            bool useResourceGraph, 
            bool fromPacific, 
            bool isSuccess)
        {
            TagList tagList = default;
            tagList.Add(SolutionConstants.HttpStatusCode, statusCode);
            tagList.Add(SolutionConstants.ResourceType, resourceType);
            tagList.Add(SolutionConstants.UseResourceGraph, useResourceGraph);
            tagList.Add(SolutionConstants.FromPacific, fromPacific);
            tagList.Add(MonitoringConstants.GetSuccessDimension(isSuccess));
            ARMGetResourceMetricDuration.Record(duration, tagList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFromPacific(HttpResponseMessage response, IActivity activity)
        {
            var responseFromPacific = false;
            var responseHeaders = response.Headers;
            if (responseHeaders.TryGetValues(CommonHttpHeaders.PacificUserQuotaRemaining, out var headerValues))
            {
                // PacificUserQuotaRemaining header is present
                responseFromPacific = true;
                var pacificUserQuotaRemaining = headerValues.FirstOrDefault();
                activity[CommonHttpHeaders.PacificUserQuotaRemaining] = pacificUserQuotaRemaining;
            }

            if (responseHeaders.TryGetValues(CommonHttpHeaders.PacificRequestDuration, out headerValues))
            {
                // PacificRequestDuration header is present
                responseFromPacific = true;
                var pacificRequestDuration = headerValues.FirstOrDefault();
                activity[CommonHttpHeaders.PacificRequestDuration] = pacificRequestDuration;
            }

            if (responseHeaders.TryGetValues(CommonHttpHeaders.PacificSnapshotTimeStamp, out headerValues))
            {
                // PacificSnapshotTimeStamp header is present
                responseFromPacific = true;
                var pacificSnapshotTimeStamp = headerValues.FirstOrDefault();
                activity[CommonHttpHeaders.PacificSnapshotTimeStamp] = pacificSnapshotTimeStamp;
            }

            return responseFromPacific;
        }

        private static Task UpdateRetryAfterPacific404(bool newValue)
        {
            var oldValue = _retryAfterPacific404;
            if (oldValue != newValue)
            {
                _retryAfterPacific404 = newValue;
                Logger.LogWarning("{key} is changed, Old: {oldVal}, New: {newVal}", 
                    SolutionConstants.RetryAfterPacific404, oldValue, newValue);
            }
            return Task.CompletedTask;
        }
    }
}
