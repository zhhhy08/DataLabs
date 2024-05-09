namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public abstract class AbstractEventTaskContext<T> : ICancellableTask, IDisposable
    {
        public abstract T TaskContext { get; }
        public abstract string Scenario { get; set; }
        public abstract void TaskMovingToPoison(string poisonReason, string? reasonDetails, string component, Exception? ex);
        public abstract void TaskMovingToRetry(string retryReason, string? reasonDetails, int retryDelayMs, string component, Exception? ex);
        public abstract EventTaskFinalStage EventFinalStage { get; set; }
        public abstract CancellationToken TaskCancellationToken { get; }
        public abstract void CancelTask();
        public abstract bool IsAlreadyTaskCancelled();
        protected abstract void Dispose(bool disposing);

        public int RetryCount { get; }
        public bool HasTaskTimeoutExpired => _taskTimeoutExpired > 0;
        public bool HasTaskDisposed => _disposed > 0;
        
        public int NumChildTasks { get; set; }
        public Action? StartWaitingChildTasksAction { get; set; }

        public OpenTelemetryActivityWrapper EventTaskActivity { get; }
        public IActivity? ChannelTopLevelActivity { get; set; }
        public long StopWatchTimeStamp { get; set; } // This stopWatchTimeStamp is used to measure several internal metrics

        public ITaskChannelManager<T>? PrevTaskChannel { get; private set; }
        public ITaskChannelManager<T>? CurrentTaskChannel { get; private set; }
        public ITaskChannelManager<T>? NextTaskChannel { get; private set; }

        public TimeSpan? TaskTimeout { get; private set; }

        protected AbstractEventTaskContext<T>? _chainedNextEventTaskContext;
        protected ITaskChannelManager<T>? _chainedNextEventTaskStartChannel;

        private CancellationTokenSource? _taskTimeOutCancellationTokenSource;
        private int _taskTimeoutExpired;

        private List<Action<AbstractEventTaskContext<T>>>? _channelMoveActions;
        private TaskCompletionSource<EventTaskFinalStage>? _taskCompletionSource;
        private int _disposed;

        public AbstractEventTaskContext(
            ActivitySource activitySource, 
            string activityName, 
            ActivityContext parentContext, 
            bool createNewTraceId, 
            int retryCount,
            DateTimeOffset topActivityStartTime)
        {
            RetryCount = retryCount;
            EventTaskActivity = new OpenTelemetryActivityWrapper(activitySource, activityName, ActivityKind.Internal, parentContext, createNewTraceId, topActivityStartTime);
            EventTaskActivity.SetTag(SolutionConstants.RetryCount, retryCount);
        }

        /*
         * When Task is timedout, below method (TaskTimeoutHandler) will be called
         * Inherited class can override TaskTimeoutHandler
         */
        public void SetTaskTimeout(TimeSpan? taskTimeout)
        {
            lock(this)
            {
                if (!taskTimeout.HasValue || taskTimeout.Value == default)
                {
                    return;
                }

                if (TaskTimeout != null && TaskTimeout.Value != default)
                {
                    // Reset previous timeout
                    CancelTaskTimeout();
                }

                TaskTimeout = taskTimeout;
                _taskTimeOutCancellationTokenSource = new CancellationTokenSource();
                _taskTimeOutCancellationTokenSource.CancelAfter(taskTimeout.Value);

                var timerCancellationToken = _taskTimeOutCancellationTokenSource.Token;
                timerCancellationToken.Register(TaskTimeoutAction);
            }
            
        }

        /*
         * This is default TaskTimeoutHandler
         * Inherited class can override this handler
         */
        protected virtual void TaskTimeoutHandler()
        {
            // Cancel Task
            CancelTask();
        }

        private void TaskTimeoutAction()
        {
            if (_taskTimeoutExpired > 0 || Interlocked.CompareExchange(ref _taskTimeoutExpired, 1, 0) != 0)
            {
                // TimerAction is already called
                return;

            }

            CancelTaskTimeout();
            TaskTimeoutHandler();
        }

        public void CancelTaskTimeout()
        {
            lock (this)
            {
                _taskTimeOutCancellationTokenSource?.Dispose();
                _taskTimeOutCancellationTokenSource = null;
            }
        }

        public virtual async Task StartEventTaskAsync(ITaskChannelManager<T> startChannel, bool waitForTaskFinish, IActivity? parentActivity)
        {
            var needCallStartAgain = false;
            try
            {
                if (waitForTaskFinish)
                {
                    _taskCompletionSource = new TaskCompletionSource<EventTaskFinalStage>();
                }

                SetNextChannel(startChannel);

                if (parentActivity != null)
                {
                    parentActivity["StartChannel"] = NextTaskChannel?.ChannelName;
                }

                await StartNextChannelAsync().ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                EventTaskActivity.AddEvent(SolutionConstants.EventName_TaskStartFailed);

                SetNextChannel(null);
                TaskMovingToPoison(SolutionUtils.GetExceptionTypeSimpleName(ex), null, "StartEventTaskAsync", ex);
                needCallStartAgain = true;
            }

            if (needCallStartAgain)
            {
                await StartNextChannelAsync().ConfigureAwait(false);
            }

            if (_taskCompletionSource != null)
            {
                await _taskCompletionSource.Task.ConfigureAwait(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddChannelMoveAction(Action<AbstractEventTaskContext<T>> action)
        {
            _channelMoveActions ??= new(2);
            _channelMoveActions.Add(action);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetChainedNextEventTaskContext(AbstractEventTaskContext<T> chainedNextEventTaskContext, ITaskChannelManager<T> startChannel)
        {
            _chainedNextEventTaskContext = chainedNextEventTaskContext;
            _chainedNextEventTaskStartChannel = startChannel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetNextChannel(ITaskChannelManager<T>? nextChannel)
        {
            if (nextChannel != null && CurrentTaskChannel == nextChannel)
            {
                // Code Bug. Recursive add to the same channel
                throw new InvalidOperationException("Code Bug!. Adding to the same Channel");
            }

            NextTaskChannel = nextChannel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task StartNextChannelAsync()
        {
            GuardHelper.ArgumentNotNull(NextTaskChannel);
            return NextTaskChannel.ExecuteEventTaskContextAsync(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCurrentChannel(ITaskChannelManager<T> channel)
        {
            if (CurrentTaskChannel == channel)
            {
                // Code Bug. Recursive add to the same channel
                throw new InvalidOperationException(
                    "Code Bug!. Adding to the same Channel. "
                    + "CurrentTaskChannel: " + CurrentTaskChannel.ChannelName
                    + ", PrevTaskChannel: " + PrevTaskChannel?.ChannelName
                    + ", NextTaskChannel: " + NextTaskChannel?.ChannelName);
            }

            PrevTaskChannel = CurrentTaskChannel;
            CurrentTaskChannel = channel;
            NextTaskChannel = null;

            // Add ChannelName as ActivityEvent
            EventTaskActivity.AddEvent(CurrentTaskChannel.ChannelName);

            // Execute Actions for ChannelMove
            if (_channelMoveActions != null)
            {
                for (int i = 0, n = _channelMoveActions.Count; i < n; i++)
                {
                    _channelMoveActions[i](this);
                }
            }
        }

        private static readonly ChainedPrevTaskFailedException _chainedPrevTaskFailedException = new("Previous Chained Task Failed");

        public void Dispose()
        {
            if (_disposed > 0 || Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                // Already disposed
                return;
            }

            CancelTaskTimeout();
            Dispose(true);
            EventTaskActivity.Dispose();

            // Execute Chained Next Task
            if (_chainedNextEventTaskContext != null && _chainedNextEventTaskStartChannel != null)
            {
                _chainedNextEventTaskContext.SetTaskTimeout(TaskTimeout);

                var nextStartChannel = _chainedNextEventTaskStartChannel;
                if (EventFinalStage == EventTaskFinalStage.DROP || EventFinalStage == EventTaskFinalStage.POISON_QUEUE)
                {
                    _ = Task.Run(async () =>
                    {
                        _chainedNextEventTaskContext.EventTaskActivity.AddEvent(SolutionConstants.EventName_TaskStartFailed);
                        _chainedNextEventTaskContext.SetNextChannel(null);
                        _chainedNextEventTaskContext.TaskMovingToPoison(PoisonReason.StreamChildTaskError.FastEnumToString(), "Previous Chained Task Failed", "StartEventTaskAsync", _chainedPrevTaskFailedException);
                        await _chainedNextEventTaskContext.StartNextChannelAsync().ConfigureAwait(false);
                    });
                }
                else
                {
                    _ = Task.Run(() => _chainedNextEventTaskContext.StartEventTaskAsync(_chainedNextEventTaskStartChannel, false, null));
                }

            }

            // If taskCompletionSource exists, call SetResult in the end to wake up CompletionSource's Task
            _taskCompletionSource?.SetResult(EventFinalStage);
        }
    }
}
