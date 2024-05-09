namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
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
        private static readonly ActivityMonitorFactory ResourceProxyClientGetCasResourceAsync =
            new("ResourceProxyClient.GetCasResourceAsync", useDataLabsEndpoint: true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetCacheKeyForCas(string subscriptionId, string provider)
        {
            return CasResourceType + "/" + subscriptionId + "/" + provider;
        }

        public async Task<DataLabsCasResponse> GetCasResponseAsync(
           DataLabsCasRequest request,
           CancellationToken cancellationToken,
           bool skipCacheRead = false,
           bool skipCacheWrite = false,
           string? scenario = null,
           string? component = null)
        {
            var callMethod = nameof(GetCasResponseAsync);
            var activityMonitorFactory = ResourceProxyClientGetCasResourceAsync;

            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: component);

            try
            {
                SetInputResourceIdAndCorrelationId(monitor.Activity, inputResourceId: null, correlationId: request.CorrelationId);

                monitor.OnStart(false);

                var timeOut = _timeOutConfigInfo.GetTimeOut(request.RetryCount);
                monitor.Activity[SolutionConstants.TimeOutValue] = timeOut;

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                monitor.Activity[SolutionConstants.TraceId] = request.TraceId;
                monitor.Activity[SolutionConstants.RetryCount] = request.RetryCount;

                var allowedTypesMap = _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(
                    ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes);

                // For now, for CAS, we only support allow or not allow scenario
                var providerConfigList = IsAllowedType(allowedTypesMap: allowedTypesMap, allowedTypeKey: ClientProviderConfigList.AllAllowedSymbol);
                if (providerConfigList == null)
                {
                    // Not Allowed Type
                    monitor.OnError(NotAllowedTypeException);

                    var errorMessage = "GetCasResponse is not Allowed";
                    return CreateCasErrorResponse(
                        correlationId: request.CorrelationId ?? string.Empty,
                        callMethod: callMethod,
                        retryFlowCount: request.RetryCount,
                        typeDimensionValue: null,
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
                        var cacheKey = GetCacheKeyForCas(request.casRequestBody.SubscriptionId, request.casRequestBody.Provider);
                        var (cacheResult, hasException) = await CacheLookupAsync(
                            callMethod: callMethod,
                            cacheProviderConfig: providerConfigList.CacheProviderConfig!,
                            cacheKey: cacheKey,
                            tenantId: null,
                            typeDimensionValue: cacheKey,
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
                        var convertedRequest = ConvertToCasRequest(request: request, scenario: monitor.Activity.Scenario, skipCacheRead: skipCacheRead, skipCacheWrite: skipCacheWrite);

                        DateTime? deadline = null;
                        if (timeOut > TimeSpan.Zero)
                        {
                            deadline = DateTime.UtcNow.Add(timeOut);
                        }
                        resourceResponse = await _client.GetCasAsync(request: convertedRequest, cancellationToken: cancellationToken, deadline: deadline).ConfigureAwait(false);
                    }

                    return ConvertToCasResponse(
                        inputCorrelationId: request.CorrelationId,
                        callMethod: callMethod,
                        retryCount: request.RetryCount,
                        typeDimensionValue: null,
                        response: resourceResponse,
                        monitor: monitor);
                }
                catch (Exception ex)
                {
                    monitor.OnError(ex);

                    var proxyClientError = cancellationToken.IsCancellationRequested ?
                        ResourceProxyClientError.CANCELLATION_REQUESTED : ResourceProxyClientError.INTERNAL_EXCEPTION;

                    return CreateCasErrorResponse(
                        correlationId: request.CorrelationId ?? string.Empty,
                        callMethod: callMethod,
                        retryFlowCount: request.RetryCount,
                        typeDimensionValue: null,
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

                return CreateCasErrorResponse(
                    correlationId: request.CorrelationId ?? string.Empty,
                    callMethod: callMethod,
                    retryFlowCount: request.RetryCount,
                    typeDimensionValue: null,
                    httpStatusCode: 0,
                    proxyClientError: ResourceProxyClientError.INTERNAL_EXCEPTION,
                    errorType: DataLabsErrorType.RETRY,
                    errorMessage: ex.Message,
                    retryAfter: 0,
                    failedComponent: SolutionConstants.ResourceProxyClient,
                    proxyDataSource: ProxyDataSource.None);
            }
        }

        private static CasRequest ConvertToCasRequest(DataLabsCasRequest request, string? scenario, bool skipCacheRead, bool skipCacheWrite)
        {
            var casRequest = new CasRequest()
            {
                TraceId = request.TraceId,
                RetryCount = request.RetryCount,
                RequestEpochTime = request.RequestTime.ToUnixTimeMilliseconds(),
                CorrelationId = request.CorrelationId,
                CasRequestBody = ConvertCasRequestBody(request.casRequestBody),
                SkipCacheRead = skipCacheRead,
                SkipCacheWrite = skipCacheWrite
            };

            if (!string.IsNullOrEmpty(scenario))
            {
                casRequest.ReqAttributes.Add(BasicActivityMonitor.Scenario, scenario);
            }

            return casRequest;
        }

        private static CasProtoRequestBody ConvertCasRequestBody(CasRequestBody requestBody)
        {
            //convert to GRPC proto model
            List<ResourceFetcherProxyService.V1.SubscriptionLocationsAndZones> convertedLocations = new();
            GuardHelper.ArgumentNotNull(requestBody.SubscriptionLocationsAndZones);

            foreach (var item in requestBody.SubscriptionLocationsAndZones)
            {
                GuardHelper.ArgumentNotNull(item.Zones);
                var zones = item.Zones.Select(
                    y => new ResourceFetcherProxyService.V1.Zones()
                    {
                        LogicalZone = y.LogicalZone,
                        PhysicalZone = y.PhysicalZone
                    }
                    ).ToList();
                var capacityLocationAndZones = new ResourceFetcherProxyService.V1.SubscriptionLocationsAndZones
                {
                    Location = item.Location
                };
                capacityLocationAndZones.Zones.Add(zones);

                convertedLocations.Add(capacityLocationAndZones);
            }

            var convertedBillingProperties = new ResourceFetcherProxyService.V1.BillingProperties()
            {
                ChannelType = requestBody.BillingProperties?.ChannelType,
                PaymentType = requestBody.BillingProperties?.PaymentType,
                WorkloadType = requestBody.BillingProperties?.WorkloadType,
                BillingType = requestBody.BillingProperties?.BillingType,
                Tier = requestBody.BillingProperties?.Tier,
                BillingAccount = new ResourceFetcherProxyService.V1.BillingAccount()
                {
                    Id = requestBody.BillingProperties?.BillingAccount?.Id,
                }
            };

            var convertedInternalSubscriptionPolicies = new ResourceFetcherProxyService.V1.InternalSubscriptionPolicies()
            {
                SubscriptionCostCategory = requestBody.InternalSubscriptionPolicies?.SubscriptionCostCategory,
                SubscriptionPcCode = requestBody.InternalSubscriptionPolicies?.SubscriptionPcCode,
                SubscriptionEnvironment = requestBody.InternalSubscriptionPolicies?.SubscriptionEnvironment,
            };

            var convertedRequest = new CasProtoRequestBody()
            {
                SubscriptionId = requestBody.SubscriptionId,
                Provider = requestBody.Provider,
                OfferCategory = requestBody.OfferCategory,
                ClientAppId = requestBody.ClientAppId,
                SubscriptionRegistrationDate = requestBody.SubscriptionRegistrationDate,
                EntitlementStartDate = requestBody.EntitlementStartDate,
                SubscriptionLocationsAndZones = { convertedLocations },
                BillingProperties = convertedBillingProperties,
                InternalSubscriptionPolicies = convertedInternalSubscriptionPolicies
            };

            return convertedRequest;
        }

        private static DataLabsCasResponse ConvertToCasResponse(
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

                if (proxyDataFormat == ProxyDataFormat.Cas)
                {
                    //var casResource = SerializationHelper.Deserialize<DataLabsCasResource>(response.Success.OutputData, false);
                    var successResponse = new DataLabsCasSuccessResponse(outputData.ToStringUtf8(), outputTimestamp: responseTime);
                    var casResponse = new DataLabsCasResponse(
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
                    return casResponse;
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

                return CreateCasErrorResponse(
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
                throw new InvalidOperationException("Unrecognized response type");
            }
        }

        private static DataLabsCasResponse CreateCasErrorResponse(
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

            return new DataLabsCasResponse(
                responseTime: DateTimeOffset.UtcNow,
                correlationId: correlationId,
                successResponse: null,
                errorResponse: errorResponse,
                attributes: null,
                dataSource: SolutionUtils.ConvertProxyDataSourceToDataLabsDataSource(proxyDataSource));
        }
    }
}