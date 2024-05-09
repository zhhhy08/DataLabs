namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;

    public abstract class AbstractBufferedEventTaskWriter<T, TEvent, TEventBatch>
        where TEvent : class
        where TEventBatch : class, IDisposable
    {
        private static readonly ILogger<AbstractBufferedEventTaskWriter<T, TEvent, TEventBatch>> Logger =
            DataLabLoggerFactory.CreateLogger<AbstractBufferedEventTaskWriter<T, TEvent, TEventBatch>>();

        private const string ActivityMonitorNameSuffix = ".BufferedEventTaskWriterSendEventDataBatchAsync";

        private readonly ActivityMonitorFactory BufferedEventTaskWriterSendEventDataBatchAsync;

        private readonly string _configPrefix;
        private TimeSpan _batchWriteTimeout;
        private int _maxBatchSize;

        public AbstractBufferedEventTaskWriter(string configPrefix)
        {
            _configPrefix = configPrefix;

            BufferedEventTaskWriterSendEventDataBatchAsync = new(configPrefix + ActivityMonitorNameSuffix);
            
            var configName = _configPrefix + SolutionConstants.EventBatchMaxSizeSuffix;
            _maxBatchSize = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                configName, UpdateEventBatchMaxSize, 300, allowMultiCallBacks: true);
            GuardHelper.ArgumentConstraintCheck(_maxBatchSize > 0, configName);

            configName = _configPrefix + SolutionConstants.EventBatchWriterTimeOutInSecSuffix;
            var batchWriteTimeoutInSec = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                configName, UpdateEventBatchWriterTimeOutInSec, 10, allowMultiCallBacks: true);
            GuardHelper.ArgumentConstraintCheck(batchWriteTimeoutInSec > 0, configName);
            _batchWriteTimeout = TimeSpan.FromSeconds(batchWriteTimeoutInSec);
        }

        protected abstract BinaryData GetBinaryData(AbstractEventTaskContext<T> eventTaskContext);
        protected abstract void HandleEmptyBinaryData(AbstractEventTaskContext<T> eventTaskContext);
        protected abstract void HandleWriteSuccess(List<AbstractEventTaskContext<T>> eventTaskContexts, long writeDurationInMilli, long endStopWatchTimestamp, string writerName);
        protected abstract void HandleTooLargeFailure(AbstractEventTaskContext<T> eventTaskContext, long maxAllowedSize);
        protected abstract void AddPropertyToEventData(IEventWriter<TEvent, TEventBatch> eventWriter, TEvent eventData, AbstractEventTaskContext<T> eventTaskContext);

        public async Task<int> HandleBufferedEventTasksAsync(
            IEventWriter<TEvent, TEventBatch> eventWriter,
            IReadOnlyList<AbstractEventTaskContext<T>> eventTaskContexts, 
            List<AbstractEventTaskContext<T>> eventTaskContextsInBatch)
        {
            // This is batch writer.
            // Clear individual task-specific asyncLocal just in case
            OpenTelemetryActivityWrapper.Current = null;

            eventTaskContextsInBatch.Clear();

            TEventBatch? eventDataBatch = null;
            CancellationTokenSource? cancellationTokenSource = null;

            int numBatchWrites = 0;

            try
            {
                for (int i = 0, n = eventTaskContexts.Count; i < n; i++)
                {
                    var eventTaskContext = eventTaskContexts[i];

                    // Check if eventTaskContext already has next Channel
                    if (eventTaskContext.NextTaskChannel != null)
                    {
                        continue;
                    }

                    var outputMessage = GetBinaryData(eventTaskContext);
                    if (outputMessage == null || outputMessage.ToMemory().Length == 0)
                    {
                        // Empty Response
                        HandleEmptyBinaryData(eventTaskContext);
                        continue;
                    }

                    if (eventDataBatch == null)
                    {
                        eventTaskContextsInBatch.Clear();

                        if (cancellationTokenSource != null)
                        {
                            cancellationTokenSource.Dispose();
                            cancellationTokenSource = null;
                        }

                        cancellationTokenSource = new CancellationTokenSource(_batchWriteTimeout);
                        var cancellationToken = cancellationTokenSource.Token;

                        eventDataBatch = await eventWriter.CreateEventDataBatch(cancellationToken).ConfigureAwait(false);
                    }

                    // Create EventData
                    var eventData = eventWriter.CreateEventData(outputMessage);
                    // Add Property
                    AddPropertyToEventData(eventWriter, eventData, eventTaskContext);

                    // Add to EventDataBatch
                    var batchSize = eventWriter.GetBatchCount(eventDataBatch);
                    if (batchSize >= _maxBatchSize || !eventWriter.TryAddToBatch(eventDataBatch, eventData))
                    {
                        // flush
                        var hasPrevData = batchSize > 0;
                        if (hasPrevData)
                        {
                            await SendEventDataBatchAsync(eventWriter, eventDataBatch, eventTaskContextsInBatch, cancellationTokenSource!.Token).ConfigureAwait(false);
                            numBatchWrites++;

                            // Clear 
                            eventDataBatch.Dispose();
                            eventDataBatch = null;
                            cancellationTokenSource.Dispose();
                            cancellationTokenSource = null;
                            eventTaskContextsInBatch.Clear();

                            // Create new batch
                            cancellationTokenSource = new CancellationTokenSource(_batchWriteTimeout);
                            var cancellationToken = cancellationTokenSource.Token;
                            eventDataBatch = await eventWriter.CreateEventDataBatch(cancellationToken).ConfigureAwait(false);
                        }

                        if (!hasPrevData || !eventWriter.TryAddToBatch(eventDataBatch, eventData))
                        {
                            // Too large single item
                            var maxAllowedSize = eventWriter.GetMaxSizeInBytes(eventDataBatch);
                            var messageSize = eventWriter.GetSizeInBytes(eventData);
                            eventTaskContext.EventTaskActivity.SetTag(SolutionConstants.TooLargeFailMessageSize, messageSize);

                            TagList tagList = default;
                            tagList.Add(SolutionConstants.SizeInBytes, messageSize);
                            tagList.Add(SolutionConstants.MaxAllowedSizeInBytes, maxAllowedSize);
                            eventTaskContext.EventTaskActivity.AddEvent(SolutionConstants.EventName_TooLargeMessage, tagList);

                            HandleTooLargeFailure(eventTaskContext, maxAllowedSize);
                        }
                        else
                        {
                            eventTaskContextsInBatch.Add(eventTaskContext);
                        }
                    }
                    else
                    {
                        eventTaskContextsInBatch.Add(eventTaskContext);
                    }
                }

                if (eventDataBatch != null && eventWriter.GetBatchCount(eventDataBatch) > 0)
                {
                    // flush
                    await SendEventDataBatchAsync(eventWriter, eventDataBatch, eventTaskContextsInBatch, cancellationTokenSource!.Token).ConfigureAwait(false);
                    numBatchWrites++;

                    // Clear
                    eventDataBatch.Dispose();
                    eventDataBatch = null;
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                    eventTaskContextsInBatch.Clear();
                }
            }
            finally
            {
                if (eventDataBatch != null)
                {
                    eventDataBatch.Dispose();
                    eventDataBatch = null;
                }

                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }

                eventTaskContextsInBatch.Clear();
            }

            return numBatchWrites;
        }

        private async Task SendEventDataBatchAsync(
            IEventWriter<TEvent, TEventBatch> eventWriter,
            TEventBatch eventDataBatch,
            List<AbstractEventTaskContext<T>> eventTaskContextsInBatch, 
            CancellationToken cancellationToken)
        {
            /*
            * When published, the result is atomic; 
            * either all events that belong to the batch were successful or all have failed. 
            * Partial success is not possible.
            */

            if (eventTaskContextsInBatch.Count == 0)
            {
                return;
            }

            // This is batched writer.
            // Clear individual task-specific asyncLocal just in case
            OpenTelemetryActivityWrapper.Current = null;

            using var monitor = BufferedEventTaskWriterSendEventDataBatchAsync.ToMonitor();

            var startTimeStamp = Stopwatch.GetTimestamp();

            try {
                monitor.OnStart(false);

                monitor.Activity[SolutionConstants.WriterName] = eventWriter.Name;
                monitor.Activity[SolutionConstants.BatchCount] = eventWriter.GetBatchCount(eventDataBatch);
                monitor.Activity[SolutionConstants.SizeInBytesInBatch] = eventWriter.GetSizeInBytes(eventDataBatch);
                monitor.Activity[SolutionConstants.MaxAllowedSizeInBytes] = eventWriter.GetMaxSizeInBytes(eventDataBatch);

                await eventWriter.SendBatchAsync(eventDataBatch, cancellationToken).ConfigureAwait(false);

                // Success
                long endTimestamp = Stopwatch.GetTimestamp();
                var writeDurationInMilli = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                HandleWriteSuccess(eventTaskContextsInBatch, writeDurationInMilli, endTimestamp, eventWriter.Name);

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        private Task UpdateEventBatchMaxSize(int newVal)
        {
            if (newVal <= 0)
            {
                Logger.LogError("{prefix}EventBatchMaxSize must be larger than 0", _configPrefix);
                return Task.CompletedTask;
            }

            var oldMaxSize = _maxBatchSize;
            if (Interlocked.CompareExchange(ref _maxBatchSize, newVal, oldMaxSize) == oldMaxSize)
            {
                Logger.LogWarning("{prefix}EventBatchMaxSize is changed, Old: {oldVal}, New: {newVal}",
                    _configPrefix, oldMaxSize, newVal);
            }

            return Task.CompletedTask;
        }

        private Task UpdateEventBatchWriterTimeOutInSec(int newVal)
        {
            if (newVal <= 0)
            {
                Logger.LogError("{prefix}BatchWriterTimeOutInSec must be larger than 0", _configPrefix);
                return Task.CompletedTask;
            }

            var oldTimeout = _batchWriteTimeout;
            if (newVal == oldTimeout.TotalSeconds)
            {
                return Task.CompletedTask;
            }

            _batchWriteTimeout = TimeSpan.FromSeconds(newVal);

            Logger.LogWarning("{prefix}BatchWriterTimeOutInSec is changed, Old: {oldVal}, New: {newVal}",
                    _configPrefix, oldTimeout.TotalSeconds, newVal);

            return Task.CompletedTask;
        }
    }
}
