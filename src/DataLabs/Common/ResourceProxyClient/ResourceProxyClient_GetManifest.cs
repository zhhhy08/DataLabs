namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices.JavaScript;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;

    public partial class ResourceProxyClient : IResourceProxyClient, IDisposable
    {
        private static readonly ActivityMonitorFactory ResourceProxyClientGetManifestConfigAsync =
            new("ResourceProxyClient.GetManifestConfigAsync", useDataLabsEndpoint: true);

        public async Task<DataLabsARMAdminResponse> GetManifestConfigAsync(
            DataLabsManifestConfigRequest request,
            CancellationToken cancellationToken,
            bool skipCacheRead = false,
            bool skipCacheWrite = false,
            string? scenario = null,
            string? component = null)
        {
            var callMethod = nameof(GetManifestConfigAsync);
            var activityMonitorFactory = ResourceProxyClientGetManifestConfigAsync;

            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: component);

            try
            {
                SetInputResourceIdAndCorrelationId(monitor.Activity, inputResourceId: request.ManifestProvider, correlationId: request.CorrelationId);

                monitor.OnStart(false);

                var timeOut = _timeOutConfigInfo.GetTimeOut(request.RetryCount);
                monitor.Activity[SolutionConstants.TimeOutValue] = timeOut;

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                monitor.Activity[SolutionConstants.TraceId] = request.TraceId;
                monitor.Activity[SolutionConstants.RetryCount] = request.RetryCount;
                monitor.Activity[SolutionConstants.ManifestProvider] = request.ManifestProvider;

                var allowedTypesMap = _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(
                    ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes);

                // For now, for GetManifestConfig, we only support allow or not allow scenario
                var providerConfigList = IsAllowedType(allowedTypesMap: allowedTypesMap, allowedTypeKey: ClientProviderConfigList.AllAllowedSymbol);
                if (providerConfigList == null)
                {
                    // Not Allowed Type
                    monitor.OnError(NotAllowedTypeException);

                    var errorMessage = "GetManifestConfigAsync is not Allowed";
                    return CreateARMAdminErrorResponse(
                        correlationId: request.CorrelationId ?? string.Empty,
                        callMethod: callMethod,
                        retryFlowCount: request.RetryCount,
                        typeDimensionValue: request.ManifestProvider,
                        httpStatusCode: 0,
                        proxyClientError: ResourceProxyClientError.NOT_ALLOWED_TYPE,
                        errorType: DataLabsErrorType.POISON,
                        errorMessage: errorMessage,
                        retryAfter: 0,
                        failedComponent: SolutionConstants.ResourceProxyClient,
                        proxyDataSource: ProxyDataSource.None);
                }

                try
                {
                    ResourceResponse? resourceResponse = null;
                    if (CanUseCache(providerConfigList, request.RetryCount, skipCacheRead))
                    {
                        var cacheKey = GetCacheKeyForManifestConfig(request.ManifestProvider);
                        var (cacheResult, hasException) = await CacheLookupAsync(
                            callMethod: callMethod,
                            cacheProviderConfig: providerConfigList.CacheProviderConfig!,
                            cacheKey: cacheKey,
                            tenantId: null,
                            typeDimensionValue: request.ManifestProvider,
                            activity: monitor.Activity,
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (!hasException)
                        {
                            // Cache lookup is already tried here. so even when cache miss, in resource fetcher proxy, we don't need cache lookup again
                            // Surely, cache could be filled in those short time. But in most of cases, cache miss here will cause another cache miss in resource fetcher proxy
                            skipCacheRead = true;
                        }

                        if (cacheResult.Found)
                        {
                            resourceResponse = ConvertCacheResultToResourceResponse(
                                correlationId: request.CorrelationId,
                                resourceCacheResult: cacheResult);
                        }
                    }

                    if (resourceResponse == null)
                    {
                        var convertedRequest = ConvertToManifestRequest(request: request, scenario: monitor.Activity.Scenario, skipCacheRead: skipCacheRead, skipCacheWrite: skipCacheWrite);

                        DateTime? deadline = null;
                        if (timeOut > TimeSpan.Zero)
                        {
                            deadline = DateTime.UtcNow.Add(timeOut);
                        }
                        resourceResponse = await _client.GetManifestConfigAsync(request: convertedRequest, cancellationToken: cancellationToken, deadline: deadline).ConfigureAwait(false);
                    }

                    return ConvertToARMAdminResponse(
                        inputCorrelationId: request.CorrelationId,
                        callMethod: callMethod,
                        retryCount: request.RetryCount,
                        typeDimensionValue: request.ManifestProvider,
                        response: resourceResponse,
                        monitor: monitor);
                }
                catch (Exception ex)
                {
                    monitor.OnError(ex);

                    var proxyClientError = cancellationToken.IsCancellationRequested ?
                        ResourceProxyClientError.CANCELLATION_REQUESTED : ResourceProxyClientError.INTERNAL_EXCEPTION;

                    return CreateARMAdminErrorResponse(
                        correlationId: request.CorrelationId ?? string.Empty,
                        callMethod: callMethod,
                        retryFlowCount: request.RetryCount,
                        typeDimensionValue: request.ManifestProvider,
                        httpStatusCode: 0,
                        proxyClientError: proxyClientError,
                        errorType: DataLabsErrorType.RETRY,
                        errorMessage: ex.Message,
                        retryAfter: 0,
                        failedComponent: SolutionConstants.ResourceProxyClient,
                        proxyDataSource: ProxyDataSource.None);
                }
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);

                return CreateARMAdminErrorResponse(
                    correlationId: request.CorrelationId ?? string.Empty,
                    callMethod: callMethod,
                    retryFlowCount: request.RetryCount,
                    typeDimensionValue: request.ManifestProvider,
                    httpStatusCode: 0,
                    proxyClientError: ResourceProxyClientError.INTERNAL_EXCEPTION,
                    errorType: DataLabsErrorType.RETRY,
                    errorMessage: ex.Message,
                    retryAfter: 0,
                    failedComponent: SolutionConstants.ResourceProxyClient,
                    proxyDataSource: ProxyDataSource.None);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetCacheKeyForManifestConfig(string manifestProvider)
        {
            return ManifestProviderResourceType + "/" + manifestProvider;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ManifestRequest ConvertToManifestRequest(
            DataLabsManifestConfigRequest request,
            string? scenario,
            bool skipCacheRead, 
            bool skipCacheWrite)
        {
            var manifestRequest = new ManifestRequest()
            {
                TraceId = request.TraceId,
                RetryCount = request.RetryCount,
                RequestEpochTime = request.RequestTime.ToUnixTimeMilliseconds(),
                CorrelationId = request.CorrelationId,
                ManifestProvider = request.ManifestProvider,
                SkipCacheRead = skipCacheRead,
                SkipCacheWrite = skipCacheWrite
            };

            if (!string.IsNullOrEmpty(scenario))
            {
                manifestRequest.ReqAttributes.Add(BasicActivityMonitor.Scenario, scenario);
            }

            return manifestRequest;
        }

        private static DataLabsARMAdminResponse ConvertToARMAdminResponse(
            string? inputCorrelationId,
            string callMethod,
            int retryCount,
            string? typeDimensionValue,
            ResourceResponse response,
            IActivityMonitor monitor)
        {
            var responseTime = DateTimeOffset.FromUnixTimeMilliseconds(response.ResponseEpochTime);
            var correlationId = string.IsNullOrEmpty(response.CorrelationId) ? inputCorrelationId : response.CorrelationId;
            var respAttributes = response.RespAttributes;

            monitor.Activity[SolutionConstants.ResponseTime] = response.ResponseEpochTime;
            monitor.Activity[SolutionConstants.ResponseCorrelationId] = correlationId;

            if (response.Success != null)
            {
                monitor.Activity[SolutionConstants.HasSuccess] = true;

                var proxyDataFormat = response.Success.Format;
                var outputData = response.Success.OutputData;
                var eTag = response.Success.Etag;
                var proxyDataSource = response.Success.DataSource;

                monitor.Activity[SolutionConstants.DataFormat] = proxyDataFormat.FastEnumToString();
                monitor.Activity[SolutionConstants.ETag] = eTag;
                monitor.Activity[SolutionConstants.ProxyDataSource] = proxyDataSource.FastEnumToString();

                if (proxyDataFormat == ProxyDataFormat.Armadmin)
                {
                    var successResponse = new DataLabsARMAdminSuccessResponse(outputData.ToStringUtf8(), outputTimestamp: responseTime);
                    var armadminResponse = new DataLabsARMAdminResponse(
                        responseTime: responseTime,
                        correlationId: correlationId,
                        successResponse: successResponse,
                        errorResponse: null,
                        attributes: respAttributes,
                        dataSource: SolutionUtils.ConvertProxyDataSourceToDataLabsDataSource(proxyDataSource));

                    AddRequestSuccessCounter(
                        callMethod: callMethod,
                        retryFlowCount: retryCount,
                        typeDimensionValue: typeDimensionValue,
                        proxyDataFormat: proxyDataFormat,
                        proxyDataSource: proxyDataSource);

                    monitor.OnCompleted();
                    return armadminResponse;
                }
                else
                {
                    throw new NotImplementedException("Not Implemented DataFormat: " + proxyDataFormat.FastEnumToString());
                }
            }
            else if (response.Error != null)
            {
                monitor.Activity[SolutionConstants.HasError] = true;
                monitor.OnError(ProxyReturnErrorResponseException);

                return CreateARMAdminErrorResponse(
                    correlationId: correlationId ?? string.Empty,
                    callMethod: callMethod,
                    retryFlowCount: retryCount,
                    typeDimensionValue: typeDimensionValue,
                    httpStatusCode: response.Error.HttpStatusCode,
                    proxyClientError: ResourceProxyClientError.PROXY_RETURN_ERROR_RESPONSE,
                    errorType: SolutionUtils.ConvertProxyErrorToDataLabsErrorType(response.Error.Type),
                    errorMessage: response.Error.Message,
                    retryAfter: response.Error.RetryAfter,
                    failedComponent: response.Error.FailedComponent,
                    proxyDataSource: response.Error.DataSource);
            }
            else
            {
                throw new InvalidOperationException("Unrecognized Response Type");
            }
        }

        private static DataLabsARMAdminResponse CreateARMAdminErrorResponse(
            string correlationId,
            string callMethod,
            int retryFlowCount,
            string? typeDimensionValue,
            int httpStatusCode,
            ResourceProxyClientError proxyClientError,
            DataLabsErrorType errorType,
            string errorMessage,
            int retryAfter,
            string failedComponent,
            ProxyDataSource proxyDataSource)
        {
            var errorResponse = CreateErrorResponse(
                callMethod: callMethod,
                retryFlowCount: retryFlowCount,
                typeDimensionValue: typeDimensionValue,
                httpStatusCode: httpStatusCode,
                proxyClientError: proxyClientError,
                errorType: errorType,
                errorMessage: errorMessage,
                retryAfter: retryAfter,
                failedComponent: failedComponent,
                proxyDataSource: proxyDataSource);

            return new DataLabsARMAdminResponse(
                responseTime: DateTimeOffset.UtcNow,
                correlationId: correlationId,
                successResponse: null,
                errorResponse: errorResponse,
                attributes: null,
                dataSource: SolutionUtils.ConvertProxyDataSourceToDataLabsDataSource(proxyDataSource));
        }
    }
}