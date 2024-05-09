namespace Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase.Services
{
    using Grpc.Core;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using static Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerService.V1.PartnerService;
    
    public class PartnerSolutionService : PartnerServiceBase
    {
        private static readonly PartnerSentErrorResponseException _partnerSentErrorResponseException = new("Partner explicitly sent ErrorResponse");
        private static readonly NotAllowedPartnerResponseException _notAllowedPartnerResponseException = new("Not Allowed Partner Response");

        private static readonly ActivityMonitorFactory PartnerSolutionServiceProcessStreamMessages =
            new("PartnerSolutionService.ProcessStreamMessages", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory PartnerSolutionServiceProcessMessage = 
            new("PartnerSolutionService.ProcessMessage", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory PartnerSolutionServiceGetDataLabsSingleResponseAsync =
            new("PartnerSolutionService.GetDataLabsSingleResponseAsync", useDataLabsEndpoint: true);

        private static readonly ActivityMonitorFactory PartnerSolutionServiceConvertStreamResponse =
            new("PartnerSolutionService.ConvertStreamResponse", useDataLabsEndpoint: true);

        public const string PartnerTraceAndMetricSourceName = "ARG.DataLabs.PartnerService";

        public const string ComponentName = "PartnerSolutionServer";
        public const string ActivityName_PartnerSolutionServerResponse = "PartnerSolutionServerResponse";
        public const string ActivityName_PartnerSolutionServerStreamResponse = "PartnerSolutionServerStreamResponse";

        public const string IO_SENT_TO_RECV = "IOSentToRecv";
        public const string STREAM_FIRST_RESPONSE_TIME = "StreamFirstResponseTime";
        public const string PARTNER_STREAM_RESPONSE = "PartnerStreamResponse";
        public const string PARTNER_SINGLE_RESPONSE = "PartnerSingleResponse";
        public const string NOT_ALLOWED_RESPONSE = "NotAllwed";

        internal static readonly ActivitySource PartnerBaseActivitySource = new ActivitySource(PartnerTraceAndMetricSourceName);
        internal static readonly Meter PartnerBaseMeter = new(PartnerTraceAndMetricSourceName, "1.0");

        public static readonly Counter<long> StreamResponseCounter = PartnerBaseMeter.CreateCounter<long>(PARTNER_STREAM_RESPONSE);
        public static readonly Counter<long> SingleResponseCounter = PartnerBaseMeter.CreateCounter<long>(PARTNER_SINGLE_RESPONSE);
        public static readonly Histogram<double> StreamFirstResponseTimeMetric = PartnerBaseMeter.CreateHistogram<double>(STREAM_FIRST_RESPONSE_TIME);
        public static readonly Histogram<int> IOSentToRecvMetric = PartnerBaseMeter.CreateHistogram<int>(IO_SENT_TO_RECV);
        
        private IDataLabsInterface PartnerInterface { get; }
        private readonly bool _isInternalPartner;

        public PartnerSolutionService(IDataLabsInterface partnerInterface)
        {
            PartnerInterface = partnerInterface;
            _isInternalPartner = MonitoringConstants.IS_INTERNAL_PARTNER;
        }

        public override async Task ProcessStreamMessages(PartnerRequest request, IServerStreamWriter<PartnerResponse> responseStream, ServerCallContext context)
        {
            var callMethod = nameof(ProcessStreamMessages);

            using var activity = new OpenTelemetryActivityWrapper(PartnerBaseActivitySource, ActivityName_PartnerSolutionServerStreamResponse,
                ActivityKind.Server, request.TraceId);
            
            var scenario = request.ReqAttributes.TryGetValue(BasicActivityMonitor.Scenario, out var scenarioValue) && !string.IsNullOrEmpty(scenarioValue) ? scenarioValue : null;
            using var methodMonitor = PartnerSolutionServiceProcessStreamMessages.ToMonitor(scenario: scenario, component: ComponentName);

            int totalResponses = 0;

            try
            {
                var dataLabsRequest = ConvertToDataLabsRequestAndMonitorStart(callMethod, request, activity, methodMonitor, context);
                var inputEventTime = ArmUtils.GetEventTime(dataLabsRequest.InputResource);

                var interfaceStartTime = DateTimeOffset.UtcNow;
                var partnerResponseStream = PartnerInterface.GetResponsesAsync(dataLabsRequest, context.CancellationToken);
                var enumerator = partnerResponseStream.GetAsyncEnumerator(context.CancellationToken);

                try
                {
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        if (++totalResponses == 1)
                        {
                            // First Response
                            var duration = (DateTimeOffset.UtcNow - interfaceStartTime).TotalMilliseconds;
                            StreamFirstResponseTimeMetric.Record(duration, new KeyValuePair<string, object?>(BasicActivityMonitor.Scenario, scenario));
                        }

                        var dataLabsResponse = enumerator.Current;
                        var partnerResponse = ConvertToPartnerResponse(
                            dataLabsResponse: dataLabsResponse,
                            inputEventTime: inputEventTime,
                            interfaceStartTime: interfaceStartTime,
                            otelActivity: activity,
                            mainActivity: methodMonitor.Activity,
                            scenario: scenario,
                            fromMultiResponse: true);

                        // Write to Server Stream
                        await responseStream.WriteAsync(partnerResponse).ConfigureAwait(false);

                        if (partnerResponse.Error != null)
                        {
                            // Partner returned explicit Error Response
                            // Stop writing server stream
                            methodMonitor.Activity[SolutionConstants.PartnerTotalResponse] = totalResponses;
                            activity.SetTag(SolutionConstants.PartnerTotalResponse, totalResponses);

                            methodMonitor.OnError(_partnerSentErrorResponseException);
                            return;
                        }
                    }
                }
                finally
                {
                    if (enumerator != null)
                    {
                        await enumerator.DisposeAsync().ConfigureAwait(false);
                    }
                }

                activity.SetTag(SolutionConstants.PartnerTotalResponse, totalResponses);
                methodMonitor.Activity[SolutionConstants.PartnerTotalResponse] = totalResponses;
                methodMonitor.OnCompleted();
                return;
            }
            catch (Exception ex)
            {
                methodMonitor.Activity[SolutionConstants.PartnerTotalResponse] = totalResponses;
                methodMonitor.OnError(ex);

                activity.SetTag(SolutionConstants.PartnerTotalResponse, totalResponses);
                activity.RecordException("ProcessStreamMessages", ex);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);

                // We return explict Error Response instead of exception
                var response = CreatePartnerExceptionResponse(ex, request, "ProcessStreamMessages");

                // Write to Server Stream
                await responseStream.WriteAsync(response).ConfigureAwait(false);
                return;
            }
        }

        public override async Task<PartnerResponse> ProcessMessage(PartnerRequest request, ServerCallContext context)
        {
            var callMethod = nameof(ProcessMessage);

            using var activity = new OpenTelemetryActivityWrapper(PartnerBaseActivitySource, ActivityName_PartnerSolutionServerResponse,
                ActivityKind.Server, request.TraceId);

            var scenario = request.ReqAttributes.TryGetValue(BasicActivityMonitor.Scenario, out var scenarioValue) && !string.IsNullOrEmpty(scenarioValue) ? scenarioValue : null;
            using var methodMonitor = PartnerSolutionServiceProcessMessage.ToMonitor(scenario: scenario, component: ComponentName);

            try
            {
                var dataLabsRequest = ConvertToDataLabsRequestAndMonitorStart(callMethod, request, activity, methodMonitor, context);
                var inputEventTime = ArmUtils.GetEventTime(dataLabsRequest.InputResource);

                var interfaceStartTime = DateTimeOffset.UtcNow;
                var dataLabsResponse = await GetDataLabsSingleResponseAsync(dataLabsRequest, context.CancellationToken).ConfigureAwait(false);
                var partnerResponse = ConvertToPartnerResponse(
                    dataLabsResponse: dataLabsResponse,
                    inputEventTime: inputEventTime,
                    interfaceStartTime: interfaceStartTime,
                    otelActivity: activity,
                    mainActivity: methodMonitor.Activity,
                    scenario: scenario,
                    fromMultiResponse: false);

                if (partnerResponse.Error != null)
                {
                    methodMonitor.OnError(_partnerSentErrorResponseException);
                }
                else
                {
                    methodMonitor.OnCompleted();
                }

                return partnerResponse;
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);

                activity.RecordException("ProcessMessage", ex);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);

                // We return explict Error Response instead of exception
                var response = CreatePartnerExceptionResponse(ex, request, "ProcessMessage");
                return response;
            }
        }

        private static PartnerResponse CreatePartnerExceptionResponse(Exception exception, PartnerRequest request, string component)
        {
            // We return explict Error Response instead of exception
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new PartnerResponse
            {
                RespEpochtime = currentTime,
                PartnerResponseEpochTime = currentTime,
                Correlationid = request.Correlationid ?? "",
                Error = new ErrorResponse
                {
                    Type = ErrorType.Poison,
                    RetryAfter = 0,
                    Message = exception.Message,
                    Code = "PartnerError",
                    FailedComponent = component
                }
            };
        }

        private static DataLabsARNV3Request ConvertToDataLabsRequestAndMonitorStart(
            string callMethod,
            PartnerRequest request,
            OpenTelemetryActivityWrapper otelActivity, 
            IActivityMonitor activityMonitor,
            ServerCallContext context)
        {
            var reqEpochTime = DateTimeOffset.FromUnixTimeMilliseconds(request.ReqEpochtime);
            var recvEpochTime = DateTimeOffset.UtcNow;

            // Set CorrelationId from Request
            var inputCorrelationId = string.IsNullOrEmpty(request.Correlationid) ? null : request.Correlationid;
            otelActivity.InputCorrelationId = inputCorrelationId;
            activityMonitor.Activity.CorrelationId = inputCorrelationId;

            var ioSentToRecvTime = (int)(recvEpochTime - reqEpochTime).TotalMilliseconds;
            if (ioSentToRecvTime <= 0)
            {
                ioSentToRecvTime = 1;
            }

            int reqSize = request.InputData?.Length == null ? 0 : request.InputData.Length;

            otelActivity.SetTag(SolutionConstants.ClientRequestTime, reqEpochTime);
            otelActivity.SetTag(SolutionConstants.ServerRecvTime, recvEpochTime);
            otelActivity.SetTag(SolutionConstants.RetryCount, request.RetryCount);
            otelActivity.SetTag(SolutionConstants.ClientSendToServerRecvTime, ioSentToRecvTime);
            otelActivity.SetTag(SolutionConstants.PartnerReqSize, reqSize);
            otelActivity.SetTag(SolutionConstants.RegionName, request.RegionName);

            activityMonitor.Activity[SolutionConstants.ClientRequestTime] = reqEpochTime;
            activityMonitor.Activity[SolutionConstants.ServerRecvTime] = recvEpochTime;
            activityMonitor.Activity[SolutionConstants.RetryCount] = request.RetryCount;
            activityMonitor.Activity[SolutionConstants.ClientSendToServerRecvTime] = ioSentToRecvTime;
            activityMonitor.Activity[SolutionConstants.PartnerReqSize] = reqSize;
            activityMonitor.Activity[SolutionConstants.RegionName] = request.RegionName;

            // Activity Monitor Start here. So it includes above basic informations in start log 
            activityMonitor.OnStart();

            var clientIp = context.Peer;
            var serverIp = MonitoringConstants.POD_IP;

            IOSentToRecvMetric.Record(ioSentToRecvTime,
                new KeyValuePair<string, object?>(SolutionConstants.CallMethod, callMethod),
                new KeyValuePair<string, object?>(SolutionConstants.ClientIP, clientIp),
                new KeyValuePair<string, object?>(SolutionConstants.ServerIP, serverIp));

            // Deserialize request.InputData
            var inputEventGridNotification = DeserializeArnV3Notification(request);

            var inputEvent = inputEventGridNotification.Data;
            var resourceData = inputEvent.Resources[0];
            var resourceCorrelationId = resourceData.CorrelationId;

            // Recheck InputCorrelationId
            if (inputCorrelationId == null && !string.IsNullOrEmpty(resourceCorrelationId))
            {
                inputCorrelationId = resourceCorrelationId;

                otelActivity.InputCorrelationId = inputCorrelationId;
                activityMonitor.Activity.CorrelationId = inputCorrelationId;
            }

            var inputResourceId = resourceData.ResourceId;
            var tenantId = ArmUtils.GetTenantId(inputEventGridNotification);

            otelActivity.InputResourceId = inputResourceId;
            activityMonitor.Activity.InputResourceId = inputResourceId;
            
            otelActivity.SetTag(SolutionConstants.TenantId, tenantId);
            activityMonitor.Activity[SolutionConstants.TenantId] = tenantId;

            // TODO
            // Quality Nuget for Input
            // Task: 21242278

            var partnerARNV3Request = new DataLabsARNV3Request(
                reqEpochTime,
                request.TraceId,
                request.RetryCount,
                inputCorrelationId,
                inputEventGridNotification,
                request.ReqAttributes,
                request.RegionName
                );

            return partnerARNV3Request;
        }

        private PartnerResponse ConvertToPartnerResponse(
            DataLabsARNV3Response dataLabsResponse,
            DateTimeOffset inputEventTime,
            DateTimeOffset interfaceStartTime,
            OpenTelemetryActivityWrapper otelActivity, 
            IActivity mainActivity,
            string? scenario,
            bool fromMultiResponse)
        {
            // This method could be called multiple times from Streaming Response as well
            TagList tagList = default;
            tagList.Add(BasicActivityMonitor.Scenario, scenario);

            var responseCounter = fromMultiResponse ? StreamResponseCounter : SingleResponseCounter;

            using var childActivityMonitor = fromMultiResponse ? PartnerSolutionServiceConvertStreamResponse.ToMonitor() : null;
            var setActivity = fromMultiResponse ? childActivityMonitor!.Activity : mainActivity;

            bool isEmptyResponse = 
                (dataLabsResponse.SuccessResponse == null && dataLabsResponse.ErrorResponse == null)
                    || (dataLabsResponse.SuccessResponse != null &&
                        !(dataLabsResponse.SuccessResponse.Resource?.Data?.Resources?.Count > 0));

            var outputCorrelationId = string.IsNullOrEmpty(dataLabsResponse.CorrelationId) ? null : dataLabsResponse.CorrelationId;

            // Set Output CorrelationId
            if (!fromMultiResponse)
            {
                otelActivity.OutputCorrelationId = outputCorrelationId;
            }
            setActivity.OutputCorrelationId = outputCorrelationId;

            if (isEmptyResponse)
            {
                if (!fromMultiResponse)
                {
                    // Set Empty Tag
                    otelActivity.SetTag(SolutionConstants.EmptyOutput, true);
                }

                setActivity.Properties[SolutionConstants.EmptyOutput] = "true";

                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var emptyResponse = new PartnerResponse
                {
                    RespEpochtime = currentTime,
                    PartnerResponseEpochTime = dataLabsResponse.ResponseTime.ToUnixTimeMilliseconds(),
                    Correlationid = outputCorrelationId ?? "",
                    Success = new SuccessResponse
                    {
                        Format = DataFormat.Arn,
                        OutputTimestamp = currentTime,
                        ArmId = string.Empty,
                        TenantId = string.Empty,
                        OutputData = null,
                        Etag = null,
                        EventType = string.Empty,
                        ResourceLocation = string.Empty,
                    },
                    InputEventtime = inputEventTime == default ? 0 : inputEventTime.ToUnixTimeMilliseconds(),
                    PartnerInterfaceStarttime = interfaceStartTime == default ? 0 : interfaceStartTime.ToUnixTimeMilliseconds()
                };

                if (dataLabsResponse.Attributes?.Count > 0)
                {
                    emptyResponse.RespAttributes.Add(dataLabsResponse.Attributes);
                }

                childActivityMonitor?.OnCompleted();

                // Empty Sucess Reponse
                tagList.Add(SolutionConstants.NumResources, 0);
                tagList.Add(MonitoringConstants.SuccessDimension);
                responseCounter.Add(1, tagList);
                return emptyResponse;
            }

            if (dataLabsResponse.SuccessResponse != null)
            {
                // TODO
                // Qulaity Nuget for Output

                var outputResource = dataLabsResponse.SuccessResponse.Resource;
                var numResources = outputResource!.Data.Resources.Count;
                var firstArmResource = outputResource!.Data.Resources[0];
                var outputResourceId = firstArmResource?.ResourceId;
                var resourceCorrelationId = firstArmResource?.CorrelationId;
                var etag = dataLabsResponse.SuccessResponse.ETag;
                var outputTimeEpoch = dataLabsResponse.SuccessResponse.OutputTimestamp.ToUnixTimeMilliseconds();

                // Recheck OutputCorrelationId
                if (outputCorrelationId == null && !string.IsNullOrEmpty(resourceCorrelationId))
                {
                    outputCorrelationId = resourceCorrelationId;

                    // set OutputCorrelationId from resource correlation Id
                    if (!fromMultiResponse)
                    {
                        otelActivity.OutputCorrelationId = outputCorrelationId;
                    }
                    setActivity.OutputCorrelationId = outputCorrelationId;
                }

                var outputTenantId = ArmUtils.GetTenantId(outputResource);
                var eventType = outputResource.EventType;
                var resourceLocation = outputResource.Data?.ResourceLocation;
                var outputBinary = SerializationHelper.SerializeToByteString(outputResource, false);
                var outputSize = outputBinary.Length;

                // Set OutputResourceId
                if (!fromMultiResponse)
                {
                    otelActivity.EventType = eventType;
                    otelActivity.OutputResourceId = outputResourceId;
                    otelActivity.SetTag(SolutionConstants.Subject, outputResource.Subject);
                    otelActivity.SetTag(SolutionConstants.PartnerOutputSize, outputSize);
                }

                setActivity.OutputResourceId = outputResourceId;

                setActivity[SolutionConstants.EventType] = eventType;
                setActivity[SolutionConstants.Subject] = outputResource.Subject;
                setActivity[SolutionConstants.OutputTenantId] = outputTenantId;
                setActivity[SolutionConstants.PartnerOutputSize] = outputSize;
                setActivity[SolutionConstants.ETag] = etag;
                setActivity[SolutionConstants.OutputTimeStamp] = outputTimeEpoch;
                setActivity[SolutionConstants.NumResources] = numResources;

                // TODO
                // Until Qulaity Nuget for Output is integrated
                if (!_isInternalPartner)
                {
                    // For non internal partner, we only allow only one response per response and the output type should be regular /providers/ type.
                    // not subscription or resource group or tenant type etc..
                    var resourceType = ArmUtils.GetResourceType(outputResourceId, providersTypeOnly: !_isInternalPartner);
                    setActivity[SolutionConstants.ResourceType] = resourceType;

                    if (numResources > 1 || string.IsNullOrWhiteSpace(resourceType))
                    {
                        // ErrorResponse
                        var notAllowedResponseMessage = 
                            "Unallowed PartnerResponse. NumResource: " + numResources 
                            + ", ResourceType: " + resourceType;

                        var notAllowedParterResponse = new PartnerResponse
                        {
                            RespEpochtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            PartnerResponseEpochTime = dataLabsResponse.ResponseTime.ToUnixTimeMilliseconds(),
                            Correlationid = outputCorrelationId ?? "",
                            Error = new ErrorResponse
                            {
                                Type = ErrorType.Poison,
                                RetryAfter = 0,
                                Message = notAllowedResponseMessage,
                                Code = NOT_ALLOWED_RESPONSE,
                                FailedComponent = NOT_ALLOWED_RESPONSE
                            }
                        };

                        setActivity["ErrorType"] = notAllowedParterResponse.Error.Type.FastEnumToString();
                        setActivity["RetryAfter"] = notAllowedParterResponse.Error.RetryAfter;
                        setActivity["ErrorDescription"] = notAllowedParterResponse.Error.Message;
                        setActivity["ErrorCode"] = notAllowedParterResponse.Error.Code;
                        setActivity["FailedComponent"] = notAllowedParterResponse.Error.FailedComponent;

                        childActivityMonitor?.OnError(_notAllowedPartnerResponseException);

                        // Add Error Dimension
                        tagList.Add(SolutionConstants.NumResources, numResources);
                        tagList.Add(SolutionConstants.PartnerErrorType, notAllowedParterResponse.Error.Type.FastEnumToString());
                        tagList.Add(SolutionConstants.PartnerFailedComponent, notAllowedParterResponse.Error.FailedComponent ?? NOT_ALLOWED_RESPONSE);
                        tagList.Add(MonitoringConstants.FailDimension);
                        responseCounter.Add(1, tagList);

                        return notAllowedParterResponse;
                    }
                }
                else
                {
                    var resourceType = ArmUtils.GetResourceType(outputResourceId);
                    setActivity[SolutionConstants.ResourceType] = resourceType;
                }

                var solutionResponse = new PartnerResponse
                {
                    RespEpochtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    PartnerResponseEpochTime = dataLabsResponse.ResponseTime.ToUnixTimeMilliseconds(),
                    Correlationid = outputCorrelationId ?? "",
                    Success = new SuccessResponse
                    {
                        Format = DataFormat.Arn,
                        OutputTimestamp = outputTimeEpoch,
                        ArmId = outputResourceId ?? "",
                        TenantId = outputTenantId ?? "",
                        OutputData = outputBinary,
                        Etag = etag,
                        EventType = eventType,
                        ResourceLocation = resourceLocation ?? ""
                    },
                    InputEventtime = inputEventTime == default ? 0 : inputEventTime.ToUnixTimeMilliseconds(),
                    PartnerInterfaceStarttime = interfaceStartTime == default ?  0 : interfaceStartTime.ToUnixTimeMilliseconds()
                };

                if (dataLabsResponse.Attributes?.Count > 0)
                {
                    solutionResponse.RespAttributes.Add(dataLabsResponse.Attributes);
                }

                childActivityMonitor?.OnCompleted();

                // Success Response
                tagList.Add(SolutionConstants.NumResources, numResources);
                tagList.Add(MonitoringConstants.SuccessDimension);
                responseCounter.Add(1, tagList);

                return solutionResponse;
            }
            else 
            {
                // ErrorResponse
                var dataLabErrorResponse = dataLabsResponse.ErrorResponse!;

                var errorType = dataLabErrorResponse.ErrorType switch
                {
                    DataLabsErrorType.DROP => ErrorType.Drop,
                    DataLabsErrorType.RETRY => ErrorType.Retry,
                    DataLabsErrorType.POISON => ErrorType.Poison,
                    _ => ErrorType.Poison,
                };

                var solutionResponse = new PartnerResponse
                {
                    RespEpochtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    PartnerResponseEpochTime = dataLabsResponse.ResponseTime.ToUnixTimeMilliseconds(),
                    Correlationid = outputCorrelationId ?? "",
                    Error = new ErrorResponse
                    {
                        Type = errorType,
                        RetryAfter = dataLabErrorResponse.RetryDelayInMilliseconds,
                        Message = dataLabErrorResponse.ErrorDescription,
                        Code = dataLabErrorResponse.ErrorCode ?? "",
                        FailedComponent = dataLabErrorResponse.FailedComponent ?? ComponentName
                    },
                    InputEventtime = inputEventTime == default ? 0 : inputEventTime.ToUnixTimeMilliseconds(),
                    PartnerInterfaceStarttime = interfaceStartTime == default ? 0 : interfaceStartTime.ToUnixTimeMilliseconds()
                };

                if (dataLabsResponse.Attributes?.Count > 0)
                {
                    solutionResponse.RespAttributes.Add(dataLabsResponse.Attributes);
                }

                setActivity["ErrorType"] = solutionResponse.Error.Type.FastEnumToString();
                setActivity["RetryAfter"] = solutionResponse.Error.RetryAfter;
                setActivity["ErrorDescription"] = solutionResponse.Error.Message;
                setActivity["ErrorCode"] = solutionResponse.Error.Code;
                setActivity["FailedComponent"] = solutionResponse.Error.FailedComponent;

                childActivityMonitor?.OnError(_partnerSentErrorResponseException);

                tagList.Add(SolutionConstants.NumResources, 1);
                tagList.Add(SolutionConstants.PartnerErrorType, solutionResponse.Error.Type.FastEnumToString());
                tagList.Add(SolutionConstants.PartnerFailedComponent, solutionResponse.Error.FailedComponent ?? "Partner");
                tagList.Add(MonitoringConstants.FailDimension);
                responseCounter.Add(1, tagList);

                return solutionResponse;
            }
        }

        private async Task<DataLabsARNV3Response> GetDataLabsSingleResponseAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            using var methodMonitor = PartnerSolutionServiceGetDataLabsSingleResponseAsync.ToMonitor();

            try
            {
                methodMonitor.OnStart(false);
                var dataLabsResponse = await PartnerInterface.GetResponseAsync(request, cancellationToken).ConfigureAwait(false);
                methodMonitor.OnCompleted(logging: false);

                return dataLabsResponse;
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
                throw;
            }
        }

        private static EventGridNotification<NotificationDataV3<GenericResource>> DeserializeArnV3Notification(PartnerRequest request)
        {
            // Deserialize request.InputData
            var notifications = SerializationHelper.DeserializeArnV3Notification(request.InputData, false);
            if (notifications == null || notifications.Length == 0)
            {
                throw new ArgumentException("InputData is empty");
            }
            if (notifications.Length > 1)
            {
                throw new ArgumentException("InputData is more than one");
            }

            return notifications[0];
        }
    }
}
