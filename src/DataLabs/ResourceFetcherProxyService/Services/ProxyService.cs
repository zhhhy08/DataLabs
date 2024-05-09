namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Services
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Google.Protobuf;
    using Google.Protobuf.Collections;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientTimeOutManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Monitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Cache;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Manager;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Utils;

    internal partial class ProxyService : ResourceProxyService.ResourceProxyServiceBase
    {
        private static readonly ILogger<ProxyService> Logger = DataLabLoggerFactory.CreateLogger<ProxyService>();

        private delegate Task<HttpResponseMessage> ClientDelegateFunc<T>(T client, object arg, string? apiVersion, CancellationToken cancellationToken);
        private delegate TimeSpan? ClientTimeOutDelegate(ClientProviderType providerType, string callMethod, string allowedType, int retryFlowCount);

        private const string ProxyFlowTimeOutString = "20/60";

        private readonly static TimeOutConfigInfo _timeOutConfigInfo;
        private static bool _useOutputCacheForRetry;
        private static bool _useInputCacheForRetry;

        private static readonly ArmAdminClientTimeOutManager _armAdminClientTimeOutManager;
        private static readonly ArmClientTimeOutManager _armClientTimeOutManager;
        private static readonly CasClientTimeOutManager _casClientTimeOutManager;
        private static readonly QFDClientTimeOutManager _qfdClientTimeOutManager;

        private readonly IClientProvidersManager _clientProvidersManager;

        static ProxyService()
        {
            _timeOutConfigInfo = new TimeOutConfigInfo(SolutionConstants.ResourceFetcherProxyMaxTimeOutInSec, ProxyFlowTimeOutString, ConfigMapUtil.Configuration);

            _useOutputCacheForRetry = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(SolutionConstants.UseOutputCacheForRetry, UpdateUseOutputCacheForRetry, false);
            _useInputCacheForRetry = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(SolutionConstants.UseInputCacheForRetry, UpdateUseInputCacheForRetry, true);

            _armAdminClientTimeOutManager = ArmAdminClientTimeOutManager.Create(ConfigMapUtil.Configuration);
            _armClientTimeOutManager = ArmClientTimeOutManager.Create(ConfigMapUtil.Configuration);
            _casClientTimeOutManager = CasClientTimeOutManager.Create(ConfigMapUtil.Configuration);
            _qfdClientTimeOutManager = QFDClientTimeOutManager.Create(ConfigMapUtil.Configuration);
        }

        public ProxyService(IClientProvidersManager clientProvidersManager)
        {
            _clientProvidersManager = clientProvidersManager;
        }

        private static void SetResourceFetcherClientContext(string? correlationId, string? otelActivityId, int retryCount)
        {
            var context = new ResourceFetcherClientContext
            {
                CorrelationId = correlationId,
                OpenTelemetryActivityId = otelActivityId,
                RetryCount = retryCount
            };

            ResourceFetcherClientContext.Current = context;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string? ParseScenario(MapField<string, string> reqAttributes)
        {
            if (reqAttributes.TryGetValue(BasicActivityMonitor.Scenario, out var scenarioValue) &&
                !string.IsNullOrEmpty(scenarioValue))
            {
                return scenarioValue;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetCorrelationIdAndResourceId(
            string? correlationId,
            string? resourceIdColumnValue,
            OpenTelemetryActivityWrapper otelActivity,
            IActivity activity)
        {
            // Set CorrelationId and Resource Id
            otelActivity.InputCorrelationId = correlationId;
            otelActivity.InputResourceId = resourceIdColumnValue;

            activity.CorrelationId = correlationId;
            activity.InputResourceId = resourceIdColumnValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCacheEntryExpired(
            CacheClientProvider cacheClientProvider,
            RFProxyHttpResponseMessage rfProxyHttpResponseMessage,
            OpenTelemetryActivityWrapper otelActivity,
            IActivity activity)
        {
            // Check cache Read TTL. let's compare whether cache entry is expired or not based on readTTL
            var insertionTimeStamp = rfProxyHttpResponseMessage.InsertionTimeStamp;
            var cacheDataFormat = rfProxyHttpResponseMessage.DataFormat;
            if (cacheClientProvider.IsCacheEntryExpired(cacheDataFormat: cacheDataFormat, insertionTime: insertionTimeStamp, activity: activity))
            {
                otelActivity.SetTag(SolutionConstants.ExpiredCacheEntry, true);
                activity[SolutionConstants.InsertionTimeStamp] = insertionTimeStamp;
                return true;
            }
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool NeedToSkipClientProvider(ClientProviderType providerType, bool skipCacheRead, int retryCount, bool isOutputType = false)
        {
            if (providerType == ClientProviderType.Cache)
            {
                if (skipCacheRead ||
                    (retryCount > 0 &&
                        (isOutputType && !_useOutputCacheForRetry) ||
                        (!isOutputType && !_useInputCacheForRetry)))
                {
                    return true;
                }
            }
            return false;
        }

        private async Task<ResourceResponse?> CheckExistingARMThrottleLimitAsync(
            string subscriptionId,
            string correlationId,
            string callMethod,
            int retryFlowCount,
            string? typeDimensionValue,
            OpenTelemetryActivityWrapper otelActivity,
            IActivityMonitor monitor,
            CancellationToken cancellationToken)
        {
            var rateLimitExist = await _clientProvidersManager.ArmThrottleManager.IsSubscriptionRateLimitExistAsync(
                subscriptionId: subscriptionId,
                activity: monitor.Activity,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!rateLimitExist)
            {
                return null;
            }

            // Throttle Exist -> Retry
            var errorMessage = "ThrottleExistInCache: " + subscriptionId;
            return CreateErrorResponse(
                correlationId: correlationId,
                callMethod: callMethod,
                retryFlowCount: retryFlowCount,
                typeDimensionValue: typeDimensionValue,
                httpStatusCode: (int)HttpStatusCode.TooManyRequests,
                providerType: ClientProviderType.Cache, // We get throttling info from cache
                proxyError: ResourceFetcherProxyError.THROTTLE_EXIST_IN_CACHE,
                errorType: ProxyErrorType.Retry,
                errorMessage: errorMessage,
                retryAfter: (int)_clientProvidersManager.ArmThrottleManager.ARMSubscriptionBackoffMilliseconds,
                exception: ResourceFetcherProxyConstants.ThrottleExistInCacheException,
                otelActivity: otelActivity,
                monitor: monitor);
        }

        private async Task ParseARMThrottleHeaderAndUpdateInCacheAsync(
            string subscriptionId,
            string? resourceType,
            HttpResponseMessage httpResponseMessage,
            OpenTelemetryActivityWrapper activity,
            CancellationToken cancellationToken)
        {
            var httpStatusCode = httpResponseMessage.StatusCode;
            activity.SetTag(SolutionConstants.HttpStatusCode, (int)httpStatusCode);

            int armReadSafeLimit = _clientProvidersManager.ArmThrottleManager.ARMSubscriptionMinReadsRemaining;
            var hasRemainingReadHeader = _clientProvidersManager.ArmThrottleManager.TryParseSubscriptionRateLimitHeader(httpResponseMessage.Headers, out var remainingRead);

            // Parse ARM Throttle Header and Update it in ARMThrottleManager which will save it in cache
            if (hasRemainingReadHeader)
            {
                // Let's log the arm header if any
                activity.SetTag(SolutionConstants.SubscriptionARMReadRemaining, remainingRead);
                activity.SetTag(SolutionConstants.SubscriptionARMReadSafeLimit, armReadSafeLimit);

                // Let's record the remaining read in metric.
                ResourceFetcherProxyMetricProvider.RecordARMRemainingSubscriptionReadsMetric(
                    remainingSubscriptionReads: remainingRead,
                    httpStatusCode: httpStatusCode,
                    resourceType: resourceType);
            }

            var needToAddThrottle = 
                (httpStatusCode == HttpStatusCode.TooManyRequests) ||
                (hasRemainingReadHeader && (remainingRead < armReadSafeLimit));

            // Metric for arm header
            if (!string.IsNullOrEmpty(subscriptionId) && needToAddThrottle)
            {
                var addedToCache = await _clientProvidersManager.ArmThrottleManager.AddSubscriptionRateLimitAsync(
                    subscriptionId: subscriptionId,
                    currentRemainingRead: remainingRead,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                
                activity.SetTag(SolutionConstants.SubscriptionThrottledAddedToCache, addedToCache);

                TagList tagList = default;
                tagList.Add(SolutionConstants.SubscriptionId, subscriptionId);
                tagList.Add(SolutionConstants.HttpStatusCode, (int)httpStatusCode);
                tagList.Add(SolutionConstants.HasARMRemainingReadHeader, hasRemainingReadHeader);
                tagList.Add(SolutionConstants.SubscriptionARMReadRemaining, remainingRead);
                tagList.Add(SolutionConstants.SubscriptionARMReadSafeLimit, armReadSafeLimit);
                tagList.Add(SolutionConstants.SubscriptionThrottledAddedToCache, addedToCache);

                activity.AddEvent(SolutionConstants.SubscriptionThrottledAddedToCache, tagList);
            }
        }

        private static async ValueTask<ResourceResponse> CreateSuccessResponseAsync<T>(
            string correlationId,
            string? cacheKey,
            string? tenantId,
            string? resourceType,
            string? typeDimensionValue,
            string callMethod,
            int retryFlowCount,
            HttpResponseMessage httpResponseMessage,
            IClientProvider<T> clientProvider,
            ResourceCacheDataFormat defaultDataFormat,
            bool skipCacheWrite,
            OpenTelemetryActivityWrapper otelActivity,
            IActivityMonitor monitor,
            CancellationToken cancellationToken)
        {
            GuardHelper.ArgumentConstraintCheck(httpResponseMessage.StatusCode == HttpStatusCode.OK);

            var providerType = clientProvider.ProviderType;
            var providerTypeName = providerType.FastEnumToString();

            // Get other metaData if it is avaiable
            string? dataETag = null;
            ResourceCacheDataFormat dataFormat = defaultDataFormat;
            long dataTimeStamp = 0;
            long? insertionTimeStamp = null;

            if (httpResponseMessage is RFProxyHttpResponseMessage rfProxyHttpResponseMessage)
            {
                dataETag = rfProxyHttpResponseMessage.DataETag;
                dataFormat = rfProxyHttpResponseMessage.DataFormat;
                dataTimeStamp = rfProxyHttpResponseMessage.DataTimeStamp;
                insertionTimeStamp = rfProxyHttpResponseMessage.InsertionTimeStamp;
            }

            if (dataFormat == ResourceCacheDataFormat.NotFoundEntry)
            {
                return CreateErrorResponse(
                    correlationId: correlationId,
                    callMethod: callMethod,
                    retryFlowCount: retryFlowCount,
                    typeDimensionValue: typeDimensionValue,
                    httpStatusCode: (int)HttpStatusCode.NotFound,
                    providerType: providerType,
                    proxyError: ResourceFetcherProxyError.NOTFOUND_ENTRY_EXIST_IN_CACHE,
                    errorType: ProxyErrorType.Retry,
                    errorMessage: SolutionConstants.NotFoundEntryExistInCache,
                    retryAfter: 0,
                    exception: ResourceFetcherProxyConstants.NotFoundEntryExistInCacheException,
                    otelActivity: otelActivity,
                    monitor: monitor);
            }

            // Get contentBytes
            var contentBytes = await ResourceFetcherProxyUtils.ConvertContentAsBytes(httpResponseMessage.Content).ConfigureAwait(false);

            // If data doesn't come from cache, update cache
            // OutputSourceOfTruth will not be added to cache here. Instead it will be updated inside IO
            if (contentBytes.Length > 0 &&
                cacheKey != null &&
                providerType != ClientProviderType.Cache &&
                providerType != ClientProviderType.OutputSourceoftruth &&
                clientProvider.CacheProvider != null &&
                clientProvider.CacheProvider.CacheProviderConfig.WriteEnabled &&
                !skipCacheWrite)
            {
                var cacheUpdated = await clientProvider.CacheProvider.AddToCacheAsync(
                    cacheKey: cacheKey,
                    tenantId: tenantId,
                    resourceType: resourceType,
                    dataFormat: dataFormat,
                    resource: contentBytes,
                    dataTimeStamp: dataTimeStamp,
                    etag: dataETag,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                otelActivity.SetTag(SolutionConstants.CacheUpdateSuccess, cacheUpdated);
            }

            // Success Metric
            ResourceFetcherProxyMetricProvider.AddRequestSuccessCounter(
                callMethod: callMethod,
                retryFlowCount: retryFlowCount,
                typeDimensionValue: typeDimensionValue,
                providerTypeName: providerTypeName);

            otelActivity.SetStatus(ActivityStatusCode.Ok);
            otelActivity.ExportToActivityMonitor(monitor.Activity);
            monitor.OnCompleted();

            return new ResourceResponse
            {
                ResponseEpochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                CorrelationId = correlationId,
                Success = new SuccessResponse
                {
                    Format = SolutionUtils.ConvertResourceDataFormatToProxyDataFormat(dataFormat),
                    OutputData = UnsafeByteOperations.UnsafeWrap(contentBytes),
                    Etag = dataETag,
                    DataSource = SolutionUtils.ConvertProviderTypeToProxyDataSource(providerType)
                }
            };
        }

        private static ResourceResponse CreateErrorResponse(
            string correlationId,
            string callMethod,
            int retryFlowCount,
            string? typeDimensionValue,
            int httpStatusCode,
            ClientProviderType providerType,
            ResourceFetcherProxyError proxyError,
            ProxyErrorType errorType,
            string errorMessage,
            int retryAfter,
            Exception exception,
            OpenTelemetryActivityWrapper otelActivity,
            IActivityMonitor monitor)
        {
            var providerTypeName = providerType.FastEnumToString();

            ResourceFetcherProxyMetricProvider.AddRequestErrorCounter(
               callMethod: callMethod,
               retryFlowCount: retryFlowCount,
               typeDimensionValue: typeDimensionValue,
               httpStatusCode: httpStatusCode,
               providerTypeName: providerTypeName,
               proxyError: proxyError);

            otelActivity.SetStatus(ActivityStatusCode.Error, errorMessage);
            otelActivity.ExportToActivityMonitor(monitor.Activity);
            monitor.OnError(exception);

            return new ResourceResponse
            {
                ResponseEpochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                CorrelationId = correlationId,
                Error = new ErrorResponse
                {
                    Type = errorType,
                    RetryAfter = retryAfter,
                    HttpStatusCode = httpStatusCode,
                    Message = errorMessage,
                    FailedComponent = providerTypeName,
                    DataSource = SolutionUtils.ConvertProviderTypeToProxyDataSource(providerType)
                }
            };
        }

        private async Task<ResourceResponse> ExecuteAsync<T>(
            ReadOnlyDictionary<string, ClientProviderList<T>> allowedTypesMap,
            ClientDelegateFunc<T> clientDelegateFunc,
            object deletegateFuncArg,
            ClientTimeOutDelegate clientTimeOutDelegate,
            string callMethod,
            string correlationId,
            string resourceIdColumnValue,
            int retryFlowCount,
            string allowedType,
            ResourceCacheDataFormat defaultDataFormat,
            string? subscriptionId,
            string? tenantId,
            string? resourceType,
            string? typeDimensionValue,
            string? cacheKey,
            bool skipCacheRead,
            bool skipCacheWrite,
            OpenTelemetryActivityWrapper otelActivity,
            IActivityMonitor monitor,
            CancellationToken parentCancellationToken)
        {
            var providerType = ClientProviderType.None;

            try
            {
                // Set Context for ResourceFetcherClient
                SetResourceFetcherClientContext(correlationId: correlationId, otelActivityId: otelActivity.ActivityId, retryCount: retryFlowCount);

                // Set CorrelationId and Resource Id
                SetCorrelationIdAndResourceId(correlationId: correlationId, resourceIdColumnValue: resourceIdColumnValue, otelActivity: otelActivity, activity: monitor.Activity);

                if (!allowedTypesMap.TryGetValue(allowedType, out var clientProviderList)
                    || clientProviderList.ClientProviders.Count == 0)
                {
                    // Not Allowed Type -> Poison
                    var errorMessage = "NotAllowedType: " + allowedType;
                    return CreateErrorResponse(
                        correlationId: correlationId,
                        callMethod: callMethod,
                        retryFlowCount: retryFlowCount,
                        typeDimensionValue: typeDimensionValue,
                        httpStatusCode: 0,
                        providerType: providerType,
                        proxyError: ResourceFetcherProxyError.NOT_ALLOWED_TYPE,
                        errorType: ProxyErrorType.Poison,
                        errorMessage: errorMessage,
                        retryAfter: 0,
                        exception: ResourceFetcherProxyConstants.NotAllowedTypeException,
                        otelActivity: otelActivity,
                        monitor: monitor);
                }

                // TimeOut Related
                var proxyFlowTimeOut = _timeOutConfigInfo.GetTimeOut(retryFlowCount);
                otelActivity.SetTag(SolutionConstants.TimeOutValue, proxyFlowTimeOut);

                using var proxyFlowTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentCancellationToken);
                proxyFlowTokenSource.CancelAfter(proxyFlowTimeOut);
                var proxyFlowCancellationToken = proxyFlowTokenSource.Token;

                var clientProviders = clientProviderList.ClientProviders;
                bool isOutputType = clientProviderList.ProviderConfigList.HasSourceOfTruthProvider;

                HttpResponseMessage? httpResponseMessage = null;

                for (int i = 0; i < clientProviders.Count; i++)
                {
                    // Initialize for each provider
                    var isLastProvider = i == clientProviders.Count - 1;
                    var provider = clientProviders[i];
                    providerType = provider.ProviderType;
                    var providerTypeName = providerType.FastEnumToString();

                    if (NeedToSkipClientProvider(providerType, skipCacheRead, retryFlowCount, isOutputType))
                    {
                        otelActivity.AddEvent(ResourceFetcherProxyUtils.GetClientProviderSkipEventName(providerType));
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(subscriptionId) &&
                        (providerType == ClientProviderType.Arm ||
                         providerType == ClientProviderType.ResourceFetcher_Arm))
                    {
                        // For ARM call, we need to check first if arm subscription is already throttled
                        var errorResponseWithRateLimit = await CheckExistingARMThrottleLimitAsync(
                            subscriptionId: subscriptionId,
                            correlationId: correlationId,
                            callMethod: callMethod,
                            retryFlowCount: retryFlowCount,
                            typeDimensionValue: typeDimensionValue,
                            otelActivity: otelActivity,
                            monitor: monitor,
                            cancellationToken: proxyFlowCancellationToken).ConfigureAwait(false);

                        if (errorResponseWithRateLimit != null)
                        {
                            return errorResponseWithRateLimit;
                        }
                    }

                    // Start Client Call
                    otelActivity.AddEvent(ResourceFetcherProxyUtils.GetClientProviderStartEventName(providerType));

                    var clientTimeOut = clientTimeOutDelegate(providerType: providerType, callMethod: callMethod, allowedType: allowedType, retryFlowCount: retryFlowCount);

                    Exception? clientException = null;
                    bool IsClientTimedOut = false;

                    if (clientTimeOut.HasValue)
                    {
                        otelActivity.SetTag(ResourceFetcherProxyUtils.GetClientProviderTypeTimeOutName(providerType), clientTimeOut);
                        
                        using var clientTokenSource = CancellationTokenSource.CreateLinkedTokenSource(proxyFlowCancellationToken);
                        clientTokenSource.CancelAfter(clientTimeOut.Value);
                        var clientCancellationToken = clientTokenSource.Token;

                        try
                        {
                            httpResponseMessage = await clientDelegateFunc(provider.Client, deletegateFuncArg, provider.ApiVersion, clientCancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            clientException = ex;
                            if (clientCancellationToken.IsCancellationRequested)
                            {
                                IsClientTimedOut = true;
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            httpResponseMessage = await clientDelegateFunc(provider.Client, deletegateFuncArg, provider.ApiVersion, proxyFlowCancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            if (ex is OperationCanceledException && ex.InnerException is TimeoutException)
                            {
                                clientException = ex.InnerException;
                            }
                            else
                            {
                                clientException = ex;
                            }
                        }
                    }

                    if (clientException != null)
                    {
                        httpResponseMessage = null;

                        var errorMessage = clientException.Message;
                        monitor.Activity[providerTypeName] = errorMessage;

                        otelActivity.AddEvent(ResourceFetcherProxyUtils.GetClientProviderExceptionEventName(providerTypeName, errorMessage, clientException));

                        if (!proxyFlowCancellationToken.IsCancellationRequested && !isLastProvider)
                        {
                            // if proxyFlow CancellationToken token is not triggered and it is not last provider, let's continue
                            continue;
                        }

                        // Here we check if it is cancellation token exception
                        var proxyErrorType = ResourceFetcherProxyError.INTERNAL_EXCEPTION;

                        if (parentCancellationToken.IsCancellationRequested)
                        {
                            // Main Cancellation Requested
                            proxyErrorType = ResourceFetcherProxyError.CANCELLATION_REQUESTED;
                        }
                        else if (proxyFlowCancellationToken.IsCancellationRequested)
                        {
                            proxyErrorType = ResourceFetcherProxyError.PROXY_FLOW_TIME_OUT;
                        }
                        else if (IsClientTimedOut)
                        {
                            proxyErrorType = ResourceFetcherProxyError.LAST_CLIENT_TIME_OUT;
                        }

                        return CreateErrorResponse(
                            correlationId: correlationId,
                            callMethod: callMethod,
                            retryFlowCount: retryFlowCount,
                            typeDimensionValue: typeDimensionValue,
                            httpStatusCode: 0,
                            providerType: providerType,
                            proxyError: proxyErrorType,
                            errorType: ProxyErrorType.Retry,
                            errorMessage: errorMessage,
                            retryAfter: 0,
                            exception: clientException,
                            otelActivity: otelActivity,
                            monitor: monitor);
                    }

                    var httpStatusCode = httpResponseMessage!.StatusCode;
                    otelActivity.SetTag(providerTypeName, (int)httpStatusCode);

                    otelActivity.AddEvent(ResourceFetcherProxyUtils.GetClientProviderFinishEventName(providerType, (int)httpStatusCode));

                    if (!string.IsNullOrWhiteSpace(subscriptionId) && 
                        (providerType == ClientProviderType.Arm ||
                         providerType == ClientProviderType.ResourceFetcher_Arm))
                    {
                        await ParseARMThrottleHeaderAndUpdateInCacheAsync(
                            subscriptionId: subscriptionId,
                            resourceType: resourceType,
                            httpResponseMessage: httpResponseMessage,
                            activity: otelActivity,
                            cancellationToken: proxyFlowCancellationToken).ConfigureAwait(false);
                    }

                    if (providerType == ClientProviderType.Cache && httpStatusCode == HttpStatusCode.OK)
                    {
                        if (IsCacheEntryExpired(
                            cacheClientProvider: (CacheClientProvider)provider,
                            rfProxyHttpResponseMessage: (RFProxyHttpResponseMessage)httpResponseMessage,
                            otelActivity: otelActivity,
                            activity: monitor.Activity))
                        {
                            // Cache Entry is expired, Let's consider it like cache entry doesn't exist
                            httpStatusCode = HttpStatusCode.NotFound;
                        }
                    }

                    if (httpStatusCode == HttpStatusCode.OK)
                    {
                        // Success
                        return await CreateSuccessResponseAsync(
                            correlationId: correlationId,
                            cacheKey: cacheKey,   
                            tenantId: tenantId,
                            resourceType: null,
                            typeDimensionValue: typeDimensionValue,
                            callMethod: callMethod,
                            retryFlowCount: retryFlowCount,
                            httpResponseMessage: httpResponseMessage,
                            clientProvider: provider,
                            defaultDataFormat: defaultDataFormat,
                            skipCacheWrite: skipCacheWrite,
                            otelActivity: otelActivity,
                            monitor: monitor,
                            cancellationToken: proxyFlowCancellationToken).ConfigureAwait(false);
                    }

                    if (!isLastProvider)
                    {
                        continue;
                    }
                    else
                    {
                        // LastProvider
                        if (cacheKey != null &&
                            httpStatusCode == HttpStatusCode.NotFound &&
                            providerType != ClientProviderType.Cache &&
                            provider.CacheProvider != null &&
                            provider.CacheProvider.CacheProviderConfig.AddNotFound)
                        {
                            var result = await provider.CacheProvider.AddNotFoundToCacheAsync(
                                cacheKey: cacheKey,
                                tenantId: tenantId,
                                resourceType: resourceType,
                                proxyFlowCancellationToken).ConfigureAwait(false);
                            otelActivity.SetTag(SolutionConstants.AddNotFoundEntryToCache, result);
                        }
                    }
                }

                string notValidResponseMessage = string.Empty;
                if (httpResponseMessage == null)
                {
                    notValidResponseMessage = "No Valid Client Provider";
                }
                else
                {
                    if (httpResponseMessage.Content != null)
                    {
                        notValidResponseMessage = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }

                    if (notValidResponseMessage.Length == 0)
                    {
                        notValidResponseMessage = httpResponseMessage.StatusCode.FastEnumToString();
                    }
                }

                return CreateErrorResponse(
                    correlationId: correlationId,
                    callMethod: callMethod,
                    retryFlowCount: retryFlowCount,
                    typeDimensionValue: typeDimensionValue,
                    httpStatusCode: httpResponseMessage == null ? 0 : (int)httpResponseMessage.StatusCode,
                    providerType: providerType,
                    proxyError: ResourceFetcherProxyError.CLIENT_RESPONSE_CODE,
                    errorType: ProxyErrorType.Retry,
                    errorMessage: notValidResponseMessage,
                    retryAfter: 0,
                    exception: ResourceFetcherProxyConstants.NotValidResponseStatusCodeException,
                    otelActivity: otelActivity,
                    monitor: monitor);
            }
            catch (Exception ex)
            {
                var proxyErrorType = ResourceFetcherProxyError.INTERNAL_EXCEPTION;
                var errorMessage = ex.Message;

                return CreateErrorResponse(
                    correlationId: correlationId,
                    callMethod: callMethod,
                    retryFlowCount: retryFlowCount,
                    typeDimensionValue: typeDimensionValue,
                    httpStatusCode: 0,
                    providerType: providerType,
                    proxyError: proxyErrorType,
                    errorType: ProxyErrorType.Retry,
                    errorMessage: errorMessage,
                    retryAfter: 0,
                    exception: ex,
                    otelActivity: otelActivity,
                    monitor: monitor);
            }
        }

        private static Task UpdateUseOutputCacheForRetry(bool newValue)
        {
            var oldValue = _useOutputCacheForRetry;
            if (oldValue != newValue)
            {
                _useOutputCacheForRetry = newValue;
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.UseOutputCacheForRetry, oldValue, newValue);
            }
            return Task.CompletedTask;
        }

        private static Task UpdateUseInputCacheForRetry(bool newValue)
        {
            var oldValue = _useInputCacheForRetry;
            if (oldValue != newValue)
            {
                _useInputCacheForRetry = newValue;
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.UseInputCacheForRetry, oldValue, newValue);
            }
            return Task.CompletedTask;
        }
    }
}