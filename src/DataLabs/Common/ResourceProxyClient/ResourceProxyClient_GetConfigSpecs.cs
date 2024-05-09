namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices.JavaScript;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;

    public partial class ResourceProxyClient : IResourceProxyClient, IDisposable
    {
        private static readonly ActivityMonitorFactory ResourceProxyClientGetConfigSpecsAsync =
            new("ResourceProxyClient.GetConfigSpecsAsync", useDataLabsEndpoint: true);

        public async Task<DataLabsARMAdminResponse> GetConfigSpecsAsync(
            DataLabsConfigSpecsRequest request,
            CancellationToken cancellationToken,
            bool skipCacheRead = false,
            bool skipCacheWrite = false,
            string? scenario = null,
            string? component = null)
        {
            var callMethod = nameof(GetConfigSpecsAsync);
            var activityMonitorFactory = ResourceProxyClientGetConfigSpecsAsync;

            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: component);

            try
            {
                SetInputResourceIdAndCorrelationId(monitor.Activity, inputResourceId: request.ApiExtension, correlationId: request.CorrelationId);

                monitor.OnStart(false);

                var timeOut = _timeOutConfigInfo.GetTimeOut(request.RetryCount);
                monitor.Activity[SolutionConstants.TimeOutValue] = timeOut;

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                monitor.Activity[SolutionConstants.TraceId] = request.TraceId;
                monitor.Activity[SolutionConstants.RetryCount] = request.RetryCount;
                monitor.Activity[SolutionConstants.ApiExtension] = request.ApiExtension;

                var allowedTypesMap = _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(
                    ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes);

                // For now, for GetConfigSpecsAsync, we only support allow or not allow scenario
                var providerConfigList = IsAllowedType(allowedTypesMap: allowedTypesMap, allowedTypeKey: ClientProviderConfigList.AllAllowedSymbol);
                if (providerConfigList == null)
                {
                    // Not Allowed Type
                    monitor.OnError(NotAllowedTypeException);

                    var errorMessage = "GetConfigSpecsAsync is not Allowed";
                    return CreateARMAdminErrorResponse(
                        correlationId: request.CorrelationId ?? string.Empty,
                        callMethod: callMethod,
                        retryFlowCount: request.RetryCount,
                        typeDimensionValue: request.ApiExtension,
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
                        var cacheKey = GetCacheKeyForConfigSpecs(request.ApiExtension);
                        var (cacheResult, hasException) = await CacheLookupAsync(
                            callMethod: callMethod,
                            cacheProviderConfig: providerConfigList.CacheProviderConfig!,
                            cacheKey: cacheKey,
                            tenantId: null,
                            typeDimensionValue: request.ApiExtension,
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
                        var convertedRequest = ConvertToConfigSpecsRequest(request: request, scenario: monitor.Activity.Scenario, skipCacheRead: skipCacheRead, skipCacheWrite: skipCacheWrite);

                        DateTime? deadline = null;
                        if (timeOut > TimeSpan.Zero)
                        {
                            deadline = DateTime.UtcNow.Add(timeOut);
                        }
                        resourceResponse = await _client.GetConfigSpecsAsync(request: convertedRequest, cancellationToken: cancellationToken, deadline: deadline).ConfigureAwait(false);
                    }

                    return ConvertToARMAdminResponse(
                        inputCorrelationId: request.CorrelationId,
                        callMethod: callMethod,
                        retryCount: request.RetryCount,
                        typeDimensionValue: request.ApiExtension,
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
                        typeDimensionValue: request.ApiExtension,
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
                    typeDimensionValue: request.ApiExtension,
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
        public static string GetCacheKeyForConfigSpecs(string apiExtension)
        {
            return ConfigSpecsResourceType + "/" + apiExtension;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ConfigSpecsRequest ConvertToConfigSpecsRequest(
            DataLabsConfigSpecsRequest request,
            string? scenario,
            bool skipCacheRead,
            bool skipCacheWrite)
        {
            var configSpecsRequest = new ConfigSpecsRequest()
            {
                TraceId = request.TraceId,
                RetryCount = request.RetryCount,
                RequestEpochTime = request.RequestTime.ToUnixTimeMilliseconds(),
                CorrelationId = request.CorrelationId,
                ApiExtension = request.ApiExtension,
                SkipCacheRead = skipCacheRead,
                SkipCacheWrite = skipCacheWrite
            };

            if (!string.IsNullOrEmpty(scenario))
            {
                configSpecsRequest.ReqAttributes.Add(BasicActivityMonitor.Scenario, scenario);
            }

            return configSpecsRequest;
        }
    }
}