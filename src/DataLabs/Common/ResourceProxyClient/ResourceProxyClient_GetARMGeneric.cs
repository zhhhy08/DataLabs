namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient
{
    using System;
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
        private static readonly ActivityMonitorFactory ResourceProxyClientCallARMGenericRequestAsync = 
            new ("ResourceProxyClient.CallARMGenericRequestAsync", useDataLabsEndpoint: true);

        public async Task<DataLabsARMGenericResponse> CallARMGenericRequestAsync(
           DataLabsARMGenericRequest request,
           CancellationToken cancellationToken,
           bool skipCacheRead = false,
           bool skipCacheWrite = false,
           string? scenario = null,
           string? component = null)
        {
            var callMethod = nameof(CallARMGenericRequestAsync);
            var activityMonitorFactory = ResourceProxyClientCallARMGenericRequestAsync;

            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: component);

            try
            {
                SetInputResourceIdAndCorrelationId(monitor.Activity, inputResourceId: request.URIPath, correlationId: request.CorrelationId);

                monitor.OnStart(false);

                var timeOut = _timeOutConfigInfo.GetTimeOut(request.RetryCount);
                monitor.Activity[SolutionConstants.TimeOutValue] = timeOut;

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                monitor.Activity[SolutionConstants.TraceId] = request.TraceId;
                monitor.Activity[SolutionConstants.URIPath] = request.URIPath;
                monitor.Activity[SolutionConstants.RetryCount] = request.RetryCount;

                var allowedTypesMap = _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(
                    ResourceProxyAllowedConfigType.CallARMGenericRequestAllowedTypes);

                var providerConfigList = IsAllowedType(allowedTypesMap: allowedTypesMap, allowedTypeKey: request.URIPath);
                if (providerConfigList == null)
                {
                    // Not Allowed Type
                    monitor.OnError(NotAllowedTypeException);

                    var errorMessage = "NotAllowedType: " + request.URIPath;
                    return CreateARMGenericErrorResponse(
                        correlationId: request.CorrelationId ?? string.Empty,
                        callMethod: callMethod,
                        retryFlowCount: request.RetryCount,
                        typeDimensionValue: request.URIPath,
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
                    // TODO
                    // Cache Support for ARM Generic request??
                    var convertedRequest = ConvertToARMGenericRequest(request: request, scenario: monitor.Activity.Scenario, skipCacheRead: skipCacheRead, skipCacheWrite: skipCacheWrite);

                    DateTime? deadline = null;
                    if (timeOut > TimeSpan.Zero)
                    {
                        deadline = DateTime.UtcNow.Add(timeOut);
                    }
                    var response = await _client.GetARMGenericResourceAsync(request: convertedRequest, cancellationToken: cancellationToken, deadline: deadline).ConfigureAwait(false);

                    return ConvertToARMGenericResponse(
                        inputCorrelationId: request.CorrelationId,
                        callMethod: callMethod,
                        retryCount: request.RetryCount,
                        typeDimensionValue: request.URIPath,
                        response: response,
                        monitor: monitor);
                }
                catch (Exception ex)
                {
                    monitor.OnError(ex);

                    var proxyClientError = cancellationToken.IsCancellationRequested ?
                        ResourceProxyClientError.CANCELLATION_REQUESTED : ResourceProxyClientError.INTERNAL_EXCEPTION;

                    return CreateARMGenericErrorResponse(
                        correlationId: request.CorrelationId ?? string.Empty,
                        callMethod: callMethod,
                        retryFlowCount: request.RetryCount,
                        typeDimensionValue: request.URIPath,
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

                return CreateARMGenericErrorResponse(
                    correlationId: request.CorrelationId ?? string.Empty,
                    callMethod: callMethod,
                    retryFlowCount: request.RetryCount,
                    typeDimensionValue: request.URIPath,
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
        private static ARMGenericRequest ConvertToARMGenericRequest(
            DataLabsARMGenericRequest request, 
            string? scenario, 
            bool skipCacheRead, 
            bool skipCacheWrite)
        {
            var genericRequest = new ARMGenericRequest()
            {
                TraceId = request.TraceId,
                RetryCount = request.RetryCount,
                RequestEpochTime = request.RequestTime.ToUnixTimeMilliseconds(),
                CorrelationId = request.CorrelationId,
                UriPath = request.URIPath,
                TenantId = request.TenantId,
                SkipCacheRead = skipCacheRead,
                SkipCacheWrite = skipCacheWrite
            };

            if (request.QueryParams != null)
            {
                genericRequest.QueryParams.Add(request.QueryParams);
            }

            if (!string.IsNullOrEmpty(scenario))
            {
                genericRequest.ReqAttributes.Add(BasicActivityMonitor.Scenario, scenario);
            }

            return genericRequest;
        }

        private static DataLabsARMGenericResponse ConvertToARMGenericResponse(
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

                if (proxyDataFormat == ProxyDataFormat.Arm)
                {
                    var successResponse = new DataLabsARMGenericSuccessResponse(outputData.ToStringUtf8(), outputTimestamp: responseTime);
                    var armGenericResponse = new DataLabsARMGenericResponse(
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
                    return armGenericResponse;
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

                return CreateARMGenericErrorResponse(
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

        private static DataLabsARMGenericResponse CreateARMGenericErrorResponse(
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

            return new DataLabsARMGenericResponse(
                responseTime: DateTimeOffset.UtcNow,
                correlationId: correlationId,
                successResponse: null,
                errorResponse: errorResponse,
                attributes: null,
                dataSource: SolutionUtils.ConvertProxyDataSourceToDataLabsDataSource(proxyDataSource));
        }
    }
}