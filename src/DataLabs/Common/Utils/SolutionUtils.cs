namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;

    public class SolutionUtils
    {
        private static readonly ILogger<SolutionUtils> Logger =
            DataLabLoggerFactory.CreateLogger<SolutionUtils>();

        public const string HttpVersion11 = "1.1";
        public const string HttpVersion20 = "2.0";

        private readonly static IDictionary<string, HttpStatusCode> _statusIntStrToHttpStatus;
        private readonly static IDictionary<int, HttpStatusCode> _statusIntToHttpStatus;
        private readonly static object _configLock = new();

        static SolutionUtils()
        {
            HttpStatusCode[] httpStatusCodes = (HttpStatusCode[])Enum.GetValues(typeof(HttpStatusCode));
            _statusIntStrToHttpStatus = new Dictionary<string, HttpStatusCode>(httpStatusCodes.Length);
            _statusIntToHttpStatus = new Dictionary<int, HttpStatusCode>(httpStatusCodes.Length);

            foreach (var httpStatusCode in httpStatusCodes)
            {
                // We need to call TryAdd because 300 has multiple names
                _statusIntStrToHttpStatus.TryAdd(((int)httpStatusCode).ToString(), httpStatusCode);
                _statusIntToHttpStatus.TryAdd((int)httpStatusCode, httpStatusCode);
            }
        }

        public static void InitializeProgram(IConfiguration configuration, int minWorkerThreads, int minCompletionThreads)
        {
            string defaultMinMaxThread = $"{SolutionConstants.MinWorkerThreads}={minWorkerThreads};{SolutionConstants.MinCompletionPortThreads}={minCompletionThreads}";
            var minMaxThreadString = configuration.GetValueWithCallBack<string>(SolutionConstants.MinMaxThreadsConfig, UpdateMinMaxThreadsConfig, defaultMinMaxThread) ?? string.Empty;
            UpdateMinMaxThreadsConfig(minMaxThreadString);

            ServicePointManager.Expect100Continue = false;
            ServicePointManager.CheckCertificateRevocationList = true;

            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 1024;
            ServicePointManager.ReusePort = true;
        }

        private static Task UpdateMinMaxThreadsConfig(string value)
        {
            // .net 7 Default Max Thread is 32767, 1000
            lock (_configLock)
            {
                var threadConfigMap = value.ConvertToDictionary(caseSensitive: false);

                ThreadPool.GetMinThreads(out var currMinWorkerThreads, out var currMinCompletionPortThreads);
                ThreadPool.GetMaxThreads(out var currMaxWorkerThreads, out var currMaxCompletionPortThreads);

                Logger.LogWarning($"Before Change: currMinWorkerThreads: {currMinWorkerThreads}, currMaxWorkerThreads: {currMaxWorkerThreads}, currMinCompletionPortThreads: {currMinCompletionPortThreads}, currMaxCompletionPortThreads: {currMaxCompletionPortThreads}");
                Console.WriteLine($"Before Change: currMinWorkerThreads: {currMinWorkerThreads}, currMaxWorkerThreads: {currMaxWorkerThreads}, currMinCompletionPortThreads: {currMinCompletionPortThreads}, currMaxCompletionPortThreads: {currMaxCompletionPortThreads}");

                if (threadConfigMap == null || threadConfigMap.Count == 0)
                {
                    return Task.CompletedTask;
                }

                var setMinWorkerThreads = currMinWorkerThreads;
                var setMinCompletionWorkThreads = currMinCompletionPortThreads;
                var setMaxWorkerThreads = currMaxWorkerThreads;
                var setMaxCompletionWorkThreads = currMaxCompletionPortThreads;

                var needToCallSetMin = false;
                if (threadConfigMap.TryGetValue(SolutionConstants.MinWorkerThreads, out var minWorkerThreadsStr) &&
                                   int.TryParse(minWorkerThreadsStr, out var newMinWorkerThreads))
                {
                    if (setMinWorkerThreads != newMinWorkerThreads)
                    {
                        needToCallSetMin = true;
                        setMinWorkerThreads = newMinWorkerThreads;
                    }
                }

                if (threadConfigMap.TryGetValue(SolutionConstants.MinCompletionPortThreads, out var minCompletionPortThreadsStr) &&
                                   int.TryParse(minCompletionPortThreadsStr, out var newMinCompletionPortThreads))
                {
                    if (setMinCompletionWorkThreads != newMinCompletionPortThreads)
                    {
                        needToCallSetMin = true;
                        setMinCompletionWorkThreads = newMinCompletionPortThreads;
                    }
                }

                var needToCallSetMax = false;
                if (threadConfigMap.TryGetValue(SolutionConstants.MaxWorkerThreads, out var maxWorkerThreadsStr) &&
                                                  int.TryParse(maxWorkerThreadsStr, out var newMaxWorkerThreads))
                {
                    if (setMaxWorkerThreads != newMaxWorkerThreads)
                    {
                        needToCallSetMax = true;
                        setMaxWorkerThreads = newMaxWorkerThreads;
                    }
                }

                if (threadConfigMap.TryGetValue(SolutionConstants.MaxCompletionPortThreads, out var maxCompletionPortThreadsStr) &&
                                                  int.TryParse(maxCompletionPortThreadsStr, out var newMaxCompletionPortThreads))
                {
                    if (setMaxCompletionWorkThreads != newMaxCompletionPortThreads)
                    {
                        needToCallSetMax = true;
                        setMaxCompletionWorkThreads = newMaxCompletionPortThreads;
                    }
                }

                if (needToCallSetMin)
                {
                    var isSetMinSuccessful = ThreadPool.SetMinThreads(setMinWorkerThreads, setMinCompletionWorkThreads);
                    if (isSetMinSuccessful)
                    {
                        Logger.LogWarning($"SetMinThreads Succeeded: MinWorkerThreads: {setMinWorkerThreads}, MinCompletionPortThreads: {setMinCompletionWorkThreads}");
                    }
                    else
                    {
                        Logger.LogError($"SetMinThreads: Failed to set MinWorkerThreads: {setMinWorkerThreads}, MinCompletionPortThreads: {setMinCompletionWorkThreads}");
                    }
                }

                if (needToCallSetMax)
                {

                    var isSetMaxSuccessful = ThreadPool.SetMaxThreads(setMaxWorkerThreads, setMaxCompletionWorkThreads);
                    if (isSetMaxSuccessful)
                    {
                        Logger.LogWarning($"SetMaxThreads Succeeded: MaxWorkerThreads: {setMaxWorkerThreads}, MaxCompletionPortThreads: {setMaxCompletionWorkThreads}");
                    }
                    else
                    {
                        Logger.LogError($"SetMaxThreads: Failed to set MaxWorkerThreads: {setMaxWorkerThreads}, MaxCompletionPortThreads: {setMaxCompletionWorkThreads}");
                    }
                }

                ThreadPool.GetMinThreads(out currMinWorkerThreads, out currMinCompletionPortThreads);
                ThreadPool.GetMaxThreads(out currMaxWorkerThreads, out currMaxCompletionPortThreads);

                Logger.LogWarning($"After Change: currMinWorkerThreads: {currMinWorkerThreads}, currMaxWorkerThreads: {currMaxWorkerThreads}, currMinCompletionPortThreads: {currMinCompletionPortThreads}, currMaxCompletionPortThreads: {currMaxCompletionPortThreads}");
                Console.WriteLine($"After Change: currMinWorkerThreads: {currMinWorkerThreads}, currMaxWorkerThreads: {currMaxWorkerThreads}, currMinCompletionPortThreads: {currMinCompletionPortThreads}, currMaxCompletionPortThreads: {currMaxCompletionPortThreads}");

                return Task.CompletedTask;
            }
        }

        public static string GetTypeName(Type type)
        {
            if (!type.IsGenericType || type.IsGenericTypeDefinition)
            {
                return !type.IsGenericTypeDefinition ? type.Name : type.Name.Remove(type.Name.IndexOf('`'));
            }
            else
            {
                var genericTypes = string.Join(',', type.GetGenericArguments().Select(GetTypeName));
                var typeName = GetTypeName(type.GetGenericTypeDefinition());

                return $"{typeName}<{genericTypes}>";
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSubJobResponse(IDictionary<string, string>? respAttributes)
        {
            if (respAttributes?.Count > 0)
            {
                // Parse Internal Response flag
                if (respAttributes.TryGetValue(DataLabsARNV3Response.AttributeKey_SUBJOB, out var attributeVal))
                {
                    // do we need to throw exception if the format is not bool ?
                    if (!bool.TryParse(attributeVal, out var subJobResponse))
                    {
                        // parsing error
                        // Let's assume it as subJob because attribute is explicitly specify
                        return true;
                    }
                    return subJobResponse;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInternalResponse(IDictionary<string, string>? respAttributes)
        {
            if (respAttributes?.Count > 0)
            {
                // Parse Internal Response flag
                if (respAttributes.TryGetValue(DataLabsARNV3Response.AttributeKey_INTERNAL, out var attributeVal))
                {
                    // do we need to throw exception if the format is not bool ?
                    if (!bool.TryParse(attributeVal, out var internalResource))
                    {
                        // parsing error
                        // Let's assume it as internal if there is error
                        return true;
                    }
                    return internalResource;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddQueryParamString(
            IEnumerable<KeyValuePair<string, string>> parameters, 
            StringBuilder stringBuilder)
        {
            if (parameters == null)
            {
                return;
            }

            foreach (var kvp in parameters)
            {
                var encodedValue =  Uri.EscapeDataString(kvp.Value);
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append('&');
                }
                stringBuilder.Append(kvp.Key).Append('=').Append(encodedValue);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddQueryParamString(
            string key,
            string value, 
            StringBuilder stringBuilder)
        {
            var encodedValue = Uri.EscapeDataString(value);
            if (stringBuilder.Length > 0)
            {
                char lastCharacter = stringBuilder[stringBuilder.Length - 1];
                if (lastCharacter != '?' && lastCharacter != '&')
                {
                    stringBuilder.Append('&');
                }
            }
            stringBuilder.Append(key).Append('=').Append(encodedValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? ParseBearerAuthorizationHeader(StringValues authHeader)
        {
            if (authHeader.Count == 0)
            {
                return null;
            }
            else if (authHeader.Count == 1)
            {
                return ParseBearerAuthorizationHeader(authHeader[0]);
            }
            else
            {
                foreach(var header in authHeader)
                {
                    return ParseBearerAuthorizationHeader(header);
                }
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long? ParseARMRemainingSubscriptionReads(StringValues header)
        {
            return ParseARMRemainingSubscriptionReads(header[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long? ParseARMRemainingSubscriptionReads(string? header)
        {
            if (header != null && long.TryParse(header, out var remainingReads))
            {
                return remainingReads;
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? ParseBearerAuthorizationHeader(string? authHeader)
        {
            if (authHeader != null && authHeader.StartsWith("Bearer ", ignoreCase: true, culture: null))
            {
                return authHeader.Substring("Bearer ".Length);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTransientHttpStatusCode(HttpStatusCode httpStatusCode)
        {
            //     • HTTP 5XX status codes (server errors)
            //     • HTTP 408 status code (request timeout)
            return httpStatusCode >= HttpStatusCode.InternalServerError ||
                httpStatusCode == HttpStatusCode.RequestTimeout;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? GetHttpVersionString(Version version)
        {
            if (version == null)
            {
                return null;
            }

            if (version.Equals(HttpVersion.Version11))
            {
                return HttpVersion11;
            }
            else if (version.Equals(HttpVersion.Version20))
            {
                return HttpVersion20;
            }
            else
            {
                return version.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DataLabsErrorType ConvertProxyErrorToDataLabsErrorType(ProxyErrorType proxyError)
        {
            return proxyError switch
            {
                ProxyErrorType.Drop => DataLabsErrorType.DROP,
                ProxyErrorType.Retry => DataLabsErrorType.RETRY,
                ProxyErrorType.Poison => DataLabsErrorType.POISON,
                _ => throw new NotSupportedException("Unsupported Conversion ProxyErrorType: " + proxyError.FastEnumToString())
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DataLabsDataSource ConvertProxyDataSourceToDataLabsDataSource(ProxyDataSource proxyDataSource)
        {
            switch (proxyDataSource)
            {
                case ProxyDataSource.None:
                    return DataLabsDataSource.NONE;
                case ProxyDataSource.Cache:
                    return DataLabsDataSource.CACHE;
                case ProxyDataSource.Arm:
                case ProxyDataSource.ResourceFetcherArm:
                    return DataLabsDataSource.ARM;
                case ProxyDataSource.ArmAdmin:
                case ProxyDataSource.ResourceFetcherArmAdmin:
                    return DataLabsDataSource.ARMADMIN;
                case ProxyDataSource.Qfd:
                case ProxyDataSource.ResourceFetcherQfd:
                    return DataLabsDataSource.QFD;
                case ProxyDataSource.Cas:
                case ProxyDataSource.ResourceFetcherCas:
                    return DataLabsDataSource.CAS;
                case ProxyDataSource.OutputSourceoftruth:
                    return DataLabsDataSource.OUTPUTSOURCEOFTRUTH;
                default:
                    throw new NotSupportedException("Unsupported Conversion ProxyDataSource: " + proxyDataSource.FastEnumToString());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ProxyDataSource ConvertProviderTypeToProxyDataSource(ClientProviderType providerType)
        {
            return providerType switch
            {
                ClientProviderType.None => ProxyDataSource.None,
                ClientProviderType.Cache => ProxyDataSource.Cache,
                ClientProviderType.Arm => ProxyDataSource.Arm,
                ClientProviderType.ArmAdmin => ProxyDataSource.ArmAdmin,
                ClientProviderType.Qfd => ProxyDataSource.Qfd,
                ClientProviderType.Cas => ProxyDataSource.Cas,
                ClientProviderType.ResourceFetcher_Arm => ProxyDataSource.ResourceFetcherArm,
                ClientProviderType.ResourceFetcher_Qfd => ProxyDataSource.ResourceFetcherQfd,
                ClientProviderType.ResourceFetcher_ArmAdmin => ProxyDataSource.ResourceFetcherArmAdmin,
                ClientProviderType.ResourceFetcher_Cas => ProxyDataSource.ResourceFetcherCas,
                ClientProviderType.OutputSourceoftruth => ProxyDataSource.OutputSourceoftruth,
                _ => throw new NotSupportedException("Unsupported Conversion ClientProviderType: " + providerType.FastEnumToString())
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ProxyDataFormat ConvertResourceDataFormatToProxyDataFormat(ResourceCacheDataFormat cacheDataFormat)
        {
            return cacheDataFormat switch
            {
                ResourceCacheDataFormat.ARN => ProxyDataFormat.Arn,
                ResourceCacheDataFormat.ARM => ProxyDataFormat.Arm,
                ResourceCacheDataFormat.CAS => ProxyDataFormat.Cas,
                ResourceCacheDataFormat.ARMAdmin => ProxyDataFormat.Armadmin,
                ResourceCacheDataFormat.PacificCollection => ProxyDataFormat.PacificCollection,
                ResourceCacheDataFormat.IdMapping => ProxyDataFormat.IdMapping,
                _ => throw new NotSupportedException("Unsupported Conversion ResourceCacheDataFormat: " + cacheDataFormat.FastEnumToString())
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ErrorResponse CreateCacheNotFoundErrorResponse()
        {
            return new ErrorResponse
            {
                Type = ProxyErrorType.Retry,
                RetryAfter = 0,
                HttpStatusCode = (int)HttpStatusCode.NotFound,
                Message = SolutionConstants.NotFoundEntryExistInCache,
                FailedComponent = ClientProviderType.Cache.FastEnumToString(),
                DataSource = ProxyDataSource.Cache
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvertIntStatusToHttpStatusCode(string intStatusCode, out HttpStatusCode httpStatusCode)
        {
            return _statusIntStrToHttpStatus.TryGetValue(intStatusCode, out httpStatusCode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvertIntStatusToHttpStatusCode(int intStatusCode, out HttpStatusCode httpStatusCode)
        {
            return _statusIntToHttpStatus.TryGetValue(intStatusCode, out httpStatusCode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetExceptionTypeSimpleName(Exception? ex)
        {
            return ex?.GetType().Name ?? "InternalError";
        }
    }
}
