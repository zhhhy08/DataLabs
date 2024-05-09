namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;

    public class EventHubAsyncTaskInfoQueue : IDisposable
    {
        private static readonly ILogger<EventHubAsyncTaskInfoQueue> Logger = DataLabLoggerFactory.CreateLogger<EventHubAsyncTaskInfoQueue>();

        private static readonly ActivityMonitorFactory EventHubAsyncTaskInfoQueueMoveMessageToRetryAsync = new("EventHubAsyncTaskInfoQueue.MoveMessageToRetryAsync", LogLevel.Critical);

        private static TimeSpan _maxElapsedDuration;
        private static TimeSpan _maxDurationExpireTaskTimeOut;

        static EventHubAsyncTaskInfoQueue()
        {
            _maxElapsedDuration = ConfigMapUtil.Configuration.GetValueWithCallBack<TimeSpan>(
                InputOutputConstants.InputEventHubMessageMaxDurationWithoutCheckPoint, 
                UpdateMaxElapsedDuration, TimeSpan.FromMinutes(2));

            _maxDurationExpireTaskTimeOut = ConfigMapUtil.Configuration.GetValueWithCallBack<TimeSpan>(
                InputOutputConstants.EventHubMaxDurationExpireTaskTimeOut,
                UpdateMaxDurationExpireTaskTimeout, TimeSpan.FromSeconds(30));
        }

        private static readonly ActivityMonitorFactory EventHubAsyncTaskInfoQueueCheckpointLatestAsync = new("EventHubAsyncTaskInfoQueue.CheckpointLatestAsync");
        private static readonly ActivityMonitorFactory EventHubAsyncTaskInfoQueueDispose = new("EventHubAsyncTaskInfoQueue.Dispose");
        private static readonly ActivityMonitorFactory EventHubAsyncTaskInfoQueueCriticalError = new("EventHubAsyncTaskInfoQueue.CriticalError", LogLevel.Critical);

        private static readonly IRetryPolicy CheckpointDefaultRetryPolicy = 
            new RetryPolicy(new EventHubCatchPermanentErrorsStrategy(), 3, TimeSpan.FromSeconds(3));

        #region Properties 

        public string PartitionId { get; }
        public string EventHubName { get; }

        public DateTimeOffset LastCheckPointedTime { get; private set; }
        public DateTimeOffset LastPartitionReadTime { get; set; }
        public int TaskInfoQueueLength => _taskInfoQueue.Count;

        internal long LastReadSequenceNumber;
        internal long NumReadEvents;
        internal long NumOfWaitingMessages;

        #endregion

        private readonly Queue<EventHubAsyncTaskInfo> _taskInfoQueue;
        
        //private readonly Func<int> _maxQueueSizeFunc;
        private readonly Func<string, long, long?, CancellationToken, Task> _updateCheckpointAsyncFunc;

        private volatile EventHubAsyncTaskInfo _lastCompletedTaskInfo;
        private volatile EventHubAsyncTaskInfo _lastCheckpointedTaskInfo;

        private SemaphoreSlim _checkpointLock = new SemaphoreSlim(1, 1);
        private System.Timers.Timer _checkPointTimer;

        private int isCheckPointWorking = 0;
        private int isQueueClosing = 0;
        private int _checkPointIntervalInSec;
        private int _checkPointTimeoutInSec;

        public EventHubAsyncTaskInfoQueue(string eventHubName, string partitionId,
            Func<string, long, long?, CancellationToken, Task> updateCheckpointAsyncFunc,
            int checkPointIntervalInSec, int checkPointTimeoutInSec)
        {
            GuardHelper.ArgumentConstraintCheck(checkPointIntervalInSec > 0);
            GuardHelper.ArgumentConstraintCheck(checkPointTimeoutInSec > 0);

            EventHubName = eventHubName;
            PartitionId = partitionId;
            _updateCheckpointAsyncFunc = updateCheckpointAsyncFunc;

            _taskInfoQueue = new Queue<EventHubAsyncTaskInfo>(1024);
            _checkPointIntervalInSec = checkPointIntervalInSec;
            _checkPointTimeoutInSec = checkPointTimeoutInSec;

            _checkPointTimer = new System.Timers.Timer();
            _checkPointTimer.Interval = _checkPointIntervalInSec * 1000;
            _checkPointTimer.Elapsed += CheckPointTimerHandlerAsync;
        }

        public void UpdateCheckPointInterval(int checkPointIntervalInSec)
        {
            if (checkPointIntervalInSec <= 0)
            {
                return;
            }

            _checkPointIntervalInSec = checkPointIntervalInSec;
            _checkPointTimer.Interval = checkPointIntervalInSec * 1000;
        }

        public void UpdateCheckPointTimeout(int checkPointTimeoutInSec)
        {
            if (checkPointTimeoutInSec <= 0)
            {
                return;
            }

            _checkPointTimeoutInSec = checkPointTimeoutInSec;
        }

        public void StartCheckPointTimer()
        {
            UpdateCheckPointInterval(_checkPointIntervalInSec);
            _checkPointTimer.Start();
        }

        private async void CheckPointTimerHandlerAsync(object sender, ElapsedEventArgs e)
        {
            try
            {
                using var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(_checkPointTimeoutInSec));
                await CheckpointLatestAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Just in case
                // Should not have exception in timer handler because CheckpointLatestAsync will catch all exceptions
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref isQueueClosing, 1, 0) != 0)
            {
                // Queue is already closing
                return;
            }

            using var methodMonitor = EventHubAsyncTaskInfoQueueDispose.ToMonitor();
            try
            {
                methodMonitor.Activity[SolutionConstants.EventHubName] = EventHubName;
                methodMonitor.Activity[SolutionConstants.PartitionId] = PartitionId;

                methodMonitor.OnStart();

                _checkPointTimer.Stop();

                // wait for running checkpointing to finish
                if (isCheckPointWorking == 1)
                {
                    _checkpointLock.Wait();
                    _checkpointLock.Release();
                }

                methodMonitor.OnCompleted();

            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
            }
            finally
            {
                // Dispose 
                _checkpointLock.Dispose();
                _checkpointLock = null;

                // Just in case don't clear Queue() to avoid unexpected(buggy) concurrency
            }
        }

        // AddTaskInfo is called from single thread. So we don't need lock
        public void AddTaskInfo(EventHubAsyncTaskInfo taskInfo)
        {
            if (isQueueClosing == 1)
            {
                return;
            }

            UpdateCompletedTasks();
            _taskInfoQueue.Enqueue(taskInfo);
        }

        internal long NumPendingMessagesForCheckPoint()
        {
            if (isQueueClosing == 1)
            {
                return 0;
            }

            var lastCheckpointedTaskInfo = _lastCheckpointedTaskInfo;
            if (lastCheckpointedTaskInfo == null)
            {
                return NumReadEvents;
            }

            var diff = LastReadSequenceNumber - lastCheckpointedTaskInfo.SequenceNumber;
            return diff < 0 ? 0 : diff;
        }

        // UpdateCompletedTasks is called from single thread. So we don't need lock
        public void UpdateCompletedTasks()
        {
            while (_taskInfoQueue.Count > 0)
            {
                var eventHubAsyncTaskInfo = _taskInfoQueue.Peek();

                if (!eventHubAsyncTaskInfo.IsCompleted)
                {
                    // Task in the top is not completed yet
                    // Track how much time is spent on the top on TaskInfoQueue
                    var elapsedTime = DateTimeOffset.UtcNow - eventHubAsyncTaskInfo.CreationTime;
                    if (elapsedTime > _maxElapsedDuration)
                    {
                        var maxDurationExpireTaskInfo = eventHubAsyncTaskInfo.CreateAndSetMaxDurationExpireTaskInfo();
                        if (maxDurationExpireTaskInfo != null)
                        {
                            // This Task has been too long on Queue. 
                            // It mean that processing task might be in some stuck state or unexpected state
                            // Let's create RetryMessage and move original eventHub Message to retryQueue
                            // Then we can checkpoint this message and continue to read eventHub messages
                            _ = Task.Run(() => MoveMessageToRetryAsync(
                                originalEventHubAsyncTaskInfo: eventHubAsyncTaskInfo, 
                                maxDurationExpireTaskInfo: maxDurationExpireTaskInfo));
                        }
                    }
                    return;
                }

                // Task has been completed. Clear Data
                eventHubAsyncTaskInfo.CleanupResources();

                if (!(_lastCompletedTaskInfo?.SequenceNumber >= eventHubAsyncTaskInfo.SequenceNumber))
                {
                    // Update LastCompletedTaskInfo
                    _lastCompletedTaskInfo = eventHubAsyncTaskInfo;

                    // Dequeue
                    _taskInfoQueue.Dequeue();
                }
                else
                {
                    // Let's first Dequeue and logging 
                    _taskInfoQueue.Dequeue();

                    // this should not happen.
                    // Log if it happens
                    using var criticalLogMonitor = EventHubAsyncTaskInfoQueueCriticalError.ToMonitor();
                    var exception = new Exception("Out Of Order Task Completion occurs in UpdateCompletedTasks");
                    criticalLogMonitor.Activity["LastCompletedOffset"] = _lastCompletedTaskInfo?.Offset;
                    criticalLogMonitor.Activity["LastCompletedSequenceNumber"] = _lastCompletedTaskInfo?.SequenceNumber;
                    criticalLogMonitor.Activity["CompletedOffset"] = eventHubAsyncTaskInfo.Offset;
                    criticalLogMonitor.Activity["CompletedSequenceNumber"] = eventHubAsyncTaskInfo.SequenceNumber;
                    criticalLogMonitor.OnError(exception, true);
                }
            }
        }

        public void CloseQueue()
        {
            Dispose();
        }

        private async Task CheckpointLatestAsync(CancellationToken cancellationToken)
        {
            if (_lastCompletedTaskInfo == null || isQueueClosing == 1)
            {
                return;
            }

            // _checkpointLock
            if (Interlocked.CompareExchange(ref isCheckPointWorking, 1, 0) != 0)
            {
                // checkpoing is still working, skip this checkpoint
                return;
            }


            using var methodMonitor = EventHubAsyncTaskInfoQueueCheckpointLatestAsync.ToMonitor(parentActivity: BasicActivity.Null);
            bool hasSemaphore = false;

            try
            {
                methodMonitor.OnStart(false);

                await _checkpointLock.WaitAsync(cancellationToken).IgnoreContext(); // might throw Cancellation exception
                hasSemaphore = true;

                // One more check after getting lock
                var lastCompletedTaskInfo = _lastCompletedTaskInfo;
                if (lastCompletedTaskInfo == null || isQueueClosing == 1)
                {
                    methodMonitor.Activity["NullLastCompletedTaskInfo"] = _lastCompletedTaskInfo == null;
                    methodMonitor.Activity["isQueueClosing"] = isQueueClosing == 1;
                    methodMonitor.OnCompleted();
                    return;
                }

                var lastCheckpointedTaskInfo = _lastCheckpointedTaskInfo;
                var shouldCheckpoint = !(lastCheckpointedTaskInfo?.SequenceNumber >= lastCompletedTaskInfo.SequenceNumber);

                if (shouldCheckpoint)
                {
                    var numRetries = 0;

                    methodMonitor.Activity[SolutionConstants.EventHubName] = EventHubName;
                    methodMonitor.Activity[SolutionConstants.PartitionId] = PartitionId;
                    methodMonitor.Activity["TaskInfoQueueSize"] = _taskInfoQueue.Count;
                    methodMonitor.Activity["LastCheckPointedOffset"] = lastCheckpointedTaskInfo?.Offset;
                    methodMonitor.Activity["LastCheckpointedSequenceNumber"] = lastCheckpointedTaskInfo?.SequenceNumber;

                    await CheckpointDefaultRetryPolicy.ExecuteAsync(
                        () => this.CheckpointAsync(PartitionId, lastCompletedTaskInfo, cancellationToken),
                        (retryCount, _, __) => numRetries = retryCount).ConfigureAwait(false);

                    LastCheckPointedTime = DateTimeOffset.UtcNow;

                    methodMonitor.Activity["LastCompletedOffset"] = lastCompletedTaskInfo.Offset;
                    methodMonitor.Activity["LastCompletedSequenceNumber"] = lastCompletedTaskInfo.SequenceNumber;
                    methodMonitor.Activity["NumCheckpointingRetries"] = numRetries;

                    _lastCheckpointedTaskInfo = lastCompletedTaskInfo;

                }

                methodMonitor.OnCompleted();
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
                // Will retry next time
            }
            finally
            {
                Interlocked.Exchange(ref isCheckPointWorking, 0);
                if (hasSemaphore)
                {
                    _checkpointLock.Release();
                }
            }
        }

        private async Task CheckpointAsync(string partitionId, EventHubAsyncTaskInfo eventHubAsyncTaskInfo, CancellationToken cancellationToken)
        {
            await _updateCheckpointAsyncFunc(partitionId, eventHubAsyncTaskInfo.Offset, eventHubAsyncTaskInfo.SequenceNumber, cancellationToken).ConfigureAwait(false);
        }

        public static async Task MoveMessageToRetryAsync(
            EventHubAsyncTaskInfo originalEventHubAsyncTaskInfo, 
            EventHubAsyncTaskInfo maxDurationExpireTaskInfo)
        {
            var messageData = originalEventHubAsyncTaskInfo.MessageData;
            var hasCompressed = originalEventHubAsyncTaskInfo.HasCompressed;
            if (messageData == null)
            {
                // Task has just completed
                return;
            }

            using var methodMonitor = EventHubAsyncTaskInfoQueueMoveMessageToRetryAsync.ToMonitor();

            try
            {
                var traceId = originalEventHubAsyncTaskInfo.ActivityContext.TraceId.ToString();
                methodMonitor.Activity[SolutionConstants.TraceId] = traceId;

                methodMonitor.OnStart(false);

                var rawInputMessage = ARNRawInputMessage.CreateRawInputMessage(
                    binaryData: messageData,
                    rawInputCorrelationId: null,
                    eventTime: default,
                    eventType: null,
                    tenantId: null,
                    resourceLocation: null,
                    deserialize: false,
                    hasCompressed: hasCompressed,
                    taskActivity: null);

                rawInputMessage.NoDeserialize = true;

                var eventTaskContext = new IOEventTaskContext<ARNRawInputMessage>(
                    InputOutputConstants.EventHubMaxDurationExpireTask,
                    DataSourceType.InputEventHub,
                    dataSourceName: originalEventHubAsyncTaskInfo.DataSourceName,
                    firstEnqueuedTime: originalEventHubAsyncTaskInfo.EnqueuedTime,
                    firstPickedUpTime: originalEventHubAsyncTaskInfo.CreationTime,
                    dataEnqueuedTime: originalEventHubAsyncTaskInfo.EnqueuedTime,
                    eventTime: rawInputMessage.EventTime,
                    inputMessage: rawInputMessage,
                    eventTaskCallBack: maxDurationExpireTaskInfo,
                    retryCount: 0,
                    retryStrategy: SolutionInputOutputService.RetryStrategy,
                    parentActivityContext: originalEventHubAsyncTaskInfo.ActivityContext,
                    topActivityStartTime: default,
                    createNewTraceId: false,
                    regionConfigData: RegionConfigManager.GetRegionConfig(originalEventHubAsyncTaskInfo.RegionName),
                    parentCancellationToken: CancellationToken.None,
                    retryChannelManager: SolutionInputOutputService.ARNMessageChannels.RawInputRetryChannelManager,
                    poisonChannelManager: SolutionInputOutputService.ARNMessageChannels.RawInputPoisonChannelManager,
                    finalChannelManager: SolutionInputOutputService.ARNMessageChannels.RawInputFinalChannelManager,
                    globalConcurrencyManager: null);

                // Cancel Original Task
                var cancellableTask = originalEventHubAsyncTaskInfo.CancellableTask;
                cancellableTask?.CancelTask();

                // SetTaskTimeOut
                eventTaskContext.SetTaskTimeout(_maxDurationExpireTaskTimeOut);

                // Start Task for internal initialization
                // We should not provide startChannel here
                await eventTaskContext.StartEventTaskAsync(null, false, methodMonitor.Activity).ConfigureAwait(false);

                // Add to RawInputRetryChannel
                eventTaskContext.TaskMovingToRetry(
                   RetryReason.EventHubMaxDurationExpire.FastEnumToString(),
                   "EventHub Message stayed too long inside Queue",
                   0,
                   IOComponent.EventHubAsyncTaskInfoQueue.FastEnumToString(),
                   null);

                await eventTaskContext.StartNextChannelAsync().ConfigureAwait(false);

                // Do not put any code after StartTaskAsync because the task might already finish inside it.
                // so EventTaskActivity might already be disposed

                methodMonitor.OnCompleted();
                return;
            }
            catch (Exception ex)
            {
                // Should not reach here
                methodMonitor.OnError(ex, isCriticalLevel: true);
                throw;
            }
        }

        private static Task UpdateMaxElapsedDuration(TimeSpan newMaxDuration)
        {
            if (newMaxDuration.TotalMilliseconds <= 0)
            {
                Logger.LogError("{config} must be larger than 0", InputOutputConstants.InputEventHubMessageMaxDurationWithoutCheckPoint);
                return Task.CompletedTask;
            }

            var oldMaxDuration = _maxElapsedDuration;
            if (newMaxDuration.TotalMilliseconds == oldMaxDuration.TotalMilliseconds)
            {
                return Task.CompletedTask;
            }

            _maxElapsedDuration = newMaxDuration;

            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                InputOutputConstants.InputEventHubMessageMaxDurationWithoutCheckPoint,
                oldMaxDuration, newMaxDuration);

            return Task.CompletedTask;
        }

        private static Task UpdateMaxDurationExpireTaskTimeout(TimeSpan newTimeout)
        {
            if (newTimeout.TotalMilliseconds <= 0)
            {
                Logger.LogError("{config} must be larger than 0", InputOutputConstants.EventHubMaxDurationExpireTaskTimeOut);
                return Task.CompletedTask;
            }

            var oldTimeout = _maxDurationExpireTaskTimeOut;
            if (newTimeout.TotalMilliseconds == oldTimeout.TotalMilliseconds)
            {
                return Task.CompletedTask;
            }

            _maxDurationExpireTaskTimeOut = newTimeout;

            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                InputOutputConstants.EventHubMaxDurationExpireTaskTimeOut,
                oldTimeout, newTimeout);

            return Task.CompletedTask;
        }
    }
}