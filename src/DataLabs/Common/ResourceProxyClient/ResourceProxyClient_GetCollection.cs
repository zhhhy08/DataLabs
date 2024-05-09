namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient
{
    using System;
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
        private static readonly ActivityMonitorFactory ResourceProxyClientGetCollectionAsync = 
            new("ResourceProxyClient.GetCollectionAsync", useDataLabsEndpoint: true);

        public async Task<DataLabsResourceCollectionResponse> GetCollectionAsync(
            DataLabsResourceRequest request,
            CancellationToken cancellationToken,
            bool skipCacheRead,
            bool skipCacheWrite,
            string? scenario,
            string? component)
        {
            var callMethod = nameof(GetCollectionAsync);
            var activityMonitorFactory = ResourceProxyClientGetCollectionAsync;
            string? resourceType = null;

            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: component);

            try
            {
                SetInputResourceIdAndCorrelationId(monitor.Activity, inputResourceId: request.ResourceId, correlationId: request.CorrelationId);

                monitor.OnStart(false);

                var timeOut = _timeOutConfigInfo.GetTimeOut(request.RetryCount);
                monitor.Activity[SolutionConstants.TimeOutValue] = timeOut;

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                resourceType = ArmUtils.GetResourceTypeForCollectionCall(request.ResourceId);

                monitor.Activity[SolutionConstants.TraceId] = request.TraceId;
                monitor.Activity[SolutionConstants.TenantId] = request.TenantId;
                monitor.Activity[SolutionConstants.RetryCount] = request.RetryCount;
                monitor.Activity[SolutionConstants.ResourceType] = resourceType;

                var allowedTypesMap = _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(
                    ResourceProxyAllowedConfigType.GetCollectionAllowedTypes);

                var providerConfigList = IsAllowedType(allowedTypesMap: allowedTypesMap, allowedTypeKey: resourceType);
                if (providerConfigList == null)
                {
                    // Not Allowed Type
                    monitor.OnError(NotAllowedTypeException);

                    var errorMessage = "NotAllowedType: " + resourceType;
                    return CreateResourceCollectionErrorResponse(
                        correlationId: request.CorrelationId ?? string.Empty,
                        callMethod: callMethod,
                        retryFlowCount: request.RetryCount,
                        typeDimensionValue: resourceType,
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
                        var cacheKey = request.ResourceId;
                        var (cacheResult, hasException) = await CacheLookupAsync(
                            callMethod: callMethod,
                            cacheProviderConfig: providerConfigList.CacheProviderConfig!,
                            cacheKey: cacheKey,
                            tenantId: request.TenantId,
                            typeDimensionValue: resourceType,
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
                        var resourceRequest = ConvertToResourceRequest(
                            request: request,
                            scenario: monitor.Activity.Scenario,
                            skipCacheRead: skipCacheRead,
                            skipCacheWrite: skipCacheWrite);

                        DateTime? deadline = null;
                        if (timeOut > TimeSpan.Zero)
                        {
                            deadline = DateTime.UtcNow.Add(timeOut);
                        }

                        resourceResponse = await _client.GetCollectionAsync(resourceRequest, cancellationToken: cancellationToken, deadline: deadline).ConfigureAwait(false);
                    }

                    return ConvertToResourceCollectionResponse(
                        inputCorrelationId: request.CorrelationId,
                        callMethod: callMethod,
                        retryCount: request.RetryCount,
                        typeDimensionValue: resourceType,
                        response: resourceResponse,
                        monitor: monitor);
                }
                catch (Exception ex)
                {
                    monitor.OnError(ex);

                    var proxyClientError = cancellationToken.IsCancellationRequested ?
                        ResourceProxyClientError.CANCELLATION_REQUESTED : ResourceProxyClientError.INTERNAL_EXCEPTION;

                    return CreateResourceCollectionErrorResponse(
                        correlationId: request.CorrelationId ?? string.Empty,
                        callMethod: callMethod,
                        retryFlowCount: request.RetryCount,
                        typeDimensionValue: resourceType,
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

                return CreateResourceCollectionErrorResponse(
                        correlationId: request.CorrelationId ?? string.Empty,
                        callMethod: callMethod,
                        retryFlowCount: request.RetryCount,
                        typeDimensionValue: resourceType,
                        httpStatusCode: 0,
                        proxyClientError: ResourceProxyClientError.INTERNAL_EXCEPTION,
                        errorType: DataLabsErrorType.RETRY,
                        errorMessage: ex.Message,
                        retryAfter: 0,
                        failedComponent: SolutionConstants.ResourceProxyClient,
                        proxyDataSource: ProxyDataSource.None);
            }
        }

        private static DataLabsResourceCollectionResponse ConvertToResourceCollectionResponse(
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

                if (proxyDataFormat == ProxyDataFormat.PacificCollection)
                {
                    var successResponse = SerializationHelper.Deserialize<DataLabsResourceCollectionSuccessResponse>(outputData, false);
                    var collectionResponse = new DataLabsResourceCollectionResponse(
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
                    return collectionResponse;
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

                return CreateResourceCollectionErrorResponse(
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

        private static DataLabsResourceCollectionResponse CreateResourceCollectionErrorResponse(
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

            return new DataLabsResourceCollectionResponse(
                responseTime: DateTimeOffset.UtcNow,
                correlationId: correlationId,
                successResponse: null,
                errorResponse: errorResponse,
                attributes: null,
                dataSource: SolutionUtils.ConvertProxyDataSourceToDataLabsDataSource(proxyDataSource));
        }
    }
}