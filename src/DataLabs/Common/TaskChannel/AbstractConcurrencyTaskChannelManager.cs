namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public abstract class AbstractConcurrentTaskChannelManager<T> : AbstractTaskChannelManager<T>
    {
        private static readonly ILogger<AbstractConcurrentTaskChannelManager<T>> Logger =
            DataLabLoggerFactory.CreateLogger<AbstractConcurrentTaskChannelManager<T>>();

        private readonly ActivityMonitorFactory AbstractConcurrentTaskChannelManagerExceuteWithConcurrencyAsync;
        private readonly ActivityMonitorFactory AbstractConcurrentTaskChannelManagerInternalExecuteTaskAsync;

        private readonly string _eventNameWithConcurrency;

        protected abstract ValueTask BeforeProcessAsync(AbstractEventTaskContext<T> eventTaskContext);
        protected abstract void Dispose(bool disposing);

        protected readonly SubTaskManager<T> _subTaskManager;

        private readonly ConfigurableConcurrencyManager? _perTypeConfigurableConcurrencyManager;
        private IConcurrencyManager? _perTypeConcurrencyManager;
        private IConcurrencyManager? _externalConcurrencyManager;

        private readonly string _concurrencyWaitTimeoutConfig;
        private readonly string _concurrencyWaitTimedOutString;
        private int _concurrencyWaitTimeoutInSec;
        private int _disposed;

        public AbstractConcurrentTaskChannelManager(string channelName, string contextTypeName)
            : base(channelName, contextTypeName)
        {
            _eventNameWithConcurrency = channelName + ".withConcurrency";
            AbstractConcurrentTaskChannelManagerExceuteWithConcurrencyAsync = new(channelName + ".ExceuteWithConcurrencyAsync");
            AbstractConcurrentTaskChannelManagerInternalExecuteTaskAsync = new(channelName + ".InternalExecuteTaskAsync");

            _subTaskManager = new SubTaskManager<T>();

            var configName = channelName + contextTypeName + SolutionConstants.TaskChannelConcurrencySuffix;
            _perTypeConfigurableConcurrencyManager = new ConfigurableConcurrencyManager(configName, ConfigurableConcurrencyManager.NO_CONCURRENCY_CONTROL);
            _perTypeConfigurableConcurrencyManager.RegisterObject(SetPerTypeConcurrencyManager);

            // Channel Timeout
            _concurrencyWaitTimeoutConfig = channelName + SolutionConstants.TaskChannelConcurrencyWaitTimeoutInSec;
            _concurrencyWaitTimeoutInSec = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(_concurrencyWaitTimeoutConfig, 
                UpdateConcurrencyWaitTimeoutInSec, 0);

            _concurrencyWaitTimedOutString = channelName + SolutionConstants.TaskChannelConcurrencyWaitTimedOutSuffix;
        }

        public override void SetBufferedTaskProcessorFactory(IBufferedTaskProcessorFactory<T> bufferedTaskProcessorFactory)
        {
            throw new NotSupportedException();
        }

        public override void AddSubTaskFactory(ISubTaskFactory<T> subTaskFactory)
        {
            _subTaskManager.AddSubTaskFactory(subTaskFactory);
        }

        public override void SetExternalConcurrencyManager(IConcurrencyManager? channelConcurrencyManager)
        {
            Interlocked.Exchange(ref _externalConcurrencyManager, channelConcurrencyManager);
        }

        private void SetPerTypeConcurrencyManager(IConcurrencyManager? channelConcurrencyManager)
        {
            Interlocked.Exchange(ref _perTypeConcurrencyManager, channelConcurrencyManager);
        }

        private async Task ExceuteWithConcurrencyAsync(AbstractEventTaskContext<T> eventTaskContext,
            IConcurrencyManager? externalConcurrencyManager,
            IConcurrencyManager? perTypeConcurrencyManager)
        {
            SetCurrentEventTaskContext(eventTaskContext);

            var hasExternalConcurrency = false;
            var hasPerTypeConcurrency = false;
            var startedInOtherThread = false;
            Exception? exception = null;

            /* 
             * We need this activityMonitor to track concurrency (sempahore) waiting time
             */
            using var monitor = CreateActivityMonitor(
                AbstractConcurrentTaskChannelManagerExceuteWithConcurrencyAsync,
                eventTaskContext);

            var isConcurrencyWaitTimedOut = false;

            try
            {
                var taskActivity = eventTaskContext.EventTaskActivity;
                taskActivity.AddEvent(_eventNameWithConcurrency);

                monitor.OnStart(false);

                var cancellationToken = eventTaskContext.TaskCancellationToken;
                var remainingConcurrencyTimeoutMilli = _concurrencyWaitTimeoutInSec <= 0 ? Timeout.Infinite : _concurrencyWaitTimeoutInSec * 1000;
                
                if (externalConcurrencyManager != null)
                {
                    monitor.Activity["ExternalConcurrencyAvailable"] = externalConcurrencyManager.NumAvailables;

                    var startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                    hasExternalConcurrency = await externalConcurrencyManager.AcquireResourceAsync(remainingConcurrencyTimeoutMilli, cancellationToken).ConfigureAwait(false);
                    var concurrencyWaitElapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;

                    monitor.Activity["ExternalConcurrencyElapsed"] = concurrencyWaitElapsed;

                    if (!hasExternalConcurrency)
                    {
                        // Timeout
                        isConcurrencyWaitTimedOut = true;
                    }

                    if (remainingConcurrencyTimeoutMilli > 0)
                    {
                        remainingConcurrencyTimeoutMilli -= concurrencyWaitElapsed;
                        remainingConcurrencyTimeoutMilli = Math.Max(1, remainingConcurrencyTimeoutMilli); // remainingConcurrencyTimeoutMilli must be larger than 0
                    }
                }

                if (!isConcurrencyWaitTimedOut && perTypeConcurrencyManager != null)
                {
                    monitor.Activity["PerTypeConcurrencyAvailable"] = perTypeConcurrencyManager.NumAvailables;

                    var startStopWatchTimeStamp = Stopwatch.GetTimestamp();
                    hasPerTypeConcurrency = await perTypeConcurrencyManager.AcquireResourceAsync(remainingConcurrencyTimeoutMilli, cancellationToken).ConfigureAwait(false);
                    var concurrencyWaitElapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;

                    monitor.Activity["PerTypeConcurrencyElapsed"] = concurrencyWaitElapsed;

                    if (!hasPerTypeConcurrency)
                    {
                        // Timeout
                        isConcurrencyWaitTimedOut = true;
                    }
                }

                if (!isConcurrencyWaitTimedOut)
                {
                    // Use Background Task
                    _ = Task.Run(() => InternalExecuteTaskAsync(
                        eventTaskContext,
                        externalConcurrencyManager,
                        perTypeConcurrencyManager,
                        monitor.Activity));

                    startedInOtherThread = true;

                    monitor.OnCompleted();
                }
            }
            catch (Exception ex)
            {
                // It could happen when TaskCancellation Token is canclled -> concurrencyManager
                // Mostly it is due to cancellation Token
                AddErrorProperty(eventTaskContext, monitor.Activity);
                monitor.OnError(ex);
                exception = ex;
            }
            finally
            {
                if (!startedInOtherThread)
                {
                    if (hasExternalConcurrency)
                    {
                        externalConcurrencyManager!.ReleaseResource();
                    }
                    if (hasPerTypeConcurrency)
                    {
                        perTypeConcurrencyManager!.ReleaseResource();
                    }
                }
            }

            if (startedInOtherThread)
            {
                // Task is already moved to background Task
                return;
            }

            if (isConcurrencyWaitTimedOut)
            {
                await MoveToRetryAndStartNextChannelAsync(
                    eventTaskContext: eventTaskContext, 
                    retryReason: _concurrencyWaitTimedOutString,
                    retryDelayMs: 0,
                    ex: null).ConfigureAwait(false);
            }
            else
            {
                // In this method, exception could happen due to cancellation token
                // Let's move to retry instead of poison
                await MoveToRetryAndStartNextChannelAsync(
                  eventTaskContext: eventTaskContext,
                  retryReason: SolutionUtils.GetExceptionTypeSimpleName(exception),
                  retryDelayMs: 0,
                  ex: exception).ConfigureAwait(false);
            }
        }

        protected async override Task ProcessEventTaskContextAsync(AbstractEventTaskContext<T> eventTaskContext)
        {
            SetCurrentEventTaskContext(eventTaskContext);
            Exception? exception = null;

            try
            {
                if (eventTaskContext.IsAlreadyTaskCancelled())
                {
                    await MoveToPoisonAndStartNextChannelAsync(
                        eventTaskContext,
                        PoisonReason.TaskCancelled.FastEnumToString(),
                        null).ConfigureAwait(false);
                    return;
                }

                var externalConcurrencyManager = _externalConcurrencyManager;
                var perTypeConcurrencyManager = _perTypeConcurrencyManager;
                var needConcurrency = externalConcurrencyManager != null || perTypeConcurrencyManager != null;

                if (needConcurrency)
                {
                    await ExceuteWithConcurrencyAsync(
                        eventTaskContext, 
                        externalConcurrencyManager, 
                        perTypeConcurrencyManager).ConfigureAwait(false);
                    return;
                }else
                {
                    await InternalExecuteTaskAsync(eventTaskContext, null, null, null).ConfigureAwait(false);
                    return;
                }
            }
            catch(Exception ex)
            {
                exception = ex;

                // Should not happen this line
                using var criticalMonitor = CreateCriticalActivityMonitor(
                    "ProcessEventTaskContextAsync",
                    eventTaskContext);

                criticalMonitor.OnError(ex, true);
            }

            if (exception != null)
            {
                // Last resort
                await MoveToPoisonAndStartNextChannelAsync(eventTaskContext, SolutionUtils.GetExceptionTypeSimpleName(exception), exception).ConfigureAwait(false);
            }
        }

        private async Task InternalExecuteTaskAsync(
            AbstractEventTaskContext<T> eventTaskContext,
            IConcurrencyManager? concurrencyManager1, 
            IConcurrencyManager? concurrencyManager2, 
            IActivity? parentActivity)
        {
            SetCurrentEventTaskContext(eventTaskContext);

            using var monitor = CreateActivityMonitor(
                AbstractConcurrentTaskChannelManagerInternalExecuteTaskAsync, 
                eventTaskContext,
                parentActivity);

            Exception? exception = null;

            try
            {
                try
                {
                    monitor.OnStart(false);

                    await BeforeProcessAsync(eventTaskContext).ConfigureAwait(false);

                    // BeforeProcessing might already set NextChannel
                    var numTaskFactory = _subTaskManager.NumTaskFactory;
                    if (eventTaskContext.NextTaskChannel == null && numTaskFactory > 0)
                    {
                        await _subTaskManager.ProcessEventTaskContextAsync(eventTaskContext).ConfigureAwait(false);
                    }

                    monitor.Activity[SolutionConstants.NextChannel] = eventTaskContext.NextTaskChannel?.ChannelName;
                    monitor.OnCompleted();
                }
                catch (Exception ex)
                {
                    exception = ex;

                    monitor.Activity[SolutionConstants.NextChannel] = eventTaskContext.NextTaskChannel?.ChannelName;
                    AddErrorProperty(eventTaskContext, monitor.Activity);
                    monitor.OnError(ex);
                }

                if (exception != null)
                {
                    await ExecuteTaskErrorAndSetNextChannelAsync(eventTaskContext, exception).ConfigureAwait(false);
                }

                // HandleNextChannelAsync Should be the last line
                await HandleNextChannelAsync(eventTaskContext).ConfigureAwait(false);
            }
            finally
            {
                concurrencyManager1?.ReleaseResource();
                concurrencyManager2?.ReleaseResource();
            }
        }

        private Task UpdateConcurrencyWaitTimeoutInSec(int newVal)
        {
            if (newVal < 0)
            {
                Logger.LogError("{config} must be equal or larger than 0", _concurrencyWaitTimeoutConfig);
                return Task.CompletedTask;
            }

            var oldVal = _concurrencyWaitTimeoutInSec;
            Interlocked.Exchange(ref _concurrencyWaitTimeoutInSec, newVal);

            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                _concurrencyWaitTimeoutConfig, oldVal, newVal);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            if (_disposed > 1 || Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                // Already disposed
                return;
            }

            Dispose(true);

            _subTaskManager.Dispose();
            _perTypeConfigurableConcurrencyManager?.Dispose();
        }
    }
}
