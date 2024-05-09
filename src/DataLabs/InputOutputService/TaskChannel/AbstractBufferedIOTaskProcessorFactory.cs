namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;
    using System.Runtime.CompilerServices;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.ServiceBus;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;

    public abstract class AbstractBufferedIOTaskProcessorFactory<TInput, TEvent, TEventBatch>
        : IBufferedTaskProcessorFactory<IOEventTaskContext<TInput>>
        where TInput : IInputMessage
        where TEvent : class
        where TEventBatch : class, IDisposable
    {
        protected readonly List<IEventWriter<TEvent, TEventBatch>> _eventWriters;
        protected readonly string _configPrefix;

        private volatile bool _disposed;

        public AbstractBufferedIOTaskProcessorFactory(List<IEventWriter<TEvent, TEventBatch>> eventWriters, string configPrefix)
        {
            GuardHelper.ArgumentNotNullOrEmpty(eventWriters);
            _eventWriters = eventWriters;
            _configPrefix = configPrefix;
        }

        public virtual IBufferedTaskProcessor<IOEventTaskContext<TInput>> CreateBufferedTaskProcessor()
        {
            return new BufferedIOTaskProcessor(this);
        }

        protected abstract BinaryData GetBinaryData(IIOEventTaskContext eventTaskContext);
        protected abstract void AddPropertyToEventData(IEventWriter<TEvent, TEventBatch> eventWriter, TEvent eventData, IIOEventTaskContext eventTaskContext, ref TagList tagList);
        protected abstract void HandleWriteSuccess(List<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts, long writeDurationInMilli, long endStopWatchTimestamp, string writerName);
        protected abstract void HandleWriteFailure(List<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts, long writeDurationInMilli, Exception firstFailedException, string firstWriterName);
        protected abstract void HandleTooLargeFailure(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext, long maxAllowedSize);

        protected void AddRetryProperties(
            IEventWriter<TEvent, TEventBatch> eventWriter,
            TEvent eventData,
            IIOEventTaskContext eventTaskContext,
            bool useOutputChannel,
            ref TagList tagList)
        {
            var inputMessage = eventTaskContext.BaseInputMessage;
            var outputMessage = eventTaskContext.OutputMessage;

            GuardHelper.ArgumentConstraintCheck((inputMessage != null || outputMessage != null));

            // In order to use InputChannel, we need InputMessage
            useOutputChannel = (inputMessage == null || (outputMessage != null && useOutputChannel));

            var isBlobPayloadRouting = eventTaskContext.IOEventTaskFlags.HasFlag(IOEventTaskFlag.RetryTaskChannelOverWriteBlobPayloadRouting);
            var isSubJob = eventTaskContext.IOEventTaskFlags.HasFlag(IOEventTaskFlag.PartnerSubJobResponse);
            var eventTaskName = eventTaskContext.EventTaskActivity.ActivityName;

            string retryEntryChannelName;
            if (isBlobPayloadRouting)
            {
                retryEntryChannelName = IOTaskChannelType.BlobPayloadRoutingChannel.FastEnumToString();
            }
            else if (useOutputChannel)
            {
                retryEntryChannelName = IOTaskChannelType.OutputChannel.FastEnumToString();
            }
            else if (eventTaskName == InputOutputConstants.PartnerInputChildEventTask
                && !string.IsNullOrEmpty(eventTaskContext.PartnerChannelName)
                && inputMessage != null
                && !isSubJob)
            {
                // For multipods case, if we send to InputChannel, it might be routed to other pods(which already successfully processed)
                // so we need to send actual Partner Channel directly because we created individual task for each pod
                retryEntryChannelName = IOTaskChannelType.PartnerChannel.FastEnumToString();
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_PartnerChannelName, eventTaskContext.PartnerChannelName); // string
            }
            else
            {
                retryEntryChannelName = IOTaskChannelType.InputChannel.FastEnumToString();
            }

            // Add Retry Entry Channel
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_ChannelType, retryEntryChannelName); // string

            // Add TopLevelActivityContext
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_ActivityId, Tracer.ConvertToActivityId(eventTaskContext.EventTaskActivity.Context)); // string

            // Add TopActivityStartTime
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_TopActivityStartTime, eventTaskContext.EventTaskActivity.TopActivityStartTime.ToUnixTimeMilliseconds()); // long

            // Parent TraceId
            var parentDifferentTraceId = eventTaskContext.EventTaskActivity.ParentDifferentTraceId;
            if (parentDifferentTraceId != null)
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_ParentTraceId, parentDifferentTraceId); // string
            }

            // Add Retry Count
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_RetryCount, eventTaskContext.RetryCount + 1); // int

            // Add Failures
            if (eventTaskContext.FailedReason?.Length > 0)
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_FailedReason, eventTaskContext.FailedReason); // string
            }
            if (eventTaskContext.FailedDescription?.Length > 0)
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_FailedDescription, eventTaskContext.FailedDescription); // string
            }

            // Add SourceOfTruthConflict
            if (IOEventTaskFlagHelper.HasSourceOfTruthConflict(eventTaskContext.IOEventTaskFlags))
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_SourceOfTruthConflict, true); // bool
            }

            // SuccessInputCacheWrite
            if (eventTaskContext.IOEventTaskFlags.HasFlag(IOEventTaskFlag.SuccessInputCacheWrite))
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_SuccessInputCacheWrite, true); // bool
            }

            // Add PartnerTotalSpentTime
            var partnerTotalSpendTime = eventTaskContext.GetPartnerMaxTotalSpentTimeIncludingChildTasks();
            if (partnerTotalSpendTime > 0)
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_Partner_SpentTime, partnerTotalSpendTime); // long
            }

            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_First_EnqueuedTime, eventTaskContext.FirstEnqueuedTime.ToUnixTimeMilliseconds()); // long
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_First_PickedUpTime, eventTaskContext.FirstPickedUpTime.ToUnixTimeMilliseconds()); // long
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_HasInput, !useOutputChannel); // bool
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_HasOutput, useOutputChannel); // bool

            //add region pair data
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_RegionName, eventTaskContext.RegionConfigData.RegionLocationName); // string

            // Add Input Related properties
            if (inputMessage == null)
            {
                // Load previous set input properties if it comes from RetryQueue
                var dataSourceType = eventTaskContext.DataSourceType;
                if (dataSourceType == DataSourceType.DeadLetterQueue ||
                    dataSourceType == DataSourceType.ServiceBus)
                {
                    var retryServiceBusTaskInfo = eventTaskContext.EventTaskCallBack as ServiceBusTaskInfo;
                    retryServiceBusTaskInfo?.TryGetInputProperties(ref tagList);
                }
            }
            else
            {
                inputMessage.AddRetryProperties(ref tagList);
            }

            if (useOutputChannel)
            {
                eventTaskContext.OutputMessage.AddRetryProperties(ref tagList);
            }


            AddProperties(eventWriter, eventData, in tagList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AddProperties(IEventWriter<TEvent, TEventBatch> eventWriter, TEvent eventData, in TagList tagList)
        {
            for (int i = 0; i < tagList.Count; i++)
            {
                var kp = tagList[i];
                eventWriter.AddProperty(eventData, kp.Key, kp.Value);
            }
        }

        public virtual void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                for (int i = 0; i < _eventWriters.Count; i++)
                {
                    _eventWriters[i].Dispose();
                }
            }
        }

        /*
         * Notice: BufferedTaskProcessor is created per BufferedQueue. so all methods is thread-safe (single thread)
         */
        protected class BufferedIOTaskProcessor : IBufferedTaskProcessor<IOEventTaskContext<TInput>>
        {
            private const string ActivityMonitorNameSuffix = ".ProcessBufferedTasksAsync";

            protected readonly BufferedIOEventTaskWriter _bufferedIOEventTaskWriter;
            protected List<AbstractEventTaskContext<IOEventTaskContext<TInput>>> _eventTaskContextsInBatch = new(16);

            private readonly ActivityMonitorFactory BufferedIOTaskProcessorProcessBufferedTasksAsync;

            private readonly AbstractBufferedIOTaskProcessorFactory<TInput, TEvent, TEventBatch> _taskProcessorFactory;
            private readonly List<IEventWriter<TEvent, TEventBatch>> _eventWriters;
            private readonly Counter<long> BatchEventWriterFailCounter;
            private readonly Random _random = new();
            private Exception _firstException;
            private string _firstFailedWriterName;

            public BufferedIOTaskProcessor(AbstractBufferedIOTaskProcessorFactory<TInput, TEvent, TEventBatch> taskProcessorFactory)
            {
                _taskProcessorFactory = taskProcessorFactory;
                _eventWriters = taskProcessorFactory._eventWriters;

                var configPrefix = taskProcessorFactory._configPrefix;

                BufferedIOTaskProcessorProcessBufferedTasksAsync = new(configPrefix + ActivityMonitorNameSuffix);
                BatchEventWriterFailCounter = IOServiceOpenTelemetry.IOServiceNameMeter.CreateCounter<long>(configPrefix + IOServiceOpenTelemetry.BatchWriteFailCounterNameSuffix);

                _bufferedIOEventTaskWriter = new BufferedIOEventTaskWriter(taskProcessorFactory);
            }

            public virtual async Task ProcessBufferedTasksAsync(IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts)
            {
                _eventTaskContextsInBatch.Clear();
                _firstException = null;
                _firstFailedWriterName = null;

                var startTimeStamp = Stopwatch.GetTimestamp();

                int numRetry = 0;
                var firstWriterId = _eventWriters.Count == 1 ? 0 : _random.Next(_eventWriters.Count);
                var firstEventWriter = _eventWriters[firstWriterId];
                var numBatchWrites = await ProcessBufferedTasksAsync(firstEventWriter, eventTaskContexts, numRetry).ConfigureAwait(false);

                if (numBatchWrites >= 0)
                {
                    // Success case
                    return;
                }

                // This is the error case where we failed to write to the first selected writer
                var writerId = firstWriterId;
                for (int i = 0; i < _eventWriters.Count - 1; i++)
                {
                    numRetry++;
                    writerId = (writerId + 1) % _eventWriters.Count;
                    var eventWriter = _eventWriters[writerId];
                    numBatchWrites = await ProcessBufferedTasksAsync(eventWriter, eventTaskContexts, numRetry).ConfigureAwait(false);
                    if (numBatchWrites >= 0)
                    {
                        // Success case
                        return;
                    }
                }

                // Failed to write to all writers.
                // Let's save the total writeDuration (including all retries)
                long endTimestamp = Stopwatch.GetTimestamp();
                var writeDurationInMilli = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

                // Check still failed taskContexts
                _eventTaskContextsInBatch.Clear();

                for (int i = 0, n = eventTaskContexts.Count; i < n; i++)
                {
                    var eventTaskContext = eventTaskContexts[i];

                    // Check if eventTaskContext already has next Channel
                    if (eventTaskContext.NextTaskChannel != null)
                    {
                        continue;
                    }

                    _eventTaskContextsInBatch.Add(eventTaskContext);
                }

                if (_eventTaskContextsInBatch.Count == 0)
                {
                    // All eventTaskContexts already has next Channel
                    return;
                }

                _taskProcessorFactory.HandleWriteFailure(_eventTaskContextsInBatch, writeDurationInMilli, _firstException, _firstFailedWriterName);
            }

            private async Task<int> ProcessBufferedTasksAsync(
                IEventWriter<TEvent, TEventBatch> eventWriter,
                IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts,
                int numRetry)
            {
                using var monitor = BufferedIOTaskProcessorProcessBufferedTasksAsync.ToMonitor();

                Exception exception = null;
                try
                {
                    monitor.OnStart(false);
                    monitor.Activity[SolutionConstants.WriterName] = eventWriter.Name;
                    monitor.Activity[SolutionConstants.NumWriterRetry] = numRetry;

                    var numBatchWrites = await _bufferedIOEventTaskWriter.HandleBufferedEventTasksAsync(eventWriter, eventTaskContexts, _eventTaskContextsInBatch).ConfigureAwait(false);

                    monitor.Activity[SolutionConstants.NumBatchWrites] = numBatchWrites;
                    monitor.OnCompleted();

                    return numBatchWrites;
                }
                catch (Exception ex)
                {
                    exception = ex;

                    if (_firstException == null)
                    {
                        _firstException = ex;
                        _firstFailedWriterName = eventWriter.Name;
                    }
                    monitor.OnError(ex);
                }

                // Error
                BatchEventWriterFailCounter.Add(1,
                    new KeyValuePair<string, object?>(MonitoringConstants.EventWriterNameDimension, eventWriter.Name),
                    new KeyValuePair<string, object?>(MonitoringConstants.EventWriterExceptionDimension, exception.GetType().Name),
                    new KeyValuePair<string, object?>(MonitoringConstants.RetryCountWithNextEventWriterDimension, numRetry));

                return -1;
            }
        }

        public class BufferedIOEventTaskWriter : AbstractBufferedEventTaskWriter<IOEventTaskContext<TInput>, TEvent, TEventBatch>
        {
            private readonly AbstractBufferedIOTaskProcessorFactory<TInput, TEvent, TEventBatch> _taskProcessorFactory;
            private TagList _propertyList = new();

            public BufferedIOEventTaskWriter(AbstractBufferedIOTaskProcessorFactory<TInput, TEvent, TEventBatch> taskProcessorFactory) :
                base(taskProcessorFactory._configPrefix)
            {
                _taskProcessorFactory = taskProcessorFactory;
            }

            protected override void AddPropertyToEventData(IEventWriter<TEvent, TEventBatch> eventWriter, TEvent eventData, AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
            {
                _propertyList.Clear();
                _taskProcessorFactory.AddPropertyToEventData(eventWriter, eventData, eventTaskContext.TaskContext, ref _propertyList);
            }

            protected override BinaryData GetBinaryData(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
            {
                return _taskProcessorFactory.GetBinaryData(eventTaskContext.TaskContext);
            }

            protected override void HandleEmptyBinaryData(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
            {
                eventTaskContext.TaskContext.TaskSuccess(Stopwatch.GetTimestamp());
            }

            protected override void HandleTooLargeFailure(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext, long maxAllowedSize)
            {
                _taskProcessorFactory.HandleTooLargeFailure(eventTaskContext, maxAllowedSize);
            }

            protected override void HandleWriteSuccess(List<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts, long writeDurationInMilli, long endStopWatchTimestamp, string writerName)
            {
                _taskProcessorFactory.HandleWriteSuccess(eventTaskContexts, writeDurationInMilli, endStopWatchTimestamp, writerName);
            }
        }
    }
}