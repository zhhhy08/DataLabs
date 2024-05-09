namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public abstract class AbstractTaskChannelManager<T> : ITaskChannelManager<T>
    {
        private readonly ActivityMonitorFactory AbstractTaskChannelManagerExecuteEventTaskContextAsync;
        private readonly ActivityMonitorFactory AbstractTaskChannelManagerExecuteTaskErrorAsync;
        protected readonly ActivityMonitorFactory AbstractTaskChannelManagerCriticalError;
        protected readonly ProcessNotMovedTaskException ProcessNotMovedTaskException;

        public string ChannelName { get; }
        public readonly string _contextTypeName;

        public abstract void AddSubTaskFactory(ISubTaskFactory<T> subTaskFactory);
        public abstract void SetBufferedTaskProcessorFactory(IBufferedTaskProcessorFactory<T> bufferedTaskProcessorFactory);
        public abstract void SetExternalConcurrencyManager(IConcurrencyManager channelConcurrencyManager);
        public abstract void Dispose();

        protected abstract Task ProcessEventTaskContextAsync(AbstractEventTaskContext<T> eventTaskContext);
        protected abstract ValueTask ProcessErrorAsync(AbstractEventTaskContext<T> eventTaskContext, Exception ex);
        protected abstract ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<T> eventTaskContext);
        protected virtual ValueTask ProcessBeforeMovingToNextChannelAsync(AbstractEventTaskContext<T> eventTaskContext)
        {
            return ValueTask.CompletedTask;
        }

        public AbstractTaskChannelManager(string channelName, string contextTypeName)
        {
            GuardHelper.ArgumentNotNullOrEmpty(channelName, nameof(channelName));
            GuardHelper.ArgumentNotNullOrEmpty(contextTypeName, nameof(contextTypeName));

            ChannelName = channelName;
            _contextTypeName = contextTypeName;

            AbstractTaskChannelManagerExecuteEventTaskContextAsync = new(channelName + ".ExecuteEventTaskContextAsync");
            AbstractTaskChannelManagerExecuteTaskErrorAsync = new(channelName + ".ExecuteTaskErrorAsync");
            AbstractTaskChannelManagerCriticalError = new(channelName + ".CriticalError", LogLevel.Critical);
            ProcessNotMovedTaskException = new ProcessNotMovedTaskException("ProcessNotMovedTaskAsync is called in " + ChannelName);
        }

        public async Task ExecuteEventTaskContextAsync(AbstractEventTaskContext<T> eventTaskContext)
        {
            try
            {
                eventTaskContext.SetCurrentChannel(this);

                /* 
                 * We initialize asyncLocal variables here with given EventTask 
                 *  so that all functions/methods in this channel will be able to inherit the eventTask specific information using async-Local
                 */
                SetAsyncLocalsForCurrentEventTaskContext(eventTaskContext);

                /*
                 * We have to call await here because asyncLocal is created per AsyncContext. 
                 * In .net AsyncContext is created when await is called
                 * If we don't call await here, asyncContext will not be created here and will be created in parent caller. 
                 * In this case, above asyncLocal setting doesn't work
                 */
                await ProcessEventTaskContextAsync(eventTaskContext).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Should not happen this line
                using var criticalMonitor = CreateCriticalActivityMonitor(
                    "ExecuteEventTaskContextAsync",
                    eventTaskContext);

                criticalMonitor.OnError(ex, true);

                // Last resort
                // One more call
                await MoveToPoisonAndStartNextChannelAsync(eventTaskContext, SolutionUtils.GetExceptionTypeSimpleName(ex), ex).ConfigureAwait(false);
            }
        }

        protected async ValueTask ExecuteTaskErrorAndSetNextChannelAsync(
            AbstractEventTaskContext<T> eventTaskContext, 
            Exception taskException)
        {
            OpenTelemetryActivityWrapper.Current = eventTaskContext.EventTaskActivity;

            // Reset Next channel here, it will be set below
            eventTaskContext.SetNextChannel(null);

            Exception? exception = null;
            using var monitor = CreateActivityMonitor(
                AbstractTaskChannelManagerExecuteTaskErrorAsync, 
                eventTaskContext);

            try
            {
                monitor.OnStart(false);
                await ProcessErrorAsync(eventTaskContext, taskException).ConfigureAwait(false);
                monitor.OnCompleted();
                return;
            }
            catch (Exception ex)
            {
                exception = ex;
                // Should not happen, This is code bug..
                AddErrorProperty(eventTaskContext, monitor.Activity);
                monitor.OnError(ex, true);
            }

            if (exception != null)
            {
                eventTaskContext.TaskMovingToPoison(SolutionUtils.GetExceptionTypeSimpleName(exception), null, ChannelName, exception);
            }
        }

        protected async Task HandleNextChannelAsync(AbstractEventTaskContext<T> eventTaskContext)
        {
            OpenTelemetryActivityWrapper.Current = eventTaskContext.EventTaskActivity;

            Exception? exception = null;

            try
            {
                await ProcessBeforeMovingToNextChannelAsync(eventTaskContext).ConfigureAwait(false);

                // Need to reset so that further channel will not call this
                var waitingAction = eventTaskContext.StartWaitingChildTasksAction;
                eventTaskContext.StartWaitingChildTasksAction = null; 

                /*
                 * Notice that channel moving code is actually performed in below method
                 * So don't put any code after these methods because eventTaskContext will be disposed inside below these method
                 */
                if (waitingAction == null && (eventTaskContext.NextTaskChannel == null || eventTaskContext.NextTaskChannel == this))
                {
                    // Task is not moved to other channel
                    await ProcessNotMovedTaskAsync(eventTaskContext).ConfigureAwait(false);
                }

                if (eventTaskContext.NextTaskChannel != null)
                {
                    await eventTaskContext.StartNextChannelAsync().ConfigureAwait(false);

                }
                else if (waitingAction != null)
                {
                    waitingAction();
                }
            }
            catch (Exception ex)
            {
                exception = ex;

                // Should not happen this line
                using var criticalMonitor = CreateCriticalActivityMonitor(
                    "HandleNextChannelAsync",
                    eventTaskContext);

                criticalMonitor.OnError(ex, true);
            }

            if (exception != null)
            {
                // Last resort
                // We can not call MoveToPoisonAndStartNextChannelAsync here because it will call HandleNextChannelAsync again
                // It will cause infinite loop
                eventTaskContext.SetNextChannel(null);
                eventTaskContext.TaskMovingToPoison(SolutionUtils.GetExceptionTypeSimpleName(exception), null, ChannelName, exception);
                await eventTaskContext.StartNextChannelAsync().ConfigureAwait(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AddErrorProperty(AbstractEventTaskContext<T> eventTaskContext, IActivity? activity)
        {
            if (activity == null)
            {
                return;
            }

            activity[SolutionConstants.IsCancelled] = eventTaskContext.IsAlreadyTaskCancelled();
            activity[SolutionConstants.IsTimeoutExpired] = eventTaskContext.HasTaskTimeoutExpired;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected async Task MoveToPoisonAndStartNextChannelAsync(
            AbstractEventTaskContext<T> eventTaskContext,
            string poisonReason,
            Exception? ex)
        {
            try
            {
                eventTaskContext.SetNextChannel(null);
                eventTaskContext.TaskMovingToPoison(poisonReason, null, ChannelName, ex);
                await HandleNextChannelAsync(eventTaskContext).ConfigureAwait(false);
            }
            catch (Exception poisonEx)
            {
                // Should not happen
                using var criticalLogMonitor = AbstractTaskChannelManagerCriticalError.ToMonitor();
                criticalLogMonitor.Activity[SolutionConstants.MethodName] = "MoveToPoisonAndStartNextChannelAsync";
                criticalLogMonitor.OnError(poisonEx, true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected async Task MoveToRetryAndStartNextChannelAsync(
            AbstractEventTaskContext<T> eventTaskContext,
            string retryReason,
            int retryDelayMs,
            Exception? ex)
        {
            try
            {
                eventTaskContext.SetNextChannel(null);
                eventTaskContext.TaskMovingToRetry(retryReason, null, retryDelayMs, ChannelName, ex);
                await HandleNextChannelAsync(eventTaskContext).ConfigureAwait(false);
            }
            catch (Exception poisonEx)
            {
                // Should not happen
                using var criticalLogMonitor = AbstractTaskChannelManagerCriticalError.ToMonitor();
                criticalLogMonitor.Activity[SolutionConstants.MethodName] = "MoveToRetryAndStartNextChannelAsync";
                criticalLogMonitor.OnError(poisonEx, true);

                // Last resort
                await MoveToPoisonAndStartNextChannelAsync(eventTaskContext, SolutionUtils.GetExceptionTypeSimpleName(poisonEx), poisonEx).ConfigureAwait(false);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected IActivityMonitor CreateActivityMonitor(
            ActivityMonitorFactory monitorFactory,
            AbstractEventTaskContext<T> eventTaskContext,
            IActivity? parentActivity = null)
        {
            return monitorFactory.ToMonitor(
                parentActivity,
                scenario: eventTaskContext.Scenario,
                component: ChannelName,
                correlationId: eventTaskContext.EventTaskActivity.InputCorrelationId,
                inputResourceId: eventTaskContext.EventTaskActivity.InputResourceId,
                outputCorrelationId: eventTaskContext.EventTaskActivity.OutputCorrelationId,
                outputResourceId: eventTaskContext.EventTaskActivity.OutputResourceId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected IActivityMonitor CreateCriticalActivityMonitor(
            string methodName,
            AbstractEventTaskContext<T> eventTaskContext)
        {
            var monitor = CreateActivityMonitor(
                AbstractTaskChannelManagerCriticalError, 
                eventTaskContext);

            monitor.Activity[SolutionConstants.MethodName] = methodName;
            AddErrorProperty(eventTaskContext, monitor.Activity);
            return monitor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetAsyncLocalsForCurrentEventTaskContext(AbstractEventTaskContext<T> eventTaskContext)
        {
            // For each channel, we will set TopActivity here so that we can track duration from TopActivity to each child method in each channel
            // Some channel (like PartitionedBufferedTaskChannel) internally uses background thread.
            // so we will keep the topActivity inside eventTaskContext and will use it wheneven we process the eventTaskContext

            OpenTelemetryActivityWrapper.Current = eventTaskContext.EventTaskActivity;

            IActivityMonitor.SetCurrentActivity(null);

            var topLevelActivity = CreateActivityMonitor(
                AbstractTaskChannelManagerExecuteEventTaskContextAsync,
                eventTaskContext).Activity;

            eventTaskContext.ChannelTopLevelActivity = topLevelActivity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void SetCurrentEventTaskContext(AbstractEventTaskContext<T> eventTaskContext)
        {
            OpenTelemetryActivityWrapper.Current = eventTaskContext.EventTaskActivity;
            IActivityMonitor.SetCurrentActivity(eventTaskContext.ChannelTopLevelActivity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void ClearCurrentEventTaskContext()
        {
            OpenTelemetryActivityWrapper.Current = null;
            IActivityMonitor.SetCurrentActivity(null);
        }
    }
}
