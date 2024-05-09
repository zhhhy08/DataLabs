namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient
{
    using global::Grpc.Net.Client;
    using Google.Protobuf;
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Grpc;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PodHealth;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using static Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1.ResourceProxyService;

    public partial class ResourceProxyClient : IResourceProxyClient, IDisposable
    {
        private static readonly ILogger<ResourceProxyClient> Logger = DataLabLoggerFactory.CreateLogger<ResourceProxyClient>();

        private static readonly ActivityMonitorFactory ResourceProxyClientCacheLookupAsyncFail = new("ResourceProxyClient.CacheLookupAsyncFail", useDataLabsEndpoint: true);

        public static readonly NotAllowedTypeException NotAllowedTypeException = new("Not Allowed Type");
        public static readonly ConvertDeleteToNotFoundException ConvertDeleteToNotFoundException = new("Convert Delete To NotFound");
        public static readonly ProxyReturnErrorResponseException ProxyReturnErrorResponseException = new("Proxy Returned ErrorResponse");

        public const string ResourceProxyClientSuccessRequestCounterName = "ResourceProxyClientSuccessRequestCounter";
        public const string ResourceProxyClientFailedRequestCounterName = "ResourceProxyClientFailedRequestCounter";
        public const string ResourceProxyClientCacheLookupCounterName = "ResourceProxyClientLookupCounter";

        private static readonly Counter<long> ResourceProxyClientSuccessRequestCounter =
            MetricLogger.CommonMeter.CreateCounter<long>(ResourceProxyClientSuccessRequestCounterName);

        private static readonly Counter<long> ResourceProxyClientFailedRequestCounter =
            MetricLogger.CommonMeter.CreateCounter<long>(ResourceProxyClientFailedRequestCounterName);

        private static readonly Counter<long> ResourceProxyClientCacheLookupCounter =
            MetricLogger.CommonMeter.CreateCounter<long>(ResourceProxyClientCacheLookupCounterName);

        private const string ManifestProviderResourceType = "microsoft.inventory/manifestprovider";
        private const string ConfigSpecsResourceType = "microsoft.inventory/configspecs";
        private const string CasResourceType = "microsoft.capacityallocation/capacityrestrictions";

        private const string ResourceProxyClientDefaultTimeOutString = "20/60";

        private string? _resourceProxyGrpcOptionStr;
        private GrpcChannel _channel;
        private ResourceProxyServiceClient _client;
        private IPodHealthManager _podHealthManager;
        private readonly object _lockObject = new();

        private readonly string? _defaultRegionName;
        private readonly IResourceCacheClient? _resourceCacheClient;
        private readonly TimeOutConfigInfo _timeOutConfigInfo;
        private readonly IResourceProxyAllowedTypesConfigManager _resourceProxyAllowedTypesConfigManager;

        private readonly string? _hostIp;
        private readonly string _grpcHostPort;
        private readonly string? _grpcAddr;

        private bool _useCacheLookupInProxyClient;
        private bool _useOutputCacheForRetry;
        private bool _useInputCacheForRetry;

        private volatile bool _disposed;

        public ResourceProxyClient(
            IResourceProxyAllowedTypesConfigManager resourceProxyAllowedTypesConfigManager, 
            IConfiguration configuration,
            IResourceCacheClient? resourceCacheClient = null,
            HttpClient? httpClient = null)
        {
            _resourceProxyAllowedTypesConfigManager = resourceProxyAllowedTypesConfigManager;
            _resourceCacheClient = resourceCacheClient;

            _defaultRegionName = configuration.GetValue<string>(SolutionConstants.PrimaryRegionName);
            GuardHelper.ArgumentNotNullOrEmpty(_defaultRegionName);

            _hostIp = configuration.GetValue<string>(SolutionConstants.HOST_IP);
            _grpcHostPort = configuration.GetValue<string>(SolutionConstants.ResourceProxyHostPort) ??
                SolutionConstants.ResourceProxyDefaultPort;
            _grpcAddr = configuration.GetValue<string>(SolutionConstants.ResourceProxyAddr);

            var resourceProxyGrpcOptionStr = configuration.GetValueWithCallBack<string>(
               SolutionConstants.ResourceProxyGrpcOption, UpdateResourceProxyGrpcOption, string.Empty);
            _resourceProxyGrpcOptionStr = resourceProxyGrpcOptionStr ?? string.Empty;

            var grpcClientOption = new GrpcClientOption(_resourceProxyGrpcOptionStr.ConvertToDictionary(caseSensitive: false));

            if (!ConfigMapUtil.RunningInContainer)
            {
                if (grpcClientOption.LBPolicy == GrpcLBPolicy.LOCAL)
                {
                    if (string.IsNullOrEmpty(_hostIp))
                    {
                        _hostIp = "localhost";
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(_grpcAddr))
                    {
                        _grpcAddr = "dns:///localhost:5073";
                    }
                }
            }

            var (podHealthManager, channel) = CreateGrpcChannel(
                grpcClientOption: grpcClientOption,
                grpcAddr: _grpcAddr,
                hostIp: _hostIp, 
                hostPort: _grpcHostPort, 
                httpClient: httpClient);

            _podHealthManager = podHealthManager;
            _channel = channel;

            _client = new ResourceProxyServiceClient(_channel);

            Logger.LogInformation("ResourceProxyClient is Created. Policy: {policy}", grpcClientOption.LBPolicy.FastEnumToString());

            _timeOutConfigInfo = new TimeOutConfigInfo(SolutionConstants.ResourceProxyCallMaxTimeOutInSec, ResourceProxyClientDefaultTimeOutString, configuration);
            _useCacheLookupInProxyClient = configuration.GetValueWithCallBack<bool>(SolutionConstants.UseCacheLookupInProxyClient, UpdateUseCacheLookupInProxyClient, true);
            _useOutputCacheForRetry = configuration.GetValueWithCallBack<bool>(SolutionConstants.UseOutputCacheForRetry, UpdateUseOutputCacheForRetry, false);
            _useInputCacheForRetry = configuration.GetValueWithCallBack<bool>(SolutionConstants.UseInputCacheForRetry, UpdateUseInputCacheForRetry, true);
        }

        private Task UpdateResourceProxyGrpcOption(string newVal)
        {
            var configKey = SolutionConstants.ResourceProxyGrpcOption;
            var oldVal = _resourceProxyGrpcOptionStr;
            if (string.IsNullOrWhiteSpace(newVal) || newVal.EqualsInsensitively(oldVal))
            {
                return Task.CompletedTask;
            }

            lock (_lockObject)
            {
                var oldClient = _client;
                var oldChannel = _channel;
                var oldPodHealthManager = _podHealthManager;
                var grpcClientOption = new GrpcClientOption(newVal.ConvertToDictionary(caseSensitive: false));

                try
                {
                    var (podHealthManager, channel) = CreateGrpcChannel(
                        grpcClientOption: grpcClientOption,
                        grpcAddr: _grpcAddr,
                        hostIp: _hostIp,
                        hostPort: _grpcHostPort);

                    var newHealthManager = podHealthManager;
                    var newChannel = channel;
                    var newClient = new ResourceProxyServiceClient(newChannel);

                    if (Interlocked.CompareExchange(ref _client, newClient, oldClient) == oldClient)
                    {
                        _channel = newChannel;
                        _podHealthManager = newHealthManager;
                        _resourceProxyGrpcOptionStr = newVal;

                        Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", configKey, oldVal, newVal);

                        // Dispose Old GrpcChannel after 1 min
                        _ = Task.Run(() => Task.Delay(TimeSpan.FromMinutes(1))
                            .ContinueWith((antecedent, info) => DisposeOldGrpcChannel((GrpcChannel)info!), oldChannel,
                            TaskContinuationOptions.None));
                    }
                    else
                    {
                        // Someone already exchanged 
                        newHealthManager.Dispose();
                        newChannel.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to create new GrpcChannel. {exception}", ex.ToString());
                }

                return Task.CompletedTask;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                try
                {
                    _channel.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Dispose Failed. {exception}", ex.ToString());
                }
            }
        }

        private static void DisposeOldGrpcChannel(GrpcChannel grpcChannel)
        {
            try
            {
                grpcChannel.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to dispose old GrpcChannel");
            }

            Logger.LogWarning("Disposed Old GrpcChannel");
        }

        private static (PodHealthManager podHealthManager, GrpcChannel channel) CreateGrpcChannel(
            GrpcClientOption grpcClientOption, string? grpcAddr, string? hostIp, string? hostPort, 
            HttpClient? httpClient = null)
        {
            if (grpcClientOption.LBPolicy != GrpcLBPolicy.LOCAL)
            {
                if (string.IsNullOrEmpty(grpcAddr))
                {
                    throw new ArgumentException($"GRPC Address is required when GRPC LB Policy is not LOCAL");
                }

                // Not Local
                var podHealthManager = new PodHealthManager(
                    serviceName: grpcAddr,
                    denyListConfigKey: SolutionConstants.ResourceProxyDenyList);

                var channel = GrpcUtils.CreateGrpcChannel(
                    addr: grpcAddr,
                    grpcClientOption: grpcClientOption,
                    podHealthManager: podHealthManager,
                    httpClient: httpClient);

                return (podHealthManager, channel);
            }
            else
            {
                // Local
                if (string.IsNullOrEmpty(hostIp) || string.IsNullOrEmpty(hostPort))
                {
                    throw new ArgumentException($"Host IP and Port are required when GRPC LB Policy is LOCAL");
                }

                var serviceAddr = "http://" + hostIp + ":" + hostPort;
                var podHealthManager = new PodHealthManager(serviceAddr);

                var channel = GrpcUtils.CreateGrpcChannel(
                    hostIp: hostIp,
                    hostPort: hostPort,
                    grpcClientOption: grpcClientOption,
                    podHealthManager: podHealthManager,
                    httpClient: httpClient);

                return (podHealthManager, channel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetInputResourceIdAndCorrelationId(IActivity activity, string? inputResourceId, string? correlationId)
        {
            if (!string.IsNullOrEmpty(inputResourceId))
            {
                activity.InputResourceId = inputResourceId;
            }

            if (!string.IsNullOrEmpty(correlationId))
            {
                activity.CorrelationId = correlationId;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ClientProviderConfigList? IsAllowedType(ReadOnlyDictionary<string, ClientProviderConfigList> allowedTypesMap, string? allowedTypeKey)
        {
            return !string.IsNullOrEmpty(allowedTypeKey) && allowedTypesMap.TryGetValue(allowedTypeKey, out var value) ? value : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanUseCache(ClientProviderConfigList clientProviderConfigList, int retryCount, bool skipCacheRead)
        {
            if (skipCacheRead || !_useCacheLookupInProxyClient || _resourceCacheClient == null || clientProviderConfigList.CacheProviderConfig == null)
            {
                return false;
            }

            var isOutputType = clientProviderConfigList.HasSourceOfTruthProvider;
            return retryCount <= 0 ||
                (isOutputType && _useOutputCacheForRetry) ||
                (!isOutputType && _useInputCacheForRetry);
        }

        private async Task<(ResourceCacheResult cacheResult, bool hasException)> CacheLookupAsync(
            string callMethod,
            CacheClientProviderConfig cacheProviderConfig,
            string cacheKey,
            string? tenantId, 
            string? typeDimensionValue,
            IActivity activity, 
            CancellationToken cancellationToken)
        {

            bool hasException = false;

            try
            {
                activity[SolutionConstants.CacheKey] = cacheKey;
                activity[SolutionConstants.TenantId] = tenantId;
                activity[SolutionConstants.Type] = typeDimensionValue;
                activity[SolutionConstants.CacheCalled] = true;

                var resourceCacheResult = await _resourceCacheClient!.GetResourceAsync(
                    resourceId: cacheKey,
                    tenantId: tenantId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (resourceCacheResult.Found)
                {
                    activity[SolutionConstants.DataFormat] = resourceCacheResult.DataFormat.FastEnumToString();
                    activity[SolutionConstants.CacheHit] = true;

                    if (cacheProviderConfig.IsCacheEntryExpired(
                        cacheDataFormat: resourceCacheResult.DataFormat,
                        insertionTime: resourceCacheResult.InsertionTimeStamp, 
                        activity: activity))
                    {
                        activity[SolutionConstants.ExpiredCacheEntry] = true;
                        return (ResourceCacheResult.NoCacheEntry, hasException);
                    }
                    
                    AddResourceProxyClientCacheLookupHitCounter(callMethod: callMethod, typeDimensionValue: typeDimensionValue, cacheDataFormat: resourceCacheResult.DataFormat);
                    return (resourceCacheResult, hasException);
                }
            }
            catch(Exception ex)
            {
                activity[SolutionConstants.CacheException] = true;
                hasException = true;

                using var failMonitor = ResourceProxyClientCacheLookupAsyncFail.ToMonitor();
                failMonitor.OnError(ex);
                // No throw exception
            }

            activity[SolutionConstants.CacheHit] = false;
            AddResourceProxyClientCacheLookupMissCounter(callMethod: callMethod, typeDimensionValue: typeDimensionValue);
            return (ResourceCacheResult.NoCacheEntry, hasException);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddResourceProxyClientCacheLookupHitCounter(string callMethod, string? typeDimensionValue, ResourceCacheDataFormat cacheDataFormat)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(SolutionConstants.Type, typeDimensionValue ?? string.Empty);
            dimensions.Add(SolutionConstants.DataFormat, cacheDataFormat.FastEnumToString());
            dimensions.Add(SolutionConstants.CacheHit, true);
            ResourceProxyClientCacheLookupCounter.Add(1, dimensions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddResourceProxyClientCacheLookupMissCounter(string callMethod, string? typeDimensionValue)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(SolutionConstants.Type, typeDimensionValue ?? string.Empty);
            dimensions.Add(SolutionConstants.DataFormat, "NONE");
            dimensions.Add(SolutionConstants.CacheHit, false);
            ResourceProxyClientCacheLookupCounter.Add(1, dimensions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddRequestErrorCounter(
            string callMethod,
            int retryFlowCount,
            string? typeDimensionValue,
            int httpStatusCode,
            ResourceProxyClientError proxyError,
            ProxyDataSource proxyDataSource)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(MonitoringConstants.RetryCountDimension, retryFlowCount);
            dimensions.Add(SolutionConstants.Type, typeDimensionValue ?? string.Empty);
            dimensions.Add(SolutionConstants.HttpStatusCode, httpStatusCode);
            dimensions.Add(SolutionConstants.ErrorType, proxyError.FastEnumToString());
            dimensions.Add(SolutionConstants.DataSource, proxyDataSource.FastEnumToString());
            ResourceProxyClientFailedRequestCounter.Add(1, dimensions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddRequestSuccessCounter(
            string callMethod,
            int retryFlowCount,
            string? typeDimensionValue,
            ProxyDataFormat proxyDataFormat,
            ProxyDataSource proxyDataSource)
        {
            TagList dimensions = default;
            dimensions.Add(SolutionConstants.CallMethod, callMethod);
            dimensions.Add(MonitoringConstants.RetryCountDimension, retryFlowCount);
            dimensions.Add(SolutionConstants.Type, typeDimensionValue ?? string.Empty);
            dimensions.Add(SolutionConstants.DataFormat, proxyDataFormat.FastEnumToString());
            dimensions.Add(SolutionConstants.DataSource, proxyDataSource.FastEnumToString());
            ResourceProxyClientSuccessRequestCounter.Add(1, dimensions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ResourceResponse ConvertCacheResultToResourceResponse(
            string? correlationId,
            ResourceCacheResult resourceCacheResult)
        {
            if (resourceCacheResult.DataFormat == ResourceCacheDataFormat.NotFoundEntry)
            {
                // For 404 Not Found
                return new ResourceResponse
                {
                    ResponseEpochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    CorrelationId = correlationId ?? string.Empty,
                    Error = SolutionUtils.CreateCacheNotFoundErrorResponse()
                };
            }
            else
            {
                return new ResourceResponse
                {
                    ResponseEpochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    CorrelationId = correlationId ?? string.Empty,
                    Success = new SuccessResponse
                    {
                        Format = SolutionUtils.ConvertResourceDataFormatToProxyDataFormat(resourceCacheResult.DataFormat),
                        OutputData = UnsafeByteOperations.UnsafeWrap(resourceCacheResult.Content),
                        Etag = resourceCacheResult.Etag,
                        DataSource = ProxyDataSource.Cache
                    }
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DataLabsProxyErrorResponse CreateErrorResponse(
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
            AddRequestErrorCounter(
                callMethod: callMethod, 
                retryFlowCount: retryFlowCount,
                typeDimensionValue: typeDimensionValue, 
                httpStatusCode: httpStatusCode, 
                proxyError: proxyClientError, 
                proxyDataSource: proxyDataSource);

            return new DataLabsProxyErrorResponse(
                errorType: errorType,
                retryDelayInMilliseconds: retryAfter,
                httpStatusCode: httpStatusCode,
                errorDescription: errorMessage,
                failedComponent: failedComponent);
        }

        private Task UpdateUseCacheLookupInProxyClient(bool newValue)
        {
            var oldValue = _useCacheLookupInProxyClient;
            if (oldValue != newValue)
            {
                _useCacheLookupInProxyClient = newValue;
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.UseCacheLookupInProxyClient, oldValue, newValue);
            }
            return Task.CompletedTask;
        }

        private Task UpdateUseOutputCacheForRetry(bool newValue)
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

        private Task UpdateUseInputCacheForRetry(bool newValue)
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