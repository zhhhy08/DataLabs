namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel.SubTasks
{
    using Google.Protobuf;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Grpc;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerSolutionClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;

    [ExcludeFromCodeCoverage]
    internal class PartnerDispatcherTaskFactory : ISubTaskFactory<IOEventTaskContext<ARNSingleInputMessage>>
    {
        private static readonly ILogger<PartnerDispatcherTaskFactory> Logger = DataLabLoggerFactory.CreateLogger<PartnerDispatcherTaskFactory>();
        private static readonly PartnerSentErrorResponseException _partnerSentErrorResponseException = new ("Partner explicitly sent ErrorResponse");
        private static readonly StreamChildTaskFailedException _streamChildTaskFailedException = new("Stream ChildTask Failed");

        public string SubTaskName => "PartnerDispatcher";
        public bool CanContinueToNextTaskOnException => false;

        private readonly string _grpcAddr;
        private string? _partnerSolutionGrpcOptionStr;
        private PartnerSolutionClient _partnerSolutionClient;
        private readonly object _lockObject = new();

        private int _streamingInputRetryDelayMs; // msecs
        private int _streamingThresholdForRetry;
        private TimeSpan _streamingTaskTimeout;
        private bool _useMultiResponses;
        private readonly PartnerDispatcherTask _partnerDispatcherTask; // singleTon

        // TODO
        // created RoundRobin Dispatcher for full sync or batched request
        //private readonly PartnerDispatcherTask _roundRobinDispatcherTask; // singleTon

        public PartnerDispatcherTaskFactory(string addr, bool useMultiResponses = false)
        {
            _grpcAddr = addr;
            var partnerSolutionGrpcOptionStr = ConfigMapUtil.Configuration.GetValueWithCallBack<string>(
                SolutionConstants.PartnerSolutionGrpcOption, UpdatePartnerSolutionGrpcOption, string.Empty, allowMultiCallBacks: true);
            _partnerSolutionGrpcOptionStr = partnerSolutionGrpcOptionStr ?? string.Empty;

            // TODO, future work. consider local communication again to reduce network issue for single pod partner
            var grpcClientOption = new GrpcClientOption(_partnerSolutionGrpcOptionStr.ConvertToDictionary(caseSensitive: false));
            _partnerSolutionClient = new PartnerSolutionClient(addr: addr, grpcClientOption: grpcClientOption);

            _partnerDispatcherTask = new PartnerDispatcherTask(this);

            _streamingInputRetryDelayMs =
                ConfigMapUtil.Configuration.GetValueWithCallBack<int>(SolutionConstants.PartnerStreamingInputRetryDelayMS, UpdateStreamingInputRetryDelay, 100, allowMultiCallBacks: true);

            _streamingThresholdForRetry =
                ConfigMapUtil.Configuration.GetValueWithCallBack<int>(SolutionConstants.PartnerStreamingThresholdForRetry, UpdateStreamingThresholdForRetry, 20, allowMultiCallBacks: true);

            var taskTimeOutInSec = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(SolutionConstants.PartnerStreamingTaskTimeOutInSec, UpdateStreamingTaskTimeOutInSec, 20, allowMultiCallBacks: true);
            _streamingTaskTimeout = TimeSpan.FromSeconds(taskTimeOutInSec);

            _useMultiResponses = useMultiResponses;
        }

        public ISubTask<IOEventTaskContext<ARNSingleInputMessage>> CreateSubTask(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            return _partnerDispatcherTask;
        }

        public void Dispose()
        {
            _partnerSolutionClient.Dispose();
        }

        private static void DisposeOldDispatcher(PartnerSolutionClient partnerSolutionClient)
        {
            partnerSolutionClient.Dispose();
            Logger.LogWarning("Disposed Old PartnerSolutionClient");
        }

        private Task UpdateStreamingTaskTimeOutInSec(int newTaskTimeOutInSec)
        {
            if (newTaskTimeOutInSec <= 0)
            {
                Logger.LogError("{config} must be larger than 0", SolutionConstants.PartnerStreamingTaskTimeOutInSec);
                return Task.CompletedTask;
            }

            var oldTimeOutInSec = _streamingTaskTimeout.TotalSeconds;
            _streamingTaskTimeout = TimeSpan.FromSeconds(newTaskTimeOutInSec);

            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", SolutionConstants.PartnerStreamingTaskTimeOutInSec, oldTimeOutInSec, newTaskTimeOutInSec);

            return Task.CompletedTask;
        }

        private Task UpdateStreamingInputRetryDelay(int newValue)
        {
            if (newValue <= 0)
            {
                Logger.LogError("{config} must be larger than 0", SolutionConstants.PartnerStreamingInputRetryDelayMS);
                return Task.CompletedTask;
            }

            var oldValue = _streamingInputRetryDelayMs;
            if (Interlocked.CompareExchange(ref _streamingInputRetryDelayMs, newValue, oldValue) == oldValue)
            {
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.PartnerStreamingInputRetryDelayMS, oldValue, newValue);
            }

            return Task.CompletedTask;
        }

        private Task UpdateStreamingThresholdForRetry(int newValue)
        {
            if (newValue <= 0)
            {
                Logger.LogError("{config} must be larger than 0", SolutionConstants.PartnerStreamingThresholdForRetry);
                return Task.CompletedTask;
            }

            var oldValue = _streamingThresholdForRetry;
            if (Interlocked.CompareExchange(ref _streamingThresholdForRetry, newValue, oldValue) == oldValue)
            {
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.PartnerStreamingThresholdForRetry, oldValue, newValue);
            }

            return Task.CompletedTask;
        }

        private Task UpdatePartnerSolutionGrpcOption(string newVal)
        {
            var configKey = SolutionConstants.PartnerSolutionGrpcOption;
            var oldVal = _partnerSolutionGrpcOptionStr;
            if (string.IsNullOrWhiteSpace(newVal) || newVal.EqualsInsensitively(oldVal))
            {
                return Task.CompletedTask;
            }

            lock (_lockObject)
            {
                var oldSolutionClient = _partnerSolutionClient;
                var grpcClientOption = new GrpcClientOption(newVal.ConvertToDictionary(caseSensitive: false));
                var newPartnerSolutionClient = new PartnerSolutionClient(addr: _grpcAddr, grpcClientOption: grpcClientOption);

                if (Interlocked.CompareExchange(ref _partnerSolutionClient, newPartnerSolutionClient, oldSolutionClient) == oldSolutionClient)
                {
                    _partnerSolutionGrpcOptionStr = newVal;

                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", configKey, oldVal, newVal);

                    // Dispose Old GrpcChannel after 30 secs
                    _ = Task.Run(() => Task.Delay(TimeSpan.FromSeconds(30))
                        .ContinueWith((antecedent, info) => DisposeOldDispatcher((PartnerSolutionClient)info), oldSolutionClient,
                        TaskContinuationOptions.None));
                }
                else
                {
                    // Someone already exchanged 
                    newPartnerSolutionClient.Dispose();
                }
            }

            return Task.CompletedTask;
        }

        private class PartnerDispatcherTask : ISubTask<IOEventTaskContext<ARNSingleInputMessage>>
        {
            private static readonly ActivityMonitorFactory PartnerDispatcherTaskProcessSingleResponseAsync = 
                new ("PartnerDispatcherTask.ProcessSingleResponseAsync");

            private static readonly ActivityMonitorFactory PartnerDispatcherTaskProcessResponseStreamAsync =
                new("PartnerDispatcherTask.ProcessResponseStreamAsync");

            public bool UseValueTask => false;

            private readonly PartnerDispatcherTaskFactory _partnerDispatcherTaskFactory;

            public PartnerDispatcherTask(PartnerDispatcherTaskFactory partnerDispatcherTaskFactory)
            {
                _partnerDispatcherTaskFactory = partnerDispatcherTaskFactory;
            }

            public async Task ProcessEventTaskContextAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                if (_partnerDispatcherTaskFactory._useMultiResponses)
                {
                    await ProcessResponseStreamAsync(eventTaskContext).ConfigureAwait(false);
                }
                else
                {
                    await ProcessSingleResponseAsync(eventTaskContext).ConfigureAwait(false);
                }
            }

            private static bool ParseSinglePartnerResponseAndSetNextChannel(
                PartnerResponse response,
                AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext,
                IActivity activity)
            {
                var taskActivity = eventTaskContext.EventTaskActivity;
                var ioEventTaskContext = eventTaskContext.TaskContext;
                SuccessResponse successResponse = response.Success;
                var responseCorrelationId = response.Correlationid;

                var inputEventTime = response.InputEventtime;
                if (inputEventTime > 0)
                {
                    // This is more accurate input eventTime because we deserialize before calling PartnerInterface and get it from PartnerService
                    ioEventTaskContext.InputMessage.EventTime = DateTimeOffset.FromUnixTimeMilliseconds(inputEventTime);
                }

                if (successResponse != null)
                {
                    activity[SolutionConstants.PartnerSingleSuccessResponse] = true;
                    taskActivity.SetTag(SolutionConstants.PartnerSingleSuccessResponse, true);

                    AddOutputMessage(ioEventTaskContext, successResponse, responseCorrelationId, response.RespAttributes, activity);

                    // Check if output is empty suceess
                    if (ioEventTaskContext.OutputMessage == null)
                    {
                        ioEventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PartnerEmptyResponse;

                        activity[SolutionConstants.EmptyOutput] = true;
                        taskActivity.SetTag(SolutionConstants.EmptyOutput, true);
                        ioEventTaskContext.TaskSuccess(Stopwatch.GetTimestamp());
                    }
                    else
                    {
                        ioEventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PartnerSuccessResponse;

                        var isSubJob = SolutionUtils.IsSubJobResponse(ioEventTaskContext.OutputMessage.RespProperties);
                        
                        if (isSubJob)
                        {
                            ioEventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PartnerSubJobResponse;
                            SolutionInputOutputService.SetNextChannelToSubJobChannel(eventTaskContext);
                        }
                        else
                        {
                            SolutionInputOutputService.SetNextChannelToSourceOfTruthChannel(eventTaskContext);
                        }
                    }
                    return true;
                }
                else if (response.Error != null)
                {
                    activity[SolutionConstants.PartnerSingleErrorResponse] = true;
                    taskActivity.SetTag(SolutionConstants.PartnerSingleErrorResponse, true);

                    HandleErrorResponse(ioEventTaskContext, response.Error, activity);
                    return false;
                }
                else
                {
                    // something wrong
                    Logger.LogCritical("Response proto has unexpected type");
                    throw new InvalidOperationException("Response proto has unexpected type");
                }
            }

            private async Task ProcessSingleResponseAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                using var methodMonitor = PartnerDispatcherTaskProcessSingleResponseAsync.ToMonitor();
                var ioEventTaskContext = eventTaskContext.TaskContext;
                var taskActivity = eventTaskContext.EventTaskActivity;
                long partnerDurationTime = 0;

                try
                {
                    OpenTelemetryActivityWrapper.Current = taskActivity;

                    methodMonitor.OnStart(true);

                    var partnerSendingId = Tracer.ConvertToActivityId(taskActivity.Context);

                    taskActivity.SetTag(SolutionConstants.PartnerSingleResponse, true);

                    var solutionRequest = new PartnerRequest()
                    {
                        TraceId = partnerSendingId,
                        RetryCount = ioEventTaskContext.RetryCount,
                        ReqEpochtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Correlationid = ioEventTaskContext.InputCorrelationId ?? "",
                        Format = DataFormat.Arn,
                        InputData = UnsafeByteOperations.UnsafeWrap(ioEventTaskContext.BaseInputMessage.SerializedData.ToMemory()),
                        RegionName = ioEventTaskContext.RegionConfigData.RegionLocationName
                    };

                    LogPartnerRequest(solutionRequest, methodMonitor.Activity);

                    DateTime? deadline = null;
                    if (eventTaskContext.TaskTimeout != null && eventTaskContext.TaskTimeout.Value > TimeSpan.Zero)
                    {
                        deadline = DateTime.UtcNow.Add(eventTaskContext.TaskTimeout.Value);
                    }
                    
                    var response = await _partnerDispatcherTaskFactory._partnerSolutionClient.SendRequestAsync(solutionRequest, cancellationToken: eventTaskContext.TaskCancellationToken, deadline: deadline).ConfigureAwait(false);
                    var result = ParseSinglePartnerResponseAndSetNextChannel(response, ioEventTaskContext, methodMonitor.Activity);

                    var partnerInterfaceStarttime = response.PartnerInterfaceStarttime;
                    if (partnerInterfaceStarttime > 0)
                    {
                        partnerDurationTime = response.RespEpochtime - partnerInterfaceStarttime;
                        partnerDurationTime = partnerDurationTime <= 0 ? 1 : partnerDurationTime;
                    }

                    if (result)
                    {
                        methodMonitor.Activity.OutputCorrelationId = eventTaskContext.EventTaskActivity.OutputCorrelationId;
                        methodMonitor.Activity.OutputResourceId = eventTaskContext.EventTaskActivity.OutputResourceId;
                        methodMonitor.OnCompleted();
                        return;
                    }
                    else
                    {
                        methodMonitor.OnError(_partnerSentErrorResponseException);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    methodMonitor.OnError(ex);
                    throw;
                }
                finally
                {
                    if (partnerDurationTime == 0)
                    {
                        partnerDurationTime = (long)methodMonitor.Activity.Elapsed.TotalMilliseconds;
                    }
                    ioEventTaskContext.PartnerTotalSpentTime += partnerDurationTime;
                }
            }

            private async Task ProcessResponseStreamAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                using var methodMonitor = PartnerDispatcherTaskProcessResponseStreamAsync.ToMonitor();
                var parentIOEventTaskContext = eventTaskContext.TaskContext;
                var parentTaskActivity = eventTaskContext.EventTaskActivity;
                long partnerDurationTime = 0;

                try
                {
                    OpenTelemetryActivityWrapper.Current = parentTaskActivity;

                    methodMonitor.OnStart(false);

                    var partnerSendingId = Tracer.ConvertToActivityId(parentTaskActivity.Context);

                    parentTaskActivity.SetTag(SolutionConstants.PartnerResponseStream, true);

                    var solutionRequest = new PartnerRequest()
                    {
                        TraceId = partnerSendingId,
                        RetryCount = parentIOEventTaskContext.RetryCount,
                        ReqEpochtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Correlationid = parentIOEventTaskContext.InputCorrelationId ?? "",
                        Format = DataFormat.Arn,
                        InputData = UnsafeByteOperations.UnsafeWrap(parentIOEventTaskContext.BaseInputMessage.SerializedData.ToMemory()),
                        RegionName = parentIOEventTaskContext.RegionConfigData.RegionLocationName
                    };

                    LogPartnerRequest(solutionRequest, methodMonitor.Activity);

                    DateTime? deadline = null;
                    if (eventTaskContext.TaskTimeout != null && eventTaskContext.TaskTimeout.Value > TimeSpan.Zero)
                    {
                        deadline = DateTime.UtcNow.Add(eventTaskContext.TaskTimeout.Value);
                    }

                    using var streamCall = _partnerDispatcherTaskFactory._partnerSolutionClient.SendRequestAndStreamResponseAsync(solutionRequest, cancellationToken: eventTaskContext.TaskCancellationToken, deadline: deadline);
                    var responseStream = streamCall.ResponseStream;

                    // Special Handling for Single Response
                    PartnerResponse firstResponse = null;
                    PartnerResponse secondResponse = null;

                    if (await responseStream.MoveNext(eventTaskContext.TaskCancellationToken))
                    {
                        firstResponse = responseStream.Current;

                        if (await responseStream.MoveNext(eventTaskContext.TaskCancellationToken))
                        {
                            secondResponse = responseStream.Current;
                        }
                    }

                    if (firstResponse == null)
                    {
                        // Empty Response
                        methodMonitor.Activity[SolutionConstants.EmptyOutput] = true;
                        parentTaskActivity.SetTag(SolutionConstants.EmptyOutput, true);

                        parentIOEventTaskContext.TaskSuccess(Stopwatch.GetTimestamp());

                        methodMonitor.OnCompleted();
                        return;
                    }

                    if (secondResponse == null)
                    {
                        // single Response
                        methodMonitor.Activity[SolutionConstants.PartnerSingleStreamResponse] = true;
                        parentTaskActivity.SetTag(SolutionConstants.PartnerSingleStreamResponse, true);

                        var result = ParseSinglePartnerResponseAndSetNextChannel(firstResponse, parentIOEventTaskContext, methodMonitor.Activity);

                        var partnerInterfaceStarttime = firstResponse.PartnerInterfaceStarttime;
                        if (partnerInterfaceStarttime > 0)
                        {
                            partnerDurationTime = firstResponse.RespEpochtime - partnerInterfaceStarttime;
                            partnerDurationTime = partnerDurationTime <= 0 ? 1 : partnerDurationTime;
                        }

                        if (result)
                        {
                            methodMonitor.Activity.OutputCorrelationId = eventTaskContext.EventTaskActivity.OutputCorrelationId;
                            methodMonitor.Activity.OutputResourceId = eventTaskContext.EventTaskActivity.OutputResourceId;
                            methodMonitor.OnCompleted();
                            return;
                        }
                        else
                        {
                            methodMonitor.OnError(_partnerSentErrorResponseException);
                            return;
                        }
                    }

                    // Multi Response
                    methodMonitor.Activity[InputOutputConstants.ParentTask] = true;
                    parentTaskActivity.SetTag(InputOutputConstants.ParentTask, true);
                    parentTaskActivity.SetTag(SolutionConstants.PartnerMultiStreamResponses, true);

                    // Set Output Response Id with MultiStreamRespones
                    parentTaskActivity.OutputResourceId = SolutionConstants.PartnerMultiStreamResponses;
                    methodMonitor.Activity.OutputResourceId = SolutionConstants.PartnerMultiStreamResponses;

                    var streamResponseProcessor = new PartnerStreamResponseProcessor(
                        eventTaskContext,
                        _partnerDispatcherTaskFactory._streamingTaskTimeout,
                        _partnerDispatcherTaskFactory._streamingInputRetryDelayMs,
                        _partnerDispatcherTaskFactory._streamingThresholdForRetry);

                    // Starting Add Child Event
                    streamResponseProcessor.StartIteration();

                    while (true)
                    {
                        var numChildErrors = streamResponseProcessor.ChildEventTaskCallBack.TotalChildMovedToPoison +
                            streamResponseProcessor.ChildEventTaskCallBack.TotalChildDropped;

                        if (numChildErrors > 0)
                        {
                            LogStreamResponseSummary(streamResponseProcessor, parentTaskActivity, methodMonitor.Activity);
                            parentTaskActivity.AddEvent("Total " + streamResponseProcessor.NumChildTask + " Stream Child Tasks created And Child Task Error");

                            streamResponseProcessor.ChildEventTaskCallBack.HandleStreamChildError();
                            streamResponseProcessor.CancelAllRunningChildTasks("Stream ChildTask Failed");

                            methodMonitor.OnError(_streamChildTaskFailedException);
                            return;
                        }

                        PartnerResponse response = null;
                        if (firstResponse != null)
                        {
                            response = firstResponse;
                            firstResponse = null; // reset FirstResponse
                        }
                        else if (secondResponse != null)
                        {
                            response = secondResponse;
                            secondResponse = null; // reset SecondResponse
                        }
                        else
                        {
                            var hasNext = await responseStream.MoveNext(eventTaskContext.TaskCancellationToken);
                            if (!hasNext)
                            {
                                break;
                            }

                            response = responseStream.Current;
                        }

                        var partnerInterfaceStarttime = response.PartnerInterfaceStarttime;
                        if (partnerInterfaceStarttime > 0)
                        {
                            partnerDurationTime = response.RespEpochtime - partnerInterfaceStarttime;
                            partnerDurationTime = partnerDurationTime <= 0 ? 1 : partnerDurationTime;
                        }

                        var result = await streamResponseProcessor.AddPartnerResponseAsync(response).ConfigureAwait(false);
                        if (!result)
                        {
                            LogStreamResponseSummary(streamResponseProcessor, parentTaskActivity, methodMonitor.Activity);
                            parentTaskActivity.AddEvent("Total " + streamResponseProcessor.NumChildTask + " Stream Child Tasks created And Error Response");

                            if (response.Error != null)
                            {
                                HandleErrorResponse(parentIOEventTaskContext, response.Error, methodMonitor.Activity);
                                streamResponseProcessor.CancelAllRunningChildTasks("PartnerSentErrorToParent");

                                methodMonitor.OnError(_partnerSentErrorResponseException);
                                return;
                            }
                            else
                            {
                                // something wrong
                                Logger.LogCritical("Response proto has unexpected type");

                                // cancel all child tasks
                                streamResponseProcessor.CancelAllRunningChildTasks("Invalid Grpc Proto");

                                throw new InvalidOperationException("Response proto has unexpected type");
                            }
                        }
                    }

                    LogStreamResponseSummary(streamResponseProcessor, parentTaskActivity, methodMonitor.Activity);
                    parentTaskActivity.AddEvent("Total " + streamResponseProcessor.NumChildTask + " Stream Child Tasks created");

                    // Notice that we need to call FinishAddChildEvent() only when all child adding is successfully finished
                    // When we call FinishAddChildEvent(), parentTask will be called through callback
                    // So parentTask should not be set to any channel here
                    await streamResponseProcessor.EndIterationAsync(methodMonitor.Activity).ConfigureAwait(false);
                    methodMonitor.OnCompleted();
                }
                catch (Exception ex)
                {
                    methodMonitor.OnError(ex);
                    throw;
                }
                finally
                {
                    if (partnerDurationTime == 0)
                    {
                        partnerDurationTime = (long)methodMonitor.Activity.Elapsed.TotalMilliseconds;
                    }
                    parentIOEventTaskContext.PartnerTotalSpentTime += partnerDurationTime;
                }
            }

            public ValueTask ProcessEventTaskContextValueAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                throw new NotImplementedException();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void LogPartnerRequest(PartnerRequest partnerRequest, IActivity activity)
            {
                activity["SendingActivityIdToPartner"] = partnerRequest.TraceId;
                activity["SendingRetryCountToPartner"] = partnerRequest.RetryCount;
                activity["SendingCorrelationidToPartner"] = partnerRequest.Correlationid;
                activity["SendingRegionNameToPartner"] = partnerRequest.RegionName;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void LogStreamResponseSummary(PartnerStreamResponseProcessor streamResponseProcessor,
                OpenTelemetryActivityWrapper taskActivity,
                IActivity activity)
            {
                activity[SolutionConstants.PartnerTotalResponse] = streamResponseProcessor.NumResponses;
                activity[SolutionConstants.TotalCreatedChildTasks] = streamResponseProcessor.NumChildTask;
                activity[SolutionConstants.StreamSuccessResponse] = streamResponseProcessor.NumSuccessResponse;
                activity[SolutionConstants.StreamErrorResponses] = streamResponseProcessor.NumErrorResponses;
                activity[SolutionConstants.StreamSameGroupResponses] = streamResponseProcessor.NumSameGroupResponses;
                activity[SolutionConstants.StreamDifferentGroup] = streamResponseProcessor.NumDifferentGroup;
                activity[SolutionConstants.StreamSubJob] = streamResponseProcessor.NumSubJob;

                taskActivity.SetTag(SolutionConstants.PartnerTotalResponse, streamResponseProcessor.NumResponses);
                taskActivity.SetTag(SolutionConstants.TotalCreatedChildTasks, streamResponseProcessor.NumChildTask);
                taskActivity.SetTag(SolutionConstants.StreamSuccessResponse, streamResponseProcessor.NumSuccessResponse);
                taskActivity.SetTag(SolutionConstants.StreamErrorResponses, streamResponseProcessor.NumErrorResponses);
                taskActivity.SetTag(SolutionConstants.StreamSameGroupResponses, streamResponseProcessor.NumSameGroupResponses);
                taskActivity.SetTag(SolutionConstants.StreamDifferentGroup, streamResponseProcessor.NumDifferentGroup);
                taskActivity.SetTag(SolutionConstants.StreamSubJob, streamResponseProcessor.NumSubJob);
            }

            private static void AddOutputMessage(IIOEventTaskContext eventTaskContext, SuccessResponse response, 
                string outputCorrelationId, IDictionary<string, string> respAttributes, IActivity activity)
            {
                var taskActivity = eventTaskContext.EventTaskActivity;
                var outputSize = response.OutputData?.Length ?? 0;
                if (outputSize == 0)
                {
                    // Empty Response
                    if (activity != null)
                    {
                        activity[SolutionConstants.EmptyOutput] = true;
                    }
                    
                    taskActivity.SetTag(SolutionConstants.EmptyOutput, true);
                    return;
                }

                // Set Output Resource Id
                if (activity != null) {
                    activity.OutputCorrelationId = outputCorrelationId;
                    activity.OutputResourceId = response.ArmId;
                    activity[SolutionConstants.PartnerOutputSize] = outputSize;
                    activity[SolutionConstants.OutputTimeStamp] = response.OutputTimestamp;
                }

                var outputMessage = new OutputMessage(
                    outputFormat: SolutionDataFormat.ARN,
                    data: BinaryData.FromBytes(response.OutputData.Memory),
                    correlationId: outputCorrelationId,
                    resourceId: response.ArmId,
                    tenantId: response.TenantId,
                    eventType: response.EventType,
                    resourceLocation: response.ResourceLocation,
                    etag: response.Etag,
                    outputTimeStamp: response.OutputTimestamp,
                    respProperties: respAttributes?.Count > 0 ? respAttributes : null,
                    parentIOEventTaskContext: eventTaskContext);

                eventTaskContext.AddOutputMessage(outputMessage);

                // Output Related stuff is already set in above AddOutputMessage
                taskActivity.SetTag(SolutionConstants.PartnerOutputSize, outputSize);
            }

            private static void HandleErrorResponse(IIOEventTaskContext eventTaskContext, ErrorResponse errorResponse, IActivity parentActivity)
            {
                if (errorResponse == null)
                {
                    return;
                }

                parentActivity[SolutionConstants.PartnerErrorType] = errorResponse.Type.FastEnumToString();
                parentActivity[SolutionConstants.PartnerRetryDelay] = errorResponse.RetryAfter;
                parentActivity[SolutionConstants.PartnerErrorCode] = errorResponse.Code;
                parentActivity[SolutionConstants.PartnerErrorMessage] = errorResponse.Message;
                parentActivity[SolutionConstants.PartnerFailedComponent] = errorResponse.FailedComponent;

                var taskActivity = eventTaskContext.EventTaskActivity;
                taskActivity.SetTag(SolutionConstants.PartnerErrorType, errorResponse.Type.FastEnumToString());
                taskActivity.SetTag(SolutionConstants.PartnerRetryDelay, errorResponse.RetryAfter);
                taskActivity.SetTag(SolutionConstants.PartnerErrorCode, errorResponse.Code);
                taskActivity.SetTag(SolutionConstants.PartnerErrorMessage, errorResponse.Message);
                taskActivity.SetTag(SolutionConstants.PartnerFailedComponent, errorResponse.FailedComponent);

                // Failed Response
                if (errorResponse.Type == ErrorType.Retry)
                {
                    eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PartnerRetryResponse;

                    eventTaskContext.TaskMovingToRetry(
                        RetryReason.PartnerLogic.FastEnumToString(),
                        errorResponse.Message,
                        errorResponse.RetryAfter,
                        IOComponent.PartnerLogic.FastEnumToString(),
                        null);
                    return;
                }
                else if (errorResponse.Type == ErrorType.Poison)
                {
                    eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PartnerPoisonResponse;

                    eventTaskContext.TaskMovingToPoison(
                        PoisonReason.PartnerLogic.FastEnumToString(),
                        errorResponse.Message,
                        IOComponent.PartnerLogic.FastEnumToString(), 
                        null);
                    return;
                }
                else if (errorResponse.Type == ErrorType.Drop)
                {
                    eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PartnerDropResponse;

                    eventTaskContext.TaskDrop(
                        DropReason.PartnerLogic.FastEnumToString(),
                        errorResponse.Message,
                        IOComponent.PartnerLogic.FastEnumToString());
                    return;
                }
                else
                {
                    // Unknown
                    var errorMsg = "Unknown ErrorType: " + errorResponse.Type.FastEnumToString() + 
                        ", ErrorResponse: " + errorResponse.Message;
                    Logger.LogCritical(errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }
            }
        }
    }
}