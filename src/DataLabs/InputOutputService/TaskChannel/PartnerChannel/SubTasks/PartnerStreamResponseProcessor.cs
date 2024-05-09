namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel.SubTasks
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Runtime.CompilerServices;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class PartnerStreamResponseProcessor
    {
        private static readonly ActivityMonitorFactory PartnerStreamResponseProcessorProcessSuccessResponseAsync =
                new ("PartnerStreamResponseProcessor.ProcessSuccessResponseAsync");

        public StreamOutputEventTaskCallBack ChildEventTaskCallBack { get; }
        public int NumResponses => _numResponses;
        public int NumSuccessResponse => _numSuccessResponse;
        public int NumErrorResponses => _numErrorResponses;
        public int NumChildTask => _numChildTask;
        public int NumSameGroupResponses=> _numSameGroupResponse;
        public int NumDifferentGroup => _numDifferentGroup;
        public int NumSubJob => _numSubJob;

        private readonly ITaskChannelManager<IOEventTaskContext<ARNSingleInputMessage>> _childDefaultNextChannel;
        private readonly AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> _parentTaskContext;
        private readonly TimeSpan _streamingTaskTimeout;

        private IOEventTaskContext<ARNSingleInputMessage> _firstChildForGroup;
        private ITaskChannelManager<IOEventTaskContext<ARNSingleInputMessage>> _firstChildForGroupNextChannel;

        private IOEventTaskContext<ARNSingleInputMessage> _lastChildForGroup;
        private string _prevGoupId;

        private int _numChildTask;
        private int _numResponses;
        private int _numSuccessResponse;
        private int _numErrorResponses;

        private int _numSameGroupResponse;
        private int _numDifferentGroup;
        private int _numSubJob;

        public PartnerStreamResponseProcessor(
            AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> parentTaskContext,
            TimeSpan streamingTaskTimeout,
            int streamingInputRetryDelayMs, 
            int streamingThresholdForRetry)
        {
            _childDefaultNextChannel =
                SolutionInputOutputService.UseSourceOfTruth ?
                SolutionInputOutputService.ARNMessageChannels.SourceOfTruthChannelManager :
                SolutionInputOutputService.ARNMessageChannels.OutputChannelManager;

            _parentTaskContext = parentTaskContext;
            _streamingTaskTimeout = streamingTaskTimeout;

            ChildEventTaskCallBack = new StreamOutputEventTaskCallBack(
                    parentTaskContext.TaskContext,
                    retryDelayMs: streamingInputRetryDelayMs,
                    maxChildForRetry: streamingThresholdForRetry);
        }

        public void StartIteration()
        {
            ChildEventTaskCallBack.StartAddChildEvent();
        }

        public async Task EndIterationAsync(IActivity parentActivity)
        {
            if (_firstChildForGroup != null)
            {
                await SendPreviousGroupTaskAsync(parentActivity).ConfigureAwait(false);
            }

            ChildEventTaskCallBack.FinishAddChildEvent();
        }

        public void CancelAllRunningChildTasks(string reason)
        {
            ChildEventTaskCallBack.CancelAllChildTasks(reason, null);
        }

        public async Task<bool> AddPartnerResponseAsync(PartnerResponse partnerResponse)
        {
            _numResponses++;

            if (_numResponses == 1)
            {
                var inputEventTime = partnerResponse.InputEventtime;
                if (inputEventTime > 0)
                {
                    // This is more accurate input eventTime because we deserialize before calling PartnerInterface and get it from PartnerService
                    _parentTaskContext.TaskContext.InputMessage.EventTime = DateTimeOffset.FromUnixTimeMilliseconds(inputEventTime);
                }
            }

            if (partnerResponse.Success != null)
            {
                _numSuccessResponse++;
                await ProcessSuccessResponseAsync(partnerResponse).ConfigureAwait(false);
                return true;
            }
            else
            {
                _numErrorResponses++;
                return false;
            }
        }

        private async Task ProcessSuccessResponseAsync(PartnerResponse response)
        {
            SuccessResponse successResponse = response.Success;
            var responseCorrelationId = response.Correlationid;

            using var methodMonitor = PartnerStreamResponseProcessorProcessSuccessResponseAsync.ToMonitor();
            {
                try
                {
                    // Set Output Resource Id to propogate through channels
                    methodMonitor.Activity.OutputCorrelationId = responseCorrelationId;
                    methodMonitor.Activity.OutputResourceId = successResponse.ArmId;

                    methodMonitor.Activity[SolutionConstants.StreamResourceId] = _numResponses;

                    methodMonitor.OnStart(false);

                    string currentGroupId = null;

                    // Parse attribute flgas
                    var respAttributes = response.RespAttributes;
                    if (respAttributes != null)
                    {
                        // Parse Group Id
                        if (respAttributes.TryGetValue(DataLabsARNV3Response.AttributeKey_GROUPID, out var attributeVal))
                        {
                            currentGroupId = attributeVal;
                            if (string.IsNullOrWhiteSpace(currentGroupId))
                            {
                                currentGroupId = null;
                            }
                        }
                    }

                    methodMonitor.Activity[SolutionConstants.StreamCurrGroupId] = currentGroupId;
                    methodMonitor.Activity[SolutionConstants.StreamPrevGroupId] = _prevGoupId;

                    var outputSize = successResponse.OutputData?.Length ?? 0;
                    if (outputSize == 0)
                    {
                        // Empty Response
                        methodMonitor.Activity[SolutionConstants.EmptyOutput] = true;
                        methodMonitor.OnCompleted();
                        return;
                    }

                    // Create current ChildEventTaskContext
                    var childEventTaskContext = CreateChildEventTaskContext(_numChildTask + 1, methodMonitor.Activity);  // 1-based child taskId
                    _numChildTask++; // we increase here so even if above CreateChildEventTaskContext throws exception, we still know how many tasks were successfully created

                    // Add Output to current ChildEventTaskContext
                    AddOutputMessage(childEventTaskContext, successResponse, responseCorrelationId, response.RespAttributes, methodMonitor.Activity);

                    childEventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PartnerSuccessResponse;
                    var isSubJob = SolutionUtils.IsSubJobResponse(childEventTaskContext.OutputMessage?.RespProperties);
                    if (isSubJob)
                    {
                        childEventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PartnerSubJobResponse;
                        _numSubJob++;
                    }

                    var childTaskActivity = childEventTaskContext.EventTaskActivity;

                    // Add basic information to TaskActivity
                    childTaskActivity.SetTag(SolutionConstants.StreamResourceId, _numResponses);
                    childTaskActivity.SetTag(SolutionConstants.StreamCurrGroupId, currentGroupId);
                    childTaskActivity.SetTag(SolutionConstants.StreamPrevGroupId, _prevGoupId);

                    // Increase Ref Count in ChildEventTaskCallBack
                    ChildEventTaskCallBack.IncreaseChildEventCount();

                    var childNextChannel = GetChildNextChannel(isSubJob);

                    // Now compare groupId to determine if we need to link current ChildEvenTask to Previous ChildEventTask
                    // Current Group Id is null
                    if (currentGroupId == null) {

                        _numDifferentGroup++;

                        if (_firstChildForGroup != null)
                        {
                            await SendPreviousGroupTaskAsync(methodMonitor.Activity).ConfigureAwait(false);
                        }

                        // Send current Task
                        methodMonitor.Activity[SolutionConstants.NextChannel] = childNextChannel.ChannelName;

                        childEventTaskContext.SetTaskTimeout(_streamingTaskTimeout);
                        await childEventTaskContext.StartEventTaskAsync(childNextChannel, false, methodMonitor.Activity).ConfigureAwait(false);

                        methodMonitor.Activity["StartedTask"] = true;
                        methodMonitor.OnCompleted();
                        return;
                    }
                    else if (currentGroupId.Equals(_prevGoupId))
                    {
                        // Group Id exists
                        // Compare if this is new Group or Previous Group
                        _numSameGroupResponse++;

                        var lastChildTraceId = _lastChildForGroup.EventTaskActivity.TraceId;
                        _lastChildForGroup.SetChainedNextEventTaskContext(childEventTaskContext, childNextChannel);

                        // Set Last Child to Previous Child such
                        _lastChildForGroup = childEventTaskContext;

                        methodMonitor.Activity["ChainedToPreviousTask"] = true;
                        methodMonitor.Activity["PreviousTaskTraceId"] = lastChildTraceId;
                        methodMonitor.OnCompleted();
                        return;
                    }else
                    {
                        // Group Id exists but different with previous Group
                        _numDifferentGroup++;

                        if (_firstChildForGroup != null)
                        {
                            await SendPreviousGroupTaskAsync(methodMonitor.Activity).ConfigureAwait(false);
                        }

                        // Set Current Child Task To firstChildForGroup
                        SetFirstChildForGroup(childEventTaskContext, childNextChannel);
                        _prevGoupId = currentGroupId;

                        methodMonitor.Activity["FirstChildTaskForGroup"] = true;
                        methodMonitor.OnCompleted();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    methodMonitor.OnError(ex);
                    throw;
                }
            }
        }

        private async Task SendPreviousGroupTaskAsync(IActivity parentActivity)
        {
            if (_firstChildForGroup == null)
            {
                return;
            }

            // Send first Task for Group
            var firstChildTraceId = _firstChildForGroup.EventTaskActivity.TraceId;
            
            _firstChildForGroup.SetTaskTimeout(_streamingTaskTimeout);
            await _firstChildForGroup.StartEventTaskAsync(_firstChildForGroupNextChannel, false, null).ConfigureAwait(false);

            if (parentActivity != null)
            {
                parentActivity["StartedPreviousGroupId"] = _prevGoupId;
                parentActivity["StartedPreviousGroupFirstTaskTraceId"] = firstChildTraceId;
            }

            SetFirstChildForGroup(null, null);
            _prevGoupId = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ITaskChannelManager<IOEventTaskContext<ARNSingleInputMessage>> GetChildNextChannel(bool isSubJob)
        {
            return isSubJob ? SolutionInputOutputService.ARNMessageChannels.SubJobChannelManager : _childDefaultNextChannel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetFirstChildForGroup(IOEventTaskContext<ARNSingleInputMessage> childEventTaskContext, ITaskChannelManager<IOEventTaskContext<ARNSingleInputMessage>> nextChannel)
        {
            _firstChildForGroup = childEventTaskContext;
            _firstChildForGroupNextChannel = nextChannel;
            _lastChildForGroup = childEventTaskContext;
        }

        private static void AddOutputMessage(IIOEventTaskContext childEventTaskContext, SuccessResponse response,
               string outputCorrelationId, IDictionary<string, string> respAttributes, IActivity activity)
        {
            var outputSize = response.OutputData?.Length ?? 0;

            activity[SolutionConstants.PartnerOutputSize] = outputSize;
            activity[SolutionConstants.OutputTenantId] = response.TenantId;
            activity[SolutionConstants.ETag] = response.Etag;
            activity[SolutionConstants.OutputTimeStamp] = response.OutputTimestamp;

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
                parentIOEventTaskContext: childEventTaskContext);

            childEventTaskContext.AddOutputMessage(outputMessage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IOEventTaskContext<ARNSingleInputMessage> CreateChildEventTaskContext(int childTaskId, IActivity activity)
        {
            var parentEventTaskContext = _parentTaskContext.TaskContext;
            var childEventTaskContext = new IOEventTaskContext<ARNSingleInputMessage>(
                eventTaskType: InputOutputConstants.StreamResponseChildEventTask,
                dataSourceType: parentEventTaskContext.DataSourceType,
                dataSourceName: parentEventTaskContext.DataSourceName,
                firstEnqueuedTime: parentEventTaskContext.FirstEnqueuedTime,
                firstPickedUpTime: parentEventTaskContext.FirstPickedUpTime,
                dataEnqueuedTime: parentEventTaskContext.DataEnqueuedTime,
                eventTime: parentEventTaskContext.EventTime,
                inputMessage: null,  // inputMessage
                eventTaskCallBack: ChildEventTaskCallBack,
                retryCount: 0,
                retryStrategy: SolutionInputOutputService.RetryStrategy,
                parentActivityContext: parentEventTaskContext.EventTaskActivity.Context,
                topActivityStartTime: parentEventTaskContext.EventTaskActivity.TopActivityStartTime,
                createNewTraceId: true, // createNewTraceId
                regionConfigData: parentEventTaskContext.RegionConfigData,
                parentCancellationToken: ChildEventTaskCallBack.AllChildTaskCancellationToken,  // cancellationToken
                retryChannelManager: null, // drop
                poisonChannelManager: null, // drop
                finalChannelManager: SolutionInputOutputService.ARNMessageChannels.FinalChannelManager,
                globalConcurrencyManager: SolutionInputOutputService.GlobalConcurrencyManager);

            var parentTaskActivity = parentEventTaskContext.EventTaskActivity;
            var childTaskActivity = childEventTaskContext.EventTaskActivity;

            childTaskActivity.InputCorrelationId = parentTaskActivity.InputCorrelationId;
            childTaskActivity.InputResourceId = parentTaskActivity.InputResourceId;
            childTaskActivity.EventType = parentTaskActivity.EventType;

            childTaskActivity.SetTag(InputOutputConstants.ChildTask, true);
            childTaskActivity.SetTag(InputOutputConstants.ChildTaskId, childTaskId);

            var childTraceId = childTaskActivity.TraceId;
            var parentTraceId = parentEventTaskContext.EventTaskActivity.TraceId;

            activity[InputOutputConstants.ChildTaskId] = childTaskId;
            activity[InputOutputConstants.ChildTaskTraceId] = childTraceId;
            activity[SolutionConstants.ParentTraceId] = parentTraceId;

            _parentTaskContext.EventTaskActivity.AddChildTraceId(childTraceId);

            return childEventTaskContext;
        }
    }
}
