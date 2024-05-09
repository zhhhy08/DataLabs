namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;

    public partial class ResourceProxyClient : IResourceProxyClient, IDisposable
    {
        private static readonly ActivityMonitorFactory ResourceProxyClientGetResourceAsync = 
            new("ResourceProxyClient.GetResourceAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory ResourceProxyClientCacheCollisionCheckFail =
            new("ResourceProxyClient.CacheCollisionCheckFail", useDataLabsEndpoint: true);

        private static readonly CacheCollisionException _cacheCollisionException = new("Cache Content has collision");

        public async Task<DataLabsResourceResponse> GetResourceAsync(
            DataLabsResourceRequest request,
            CancellationToken cancellationToken,
            bool getDeletedResource,
            bool skipCacheRead = false,
            bool skipCacheWrite = false,
            string? scenario = null,
            string? component = null)
        {
            var callMethod = nameof(GetResourceAsync);
            var activityMonitorFactory = ResourceProxyClientGetResourceAsync;
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

                resourceType = ArmUtils.GetResourceType(request.ResourceId);

                monitor.Activity[SolutionConstants.TraceId] = request.TraceId;
                monitor.Activity[SolutionConstants.TenantId] = request.TenantId;
                monitor.Activity["RegionNameNotNull"] = string.IsNullOrEmpty(request.RegionName) ? "false" : "true";
                monitor.Activity[SolutionConstants.RegionName] = string.IsNullOrEmpty(request.RegionName) ? _defaultRegionName : request.RegionName;
                monitor.Activity[SolutionConstants.RetryCount] = request.RetryCount;
                monitor.Activity[SolutionConstants.ResourceType] = resourceType;

                var allowedTypesMap = _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(
                    ResourceProxyAllowedConfigType.GetResourceAllowedTypes);

                var providerConfigList = IsAllowedType(allowedTypesMap: allowedTypesMap, allowedTypeKey: resourceType);
                if (providerConfigList == null)
                {
                    // Not Allowed Type
                    monitor.OnError(NotAllowedTypeException);

                    var errorMessage = "NotAllowedType: " + resourceType;
                    return CreateResourceErrorResponse(
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
                        resourceResponse = await _client.GetResourceAsync(resourceRequest, cancellationToken: cancellationToken, deadline: deadline).ConfigureAwait(false);
                    }

                    var dlResourceResponse = ConvertToResourceResponse(
                        inputCorrelationId: request.CorrelationId,
                        callMethod: callMethod,
                        retryCount: request.RetryCount,
                        typeDimensionValue: resourceType,
                        response: resourceResponse,
                        getDeletedResource: getDeletedResource,
                        monitor: monitor);

                    CacheCollisionCheck(dlResourceResponse, request.ResourceId, request.TenantId, _resourceCacheClient);

                    return dlResourceResponse;
                }
                catch (Exception ex)
                {
                    monitor.OnError(ex);

                    var proxyClientError = cancellationToken.IsCancellationRequested ?
                        ResourceProxyClientError.CANCELLATION_REQUESTED : ResourceProxyClientError.INTERNAL_EXCEPTION;

                    return CreateResourceErrorResponse(
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

                return CreateResourceErrorResponse(
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
        
        private ResourceRequest ConvertToResourceRequest(
            DataLabsResourceRequest request,
            string? scenario, 
            bool skipCacheRead, 
            bool skipCacheWrite)
        {
            var resourceRequest = new ResourceRequest()
            {
                TraceId = request.TraceId,
                RetryCount = request.RetryCount,
                RequestEpochTime = request.RequestTime.ToUnixTimeMilliseconds(),
                CorrelationId = request.CorrelationId,
                ResourceId = request.ResourceId,
                TenantId = request.TenantId,
                SkipCacheRead = skipCacheRead,
                SkipCacheWrite = skipCacheWrite,
                RegionName = request.RegionName ?? _defaultRegionName
            };

            if (!string.IsNullOrEmpty(scenario))
            {
                resourceRequest.ReqAttributes.Add(BasicActivityMonitor.Scenario, scenario);
            }

            return resourceRequest;
        }

        private static bool CacheCollisionCheck(DataLabsResourceResponse dataLabsResourceResponse, 
            string originalResourceId, string? originalTenantId,
            IResourceCacheClient? resourceCacheClient)
        {
            if (string.IsNullOrWhiteSpace(originalResourceId) ||
                (dataLabsResourceResponse?.DataSource != DataLabsDataSource.CACHE))
            {
                return false;
            }

            bool hasCollision = false;
            string? collidingResourceId = null;
            if (dataLabsResourceResponse.SuccessARNV3Response != null)
            {
                // ARNV3 
                var eventV3 = dataLabsResourceResponse.SuccessARNV3Response.Resource?.Data;
                if (eventV3?.Resources?.Count > 0)
                {
                    var firstResource = eventV3.Resources[0];
                    var cacheV3ResourceId = firstResource.ResourceId; // ARN V3 ResourceId
                    var armId = firstResource.ArmResource?.Id;

                    var hasV3ResourceId = !string.IsNullOrWhiteSpace(cacheV3ResourceId);
                    var hasArmId = !string.IsNullOrWhiteSpace(armId);

                    if (!hasV3ResourceId && !hasArmId)
                    {
                        // Not valid ARM Id for some reason
                        return false;
                    }

                    // ARN V3 ResourceId comparison
                    if (hasV3ResourceId &&
                        cacheV3ResourceId.Equals(originalResourceId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Match
                        return false;
                    }

                    // ARM Id comparison
                    if (hasArmId &&
                        armId!.Equals(originalResourceId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Match
                        return false;
                    }

                    hasCollision = true;
                    collidingResourceId = hasV3ResourceId ? cacheV3ResourceId : armId;
                }
            }
            else if (dataLabsResourceResponse.SuccessARMResponse != null)
            {
                var resource = dataLabsResourceResponse.SuccessARMResponse.Resource;
                if (resource == null)
                {
                    return false;
                }

                var armId = resource.Id;

                // ARM Id comparison
                if (string.IsNullOrWhiteSpace(armId) ||
                    armId.Equals(originalResourceId, StringComparison.OrdinalIgnoreCase))
                {
                    // Match
                    return false;
                }

                hasCollision = true;
                collidingResourceId = armId;
            }

            if (hasCollision)
            {
                // Hash Collision
                // Let's log this event and just return the response for now because it might be false negative
                // But we need to investigate this issue with monitoring
                using var monitor = ResourceProxyClientCacheCollisionCheckFail.ToMonitor();
                monitor.Activity[SolutionConstants.ResourceId] = originalResourceId;
                monitor.Activity[SolutionConstants.CollidingResourceId] = collidingResourceId;

                if (resourceCacheClient != null)
                {
                    var cacheKey = resourceCacheClient.GetCacheKey(originalResourceId, originalTenantId);
                    monitor.Activity[SolutionConstants.CacheKey] = cacheKey;
                }

                monitor.OnError(_cacheCollisionException);
            }

            return hasCollision;
        }

        private static DataLabsResourceResponse ConvertToResourceResponse(
            string? inputCorrelationId,
            string callMethod,
            int retryCount,
            string? typeDimensionValue,
            ResourceResponse response,
            bool getDeletedResource,
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

                if (proxyDataFormat == ProxyDataFormat.Arn)
                {
                    // ARN V3
                    if (outputData.Length == 0)
                    {
                        throw new InvalidOperationException("Success OutputData is empty");
                    }

                    var notifications = SerializationHelper.DeserializeArnV3Notification(outputData, false);
                    if (notifications == null || notifications.Length == 0)
                    {
                        throw new InvalidOperationException("Success OutputData is empty");
                    }

                    var eventGridNotification = notifications[0];

                    // If this is a deleted resource, partners may choose to get a NotFound message.
                    if (!getDeletedResource &&
                        eventGridNotification.EventType.EndsWith("delete", StringComparison.OrdinalIgnoreCase))
                    {
                        monitor.Activity[SolutionConstants.DeleteToNotFound] = true;

                        monitor.OnError(ConvertDeleteToNotFoundException);

                        // Convert Delete to Not Found
                        return CreateResourceErrorResponse(
                            correlationId: correlationId ?? string.Empty,
                            callMethod: callMethod,
                            retryFlowCount: retryCount,
                            typeDimensionValue: typeDimensionValue,
                            httpStatusCode: (int)HttpStatusCode.NotFound,
                            proxyClientError: ResourceProxyClientError.DELETE_TO_NOT_FOUND,
                            errorType: DataLabsErrorType.RETRY,
                            errorMessage: ResourceProxyClientError.DELETE_TO_NOT_FOUND.FastEnumToString(),
                            retryAfter: 0,
                            failedComponent: SolutionConstants.ResourceProxyClient,
                            proxyDataSource: proxyDataSource);
                    }

                    var outputTimeStamp = eventGridNotification.EventTime;
                    var arnV3SuccessResponse = new DataLabsARNV3SuccessResponse(resource: eventGridNotification, outputTimestamp: outputTimeStamp, eTag: eTag);
                    var resourceResponse = new DataLabsResourceResponse(
                        responseTime: responseTime,
                        correlationId: correlationId,
                        successARNV3Response: arnV3SuccessResponse,
                        successARMResponse: null,
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
                    return resourceResponse;

                }
                else if (proxyDataFormat == ProxyDataFormat.Arm)
                {
                    // ARM
                    var armResource = outputData.Length > 0 ? SerializationHelper.Deserialize<GenericResource>(outputData, false) : null;
                    var armSuccessResponse = new DataLabsARMSuccessResponse(armResource, DateTimeOffset.UtcNow);
                    var resourceResponse = new DataLabsResourceResponse(
                       responseTime: responseTime,
                       correlationId: correlationId,
                       successARNV3Response: null,
                       successARMResponse: armSuccessResponse,
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
                    return resourceResponse;
                }
                else
                {
                    // Unsupported dataFormat
                    throw new InvalidOperationException("Not Expected DataFormat: " + proxyDataFormat.FastEnumToString());
                }
            }
            else if (response.Error != null)
            {
                monitor.Activity[SolutionConstants.HasError] = true;
                monitor.OnError(ProxyReturnErrorResponseException);

                return CreateResourceErrorResponse(
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

        private static DataLabsResourceResponse CreateResourceErrorResponse(
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

            return new DataLabsResourceResponse(
                responseTime: DateTimeOffset.UtcNow,
                correlationId: correlationId,
                successARNV3Response: null,
                successARMResponse: null,
                errorResponse: errorResponse,
                attributes: null,
                dataSource: SolutionUtils.ConvertProxyDataSourceToDataLabsDataSource(proxyDataSource));
        }
    }
}