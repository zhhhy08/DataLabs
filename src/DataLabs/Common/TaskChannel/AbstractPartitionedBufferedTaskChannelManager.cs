namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public abstract class AbstractPartitionedBufferedTaskChannelManager<T> : AbstractTaskChannelManager<T>
    {
        private static readonly ILogger<AbstractPartitionedBufferedTaskChannelManager<T>> Logger =
            DataLabLoggerFactory.CreateLogger<AbstractPartitionedBufferedTaskChannelManager<T>>();

        private const string ChannelWriteToReadMetric = "ChannelWriteToReadMetric";
        private static readonly Histogram<int> ChannelWriteToReadMetricDuration = MetricLogger.CommonMeter.CreateHistogram<int>(ChannelWriteToReadMetric);

        private readonly ActivityMonitorFactory AbstractPartitionedBufferedTaskChannelManagerWriteToChannelAsync;
        private readonly ActivityMonitorFactory AbstractPartitionedBufferedTaskChannelManagerInternalExecuteTaskAsync;
        private readonly ActivityMonitorFactory AbstractPartitionedBufferedTaskChannelManagerInternalExecuteBufferedTasksAsync;
        private readonly ActivityMonitorFactory AbstractPartitionedBufferedTaskChannelManagerBeforeProcessAsync;

        protected virtual long GetConsumerPartitionId(AbstractEventTaskContext<T> eventTaskContext)
        {
            // -1 or 0: round robin distribution to all consumers
            return -1;
        }

        protected abstract ValueTask BeforeProcessAsync(AbstractEventTaskContext<T> eventTaskContext);
        protected abstract void Dispose(bool disposing);

        private IBufferedTaskProcessorFactory<T>? _bufferedTaskProcessorFactory;
        private readonly object _updateLock = new object();
        private readonly SubTaskManager<T> _subTaskManager;
        private readonly string _configNumQueue;
        private readonly string _configQueueLength;
        private readonly string _configDelay;
        private readonly string _configMaxBufferedSize;

        private ChannelWriter<AbstractEventTaskContext<T>>[] _channelWriters;
        private int _numChannels;
        private int _delayInMilli;
        private int _maxBufferedSize;
        private int _maxBoundSize;

        private volatile bool _disposed;

        public AbstractPartitionedBufferedTaskChannelManager(string channelName, string contextTypeName, int initMaxBoundSize = 1000, int intNumQueue = 5, int initDelayInMilli = 0, int initXaxBufferedSize = 500)
            : base(channelName, contextTypeName)
        {
            AbstractPartitionedBufferedTaskChannelManagerWriteToChannelAsync = new(channelName + ".WriteToChannelAsync");
            AbstractPartitionedBufferedTaskChannelManagerInternalExecuteTaskAsync = new(channelName + ".InternalExecuteTaskAsync");
            AbstractPartitionedBufferedTaskChannelManagerInternalExecuteBufferedTasksAsync = new(channelName + ".InternalExecuteBufferedTasksAsync");
            AbstractPartitionedBufferedTaskChannelManagerBeforeProcessAsync = new(channelName + ".BeforeProcessAsync");

            _subTaskManager = new SubTaskManager<T>();

            // do we need config per ContextType?
            // Let's use channelName for now
            _configQueueLength = channelName + SolutionConstants.BufferedChannelQueueLengthSuffix;
            _configNumQueue = channelName + SolutionConstants.BufferedChannelNumQueueSuffix;
            _configDelay = channelName + SolutionConstants.BufferedChannelDelaySuffix;
            _configMaxBufferedSize = channelName + SolutionConstants.BufferedChannelMaxBufferedSizeSuffix;

            _maxBoundSize = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(_configQueueLength,
                UpdateQueueLength, initMaxBoundSize, allowMultiCallBacks: true);
            _numChannels = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(_configNumQueue,
                UpdateNumQueue, intNumQueue, allowMultiCallBacks: true);
            _delayInMilli = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(_configDelay,
                UpdateDelay, initDelayInMilli, allowMultiCallBacks: true);
            _maxBufferedSize = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(_configMaxBufferedSize,
                UpdateMaxBufferedSize, initXaxBufferedSize, allowMultiCallBacks: true);

            _channelWriters = new ChannelWriter<AbstractEventTaskContext<T>>[_numChannels];

            for (int i = 0; i < _numChannels; i++)
            {
                var channel = CreateChannel(_maxBoundSize);
                _channelWriters[i] = channel.Writer;
                var partitionId = i; // we need to create local variable to avoid lambda closure issue, don't use loop variable directly
                _ = Task.Run(() => StartReaderTaskAsync(partitionId, channel.Reader)); // background
            }
        }

        private static Channel<AbstractEventTaskContext<T>> CreateChannel(int maxBoundSize)
        {
            if (maxBoundSize <= 0)
            {
                // unbounded
                return Channel.CreateUnbounded<AbstractEventTaskContext<T>>(
                new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = true,
                    AllowSynchronousContinuations = false
                });
            }
            else
            {
                return Channel.CreateBounded<AbstractEventTaskContext<T>>(
                new BoundedChannelOptions(maxBoundSize)
                {
                    SingleWriter = false,
                    SingleReader = true,
                    FullMode = BoundedChannelFullMode.Wait,
                    AllowSynchronousContinuations = false
                });
            }
        }

        public override void SetBufferedTaskProcessorFactory(IBufferedTaskProcessorFactory<T> bufferedTaskProcessorFactory)
        {
            if (_subTaskManager.NumTaskFactory > 0)
            {
                throw new InvalidOperationException("Cannot set buffered task processor factory after subtask factory is set");
            }
            _bufferedTaskProcessorFactory = bufferedTaskProcessorFactory;
        }

        public override void AddSubTaskFactory(ISubTaskFactory<T> subTaskFactory)
        {
            if (_bufferedTaskProcessorFactory != null)
            {
                throw new InvalidOperationException("Cannot add subtask factory after buffered task processor factory is set");
            }
            _subTaskManager.AddSubTaskFactory(subTaskFactory);
        }

        public override void SetExternalConcurrencyManager(IConcurrencyManager channelConcurrencyManager)
        {
        }

        protected override async Task ProcessEventTaskContextAsync(AbstractEventTaskContext<T> eventTaskContext)
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

                try
                {
                    var consumerPartitionId = GetConsumerPartitionId(eventTaskContext);
                    if (consumerPartitionId > 0)
                    {
                        consumerPartitionId %= _numChannels;
                    }
                    else
                    {
                        consumerPartitionId = NextWriteId();
                    }

                    eventTaskContext.StopWatchTimeStamp = Stopwatch.GetTimestamp();

                    await _channelWriters[consumerPartitionId].WriteAsync(eventTaskContext).ConfigureAwait(false);
                }
                catch (Exception ex1)
                {
                    // Failed to write
                    exception = ex1;
                }

                if (exception != null)
                {
                    // In most of case, this should not happen.
                    // However when taskContext is cancelled, it could happen.
                    // Logs only when exception occurs

                    using var errorMonitor = CreateActivityMonitor(
                        AbstractPartitionedBufferedTaskChannelManagerWriteToChannelAsync,
                        eventTaskContext);

                    AddErrorProperty(eventTaskContext, errorMonitor.Activity);
                    errorMonitor.OnError(exception);

                    await ExecuteTaskErrorAndSetNextChannelAsync(eventTaskContext, exception).ConfigureAwait(false);
                    
                    // HandleNextChannelAsync Should be the last line
                    await HandleNextChannelAsync(eventTaskContext).ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception ex2)
            {
                exception = ex2;

                // Should not happen this line
                using var criticalMonitor = CreateCriticalActivityMonitor(
                    "ProcessEventTaskContextAsync",
                    eventTaskContext);

                criticalMonitor.OnError(ex2, true);
            }

            if (exception != null)
            {
                // Last resort
                await MoveToPoisonAndStartNextChannelAsync(eventTaskContext, SolutionUtils.GetExceptionTypeSimpleName(exception), exception).ConfigureAwait(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NextWriteId()
        {
            return ThreadSafeRandom.Next(_numChannels);
        }

        private async Task StartReaderTaskAsync(int partitionId, ChannelReader<AbstractEventTaskContext<T>> channelReader)
        {
            List<AbstractEventTaskContext<T>>? eventTaskContexts = new(_maxBufferedSize);
            IBufferedTaskProcessor<T>? bufferedTaskProcessor = null;

            while (!_disposed)
            {
                try
                {
                    while (await channelReader.WaitToReadAsync().ConfigureAwait(false))
                    {
                        if (_bufferedTaskProcessorFactory != null)
                        {
                            bufferedTaskProcessor ??= _bufferedTaskProcessorFactory.CreateBufferedTaskProcessor();

                            if (_delayInMilli > 0)
                            {
                                await Task.Delay(_delayInMilli).ConfigureAwait(false);
                            }

                            eventTaskContexts.Clear();

                            ClearCurrentEventTaskContext();

                            while (channelReader.TryRead(out var eventTaskContext))
                            {
                                if (eventTaskContext != null)
                                {
                                    SetCurrentEventTaskContext(eventTaskContext);

                                    if (eventTaskContext.StopWatchTimeStamp > 0)
                                    {
                                        var channelWriteToReadElapsed = (int)Stopwatch.GetElapsedTime(eventTaskContext.StopWatchTimeStamp).TotalMilliseconds;
                                        channelWriteToReadElapsed = channelWriteToReadElapsed < 0 ? 0 : channelWriteToReadElapsed;
                                        // This elapsed time is from the time when the task is before calling channel WriteAsync
                                        //  to the time when it is read from the channel
                                        // It might be delayed due to several factors. Consumer might take time to processes the batch of tasks
                                        // We can know it from 
                                        ChannelWriteToReadMetricDuration.Record(channelWriteToReadElapsed,
                                            new KeyValuePair<string, object?>(MonitoringConstants.NameDimension, ChannelName),
                                            new KeyValuePair<string, object?>(MonitoringConstants.PartitionIdDimension, partitionId));
                                    }

                                    if (eventTaskContext.IsAlreadyTaskCancelled())
                                    {
                                        await MoveToPoisonAndStartNextChannelAsync(
                                            eventTaskContext,
                                            PoisonReason.TaskCancelled.FastEnumToString(),
                                            null).ConfigureAwait(false);
                                        continue;
                                    }

                                    Exception? exception = null;
                                    try
                                    {
                                        await BeforeProcessAsync(eventTaskContext).ConfigureAwait(false);
                                    }
                                    catch (Exception pex)
                                    {
                                        exception = pex;

                                        using var errorMonitor = CreateActivityMonitor(
                                            AbstractPartitionedBufferedTaskChannelManagerBeforeProcessAsync, 
                                            eventTaskContext);
                                        AddErrorProperty(eventTaskContext, errorMonitor.Activity);
                                        errorMonitor.OnError(pex);
                                    }

                                    if (exception != null)
                                    {
                                        await ExecuteTaskErrorAndSetNextChannelAsync(eventTaskContext, exception).ConfigureAwait(false);
                                    }

                                    if (eventTaskContext.NextTaskChannel != null)
                                    {
                                        // TaskContext is already moved to other channel
                                        await HandleNextChannelAsync(eventTaskContext).ConfigureAwait(false);
                                        continue;
                                    }

                                    eventTaskContexts.Add(eventTaskContext);

                                    if (eventTaskContexts.Count >= _maxBufferedSize)
                                    {
                                        ClearCurrentEventTaskContext();

                                        await InternalExecuteBufferedTasksAsync(bufferedTaskProcessor, eventTaskContexts).ConfigureAwait(false);
                                        eventTaskContexts.Clear();
                                    }
                                }
                            }

                            ClearCurrentEventTaskContext();

                            if (eventTaskContexts.Count > 0)
                            {
                                await InternalExecuteBufferedTasksAsync(bufferedTaskProcessor, eventTaskContexts).ConfigureAwait(false);
                                eventTaskContexts.Clear();
                            }
                        }
                        else
                        {
                            while (channelReader.TryRead(out var eventTaskContext))
                            {
                                if (eventTaskContext != null)
                                {
                                    await InternalExecuteTaskAsync(eventTaskContext).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!_disposed)
                    {
                        // Should not happen
                        using var criticalLogMonitor = AbstractTaskChannelManagerCriticalError.ToMonitor();
                        criticalLogMonitor.Activity[SolutionConstants.MethodName] = "StartReaderTaskAsync";
                        criticalLogMonitor.OnError(ex, true);
                    }
                }
            }
        }

        private async Task InternalExecuteBufferedTasksAsync(IBufferedTaskProcessor<T> bufferedTaskProcessor, List<AbstractEventTaskContext<T>> eventTaskContexts)
        {
            if (eventTaskContexts == null || eventTaskContexts.Count == 0)
            {
                return;
            }

            // Reset for batching processing
            ClearCurrentEventTaskContext();

            Exception? exception = null;
            using var monitor = AbstractPartitionedBufferedTaskChannelManagerInternalExecuteBufferedTasksAsync.ToMonitor(component: ChannelName);

            try
            {
                monitor.OnStart(false);
                await bufferedTaskProcessor.ProcessBufferedTasksAsync(eventTaskContexts).ConfigureAwait(false);
                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                exception = ex;
                monitor.OnError(ex);
            }

            if (exception != null)
            {
                for (int i = 0; i < eventTaskContexts.Count; i++)
                {
                    var eventTaskContext = eventTaskContexts[i];
                    if (eventTaskContext.NextTaskChannel == null)
                    {
                        // TaskContext is not yet set to other channel
                        SetCurrentEventTaskContext(eventTaskContext);
                        await ExecuteTaskErrorAndSetNextChannelAsync(eventTaskContext, exception).ConfigureAwait(false);
                    }
                }
            }

            for (int i = 0; i < eventTaskContexts.Count; i++)
            {
                var eventTaskContext = eventTaskContexts[i];
                SetCurrentEventTaskContext(eventTaskContext);
                await HandleNextChannelAsync(eventTaskContext).ConfigureAwait(false);
            }
        }

        private async Task InternalExecuteTaskAsync(AbstractEventTaskContext<T> eventTaskContext)
        {
            SetCurrentEventTaskContext(eventTaskContext);

            Exception? exception = null;
            using var monitor = CreateActivityMonitor(
                AbstractPartitionedBufferedTaskChannelManagerInternalExecuteTaskAsync,
                eventTaskContext);

            try
            {
                var numTaskFactory = _subTaskManager.NumTaskFactory;

                monitor.OnStart(false);

                await BeforeProcessAsync(eventTaskContext).ConfigureAwait(false);

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
                monitor.OnError(ex);
            }

            if (exception != null)
            {
                await ExecuteTaskErrorAndSetNextChannelAsync(eventTaskContext, exception).ConfigureAwait(false);
            }

            // HandleNextChannelAsync Should be the last line
            await HandleNextChannelAsync(eventTaskContext).ConfigureAwait(false);
        }

        private Task UpdateDelay(int newVal)
        {
            if (newVal < 0)
            {
                return Task.CompletedTask;
            }

            var oldVal = _delayInMilli;
            Interlocked.Exchange(ref _delayInMilli, newVal);
             
            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", _configDelay, oldVal, newVal);

            return Task.CompletedTask;
        }

        private Task UpdateMaxBufferedSize(int newVal)
        {
            if (newVal < 0)
            {
                return Task.CompletedTask;
            }

            var oldVal = _maxBufferedSize;
            Interlocked.Exchange(ref _maxBufferedSize, newVal);

            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", _configMaxBufferedSize, oldVal, newVal);

            return Task.CompletedTask;
        }

        private Task UpdateNumQueue(int newVal)
        {
            if (newVal <= 0)
            {
                Logger.LogError("{config} must be larger than 0", _configNumQueue);
                return Task.CompletedTask;
            }

            lock (_updateLock)
            {
                if (_disposed)
                {
                    return Task.CompletedTask;
                }

                var oldMaxSize = _numChannels;
                if (newVal == oldMaxSize)
                {
                    return Task.CompletedTask;
                }

                var oldChannelWriters = _channelWriters;
                if (newVal <= oldChannelWriters.Length)
                {
                    // Just change _numReaders, to avoid race condition, we don't decrease array size
                    if (Interlocked.CompareExchange(ref _numChannels, newVal, oldMaxSize) == oldMaxSize)
                    {
                        Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                            _configNumQueue, oldMaxSize, newVal);
                    }
                }
                else
                {
                    // newVal > oldChannelWriters.Length
                    // We need to increase channelWriter Array
                    // To avoid race condition,
                    // 1. first create ChannelArray
                    // 2. Reaplce _maxBatchWriters
                    var newChannelWriters = new ChannelWriter<AbstractEventTaskContext<T>>[newVal];
                    for (int i = 0; i < newVal; i++)
                    {
                        if (i < oldChannelWriters.Length)
                        {
                            newChannelWriters[i] = oldChannelWriters[i];
                        }
                        else
                        {
                            // Create new channel
                            var channel = CreateChannel(_maxBoundSize);
                            newChannelWriters[i] = channel.Writer;
                            var partitionId = i; // we need to create local variable to avoid lambda closure issue, don't use loop variable directly
                            _ = Task.Run(() => StartReaderTaskAsync(partitionId, channel.Reader)); // background
                        }
                    }

                    // Replace writers
                    Interlocked.Exchange(ref _channelWriters, newChannelWriters);

                    if (Interlocked.CompareExchange(ref _numChannels, newVal, oldMaxSize) == oldMaxSize)
                    {
                        Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                         _configNumQueue, oldMaxSize, newVal);
                    }
                }
            }
            return Task.CompletedTask;
        }

        private Task UpdateQueueLength(int newVal)
        {
            if (newVal <= 0)
            {
                Logger.LogError("{config} must be larger than 0", _configQueueLength);
                return Task.CompletedTask;
            }

            lock (_updateLock)
            {
                if (_disposed)
                {
                    return Task.CompletedTask;
                }

                var oldMaxSize = _maxBoundSize;
                if (newVal == oldMaxSize)
                {
                    return Task.CompletedTask;
                }

                // We need to recreate channelWriter Array
                // To avoid race condition,
                // 1. first create ChannelArray
                // 2. Replace _maxBatchWriters
                var numChannels = _numChannels;
                var newChannelWriters = new ChannelWriter<AbstractEventTaskContext<T>>[numChannels];

                for (int i = 0; i < numChannels; i++)
                {
                    var channel = CreateChannel(newVal);
                    newChannelWriters[i] = channel.Writer;
                    var partitionId = i; // we need to create local variable to avoid lambda closure issue, don't use loop variable directly
                    _ = Task.Run(() => StartReaderTaskAsync(partitionId, channel.Reader)); // background
                }

                // Replace writers
                Interlocked.Exchange(ref _maxBoundSize, newVal);
                Interlocked.Exchange(ref _channelWriters, newChannelWriters);
                
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                     _configQueueLength, oldMaxSize, newVal);
            }

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                var channelWriters = _channelWriters;
                for (int i = 0; i < channelWriters.Length; i++)
                {
                    try
                    {
                        channelWriters[i].Complete();
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }

                Dispose(true);

                _bufferedTaskProcessorFactory?.Dispose();
                _subTaskManager.Dispose();
            }
        }
    }
}
