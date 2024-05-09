namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.FinalChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PoisonChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RetryChannel;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    public class IOEventTaskContext<TInput> : AbstractEventTaskContext<IOEventTaskContext<TInput>>, IIOEventTaskContext where TInput : IInputMessage
    {
        // Constants
        private static readonly ActivityMonitorFactory IOEventTaskContextCriticalError = new("IOEventTaskContext.CriticalError", LogLevel.Critical);
        private static string componentDimensionName = "IOEventTaskContext";

        public override string Scenario { get; set; }
        public override IOEventTaskContext<TInput> TaskContext => this;

        // Data Source and Enqueued and PickedUp Times
        public DataSourceType DataSourceType { get; }
        public string DataSourceName { get; }
        public DateTimeOffset FirstEnqueuedTime { get; }
        public DateTimeOffset FirstPickedUpTime { get; }
        public DateTimeOffset DataEnqueuedTime { get; }
        public DateTimeOffset EventTime { get; }

        // Partner Spent Time
        private long _partnerTotalSpentTime;
        public long PartnerTotalSpentTime
        {
            get
            {
                return _partnerTotalSpentTime;
            }
            set
            {
                if (value > 0)
                {
                    _partnerTotalSpentTime = value;
                    if (EventTaskCallBack != null)
                    {
                        EventTaskCallBack.PartnerTotalSpentTime = value;
                    }
                }
            }
        }

        // Input Message
        public TInput InputMessage { get; set; }
        public IInputMessage BaseInputMessage => InputMessage;
        public OutputMessage OutputMessage { get; private set; }
        public string InputCorrelationId => InputMessage?.CorrelationId;

        // Task Cancellation Token And Source
        public override CancellationToken TaskCancellationToken { get; }

        // EventTaskCallBack, this could be composite EventTaskCallback
        public IEventTaskCallBack EventTaskCallBack { get; }
        public AbstractChildEventTaskCallBack<TInput> ChildEventTaskCallBack { get; private set; }

        // EventTaskFlag
        public IOEventTaskFlag IOEventTaskFlags { get; set; }

        // PartnerChannelName
        public string PartnerChannelName { get; set; }

        // Final Stage
        public override EventTaskFinalStage EventFinalStage { get; set; }

        // Retry Related
        public IRetryStrategy RetryStrategy { get; }
        public TimeSpan RetryDelay { get; set; }

        // Fail Related
        public string FailedComponent { get; private set; }
        public string FailedReason { get; private set; }
        public string FailedDescription { get; private set; }

        // Region data related
        public RegionConfig RegionConfigData { get; private set; }

        // Task Cancellation
        private CancellationTokenSource? _taskCancellationTokenSource;

        // Time Related (All TimeStamps are stamps returned from StopWatch, not milliseconds)
        private long _taskDoneStopWatchTimeStamp;

        // Task Channels        
        private readonly IConcurrencyManager _globalConcurrencyManager;
        private readonly IRetryChannelManager<TInput> _retryChannelManager; // Could be null -> Drop
        private readonly IPoisonChannelManager<TInput> _poisonChannelManager; // Could be null -> Drop
        private readonly IFinalChannelManager<TInput> _finalChannelManager;

        // Action guard
        private int _taskStarted;
        private int _taskCancelCalled;
        private int _taskTimeOutCalled;
        private int _taskDropCalled;
        private int _taskErrorCalled;
        private int _taskMovingToRetryCalled;
        private int _taskMovingToPoisonCalled;
        private int _taskSuccessCalled;
        private int _taskCompleted;

        private readonly object _disposeLock = new object();

        private volatile bool _concurrencyIncreased;
        private volatile bool _disposed;

        public IOEventTaskContext(
            string eventTaskType,
            DataSourceType dataSourceType,
            string dataSourceName,
            DateTimeOffset firstEnqueuedTime,
            DateTimeOffset firstPickedUpTime,
            DateTimeOffset dataEnqueuedTime,
            DateTimeOffset eventTime,
            TInput inputMessage,
            IEventTaskCallBack eventTaskCallBack,
            int retryCount,
            IRetryStrategy retryStrategy,
            ActivityContext parentActivityContext,
            DateTimeOffset topActivityStartTime,
            bool createNewTraceId,
            RegionConfig regionConfigData,
            CancellationToken parentCancellationToken,
            IRetryChannelManager<TInput> retryChannelManager,
            IPoisonChannelManager<TInput> poisonChannelManager,
            IFinalChannelManager<TInput> finalChannelManager,
            IConcurrencyManager globalConcurrencyManager) :
                base(IOServiceOpenTelemetry.IOActivitySource,
                    activityName: eventTaskType,
                    parentContext: parentActivityContext,
                    createNewTraceId: createNewTraceId,
                    retryCount: retryCount,
                    topActivityStartTime: topActivityStartTime)
        {
            DataSourceType = dataSourceType;
            DataSourceName = dataSourceName;
            FirstEnqueuedTime = firstEnqueuedTime == default ? DataEnqueuedTime : firstEnqueuedTime;
            FirstPickedUpTime = firstPickedUpTime == default ? DateTimeOffset.UtcNow : firstPickedUpTime;
            DataEnqueuedTime = dataEnqueuedTime == default ? DateTimeOffset.UtcNow : dataEnqueuedTime;
            EventTime = eventTime == default ? (inputMessage?.EventTime ?? default) : eventTime;

            InputMessage = inputMessage;
            EventTaskCallBack = eventTaskCallBack;
            RetryStrategy = retryStrategy;

            RegionConfigData = regionConfigData;
            _retryChannelManager = retryChannelManager;
            _poisonChannelManager = poisonChannelManager;
            _finalChannelManager = finalChannelManager;
            _globalConcurrencyManager = globalConcurrencyManager;

            _taskCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentCancellationToken);
            TaskCancellationToken = _taskCancellationTokenSource.Token;

            EventTaskActivity.SetTag(SolutionConstants.DataSourceType, DataSourceType.FastEnumToString());
            EventTaskActivity.SetTag(SolutionConstants.DataSourceName, DataSourceName);
            EventTaskActivity.SetTag(SolutionConstants.RetryCount, RetryCount);
            EventTaskActivity.SetTag(SolutionConstants.DataEnqueuedTime, DataEnqueuedTime);
            EventTaskActivity.SetTag(SolutionConstants.RegionName, this.RegionConfigData.RegionLocationName);


            if (InputMessage != null)
            {
                InputMessage.AddCommonTags(EventTaskActivity);
                if (InputMessage.HasDeserializedObject)
                {
                    EventTaskActivity.SetTag(SolutionConstants.HasDeserializedObject, true);
                }
                EventTaskActivity.InputCorrelationId = InputMessage.CorrelationId;
                EventTaskActivity.InputResourceId = InputMessage.ResourceId;
            }

            EventTaskActivity.AddEvent(SolutionConstants.EventName_TaskCreated);

            if (FirstEnqueuedTime != default)
            {
                if (IOServiceOpenTelemetry.IsInputProviderRelatedTask(eventTaskType))
                {
                    if (FirstPickedUpTime != default)
                    {
                        var fromEnqueuedTimeToFirstPickedUpTime = (int)(FirstPickedUpTime - FirstEnqueuedTime).TotalMilliseconds;
                        if (fromEnqueuedTimeToFirstPickedUpTime <= 0)
                        {
                            fromEnqueuedTimeToFirstPickedUpTime = 1;
                        }

                        EventTaskActivity.SetTag(InputOutputConstants.DelayFromInputEnqueueToPickup, fromEnqueuedTimeToFirstPickedUpTime);

                        IOServiceOpenTelemetry.DelayFromInputEnqueueToPickupMetric.Record(fromEnqueuedTimeToFirstPickedUpTime,
                            new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, EventTaskActivity.ActivityName),
                            new KeyValuePair<string, object>(MonitoringConstants.DataSourceTypeDimension, DataSourceType.FastEnumToString()),
                            new KeyValuePair<string, object>(MonitoringConstants.DataSourceNameDimension, DataSourceName));

                        if (EventTime != default)
                        {
                            var fromEventTimeToInputEnqueue = (int)(EventTime - FirstPickedUpTime).TotalMilliseconds;
                            if (fromEventTimeToInputEnqueue <= 0)
                            {
                                fromEventTimeToInputEnqueue = 1;
                            }

                            EventTaskActivity.SetTag(InputOutputConstants.DelayFromEventTimeToInputEnqueue, fromEventTimeToInputEnqueue);

                            IOServiceOpenTelemetry.DelayFromEventTimeToInputEnqueue.Record(fromEventTimeToInputEnqueue,
                                new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, EventTaskActivity.ActivityName),
                                new KeyValuePair<string, object>(MonitoringConstants.DataSourceTypeDimension, DataSourceType.FastEnumToString()),
                                new KeyValuePair<string, object>(MonitoringConstants.DataSourceNameDimension, DataSourceName));
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsTaskInFinalStageChannel()
        {
            return IOEventTaskFlagHelper.IsTaskInFinalStageChannel(IOEventTaskFlags);
        }

        public long GetPartnerMaxTotalSpentTimeIncludingChildTasks()
        {
            if (PartnerTotalSpentTime == 0 && ChildEventTaskCallBack != null)
            {
                return ChildEventTaskCallBack.PartnerTotalSpentTime;
            }

            return PartnerTotalSpentTime;
        }

        public void SetChildEventTaskCallBack(AbstractChildEventTaskCallBack<TInput> childEventTaskCallBack)
        {
            ChildEventTaskCallBack = childEventTaskCallBack;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOutputMessage(OutputMessage outputMessage)
        {
            OutputMessage = outputMessage;

            if (outputMessage == null)
            {
                return;
            }

            EventTaskActivity.OutputResourceId = outputMessage.ResourceId;
            EventTaskActivity.OutputCorrelationId = outputMessage.CorrelationId;

            EventTaskActivity.SetTag(SolutionConstants.OutputResourceType, outputMessage.ResourceType);
            EventTaskActivity.SetTag(SolutionConstants.OutputTimeStamp, outputMessage.OutputTimeStamp);

            EventTaskActivity.AddEvent(InputOutputConstants.EventName_OutputMessageAdded);
        }

        public override async Task StartEventTaskAsync(
            ITaskChannelManager<IOEventTaskContext<TInput>> startChannel, bool waitForTaskFinish, IActivity? parentActivity)
        {

            var needCallStartAgain = false;

            try
            {
                if (_taskStarted > 0 || Interlocked.CompareExchange(ref _taskStarted, 1, 0) != 0)
                {
                    // Task already started
                    return;
                }

                var currentUtcTime = DateTimeOffset.UtcNow;
                // EventHub Read Delay
                if (FirstEnqueuedTime != default)
                {
                    var delayFromInputEnqueueToStart = (int)(currentUtcTime - FirstEnqueuedTime).TotalMilliseconds;
                    if (delayFromInputEnqueueToStart <= 0)
                    {
                        delayFromInputEnqueueToStart = 1;
                    }

                    EventTaskActivity.SetTag(InputOutputConstants.DelayFromInputEnqueueToStart, delayFromInputEnqueueToStart);

                    IOServiceOpenTelemetry.DelayFromInputEnqueueToStartMetric.Record(delayFromInputEnqueueToStart,
                        new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, EventTaskActivity.ActivityName),
                        new KeyValuePair<string, object>(MonitoringConstants.DataSourceTypeDimension, DataSourceType.FastEnumToString()),
                        new KeyValuePair<string, object>(MonitoringConstants.DataSourceNameDimension, DataSourceName));
                }

                // Increase Global Task Concurrency
                if (_globalConcurrencyManager != null)
                {
                    await _globalConcurrencyManager.AcquireResourceAsync(TaskCancellationToken).IgnoreContext();
                    _concurrencyIncreased = true;
                }

                // Start Delay
                var startDealyInMilli = (int)EventTaskActivity.Elapsed.TotalMilliseconds;
                EventTaskActivity.SetTag(InputOutputConstants.StartDelay, startDealyInMilli);

                IOServiceOpenTelemetry.StartDelayMetric.Record(startDealyInMilli,
                    new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, EventTaskActivity.ActivityName));

                TagList tagList = default;
                EventTaskCallBack.TaskStarted(this, ref tagList);
                EventTaskActivity.AddEvent(SolutionConstants.EventName_TaskStarted, tagList);

                if (startChannel != null)
                {
                    await base.StartEventTaskAsync(startChannel, waitForTaskFinish, parentActivity).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                EventTaskActivity.AddEvent(SolutionConstants.EventName_TaskStartFailed);

                if (startChannel != null)
                {
                    SetNextChannel(null);
                    TaskMovingToPoison(SolutionUtils.GetExceptionTypeSimpleName(ex), null, "IOEventTaskContext.StartEventTaskAsync", ex);
                    startChannel = NextTaskChannel;
                    needCallStartAgain = true;
                }
            }

            if (needCallStartAgain)
            {
                await base.StartEventTaskAsync(startChannel, waitForTaskFinish, parentActivity).ConfigureAwait(false);
            }
        }

        public void DecreaseGlobalConcurrency()
        {
            // Usually this is called for parent Task when child tasks are created
            if (_concurrencyIncreased)
            {
                _globalConcurrencyManager.ReleaseResource();
                _concurrencyIncreased = false;
            }
        }

        protected override void TaskTimeoutHandler()
        {
            try
            {
                // check if Any final stage call is already called
                if (IsTaskInFinalStageChannel() ||
                   _taskCancelCalled > 0 || _taskDropCalled > 0 ||
                   _taskErrorCalled > 0 || _taskMovingToRetryCalled > 0 ||
                   _taskMovingToPoisonCalled > 0 ||
                   _taskSuccessCalled > 0 || _taskCompleted > 0)
                {
                    // Channel is already in Error Channel OR moving to final Stage.
                    // we don't need timeHandler Task
                    return;
                }

                if (_taskTimeOutCalled > 0 || Interlocked.CompareExchange(ref _taskTimeOutCalled, 1, 0) != 0)
                {
                    // Task Timeout Handler is already called
                    return;
                }

                CancelTaskTimeout();
                EventTaskCallBack.TaskTimeoutCalled(this);

                // Do not call CancelTask because it will move Task to Drop
                // we have to just cancel cancellation token
                lock (_disposeLock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    // cancel TaskCancellationToken
                    _taskCancellationTokenSource?.Cancel();
                }
            }
            catch (Exception ex)
            {
                // should not happen
                LogCritical(ex, "TaskTimeoutHandler");
            }
        }

        public override void CancelTask()
        {
            if (_taskCancelCalled > 0 || Interlocked.CompareExchange(ref _taskCancelCalled, 1, 0) != 0)
            {
                // Task already cancelled
                return;
            }

            EventTaskCallBack.TaskCancelCalled(this);
            // cancel Task TimeOut
            CancelTaskTimeout();

            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                // cancel TaskCancellationToken
                _taskCancellationTokenSource?.Cancel();

                // Cancel child Tasks
                var childEventTaskCallBack = ChildEventTaskCallBack;
                childEventTaskCallBack?.CancelAllChildTasks("ParentTask is cancelled", null);
            }

            // Cancelling Task will be moved to Drop
        }

        public override bool IsAlreadyTaskCancelled()
        {
            return _taskCancelCalled > 0 || EventTaskCallBack.IsTaskCancelled;
        }

        public void TaskDrop(string dropReason, string reasonDetails, string fromComponent)
        {
            if (IsAlreadyTaskDropCalled())
            {
                return;
            }

            try
            {
                FailedComponent ??= fromComponent;

                IOServiceOpenTelemetry.DroppedCounter.Add(1,
                    new KeyValuePair<string, object>(MonitoringConstants.COMPONENT_DIMENSION, componentDimensionName),
                    new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, EventTaskActivity.ActivityName),
                    new KeyValuePair<string, object>(MonitoringConstants.ReasonDimension, dropReason),
                    new KeyValuePair<string, object>(MonitoringConstants.RetryCountDimension, RetryCount));

                _taskDoneStopWatchTimeStamp = Stopwatch.GetTimestamp();

                var reason = dropReason;
                SetFailedReason(reason, reasonDetails, fromComponent);

                TagList tagList = default;
                tagList.Add("DropReason", reason);
                tagList.Add("DropDetails", reasonDetails);
                tagList.Add("DropFromComponent", fromComponent);

                EventFinalStage = EventTaskFinalStage.DROP;
                EventTaskActivity.AddEvent(SolutionConstants.EventName_TaskDropped, tagList);
                EventTaskActivity.SetTag(SolutionConstants.TaskFinalStage, EventFinalStage.FastEnumToString());

                SetNextChannel(_finalChannelManager);
                EventTaskCallBack.TaskDropped(this);
                return;
            }
            catch (Exception ex)
            {
                // should not happen
                EventTaskActivity.RecordException("TaskDropped", ex);
                LogCritical(ex, "TaskDropped");

                // Last resort because we fail to move to final channel
                TaskProcessCompleted(true);
            }
        }

        public void TaskMovedToEventHub(long writeDurationInMilli, int batchCount, long stopWatchTimestamp)
        {
            try
            {
                EventTaskActivity.SetTag(InputOutputConstants.EventHubWriteSuccess, true);
                EventTaskActivity.SetTag(InputOutputConstants.EventHubWriteDuration, writeDurationInMilli);
                EventTaskActivity.SetTag(InputOutputConstants.EventHubBatchSize, batchCount);

                TaskSuccess(stopWatchTimestamp);
            }
            catch (Exception ex)
            {
                // should not happen
                EventTaskActivity.RecordException("TaskMovedToEventHub", ex);
                LogCritical(ex, "TaskMovedToEventHub");

                // Last Resort
                TaskDrop(SolutionUtils.GetExceptionTypeSimpleName(ex), ex.Message, IOComponent.EventHubWriter.FastEnumToString());
            }
        }

        public void TaskFailedToMoveToEventHub(Exception taskException, long writeDurationInMilli, int count, int retryDelay)
        {
            try
            {
                EventTaskActivity.SetTag(InputOutputConstants.EventHubWriteSuccess, false);
                EventTaskActivity.SetTag(InputOutputConstants.EventHubWriteDuration, writeDurationInMilli);
                EventTaskActivity.SetTag(InputOutputConstants.EventHubWriteFailedCountInSameBatch, count);

                TaskError(taskException, IOComponent.EventHubWriter.FastEnumToString(), retryDelay);
            }
            catch (Exception ex)
            {
                // should not happen
                EventTaskActivity.RecordException("TaskFailedToMoveToEventHub", ex);
                LogCritical(ex, "TaskFailedToMoveToEventHub");

                TaskMovingToPoison(SolutionUtils.GetExceptionTypeSimpleName(ex), null, IOComponent.EventHubWriter.FastEnumToString(), ex);
            }

            return;
        }

        public void TaskMovedToArn(long writeDurationInMilli, int batchCount, long stopWatchTimestamp,
            IOComponent component, string successTag, string durationTag, string batchSizeTag)
        {
            try
            {
                GuardHelper.ArgumentNotNullOrEmpty(successTag);
                GuardHelper.ArgumentNotNullOrEmpty(durationTag);
                GuardHelper.ArgumentNotNullOrEmpty(batchSizeTag);

                EventTaskActivity.SetTag(successTag, true);
                EventTaskActivity.SetTag(durationTag, writeDurationInMilli);
                EventTaskActivity.SetTag(batchSizeTag, batchCount);

                TaskSuccess(stopWatchTimestamp);
            }
            catch (Exception ex)
            {
                // should not happen
                string componentName = component.SafeFastEnumToString();
                string eventName = $"TaskMovedToArn_{componentName}";
                EventTaskActivity.RecordException(eventName, ex);
                LogCritical(ex, eventName);

                // Last Resort
                TaskDrop(SolutionUtils.GetExceptionTypeSimpleName(ex), ex.Message, componentName);
            }
        }

        public void TaskFailedToMoveToArn(Exception taskException, long writeDurationInMilli, int batchCount, int retryDelay,
            IOComponent component, string successTag, string durationTag, string batchSizeTag)
        {
            string componentName = component.SafeFastEnumToString();

            try
            {
                GuardHelper.ArgumentNotNullOrEmpty(successTag);
                GuardHelper.ArgumentNotNullOrEmpty(durationTag);
                GuardHelper.ArgumentNotNullOrEmpty(batchSizeTag);

                EventTaskActivity.SetTag(successTag, false);
                EventTaskActivity.SetTag(durationTag, writeDurationInMilli);
                EventTaskActivity.SetTag(batchSizeTag, batchCount);

                TaskError(taskException, componentName, retryDelay);
            }
            catch (Exception ex)
            {
                // should not happen
                string eventName = $"TaskFailedToMoveToArn_{componentName}";
                EventTaskActivity.RecordException(eventName, ex);
                LogCritical(ex, eventName);

                TaskMovingToPoison(SolutionUtils.GetExceptionTypeSimpleName(ex), null, componentName, ex);
            }

            return;
        }

        public override void TaskMovingToRetry(string retryReason, string reasonDetails,
            int retryDelayMs, string fromComponent, Exception taskException)
        {
            if (IsAlreadyTaskMovingToRetryCalled())
            {
                return;
            }

            if (IsAlreadyTaskCancelled())
            {
                EventTaskActivity.SetTag(SolutionConstants.TaskCancelled, true);
                TaskDrop(DropReason.TaskCancelCalled.FastEnumToString(), reasonDetails ?? taskException?.Message, fromComponent);
                return;
            }

            FailedComponent ??= fromComponent;

            IOServiceOpenTelemetry.RetryQueueMovingCounter.Add(1,
                new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, EventTaskActivity.ActivityName),
                new KeyValuePair<string, object>(MonitoringConstants.ReasonDimension, retryReason),
                new KeyValuePair<string, object>(MonitoringConstants.RetryCountDimension, RetryCount));

            if (_retryChannelManager == null)
            {
                // Moving to Drop
                TaskDrop(retryReason, reasonDetails ?? taskException?.Message ?? retryReason, fromComponent);
                return;
            }

            try
            {
                var reason = retryReason;
                reasonDetails ??= taskException?.Message ?? "";
                SetFailedReason(reason, reasonDetails, fromComponent);

                var poisonReason = PoisonReason.None;

                if (RetryCount >= RetryStrategy.MaxRetryCount)
                {
                    poisonReason = PoisonReason.MaxRetryLimit;
                    RetryDelay = TimeSpan.Zero;
                }
                else
                {
                    retryDelayMs = retryDelayMs < 0 ? 0 : retryDelayMs;

                    // Use RetryStrategy
                    if (RetryStrategy.ShouldRetry(RetryCount, taskException, out TimeSpan retryAfter))
                    {
                        RetryDelay = retryAfter;
                        if (retryDelayMs > 0)
                        {
                            // RetryDelay is explicitly specified
                            // Let's add the explicit delay to the retry delay
                            RetryDelay += TimeSpan.FromMilliseconds(retryDelayMs);
                        }
                    }
                    else
                    {
                        // Move to Poison
                        poisonReason = PoisonReason.RetryStrategy;
                        RetryDelay = TimeSpan.Zero;
                    }
                }

                EventTaskActivity.RecordException(fromComponent, taskException);

                TagList tagList = default;
                tagList.Add("RetryReason", reason);
                tagList.Add("RetryDetails", reasonDetails);
                tagList.Add("RetryFromComponent", fromComponent);
                tagList.Add("CurrentRetry", RetryCount);
                tagList.Add("RetryDelayInMilli", RetryDelay.TotalMilliseconds);

                EventTaskActivity.AddEvent(InputOutputConstants.EventName_TaskMovingToRetry, tagList);

                if (poisonReason == PoisonReason.None)
                {
                    SetNextChannel(_retryChannelManager);
                    return;
                }
                else
                {
                    TaskMovingToPoison(poisonReason.FastEnumToString(),
                        "CurrentRetryCount is " + RetryCount, IOComponent.RetryChannel.FastEnumToString(), null);
                    return;
                }
            }
            catch (Exception ex)
            {
                // should not happen
                EventTaskActivity.RecordException("TaskMovingToRetry", ex);
                LogCritical(ex, "TaskMovingToRetry");

                // Moving to Retry fails -> Poison
                TaskMovingToPoison(SolutionUtils.GetExceptionTypeSimpleName(ex), null, IOComponent.RetryChannel.FastEnumToString(), ex);
                return;
            }
        }

        public void TaskMovedToRetry(long writeDurationInMilli, int batchCount, long stopWatchTimestamp)
        {
            try
            {
                _taskDoneStopWatchTimeStamp = stopWatchTimestamp;

                EventTaskActivity.SetTag(InputOutputConstants.RetryQueueWriteSuccess, true);
                EventTaskActivity.SetTag(InputOutputConstants.RetryQueueWriteDuration, writeDurationInMilli);
                EventTaskActivity.SetTag(InputOutputConstants.RetryQueueBatchSize, batchCount);

                EventFinalStage = EventTaskFinalStage.RETRY_QUEUE;
                EventTaskActivity.AddEvent(InputOutputConstants.EventName_TaskMovedToRetry);
                EventTaskActivity.SetTag(SolutionConstants.TaskFinalStage, EventFinalStage.FastEnumToString());

                SetNextChannel(_finalChannelManager);
                EventTaskCallBack.TaskMovedToRetry(this);
                return;
            }
            catch (Exception ex)
            {
                // should not happen
                EventTaskActivity.RecordException("TaskMovedToRetry", ex);
                LogCritical(ex, "TaskMovedToRetry");

                // Last resort because we fail to move to final channel
                TaskProcessCompleted(true);
            }
        }

        public void TaskFailedToMoveToRetry(string poisonReason, Exception retryException, long writeDurationInMilli, int count)
        {
            try
            {
                EventTaskActivity.SetTag(InputOutputConstants.RetryQueueWriteSuccess, false);
                EventTaskActivity.SetTag(InputOutputConstants.RetryQueueWriteDuration, writeDurationInMilli);
                EventTaskActivity.SetTag(InputOutputConstants.RetryQueueFailedCountInSameBatch, count);

                IOEventTaskFlags |= IOEventTaskFlag.FailedToMoveToRetry;

                EventTaskActivity.RecordException(IOComponent.RetryChannel.FastEnumToString(), retryException);
                EventTaskActivity.AddEvent(InputOutputConstants.EventName_TaskFailedToMoveToRetry);

                // Moving to Retry fails -> Poison
                TaskMovingToPoison(poisonReason, null, IOComponent.RetryChannel.FastEnumToString(), retryException);
                return;
            }
            catch (Exception ex)
            {
                // Shoud not happen
                EventTaskActivity.RecordException("TaskFailedToMoveToRetryAsync", ex);
                LogCritical(ex, "TaskFailedToMoveToRetryAsync");

                // Last Resort
                TaskDrop(SolutionUtils.GetExceptionTypeSimpleName(ex), ex.Message, IOComponent.PoisonChannel.FastEnumToString());
            }
        }

        // It should not throw exception
        public override void TaskMovingToPoison(string poisonReason, string reasonDetails,
            string fromComponent, Exception taskException)
        {
            // Check all possible error situation which could happen inside poison channel
            if (IsAlreadyTaskCancelled())
            {
                EventTaskActivity.SetTag(SolutionConstants.TaskCancelled, true);
                TaskDrop(DropReason.TaskCancelCalled.FastEnumToString(), reasonDetails ?? taskException?.Message, fromComponent);
                return;
            }

            if (_poisonChannelManager != null && CurrentTaskChannel == _poisonChannelManager)
            {
                // This is possible where we get some error in poison channel
                // Poison is called inside PoisonChannel
                // Move to Drop
                EventTaskActivity.SetTag(InputOutputConstants.PoisonInsidePoison, true);
                TaskDrop(DropReason.PoisonInsidePoison.FastEnumToString(), reasonDetails ?? taskException?.Message, fromComponent);
                return;
            }

            IOServiceOpenTelemetry.PoisonQueueMovingCounter.Add(1,
                new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, EventTaskActivity.ActivityName),
                new KeyValuePair<string, object>(MonitoringConstants.ReasonDimension, poisonReason),
                new KeyValuePair<string, object>(MonitoringConstants.RetryCountDimension, RetryCount));

            if (_poisonChannelManager == null)
            {
                // Moving to Drop
                TaskDrop(poisonReason, reasonDetails ?? taskException?.Message, fromComponent);
                return;
            }

            if (IsAlreadyTaskMovingToPoisonCalled())
            {
                return;
            }

            if (SolutionInputOutputService.DropPoisonMessage)
            {
                // Drop Poison Message
                EventTaskActivity.SetTag(InputOutputConstants.DropPoisonMessage, true);
                TaskDrop(poisonReason, reasonDetails ?? taskException?.Message, fromComponent);
                return;
            }

            try
            {
                FailedComponent ??= fromComponent;

                EventTaskActivity.RecordException(fromComponent, taskException);

                var reason = poisonReason;
                reasonDetails ??= taskException?.Message ?? "";

                SetFailedReason(reason, reasonDetails, fromComponent);

                TagList tagList = default;
                tagList.Add("PoisonReason", reason);
                tagList.Add("PoisonDetails", reasonDetails);
                tagList.Add("PoisonFromComponent", fromComponent);

                EventTaskActivity.AddEvent(InputOutputConstants.EventName_TaskMovingToPoison, tagList);

                SetNextChannel(_poisonChannelManager);
                return;
            }
            catch (Exception ex)
            {
                // should not happen
                EventTaskActivity.RecordException("TaskMovingToPoisonAsync", ex);
                LogCritical(ex, "TaskMovingToPoisonAsync");

                // Moving to Poison fail -> Drop
                TaskDrop(SolutionUtils.GetExceptionTypeSimpleName(ex), ex.Message, IOComponent.PoisonChannel.FastEnumToString());
            }
        }

        public void TaskMovedToPoison(long writeDurationInMilli, int batchCount, long stopWatchTimestamp)
        {
            try
            {
                _taskDoneStopWatchTimeStamp = stopWatchTimestamp;

                EventTaskActivity.SetTag(InputOutputConstants.PoisonQueueWriteSuccess, true);
                EventTaskActivity.SetTag(InputOutputConstants.PoisonQueueWriteDuration, writeDurationInMilli);
                EventTaskActivity.SetTag(InputOutputConstants.PoisonQueueBatchSize, batchCount);

                EventFinalStage = EventTaskFinalStage.POISON_QUEUE;
                EventTaskActivity.AddEvent(InputOutputConstants.EventName_TaskMovedToPoison);
                EventTaskActivity.SetTag(SolutionConstants.TaskFinalStage, EventFinalStage.FastEnumToString());

                SetNextChannel(_finalChannelManager);
                EventTaskCallBack.TaskMovedToPoison(this);
                return;
            }
            catch (Exception ex)
            {
                // should not happen
                EventTaskActivity.RecordException("TaskMovedToPoison", ex);
                LogCritical(ex, "TaskMovedToPoison");

                // Last resort because we fail to move to final channel
                TaskProcessCompleted(true);
            }
        }

        public void TaskFailedToMoveToPoison(string failedReason, Exception poisonException, long writeDurationInMilli, int count)
        {
            try
            {
                EventTaskActivity.SetTag(InputOutputConstants.PoisonQueueWriteSuccess, false);
                EventTaskActivity.SetTag(InputOutputConstants.PoisonQueueWriteDuration, writeDurationInMilli);
                EventTaskActivity.SetTag(InputOutputConstants.PoisonQueueFailedCountInSameBatch, count);

                IOEventTaskFlags |= IOEventTaskFlag.FailedToMoveToPoison;

                EventTaskActivity.RecordException(IOComponent.PoisonChannel.FastEnumToString(), poisonException);
                EventTaskActivity.AddEvent(InputOutputConstants.EventName_TaskFailedToMoveToPoison);

                // Moving to Poison fails -> Drop
                TaskDrop(failedReason, poisonException?.Message, IOComponent.PoisonChannel.FastEnumToString());
                return;

            }
            catch (Exception ex)
            {
                EventTaskActivity.RecordException("TaskFailedToMoveToPoison", ex);
                LogCritical(ex, "TaskFailedToMoveToPoison");

                // Last Resort
                TaskDrop(SolutionUtils.GetExceptionTypeSimpleName(ex), ex.Message, IOComponent.PoisonChannel.FastEnumToString());
            }
        }

        public void TaskError(Exception taskException, string failedComponent, int retryDelayMs)
        {
            if (IsAlreadyTaskErrorCalled())
            {
                return;
            }

            if (IsAlreadyTaskCancelled())
            {
                EventTaskActivity.SetTag(SolutionConstants.TaskCancelled, true);
                TaskDrop(DropReason.TaskCancelCalled.FastEnumToString(), taskException?.Message, failedComponent);
                return;
            }

            try
            {
                var retryReason = SolutionUtils.GetExceptionTypeSimpleName(taskException);

                FailedComponent ??= failedComponent;

                EventTaskCallBack.TaskErrorCalled(this, taskException);

                EventTaskActivity.RecordException(failedComponent, taskException);

                TagList tagList = default;
                tagList.Add(SolutionConstants.TaskFailedComponent, failedComponent);

                EventTaskActivity.AddEvent(SolutionConstants.EventName_TaskError, tagList);

                // Move to Retry Queue
                TaskMovingToRetry(retryReason, null, retryDelayMs, failedComponent, taskException);
                return;
            }
            catch (Exception ex)
            {
                // should not happen
                EventTaskActivity.RecordException("TaskErrorAsync", ex);
                LogCritical(ex, "TaskErrorAsync");

                // Moving to Retry fails -> Poison
                TaskMovingToPoison(SolutionUtils.GetExceptionTypeSimpleName(ex), null, failedComponent, ex);

                return;
            }
        }

        public void TaskSuccess(long stopWatchTimestamp)
        {
            if (IsAlreadyTaskSuccessCalled())
            {
                return;
            }

            try
            {
                _taskDoneStopWatchTimeStamp = stopWatchTimestamp <= 0 ? Stopwatch.GetTimestamp() : stopWatchTimestamp;

                EventFinalStage = EventTaskFinalStage.SUCCESS;
                EventTaskActivity.AddEvent(SolutionConstants.EventName_TaskSuccess);

                EventTaskActivity.SetTag(SolutionConstants.TaskFinalStage, EventFinalStage.FastEnumToString());
                EventTaskActivity.SetTag(SolutionConstants.TaskChannelBeforeSuccess, CurrentTaskChannel?.ChannelName);

                SetNextChannel(_finalChannelManager);
                EventTaskCallBack.TaskSuccess(this);
                return;
            }
            catch (Exception ex)
            {
                // should not happen
                EventTaskActivity.RecordException("TaskSuccess", ex);
                LogCritical(ex, "TaskSuccess");

                // Last resort because we fail to move to final channel
                TaskProcessCompleted(true);
            }
        }

        internal void TaskProcessCompleted(bool callDispose)
        {
            if (_taskCompleted > 0 || Interlocked.CompareExchange(ref _taskCompleted, 1, 0) != 0)
            {
                return;
            }

            try
            {
                if (_concurrencyIncreased)
                {
                    _globalConcurrencyManager.ReleaseResource();
                    _concurrencyIncreased = false;
                }

                var success = IOEventTaskFlagHelper.IsSuccessFinalStage(EventFinalStage);

                EventTaskActivity.SetTag(SolutionConstants.PartnerResponseFlags, IOEventTaskFlags.GetPartnerResponseFlags());
                EventTaskActivity.SetTag(SolutionConstants.TaskFinalStatus, GetFinalStatus(success));

                // Metric
                var endTimeStamp = _taskDoneStopWatchTimeStamp > 0 ? _taskDoneStopWatchTimeStamp : Stopwatch.GetTimestamp();
                var e2edurationInMilli = (int)Stopwatch.GetElapsedTime(EventTaskActivity.CreatedStopWatchTimeStamp, endTimeStamp).TotalMilliseconds;
                e2edurationInMilli = e2edurationInMilli <= 0 ? 1 : e2edurationInMilli;

                EventTaskActivity.SetTag(InputOutputConstants.E2EDuration, e2edurationInMilli);

                var currentUtcTime = DateTimeOffset.UtcNow;

                IOServiceOpenTelemetry.ProcessedCounter.Add(1,
                    new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, EventTaskActivity.ActivityName),
                    new KeyValuePair<string, object>(MonitoringConstants.RetryCountDimension, RetryCount),
                    MonitoringConstants.GetSuccessDimension(success));

                IOServiceOpenTelemetry.E2EDurationMetric.Record(e2edurationInMilli,
                    new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, EventTaskActivity.ActivityName),
                    new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, RetryCount > 0),
                    MonitoringConstants.GetSuccessDimension(success));

                if (HasTaskTimeoutExpired)
                {
                    EventTaskActivity.SetTag(SolutionConstants.IsTimeoutExpired, true);
                    IOServiceOpenTelemetry.TaskTimeoutExpiredCounter.Add(1,
                        new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, EventTaskActivity.ActivityName),
                        new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, RetryCount > 0));
                }
            }
            catch (Exception ex)
            {
                // Should not happen
                LogCritical(ex, "TaskProcessCompleted");
            }
            finally
            {
                // Input and Output resources will be release here
                // So make sure that metrics will not use it. 
                if (callDispose)
                {
                    Dispose();
                }
            }
        }

        private string GetFinalStatus(bool success)
        {
            if (success)
            {
                return SolutionConstants.Success;
            }

            if (HasTaskTimeoutExpired)
            {
                return SolutionConstants.IsTimeoutExpired;
            }
            else if (IsAlreadyTaskCancelled())
            {
                return EventTaskCallBack.IsTaskCancelled ?
                    EventTaskCallBack.TaskCancelledReason : SolutionConstants.IsCancelled;
            }
            else
            {
                return SolutionConstants.Fail;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogCritical(Exception ex, string failedMethod)
        {
            using var criticalLogMonitor = IOEventTaskContextCriticalError.ToMonitor(component: "IOEventTaskContext");
            criticalLogMonitor.Activity[SolutionConstants.MethodName] = failedMethod;
            criticalLogMonitor.Activity[SolutionConstants.CorrelationId] = InputCorrelationId;
            criticalLogMonitor.Activity[SolutionConstants.DataSourceType] = DataSourceType.FastEnumToString();
            criticalLogMonitor.Activity[SolutionConstants.DataSourceName] = DataSourceName;
            criticalLogMonitor.OnError(ex, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsAlreadyTaskDropCalled()
        {
            if (_taskDropCalled > 0 || Interlocked.CompareExchange(ref _taskDropCalled, 1, 0) != 0)
            {
                // Should not happen
                Exception ex = new InvalidOperationException("TaskMethod is already called");
                LogCritical(ex, "IsAlreadyTaskDropCalled");
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsAlreadyTaskErrorCalled()
        {
            if (_taskErrorCalled > 0 || Interlocked.CompareExchange(ref _taskErrorCalled, 1, 0) != 0)
            {
                // Should not happen
                Exception ex = new InvalidOperationException("TaskMethod is already called");
                LogCritical(ex, "IsAlreadyTaskErrorCalled");
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlreadyTaskMovingToRetryCalled()
        {
            if (_taskMovingToRetryCalled > 0 || Interlocked.CompareExchange(ref _taskMovingToRetryCalled, 1, 0) != 0)
            {
                // Should not happen
                Exception ex = new InvalidOperationException("TaskMethod is already called");
                LogCritical(ex, "IsAlreadyTaskMovingToRetryCalled");
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlreadyTaskMovingToPoisonCalled()
        {
            if (_taskMovingToPoisonCalled > 0 || Interlocked.CompareExchange(ref _taskMovingToPoisonCalled, 1, 0) != 0)
            {
                // Should not happen
                Exception ex = new InvalidOperationException("TaskMethod is already called");
                LogCritical(ex, "IsAlreadyTaskMovingToPoisonCalled");
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlreadyTaskSuccessCalled()
        {
            if (_taskSuccessCalled > 0 || Interlocked.CompareExchange(ref _taskSuccessCalled, 1, 0) != 0)
            {
                // Should not happen
                Exception ex = new InvalidOperationException("TaskMethod is already called");
                LogCritical(ex, "IsAlreadyTaskSuccessCalled");
                return true;
            }
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;

                if (FailedComponent?.Length > 0)
                {
                    EventTaskActivity.SetTag(SolutionConstants.TaskFailedComponent, FailedComponent);
                }

                if (FailedReason?.Length > 0)
                {
                    EventTaskActivity.SetTag(SolutionConstants.TaskFailedReason, FailedReason);
                }

                if (FailedDescription?.Length > 0)
                {
                    EventTaskActivity.SetTag(SolutionConstants.TaskFailedDescription, FailedDescription);
                }

                var success = IOEventTaskFlagHelper.IsSuccessFinalStage(EventFinalStage);
                EventTaskActivity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

                CancelTaskTimeout();

                _taskCancellationTokenSource?.Dispose();
                _taskCancellationTokenSource = null;

                EventTaskCallBack.FinalCleanup();

                ChildEventTaskCallBack = null;
                InputMessage = default;
                OutputMessage = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetFailedReason(string reason, string description, string fromComponent)
        {
            // Reason is mandatory field
            if (string.IsNullOrEmpty(reason))
            {
                return;
            }

            if (FailedReason == null)
            {
                FailedReason = reason;
            }
            else if (FailedReason.Length < 128)
            {
                FailedReason = FailedReason + "->" + reason;
            }

            if (FailedReason.Length > 128)
            {
                FailedReason = FailedReason[..128];
            }

            description ??= fromComponent;

            if (FailedDescription == null)
            {
                FailedDescription = description;
            }
            else if (FailedDescription.Length < 128)
            {
                FailedDescription = FailedDescription + "->" + description;
            }

            if (FailedDescription.Length > 128)
            {
                FailedDescription = FailedDescription[..128];
            }
        }
    }
}
