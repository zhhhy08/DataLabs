namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RawInputChannel.SubTasks
{
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;

    internal class PayloadDisassemblyTaskFactory : ISubTaskFactory<IOEventTaskContext<ARNRawInputMessage>>
    {
        private static readonly ILogger<PayloadDisassemblyTaskFactory> Logger = DataLabLoggerFactory.CreateLogger<PayloadDisassemblyTaskFactory>();

        private static readonly ActivityMonitorFactory PayloadDisassemblyTaskFactoryProcessEventTaskContextAsync =
           new("PayloadDisassemblyTaskFactory.ProcessEventTaskContextAsync");

        private static readonly ActivityMonitorFactory PayloadDisassemblyTaskFactoryProcessChildTask =
           new("PayloadDisassemblyTaskFactory.ProcessChildTask");

        private static readonly string DefaultPartnerNonRetryableCode =
            string.Join(ConfigMapExtensions.LIST_DELIMITER, (int)HttpStatusCode.Unauthorized, (int)HttpStatusCode.Forbidden);

        private static readonly NoResourceInNotificationException _noResourceInNotification = new("No Resource In Notification");

        public string SubTaskName => "PayloadDisassembly";
        public bool CanContinueToNextTaskOnException => false;

        private readonly PayloadDisassemblyTask _payloadDisassemblyTask; // singleton

        private static HashSet<int> _partnerBlobNonRetryableCodes;
        private static int _maxBatchedChildBeforeMoveToRetryQueue;
        private static bool _enableBlobPayloadRouting;
        private static HashSet<string> _blobPayloadRoutingTypes;
        private static object _updateLock = new();

        public PayloadDisassemblyTaskFactory(IPartnerBlobClient partnerBlobClient)
        {
            _maxBatchedChildBeforeMoveToRetryQueue = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                InputOutputConstants.MaxBatchedChildBeforeMoveToRetryQueue, UpdateMaxBatchedChild, 100);

            _partnerBlobNonRetryableCodes = ConfigMapUtil.Configuration.GetValueWithCallBack<string>(
                InputOutputConstants.PartnerBlobNonRetryableCodes, UpdatePartnerBlobNonRetryableCodes, DefaultPartnerNonRetryableCode)
                    .ConvertToIntSet(stringSplitOptions: StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            _enableBlobPayloadRouting = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(
                InputOutputConstants.EnableBlobPayloadRouting, UpdateEnableBlobPayloadRouting, false);

            _blobPayloadRoutingTypes = (ConfigMapUtil.Configuration.GetValueWithCallBack<string>(
                InputOutputConstants.BlobPayloadRoutingTypes, UpdateBlobPayloadRoutingTypes, string.Empty) ?? string.Empty).ConvertToSet(false);

            _payloadDisassemblyTask = new PayloadDisassemblyTask(partnerBlobClient);
        }

        public ISubTask<IOEventTaskContext<ARNRawInputMessage>> CreateSubTask(AbstractEventTaskContext<IOEventTaskContext<ARNRawInputMessage>> eventTaskContext)
        {
            return _payloadDisassemblyTask;
        }

        public void Dispose()
        {
        }

        private enum ChildTaskNextDestination
        {
            Filtered,
            InputChannel,
            RetryChannel,
            BlobPayloadRoutingChannel
        }

        private class PayloadDisassemblyTask : ISubTask<IOEventTaskContext<ARNRawInputMessage>>
        {
            #region Fields

            private readonly IPartnerBlobClient _partnerBlobClient;
        
            #endregion

            #region Properties

            public bool UseValueTask => false;

            #endregion

            public PayloadDisassemblyTask(IPartnerBlobClient partnerBlobClient)
            {
                _partnerBlobClient = partnerBlobClient;
            }

            public async Task ProcessEventTaskContextAsync(AbstractEventTaskContext<IOEventTaskContext<ARNRawInputMessage>> eventTaskContext)
            {
                var parentTaskActivity = eventTaskContext.EventTaskActivity;
                OpenTelemetryActivityWrapper.Current = parentTaskActivity;

                var parentEventTaskContext = eventTaskContext.TaskContext;
                parentTaskActivity.SetTag(InputOutputConstants.ParentTask, true);
                parentTaskActivity.SetTag(SolutionConstants.HasEventHubARNRawInputMessage, true);

                using var parentMonitor = PayloadDisassemblyTaskFactoryProcessEventTaskContextAsync.ToMonitor();

                List<ValueTuple<EventGridNotification<NotificationDataV3<GenericResource>>, bool>> eventGridNotifications = null;

                var totalChild = 0;
                var numFilteredChild = 0;
                var numMovedToRetryChild = 0;
                var numInputChannelChild = 0;
                var numBlobPayloadRouting = 0;

                try
                {
                    parentMonitor.OnStart(false);

                    var deserializedObject = parentEventTaskContext.InputMessage.DeserializedObject;
                    var rawNotifications = deserializedObject.NotificationDataV3s;

                    if (rawNotifications == null || rawNotifications.Length == 0)
                    {
                        // something wrong, impossible
                        throw new Exception("Empty Raw Notification");
                    }

                    SolutionLoggingUtils.LogRawARNV3Notification(rawNotifications, parentTaskActivity);

                    // Unbatch of batched resource or blob URI
                    eventGridNotifications = await SerializationHelper.DeserializeArnV3ToEachResourceAsync(deserializedObject.NotificationDataV3s,
                        _partnerBlobClient, eventTaskContext.RetryCount, eventTaskContext.TaskCancellationToken).ConfigureAwait(false);

                    if (eventGridNotifications == null || eventGridNotifications.Count == 0)
                    {
                        parentMonitor.OnError(_noResourceInNotification);
                        parentEventTaskContext.TaskMovingToPoison(PoisonReason.NoResourceInNotification.FastEnumToString(), null, IOComponent.RawInputChannel.FastEnumToString(), null);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // something wrong => Moved to Poison or Retry
                    parentMonitor.OnError(ex);

                    if (ex is SerializationException)
                    {
                        // Deserialization fail is NOT retryable. Move to Poison
                        parentEventTaskContext.TaskMovingToPoison(PoisonReason.DeserializeError.FastEnumToString(), null, IOComponent.RawInputChannel.FastEnumToString(), ex);
                        return;
                    }

                    // Let's check if response Code is nonRetryable
                    var nonRetryable = false;
                    if (ex is HttpRequestException httpRequestException)
                    {
                        if (httpRequestException.StatusCode.HasValue)
                        {
                            nonRetryable = _partnerBlobNonRetryableCodes.Contains((int)httpRequestException.StatusCode.Value);
                        }
                    }
                    else if (ex is HttpRequestErrorException httpRequestErrorException)
                    {
                        nonRetryable = _partnerBlobNonRetryableCodes.Contains((int)httpRequestErrorException.Response.StatusCode);
                    }

                    if (nonRetryable)
                    {
                        // Non Retryable
                        parentEventTaskContext.TaskMovingToPoison(PoisonReason.PartnerBlobNonRetryableCode.FastEnumToString(), null, IOComponent.RawInputChannel.FastEnumToString(), ex);
                    }
                    else
                    {
                        // Retryable
                        parentEventTaskContext.TaskMovingToRetry(RetryReason.PayloadDisassemblyError.FastEnumToString(), null, 0, IOComponent.RawInputChannel.FastEnumToString(), ex);
                    }

                    return;
                }

                // Create ChildEvent callback
                var childEventTaskCallBack = new RawInputChildEventTaskCallBack(parentEventTaskContext);

                childEventTaskCallBack.StartAddChildEvent();

                totalChild = eventGridNotifications.Count;

                try
                {
                    for (int i = 0; i < totalChild; i++)
                    {
                        if (parentEventTaskContext.TaskCancellationToken.IsCancellationRequested)
                        {
                            LogRawInputSummary(
                                totalChild: totalChild,
                                numFilteredChild: numFilteredChild,
                                numMovedToRetryChild: numMovedToRetryChild,
                                numBlobPayloadRouting: numBlobPayloadRouting,
                                numInputChannelChild: numInputChannelChild,
                                taskActivity: parentTaskActivity,
                                activity: parentMonitor.Activity);

                            parentTaskActivity.AddEvent(
                                $"In RawInput, Total {totalChild} child, {numFilteredChild} filtered, {numMovedToRetryChild} movedToRetry, {numBlobPayloadRouting} movedToBlobPayloadRouting, {numInputChannelChild} movedToInput and got CancellationTokenRequested");

                            childEventTaskCallBack.CancelAllChildTasks("parentEventTaskContext got CancellationTokenRequested", null);

                            parentEventTaskContext.TaskCancellationToken.ThrowIfCancellationRequested();
                        }

                        using var childMonitor = PayloadDisassemblyTaskFactoryProcessChildTask.ToMonitor();
                        {
                            var singleResourceEventGridEvent = eventGridNotifications[i].Item1;
                            var isBlobPayload = eventGridNotifications[i].Item2;

                            if (singleResourceEventGridEvent.Data?.Resources?.Count > 0)
                            {
                                childMonitor.Activity.InputResourceId = singleResourceEventGridEvent.Data.Resources[0].ResourceId;
                                childMonitor.Activity.CorrelationId = singleResourceEventGridEvent.Data.Resources[0].CorrelationId;
                            }

                            try
                            {
                                childMonitor.OnStart(false);

                                var childTaskId = i + 1; // 1-based id
                                childMonitor.Activity[InputOutputConstants.ChildTaskId] = childTaskId;

                                // Create new IOEventTaskContext and Add to InputChannel
                                var childNextDestination = await CreateChildTaskAndStartAsync(
                                        singleResourceEventGridEvent: singleResourceEventGridEvent,
                                        binaryData: null,
                                        parentEventTaskContext: parentEventTaskContext,
                                        childEventTaskCallBack: childEventTaskCallBack,
                                        childTaskId: childTaskId,
                                        totalChilds: totalChild,
                                        taskTimeout: parentEventTaskContext.TaskTimeout,
                                        childActivity: childMonitor.Activity,
                                        useSameTraceId: totalChild == 1,
                                        isBlobPayload: isBlobPayload).ConfigureAwait(false);

                                switch (childNextDestination)
                                {
                                    case ChildTaskNextDestination.Filtered:
                                        numFilteredChild++;
                                        break;
                                    case ChildTaskNextDestination.RetryChannel:
                                        numMovedToRetryChild++;
                                        break;
                                    case ChildTaskNextDestination.BlobPayloadRoutingChannel:
                                        numBlobPayloadRouting++;
                                        break;
                                    case ChildTaskNextDestination.InputChannel:
                                    default:
                                        numInputChannelChild++;
                                        break;
                                }

                                childMonitor.OnCompleted();
                            }
                            catch (Exception childException)
                            {

                                LogRawInputSummary(
                                    totalChild: totalChild,
                                    numFilteredChild: numFilteredChild,
                                    numMovedToRetryChild: numMovedToRetryChild,
                                    numBlobPayloadRouting: numBlobPayloadRouting,
                                    numInputChannelChild: numInputChannelChild,
                                    taskActivity: parentTaskActivity,
                                    activity: parentMonitor.Activity);

                                parentTaskActivity.AddEvent(
                                    $"In RawInput, Total {totalChild} child, {numFilteredChild} filtered, {numMovedToRetryChild} movedToRetry, {numBlobPayloadRouting} movedToBlobPayloadRouting, {numInputChannelChild} movedToInput and throw Exception");

                                childEventTaskCallBack.CancelAllChildTasks("ChildTask CreationAndStart Failed", null);

                                childMonitor.OnError(childException);
                                throw;
                            }
                        }
                    }

                    LogRawInputSummary(
                        totalChild: totalChild,
                        numFilteredChild: numFilteredChild,
                        numMovedToRetryChild: numMovedToRetryChild,
                        numBlobPayloadRouting: numBlobPayloadRouting,
                        numInputChannelChild: numInputChannelChild,
                        taskActivity: parentTaskActivity,
                        activity: parentMonitor.Activity);

                    parentTaskActivity.AddEvent(
                        $"In RawInput, Total {totalChild} child, {numFilteredChild} filtered, {numMovedToRetryChild} movedToRetry, {numBlobPayloadRouting} movedToBlobPayloadRouting, {numInputChannelChild} movedToInput");

                    // Notice that we need to call FinishAddChildEvent() only when all child adding is successfully finished
                    // When we call FinishAddChildEvent(), parentTask will be called through callback
                    // So parentTask should not be set to any channel here
                    // This line should be the last
                    childEventTaskCallBack.FinishAddChildEvent();

                    parentMonitor.OnCompleted();
                }
                catch (Exception ex)
                {
                    parentMonitor.OnError(ex);
                    throw;
                }
            }

            public ValueTask ProcessEventTaskContextValueAsync(AbstractEventTaskContext<IOEventTaskContext<ARNRawInputMessage>> eventTaskContext)
            {
                throw new NotImplementedException();
            }

            #region Private Methods

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static IOEventTaskContext<ARNSingleInputMessage> CreateChildEventTaskContext(
                IOEventTaskContext<ARNRawInputMessage> parentEventTaskContext,
                EventGridNotification<NotificationDataV3<GenericResource>> singleResourceEventGridEvent,
                BinaryData serializedData,
                RawInputChildEventTaskCallBack childEventTaskCallBack,
                IActivity childActivity,
                bool useSameTraceId)
            {
                // serializedData might be null or already there
                // For example, single Input request, we can use origina binaryData
                // For blob or multi(batched) request, because we break into each resource, binaryData is mostly null

                var inputMessage = ARNSingleInputMessage.CreateSingleInputMessage(singleResourceEventGridEvent, serializedData, null);

                // Set Correlation and Output Resource Id to propogate through channels
                childActivity.CorrelationId = inputMessage.CorrelationId;
                childActivity.InputResourceId = inputMessage.ResourceId;

                (TrafficTunerResult result, TrafficTunerNotAllowedReason reason) trafficTunerResult;

                if (RegionConfigManager.IsBackupRegionPairName(parentEventTaskContext.RegionConfigData.RegionLocationName))
                {
                    trafficTunerResult = SolutionInputOutputService.BackupEventhubsTrafficTuner.InputTrafficTuner.EvaluateTunerResult(inputMessage, 0);
                }
                else
                {
                    trafficTunerResult = SolutionInputOutputService.InputEventhubsTrafficTuner.InputTrafficTuner.EvaluateTunerResult(inputMessage, 0);
                }

                if (trafficTunerResult.result != TrafficTunerResult.Allowed)
                {
                    childActivity[SolutionConstants.TaskFiltered] = true;
                    childActivity[SolutionConstants.TrafficTunerResult] = trafficTunerResult.reason.FastEnumToString();
                    return null;
                }

                var childEventTask = new IOEventTaskContext<ARNSingleInputMessage>(
                    InputOutputConstants.RawInputChildEventTask,
                    parentEventTaskContext.DataSourceType,
                    parentEventTaskContext.DataSourceName,
                    parentEventTaskContext.FirstEnqueuedTime,
                    parentEventTaskContext.FirstPickedUpTime,
                    parentEventTaskContext.DataEnqueuedTime,
                    inputMessage.EventTime,
                    inputMessage,
                    childEventTaskCallBack,
                    retryCount: parentEventTaskContext.RetryCount,
                    SolutionInputOutputService.RetryStrategy,
                    parentEventTaskContext.EventTaskActivity.Context,
                    parentEventTaskContext.EventTaskActivity.TopActivityStartTime,
                    createNewTraceId: !useSameTraceId,
                    regionConfigData: parentEventTaskContext.RegionConfigData,
                    parentEventTaskContext.TaskCancellationToken,
                    SolutionInputOutputService.ARNMessageChannels.RetryChannelManager,
                    SolutionInputOutputService.ARNMessageChannels.PoisonChannelManager,
                    SolutionInputOutputService.ARNMessageChannels.FinalChannelManager,
                    SolutionInputOutputService.GlobalConcurrencyManager);

                IOServiceOpenTelemetry.ReportInputIndividualResourceCounter(inputMessage.EventAction);

                var topic = singleResourceEventGridEvent.Topic;
                var subject = singleResourceEventGridEvent.Subject;
                var eventType = singleResourceEventGridEvent.EventType;
                var eventTime = singleResourceEventGridEvent.EventTime.ToString("o");

                var childTaskActivity = childEventTask.EventTaskActivity;
                childTaskActivity.SetTag(InputOutputConstants.ChildTask, true);
                childTaskActivity.SetTag(SolutionConstants.Topic, topic);
                childTaskActivity.SetTag(SolutionConstants.Subject, subject);
                childTaskActivity.SetTag(SolutionConstants.EventType, eventType);
                childTaskActivity.SetTag(SolutionConstants.EventTime, eventTime);

                return childEventTask;
            }

            private static bool IsMoveToBlobPayloadRoutingChannel(
                ARNSingleInputMessage inputMessage,
                IActivity activity)
            {
                var notificationData = inputMessage.DeserializedObject.NotificationDataV3.Data;
                object resourceRoutingLocation = null;
                object notificationRoutingLocation = null;
                string resourceRoutingLocationStr;
                string notificationRoutingLocationStr;

                activity[SolutionConstants.IsBlobPayload] = bool.TrueString;

                // feature flag
                if (!_enableBlobPayloadRouting)
                {
                    return false;
                }

                activity[SolutionConstants.EnableBlobPayloadRouting] = bool.TrueString;

                // type match
                if (_blobPayloadRoutingTypes?.Contains(inputMessage.ResourceType, StringComparer.OrdinalIgnoreCase) != true)
                {
                    activity[SolutionConstants.BlobPayloadRoutingTypeMatch] = bool.FalseString;
                    return false;
                }

                activity[SolutionConstants.BlobPayloadRoutingTypeMatch] = bool.TrueString;

                // single resource
                notificationData?.Resources?[0]?.AdditionalResourceProperties?.TryGetValue(InputOutputConstants.ArnRoutingLocation, out resourceRoutingLocation);
                resourceRoutingLocationStr = resourceRoutingLocation?.ToString();
                activity[SolutionConstants.ResourceRoutingLocation] = resourceRoutingLocationStr;

                if (string.IsNullOrWhiteSpace(resourceRoutingLocationStr))
                {
                    // resource has non-empty routing location
                    return false;
                }

                notificationData?.AdditionalBatchProperties?.TryGetValue(InputOutputConstants.ArnRoutingLocation, out notificationRoutingLocation);
                notificationRoutingLocationStr = notificationRoutingLocation?.ToString();
                activity[SolutionConstants.NotificationRoutingLocation] = notificationRoutingLocationStr;

                return notificationRoutingLocationStr != resourceRoutingLocationStr;
            }

            private static async Task<ChildTaskNextDestination> CreateChildTaskAndStartAsync(
                EventGridNotification<NotificationDataV3<GenericResource>> singleResourceEventGridEvent,
                BinaryData binaryData,
                IOEventTaskContext<ARNRawInputMessage> parentEventTaskContext,
                RawInputChildEventTaskCallBack childEventTaskCallBack,
                int childTaskId,
                int totalChilds,
                TimeSpan? taskTimeout,
                IActivity childActivity,
                bool useSameTraceId,
                bool isBlobPayload)
            {
                var childEventTaskContext = CreateChildEventTaskContext(
                    parentEventTaskContext,
                    singleResourceEventGridEvent,
                    binaryData,
                    childEventTaskCallBack,
                    childActivity,
                    useSameTraceId: useSameTraceId);

                if (childEventTaskContext == null)
                {
                    // This is filtered
                    return ChildTaskNextDestination.Filtered;
                }

                var parentTraceId = parentEventTaskContext.EventTaskActivity.TraceId;

                var childTaskActivity = childEventTaskContext.EventTaskActivity;
                childTaskActivity.SetTag(InputOutputConstants.ChildTaskId, childTaskId);

                var childTraceId = childTaskActivity.TraceId;
                childActivity[InputOutputConstants.ChildTaskTraceId] = childTraceId;
                childActivity[SolutionConstants.ParentTraceId] = parentTraceId;

                if (!useSameTraceId)
                {
                    parentEventTaskContext.EventTaskActivity.AddChildTraceId(childTraceId);
                }

                // Increase ChildEvent Reference Count
                childEventTaskCallBack.IncreaseChildEventCount();

                childEventTaskContext.SetTaskTimeout(taskTimeout);

                bool moveToBlobPayloadRoutingChannel = isBlobPayload
                    && IsMoveToBlobPayloadRoutingChannel(childEventTaskContext.InputMessage, childActivity);

                if (moveToBlobPayloadRoutingChannel)
                {
                    childActivity[SolutionConstants.MoveToBlobPayloadRoutingChannel] = bool.TrueString;

                    // overwrite RetryTaskChannel
                    childEventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.RetryTaskChannelOverWriteBlobPayloadRouting;

                    var childNextChannel = SolutionInputOutputService.ARNMessageChannels.BlobPayloadRoutingChannelManager;
                    childActivity[SolutionConstants.NextChannel] = childNextChannel.ChannelName;

                    await childEventTaskContext.StartEventTaskAsync(childNextChannel, false, childActivity).ConfigureAwait(false);
                    return ChildTaskNextDestination.BlobPayloadRoutingChannel;
                }

                var moveToRetryChannel = childTaskId > _maxBatchedChildBeforeMoveToRetryQueue;
                if (moveToRetryChannel)
                {
                    // Start Task for internal initialization
                    // We should not provide startChannel here
                    await childEventTaskContext.StartEventTaskAsync(null, false, childActivity).ConfigureAwait(false);

                    // Add to RetryChannel
                    childEventTaskContext.TaskMovingToRetry(
                       RetryReason.LargeBatchedRawInput.FastEnumToString(),
                       $"Large Batched Raw Input. ChildTaskId: {childTaskId}, TotalChilds: {totalChilds}",
                       0,
                       IOComponent.RawInputChannel.FastEnumToString(),
                       null);

                    // Start Child Task
                    await childEventTaskContext.StartNextChannelAsync().ConfigureAwait(false);
                    return ChildTaskNextDestination.RetryChannel;
                }
                else
                {
                    var childNextChannel = SolutionInputOutputService.ARNMessageChannels.InputChannelManager;
                    childActivity[SolutionConstants.NextChannel] = childNextChannel.ChannelName;

                    // Start Child Task
                    await childEventTaskContext.StartEventTaskAsync(childNextChannel, false, childActivity).ConfigureAwait(false);
                    return ChildTaskNextDestination.InputChannel;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void LogRawInputSummary(
                int totalChild,
                int numFilteredChild,
                int numMovedToRetryChild,
                int numBlobPayloadRouting,
                int numInputChannelChild,
                OpenTelemetryActivityWrapper taskActivity,
                IActivity activity)
            {
                activity[SolutionConstants.TotalResourcesInRawInput] = totalChild;
                taskActivity.SetTag(SolutionConstants.TotalResourcesInRawInput, totalChild);

                activity[SolutionConstants.TotalFilteredChildTasks] = numFilteredChild;
                taskActivity.SetTag(SolutionConstants.TotalFilteredChildTasks, numFilteredChild);

                activity[SolutionConstants.TotalMovedToRetryChildTasks] = numMovedToRetryChild;
                taskActivity.SetTag(SolutionConstants.TotalMovedToRetryChildTasks, numMovedToRetryChild);

                activity[SolutionConstants.TotalMovedToBlobPayloadRoutingChildTasks] = numBlobPayloadRouting;
                taskActivity.SetTag(SolutionConstants.TotalMovedToBlobPayloadRoutingChildTasks, numBlobPayloadRouting);

                activity[SolutionConstants.TotalInputChannelChildTasks] = numInputChannelChild;
                taskActivity.SetTag(SolutionConstants.TotalInputChannelChildTasks, numInputChannelChild);
            }

            #endregion
        }

        private static Task UpdateMaxBatchedChild(int newVal)
        {
            if (newVal <= 0)
            {
                Logger.LogError("{config} must be larger than 0", InputOutputConstants.MaxBatchedChildBeforeMoveToRetryQueue);
                return Task.CompletedTask;
            }

            int oldVal = _maxBatchedChildBeforeMoveToRetryQueue;
            if (newVal == oldVal)
            {
                return Task.CompletedTask;
            }

            if (Interlocked.CompareExchange(ref _maxBatchedChildBeforeMoveToRetryQueue, newVal, oldVal) == oldVal)
            {
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    InputOutputConstants.MaxBatchedChildBeforeMoveToRetryQueue, oldVal, newVal);
            }

            return Task.CompletedTask;
        }

        private static Task UpdatePartnerBlobNonRetryableCodes(string newVal)
        {
            var newResponseCodeSet = newVal.ConvertToIntSet(stringSplitOptions: StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (newResponseCodeSet == null || newResponseCodeSet.Count == 0)
            {
                return Task.CompletedTask;
            }

            lock (_updateLock)
            {
                var oldVal = string.Join(ConfigMapExtensions.LIST_DELIMITER, _partnerBlobNonRetryableCodes);

                Interlocked.Exchange(ref _partnerBlobNonRetryableCodes, newResponseCodeSet);

                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", 
                    InputOutputConstants.PartnerBlobNonRetryableCodes, oldVal, newVal);
            }

            return Task.CompletedTask;
        }

        private static Task UpdateEnableBlobPayloadRouting(bool newVal)
        {
            var oldVal = _enableBlobPayloadRouting;

            lock (_updateLock)
            {
                if (newVal == oldVal)
                {
                    return Task.CompletedTask;
                }

                _enableBlobPayloadRouting = newVal;
            }

            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                InputOutputConstants.EnableBlobPayloadRouting, oldVal, newVal);

            return Task.CompletedTask;
        }


        private static Task UpdateBlobPayloadRoutingTypes(string newVal)
        {
            var newRoutingTypes = newVal?.ConvertToSet(false);
            if (newRoutingTypes == null || newRoutingTypes.Count == 0)
            {
                return Task.CompletedTask;
            }

            string oldVal;
            lock (_updateLock)
            {
                oldVal = string.Join(
                    ConfigMapExtensions.LIST_DELIMITER,
                Interlocked.Exchange(ref _blobPayloadRoutingTypes, newRoutingTypes) ?? new HashSet<string>());
            }

            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                InputOutputConstants.BlobPayloadRoutingTypes, oldVal, newVal);

            return Task.CompletedTask;
        }
    }
}
