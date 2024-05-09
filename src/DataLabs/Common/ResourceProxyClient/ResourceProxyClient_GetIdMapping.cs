namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient
{
    using System;
    using System.Collections.Generic;
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
        private static readonly ActivityMonitorFactory ResourceProxyClientGetIdMappingAsync =
            new("ResourceProxyClient.GetIdMappingAsync");

        public async Task<DataLabsIdMappingResponse> GetIdMappingsAsync(
            DataLabsIdMappingRequest request,
            CancellationToken cancellationToken,
            bool skipCacheRead,
            bool skipCacheWrite,
            string? scenario,
            string? component)
        {
            var callMethod = nameof(GetIdMappingsAsync);
            var activityMonitorFactory = ResourceProxyClientGetIdMappingAsync;
            var resourceType = request.ResourceType;

            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: component);

            try
            {
                monitor.OnStart(false);

                var timeOut = _timeOutConfigInfo.GetTimeOut(request.RetryCount);
                monitor.Activity[SolutionConstants.TimeOutValue] = timeOut;

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                monitor.Activity[SolutionConstants.TraceId] = request.TraceId;
                monitor.Activity[SolutionConstants.RetryCount] = request.RetryCount;
                monitor.Activity[SolutionConstants.ResourceType] = resourceType;

                var allowedTypesMap = _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(
                    ResourceProxyAllowedConfigType.GetIdMappingAllowedTypes);

                var providerConfigList = IsAllowedType(allowedTypesMap: allowedTypesMap, allowedTypeKey: ClientProviderConfigList.AllAllowedSymbol);
                if (providerConfigList == null)
                {
                    // Not Allowed Type
                    monitor.OnError(NotAllowedTypeException);

                    var errorMessage = "GetIdMappingAsync is not Allowed";
                    return CreateIdMappingErrorResponse(
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
                    // TODO
                    // Cache Support for IdMapping response?
                    var idMappingRequest = ConvertToIdMappingRequest(
                        request: request,
                        scenario: monitor.Activity.Scenario,
                        skipCacheRead: skipCacheRead,
                        skipCacheWrite: skipCacheWrite);

                    DateTime? deadline = null;
                    if (timeOut > TimeSpan.Zero)
                    {
                        deadline = DateTime.UtcNow.Add(timeOut);
                    }

                    var idMappingResponse = await _client.GetIdMappingsAsync(idMappingRequest, cancellationToken: cancellationToken, deadline: deadline).ConfigureAwait(false);

                    return ConvertToIdMappingResponse(
                        inputCorrelationId: request.CorrelationId,
                        callMethod: callMethod,
                        retryCount: request.RetryCount,
                        typeDimensionValue: resourceType,
                        response: idMappingResponse,
                        monitor: monitor);
                }
                catch (Exception ex)
                {
                    monitor.OnError(ex);

                    var proxyClientError = cancellationToken.IsCancellationRequested ?
                        ResourceProxyClientError.CANCELLATION_REQUESTED : ResourceProxyClientError.INTERNAL_EXCEPTION;

                    return CreateIdMappingErrorResponse(
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

                return CreateIdMappingErrorResponse(
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IdMappingRequest ConvertToIdMappingRequest(
            DataLabsIdMappingRequest request,
            string? scenario,
            bool skipCacheRead,
            bool skipCacheWrite)
        {
            var idMappingRequest = new IdMappingRequest()
            {
                TraceId = request.TraceId,
                RetryCount = request.RetryCount,
                RequestEpochTime = request.RequestTime.ToUnixTimeMilliseconds(),
                CorrelationId = request.CorrelationId,
                ResourceType = request.ResourceType,
                IdMappingProtoRequestBody = ConvertToIdMappingRequestBody(request.IdMappingRequestBody),
                SkipCacheRead = skipCacheRead,
                SkipCacheWrite = skipCacheWrite
            };

            return idMappingRequest;
        }

        private static IdMappingProtoRequestBody ConvertToIdMappingRequestBody(IdMappingRequestBody requestBody)
        {
            //convert to GRPC proto model
            GuardHelper.ArgumentNotNullOrEmpty(requestBody.AliasResourceIds);

            var convertedRequest = new IdMappingProtoRequestBody()
            {
                AliasResourceIds = { requestBody.AliasResourceIds }
            };

            return convertedRequest;
        }

        private static DataLabsIdMappingResponse ConvertToIdMappingResponse(
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

                if (proxyDataFormat == ProxyDataFormat.IdMapping)
                {
                    var successResponse = SerializationHelper.Deserialize<List<IdMapping>>(outputData, false);
                    var idMappingResponse = new DataLabsIdMappingResponse(
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
                    return idMappingResponse;
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

                return CreateIdMappingErrorResponse(
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

        private static DataLabsIdMappingResponse CreateIdMappingErrorResponse(
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

            return new DataLabsIdMappingResponse(
                responseTime: DateTimeOffset.UtcNow,
                correlationId: correlationId,
                successResponse: null,
                errorResponse: errorResponse,
                attributes: null,
                dataSource: SolutionUtils.ConvertProxyDataSourceToDataLabsDataSource(proxyDataSource));
        }
    }
}