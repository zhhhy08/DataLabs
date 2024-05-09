namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AccessTokenProvider;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;

    public class ResourceFetcherClient : IResourceFetcherClient
    {
        // IARMClient
        private static readonly ActivityMonitorFactory ResourceFetcherClientGetResourceAsync = new("ResourceFetcherClient.GetResourceAsync");
        private static readonly ActivityMonitorFactory ResourceFetcherClientGetGenericRestApiAsync = new("ResourceFetcherClient.GetGenericRestApiAsync");

        // IQFDClient
        private static readonly ActivityMonitorFactory ResourceFetcherClientGetPacificResourceAsync = new("ResourceFetcherClient.GetPacificResourceAsync");
        private static readonly ActivityMonitorFactory ResourceFetcherClientGetPacificCollectionAsync = new("ResourceFetcherClient.GetPacificCollectionAsync");

        // IARMAdminClient
        private static readonly ActivityMonitorFactory ResourceFetcherClientGetManifestConfig = new("ResourceFetcherClient.GetManifestConfig");
        private static readonly ActivityMonitorFactory ResourceFetcherClientGetConfigSpecs = new("ResourceFetcherClient.GetConfigSpecs");

        // ICASClient
        private static readonly ActivityMonitorFactory ResourceFetcherClientGetCasCapacityCheckAsync = new("ResourceFetcherClient.GetCasCapacityCheckAsync");

        private static readonly int MaxSizeForRequestUri = 512;

        // Metric
        private const string RFClientGetResourceMetric = "RFClientGetResourceMetric";
        private const string RFClientGetGenericRestApiMetric = "RFClientGetGenericRestApiMetric";
        private const string RFClientGetPacificResourceMetric = "RFClientGetPacificResourceMetric";
        private const string RFClientGetPacificCollectionMetric = "RFClientGetPacificCollectionMetric";
        private const string RFClientGetManifestConfigMetric = "RFClientGetManifestConfigMetric";
        private const string RFClientGetConfigSpecsMetric = "RFClientGetConfigSpecsMetric";
        private const string RFClientGetCasCapacityCheckMetric = "RFClientGetCasCapacityCheckMetric";

        private static readonly Histogram<long> RFClientGetResourceMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(RFClientGetResourceMetric);
        private static readonly Histogram<long> RFClientGetGenericRestApiMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(RFClientGetGenericRestApiMetric);
        private static readonly Histogram<long> RFClientGetPacificResourceMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(RFClientGetPacificResourceMetric);
        private static readonly Histogram<long> RFClientGetPacificCollectionMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(RFClientGetPacificCollectionMetric);
        private static readonly Histogram<long> RFClientGetManifestConfigMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(RFClientGetManifestConfigMetric);
        private static readonly Histogram<long> RFClientGetConfigSpecsMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(RFClientGetConfigSpecsMetric);
        private static readonly Histogram<long> RFClientGetCasCapacityCheckMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(RFClientGetCasCapacityCheckMetric);

        public static readonly string UserAgent = RestClient.CreateUserAgent("ResourceFetcherClient");

        private readonly IRestClient _restClient;
        private readonly IAccessTokenProvider _accessTokenProvider;
        private readonly ResourceFetcherClientOptions _clientOptions;
        private readonly string[] _resourceFetcherResourceScopes;

        private volatile bool _disposed;

        public static bool NeedToCreateDefaultResourceFetcherClient(IConfiguration configuration)
        {
            var endPoints = configuration.GetValue<string>(SolutionConstants.ResourceFetcherEndpoints).ConvertToSet(caseSensitive: false);
            return endPoints?.Count > 0;
        }

        /* Default ResourceFetcherClient should be registered as singleTon in service Collection */
        public static ResourceFetcherClient CreateResourceFetcherClientFromDataLabConfig(IConfiguration configuration)
        {
            // Using default configuration in DataLabs
            var endPointSelector = new EndPointSelector(SolutionConstants.ResourceFetcherEndpoints, SolutionConstants.ResourceFetcherBackupEndpoints, configuration);

            var resourceFetcherTokenResource = configuration.GetValue<string>(SolutionConstants.ResourceFetcherTokenResource);
            GuardHelper.ArgumentNotNullOrEmpty(resourceFetcherTokenResource, SolutionConstants.ResourceFetcherTokenResource);

            var resourceFetcherHomeTenantId = configuration.GetValue<string>(SolutionConstants.ResourceFetcherHomeTenantId);
            GuardHelper.ArgumentNotNullOrEmpty(resourceFetcherHomeTenantId, SolutionConstants.ResourceFetcherHomeTenantId);

            var clientOptions = new ResourceFetcherClientOptions(configuration)
            {
                EndPointSelector = endPointSelector,
                ResourceFetcherTokenResource = resourceFetcherTokenResource,
                ResourceFetcherHomeTenantId = resourceFetcherHomeTenantId,
                PartnerName = MonitoringConstants.SCALE_UNIT
            };

            IAccessTokenProvider accessTokenProvider = MonitoringConstants.IsLocalDevelopment ? new TestAccessTokenProvider() : new DefaultAzureCredentialTokenProvider();

            return new ResourceFetcherClient(
                accessTokenProvider: accessTokenProvider,
                clientOptions: clientOptions);
        }

        public ResourceFetcherClient(IAccessTokenProvider accessTokenProvider, ResourceFetcherClientOptions clientOptions)
        {
            GuardHelper.ArgumentNotNull(clientOptions.EndPointSelector);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.ResourceFetcherTokenResource);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.ResourceFetcherHomeTenantId);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.PartnerName);

            _clientOptions = clientOptions;

            var tokenResource = clientOptions.ResourceFetcherTokenResource.TrimEnd('/');
            _resourceFetcherResourceScopes = new[] { $"{tokenResource}/.default" };

            _accessTokenProvider = accessTokenProvider;
            _restClient = new RestClient(options: _clientOptions);
        }

        // This is used for integration test
        public ResourceFetcherClient(HttpClient testHttpClient, IConfiguration configuration)
        {
            // Using default configuration in DataLabs
            var endPointSelector = new EndPointSelector(SolutionConstants.ResourceFetcherEndpoints, SolutionConstants.ResourceFetcherBackupEndpoints, configuration);

            var resourceFetcherTokenResource = configuration.GetValue<string>(SolutionConstants.ResourceFetcherTokenResource);
            GuardHelper.ArgumentNotNullOrEmpty(resourceFetcherTokenResource, SolutionConstants.ResourceFetcherTokenResource);

            var resourceFetcherHomeTenantId = configuration.GetValue<string>(SolutionConstants.ResourceFetcherHomeTenantId);
            GuardHelper.ArgumentNotNullOrEmpty(resourceFetcherHomeTenantId, SolutionConstants.ResourceFetcherHomeTenantId);

            var clientOptions = new ResourceFetcherClientOptions(configuration)
            {
                EndPointSelector = endPointSelector,
                ResourceFetcherTokenResource = resourceFetcherTokenResource,
                ResourceFetcherHomeTenantId = resourceFetcherHomeTenantId,
                PartnerName = MonitoringConstants.SCALE_UNIT
            };

            GuardHelper.ArgumentNotNull(clientOptions.EndPointSelector);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.ResourceFetcherTokenResource);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.ResourceFetcherHomeTenantId);
            GuardHelper.ArgumentNotNullOrEmpty(clientOptions.PartnerName);

            _clientOptions = clientOptions;

            var tokenResource = clientOptions.ResourceFetcherTokenResource.TrimEnd('/');
            _resourceFetcherResourceScopes = new[] { $"{tokenResource}/.default" };

            _accessTokenProvider = new TestAccessTokenProvider();
            _restClient = new RestClient(options: _clientOptions, httpClient: testHttpClient);
        }

        // IARMClient interface
        public async Task<HttpResponseMessage> GetResourceAsync(
            string resourceId,
            string? tenantId,
            string? apiVersion,
            bool useResourceGraph,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            using var monitor = ResourceFetcherClientGetResourceAsync.ToMonitor();

            long startTimeStamp = 0;
            string? resourceType = null;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(resourceId, nameof(resourceId));

                resourceType = ArmUtils.GetResourceType(resourceId);

                var route = SolutionConstants.ResourceFetcher_ArmGetResourceRoute;

                var stringBuilder = new StringBuilder(MaxSizeForRequestUri).Append(route).Append('?');

                // resourceId
                SolutionUtils.AddQueryParamString(SolutionConstants.DL_RESOURCEID, resourceId, stringBuilder);

                // tenantId
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_TENANTID, tenantId, stringBuilder);
                }

                // apiVersion
                if (!string.IsNullOrWhiteSpace(apiVersion))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_APIVERSION, apiVersion, stringBuilder);
                }

                var requestUri = stringBuilder.ToString();
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await InternalCallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Get,
                    headers: null,
                    jsonRequestContent: (string?)null,
                    clientRequestId: clientRequestId,
                    activity: monitor.Activity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(SolutionConstants.ResourceType, resourceType);
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                RFClientGetResourceMetricDuration.Record(restClientDuration, tagList);

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
                    RFClientGetResourceMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        // IARMClient Interface
        public async Task<HttpResponseMessage> GetGenericRestApiAsync(
            string uriPath,
            IEnumerable<KeyValuePair<string, string>>? parameters,
            string? tenantId,
            string? apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {

            using var monitor = ResourceFetcherClientGetGenericRestApiAsync.ToMonitor();

            long startTimeStamp = 0;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(uriPath, nameof(uriPath));

                var route = SolutionConstants.ResourceFetcher_ArmGetGenericRestApiRoute;

                var stringBuilder = new StringBuilder(MaxSizeForRequestUri).Append(route).Append('?');

                // uriPath
                SolutionUtils.AddQueryParamString(SolutionConstants.DL_URIPATH, uriPath, stringBuilder);

                // parameters
                if (parameters != null)
                {
                    var paramSb = new StringBuilder(256);
                    SolutionUtils.AddQueryParamString(parameters, paramSb);
                    var concatedParams = paramSb.ToString();

                    // concated query param string has &. we need to encode it again to add it as key/value pair
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_PARAMETERS, concatedParams, stringBuilder);
                }

                // tenantId
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_TENANTID, tenantId, stringBuilder);
                }

                // apiVersion
                if (!string.IsNullOrWhiteSpace(apiVersion))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_APIVERSION, apiVersion, stringBuilder);
                }

                var requestUri = stringBuilder.ToString();
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await InternalCallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Get,
                    headers: null,
                    jsonRequestContent: (string?)null,
                    clientRequestId: clientRequestId,
                    activity: monitor.Activity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(SolutionConstants.URIPath, uriPath);
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                RFClientGetGenericRestApiMetricDuration.Record(restClientDuration, tagList);

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
                    RFClientGetGenericRestApiMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        // IQFDClient Interface
        public async Task<HttpResponseMessage> GetPacificResourceAsync(
            string resourceId,
            string? tenantId,
            string? apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            using var monitor = ResourceFetcherClientGetPacificResourceAsync.ToMonitor();

            long startTimeStamp = 0;
            string? resourceType = null;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(resourceId, nameof(resourceId));

                resourceType = ArmUtils.GetResourceType(resourceId);

                var route = SolutionConstants.ResourceFetcher_QfdGetPacificResourceRoute;

                var stringBuilder = new StringBuilder(MaxSizeForRequestUri).Append(route).Append('?');

                // resourceId
                SolutionUtils.AddQueryParamString(SolutionConstants.DL_RESOURCEID, resourceId, stringBuilder);

                // tenantId
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_TENANTID, tenantId, stringBuilder);
                }

                // apiVersion
                if (!string.IsNullOrWhiteSpace(apiVersion))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_APIVERSION, apiVersion, stringBuilder);
                }

                var requestUri = stringBuilder.ToString();
                monitor.Activity[SolutionConstants.RequestURI] = requestUri.ToString();

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await InternalCallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Get,
                    headers: null,
                    jsonRequestContent: (string?)null,
                    clientRequestId: clientRequestId,
                    activity: monitor.Activity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(SolutionConstants.ResourceType, resourceType);
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                RFClientGetPacificResourceMetricDuration.Record(restClientDuration, tagList);

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
                    RFClientGetPacificResourceMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        // IQFDClient interface
        public async Task<HttpResponseMessage> GetPacificCollectionAsync(
            string resourceId,
            string? tenantId,
            string? apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            using var monitor = ResourceFetcherClientGetPacificCollectionAsync.ToMonitor();

            long startTimeStamp = 0;
            string? resourceType = null;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(resourceId, nameof(resourceId));

                var route = SolutionConstants.ResourceFetcher_QfdGetPacificCollectionRoute;

                var stringBuilder = new StringBuilder(MaxSizeForRequestUri).Append(route).Append('?');

                // resourceId
                SolutionUtils.AddQueryParamString(SolutionConstants.DL_RESOURCEID, resourceId, stringBuilder);

                // tenantId
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_TENANTID, tenantId, stringBuilder);
                }

                // apiVersion
                if (!string.IsNullOrWhiteSpace(apiVersion))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_APIVERSION, apiVersion, stringBuilder);
                }

                var requestUri = stringBuilder.ToString();
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await InternalCallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Get,
                    headers: null,
                    jsonRequestContent: (string?)null,
                    clientRequestId: clientRequestId,
                    activity: monitor.Activity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(SolutionConstants.ResourceType, resourceType);
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                RFClientGetPacificCollectionMetricDuration.Record(restClientDuration, tagList);

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
                    RFClientGetPacificCollectionMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        public Task<HttpResponseMessage> GetPacificIdMappingsAsync(
            IdMappingRequestBody idMappingRequestBody,
            string? correlationId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        // IARMAdminClient Interface
        public async Task<HttpResponseMessage> GetManifestConfigAsync(
            string manifestProvider,
            string? apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            using var monitor = ResourceFetcherClientGetManifestConfig.ToMonitor();

            long startTimeStamp = 0;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(manifestProvider, nameof(manifestProvider));

                var route = SolutionConstants.ResourceFetcher_ArmAdminGetManifestConfigRoute;

                var stringBuilder = new StringBuilder(MaxSizeForRequestUri).Append(route).Append('?');

                // manifestProvider
                SolutionUtils.AddQueryParamString(SolutionConstants.DL_MANIFESTPROVIDER, manifestProvider, stringBuilder);

                // apiVersion
                if (!string.IsNullOrWhiteSpace(apiVersion))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_APIVERSION, apiVersion, stringBuilder);
                }

                var requestUri = stringBuilder.ToString();
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await InternalCallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Get,
                    headers: null,
                    jsonRequestContent: (string?)null,
                    clientRequestId: clientRequestId,
                    activity: monitor.Activity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                RFClientGetManifestConfigMetricDuration.Record(restClientDuration, tagList);

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
                    RFClientGetManifestConfigMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        // IARMAdminClient Interface
        public async Task<HttpResponseMessage> GetConfigSpecsAsync(
            string apiExtension,
            string? apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            using var monitor = ResourceFetcherClientGetConfigSpecs.ToMonitor();

            long startTimeStamp = 0;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNullOrEmpty(apiExtension, nameof(apiExtension));

                var route = SolutionConstants.ResourceFetcher_ArmAdminGetConfigSpecsRoute;

                var stringBuilder = new StringBuilder(MaxSizeForRequestUri).Append(route).Append('?');

                // apiExtension
                SolutionUtils.AddQueryParamString(SolutionConstants.DL_APIEXTENSION, apiExtension, stringBuilder);

                // apiVersion
                if (!string.IsNullOrWhiteSpace(apiVersion))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_APIVERSION, apiVersion, stringBuilder);
                }

                var requestUri = stringBuilder.ToString();
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await InternalCallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Get,
                    headers: null,
                    jsonRequestContent: (string?)null,
                    clientRequestId: clientRequestId,
                    activity: monitor.Activity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                RFClientGetConfigSpecsMetricDuration.Record(restClientDuration, tagList);

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
                    RFClientGetConfigSpecsMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        // ICasClient Interface
        public async Task<HttpResponseMessage> GetCasCapacityCheckAsync(
              CasRequestBody casRequestBody,
              string? apiVersion,
              string? clientRequestId,
              CancellationToken cancellationToken)
        {
            using var monitor = ResourceFetcherClientGetCasCapacityCheckAsync.ToMonitor();

            long startTimeStamp = 0;

            try
            {
                monitor.OnStart(false);

                GuardHelper.ArgumentNotNull(casRequestBody, nameof(casRequestBody));

                var route = SolutionConstants.ResourceFetcher_CASGetCasCapacityCheckRoute;

                var stringBuilder = new StringBuilder(MaxSizeForRequestUri).Append(route).Append('?');

                // apiVersion
                if (!string.IsNullOrWhiteSpace(apiVersion))
                {
                    SolutionUtils.AddQueryParamString(SolutionConstants.DL_APIVERSION, apiVersion, stringBuilder);
                }

                var requestUri = stringBuilder.ToString();
                monitor.Activity[SolutionConstants.RequestURI] = requestUri;

                startTimeStamp = Stopwatch.GetTimestamp();

                var response = await InternalCallRestApiAsync(
                    endPointSelector: _clientOptions.EndPointSelector,
                    requestUri: requestUri,
                    httpMethod: HttpMethod.Post,
                    headers: null,
                    jsonRequestContent: casRequestBody,
                    clientRequestId: clientRequestId,
                    activity: monitor.Activity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                long endTimestamp = Stopwatch.GetTimestamp();
                var restClientDuration = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                TagList tagList = default;
                tagList.Add(SolutionConstants.HttpStatusCode, response.StatusCode.FastEnumToString());
                tagList.Add(MonitoringConstants.GetSuccessDimension(true));
                RFClientGetCasCapacityCheckMetricDuration.Record(restClientDuration, tagList);

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
                    RFClientGetCasCapacityCheckMetricDuration.Record(restClientDuration, tagList);
                }

                throw;
            }
        }

        private async Task<HttpResponseMessage> InternalCallRestApiAsync<TRequest>(
            IEndPointSelector endPointSelector,
            string requestUri,
            HttpMethod httpMethod,
            IEnumerable<KeyValuePair<string, string>>? headers,
            TRequest? jsonRequestContent,
            string? clientRequestId,
            IActivity activity,
            CancellationToken cancellationToken) where TRequest : class
        {

            // AccessToken to ResourceFetcher Service
            var accessToken = await _accessTokenProvider.GetAccessTokenAsync(
                tenantId: _clientOptions.ResourceFetcherHomeTenantId,
                scopes: _resourceFetcherResourceScopes,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Add Common Headers
            // TraceId
            // CorrelationId

            var clientContext = ResourceFetcherClientContext.Current;
            var inputCorrelationId = clientContext?.CorrelationId;
            var otelActivityId = clientContext?.OpenTelemetryActivityId;
            var retryFlowCount = clientContext?.RetryCount ?? 0;

            KeyPairList<string, string> keyPairList = default;

            // Partner Name
            keyPairList.Add(CommonHttpHeaders.DataLabs_PartnerName, _clientOptions.PartnerName);

            // Input Correlation Id
            if (inputCorrelationId != null)
            {
                keyPairList.Add(CommonHttpHeaders.DataLabs_InputCorrelationId, inputCorrelationId);
                activity[CommonHttpHeaders.DataLabs_InputCorrelationId] = inputCorrelationId;
            }

            // OpenTelemetry ActivityId
            if (otelActivityId != null)
            {
                keyPairList.Add(CommonHttpHeaders.DataLabs_OpenTelemetry_ActivityId, otelActivityId);
                activity[CommonHttpHeaders.DataLabs_OpenTelemetry_ActivityId] = otelActivityId;
            }

            // RetryCount
            keyPairList.Add(CommonHttpHeaders.DataLabs_RetryFlowCount, retryFlowCount.ToString());
            activity[CommonHttpHeaders.DataLabs_RetryFlowCount] = retryFlowCount;

            // Generate Client Request Id here for this call to track this external call.
            clientRequestId ??= Guid.NewGuid().ToString();

            activity[CommonHttpHeaders.ClientRequestId] = clientRequestId;

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    keyPairList.Add(header);
                }
            }

            var response = await _restClient.CallRestApiAsync(
                endPointSelector: endPointSelector,
                requestUri: requestUri,
                httpMethod: httpMethod,
                accessToken: accessToken,
                headers: keyPairList,
                jsonRequestContent: jsonRequestContent,
                clientRequestId: clientRequestId,
                skipUriPathLogging: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            activity[SolutionConstants.HttpStatusCode] = (int)response.StatusCode;
            activity[SolutionConstants.HttpVersion] = SolutionUtils.GetHttpVersionString(response.Version);

            var responseHeaders = response.Headers;

            if (response.StatusCode == HttpStatusCode.OK)
            {
                // we will return new response which has source(actual client)'s resource code and headers
                try
                {
                    // Parse Source's Status Code
                    string? sourceStatusCodeString = null;
                    if (responseHeaders.TryGetValues(CommonHttpHeaders.DataLabs_Source_StatusCode, out var headerValues))
                    {
                        sourceStatusCodeString = headerValues.FirstOrDefault();
                    }

                    activity[SolutionConstants.SourceStatusCode] = sourceStatusCodeString;

                    if (string.IsNullOrWhiteSpace(sourceStatusCodeString) ||
                        !SolutionUtils.TryConvertIntStatusToHttpStatusCode(sourceStatusCodeString, out HttpStatusCode sourceHttpStatusCode))
                    {
                        // Something wrong
                        // This should not happen
                        throw new Exception("Bug!.. Can't find source's status code in resource fetcher's response header");
                    }

                    var responseByteArray = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

                    // Create a new response with source headers
                    var newResponse = new HttpResponseMessage(sourceHttpStatusCode)
                    {
                        // Copy content
                        Content = new ReadOnlyMemoryHttpContent(new ReadOnlyMemory<byte>(responseByteArray))
                    };

                    // Get ETag if any
                    if (response.Headers.ETag != null)
                    {
                        var entityTagHeaderValue = response.Headers.ETag;
                        newResponse.Headers.ETag = new EntityTagHeaderValue(entityTagHeaderValue.Tag, entityTagHeaderValue.IsWeak);
                    }

                    // Extract source response headers
                    foreach (var header in response.Headers)
                    {
                        var headerKey = header.Key;
                        if (headerKey.StartsWith(CommonHttpHeaders.DataLabs_Source_Header_Prefix))
                        {
                            var newHeaderKeyName = headerKey.Substring(CommonHttpHeaders.DataLabs_Source_Header_Prefix.Length);
                            newResponse.Headers.TryAddWithoutValidation(newHeaderKeyName, header.Value);
                        }
                    }

                    return newResponse;

                }
                finally
                {
                    response.Dispose();
                }
            }
            else
            {
                // check if CommonHttpHeaders.DataLabs_AuthError exists
                if (responseHeaders.TryGetValues(CommonHttpHeaders.DataLabs_AuthError, out var headerValues))
                {
                    var authError = headerValues.FirstOrDefault();
                    activity[SolutionConstants.AuthError] = authError;
                }
            }

            return response;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _restClient.Dispose();
                _accessTokenProvider.Dispose();
            }
        }
    }
}
