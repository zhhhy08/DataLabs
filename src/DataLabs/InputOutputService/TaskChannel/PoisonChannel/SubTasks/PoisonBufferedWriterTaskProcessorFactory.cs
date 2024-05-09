namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PoisonChannel.SubTasks
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;

    public class PoisonBufferedWriterTaskProcessorFactory<TInput, TEvent, TEventBatch>
        : AbstractBufferedIOTaskProcessorFactory<TInput, TEvent, TEventBatch>
        where TInput : IInputMessage
        where TEvent : class
        where TEventBatch : class, IDisposable
    {
        private static readonly ILogger<PoisonBufferedWriterTaskProcessorFactory<TInput, TEvent, TEventBatch>> Logger =
            DataLabLoggerFactory.CreateLogger<PoisonBufferedWriterTaskProcessorFactory<TInput, TEvent, TEventBatch>>();

        public PoisonBufferedWriterTaskProcessorFactory(List<IEventWriter<TEvent, TEventBatch>> eventWriters) 
            : base(eventWriters, SolutionConstants.PoisonQueuePrefix)
        {
        }

        public override IBufferedTaskProcessor<IOEventTaskContext<TInput>> CreateBufferedTaskProcessor()
        {
            return new PoisonBufferedTaskProcessor(this);
        }

        protected override BinaryData GetBinaryData(IIOEventTaskContext eventTaskContext)
        {
            // For Poison, if we have inputDate, use it.
            var inputMessage = eventTaskContext.BaseInputMessage;
            if (inputMessage == null)
            {
                // InputMessage could be null if it comes from RetryServiceBus or Stream ChildTask
                GuardHelper.ArgumentConstraintCheck(eventTaskContext.OutputMessage != null);
                return eventTaskContext.OutputMessage.GetOutputMessage();
            }
            else
            {
                return inputMessage.SerializedData;
            }
        }

        protected override void AddPropertyToEventData(
            IEventWriter<TEvent, TEventBatch> eventWriter, 
            TEvent eventData, 
            IIOEventTaskContext eventTaskContext,
            ref TagList tagList)
        {
            // Add DeadLetterMark Property
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_DeadLetterMark, true);

            bool useOutputChannel = eventTaskContext.BaseInputMessage == null;

            // Add Properties for Retry
            AddRetryProperties(eventWriter, eventData, eventTaskContext, useOutputChannel, ref tagList);
        }

        protected override void HandleWriteSuccess(
            List<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts,
            long writeDurationInMilli,
            long endStopWatchTimestamp,
            string queueName)
        {
            if (writeDurationInMilli < 0)
            {
                writeDurationInMilli = 0;
            }

            int batchSize = eventTaskContexts.Count;

            var namePair = new KeyValuePair<string, object?>(MonitoringConstants.QueueNameDimension, queueName);
            IOServiceOpenTelemetry.PoisonWriteSuccessDuration.Record((int)writeDurationInMilli, namePair);
            IOServiceOpenTelemetry.PoisonSuccessBatchSizeMetric.Record(batchSize, namePair);
            IOServiceOpenTelemetry.PoisonQueueWriteSuccessCounter.Add(batchSize, namePair);

            for (var i = 0; i < batchSize; i++)
            {
                var eventTaskContext = eventTaskContexts[i].TaskContext;
                eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PoisonQueueBatchWriteSuccess;
                eventTaskContext.TaskMovedToPoison(writeDurationInMilli, batchSize, endStopWatchTimestamp);
            }
        }

        protected override void HandleWriteFailure(
            List<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts,
            long writeDurationInMilli,
            Exception firstFailedException,
            string firstWriterName)
        {
            if (writeDurationInMilli < 0)
            {
                writeDurationInMilli = 0;
            }

            var serviceBusException = firstFailedException as ServiceBusException ?? firstFailedException?.InnerException as ServiceBusException;
            var failReason = serviceBusException != null ? serviceBusException.Reason.FastEnumToString() :
                (firstFailedException != null ? SolutionUtils.GetExceptionTypeSimpleName(firstFailedException) : PoisonReason.RetryQueueWriteFail.FastEnumToString());

            int count = eventTaskContexts.Count;

            var namePair = new KeyValuePair<string, object?>(MonitoringConstants.FirstFailedWriterNameDimension, firstWriterName);
            IOServiceOpenTelemetry.PoisonWriteFailDuration.Record((int)writeDurationInMilli, namePair);
            IOServiceOpenTelemetry.PoisonQueueWriteFinalFailCounter.Add(count, namePair);

            for (var i = 0; i < count; i++)
            {
                var eventTaskContext = eventTaskContexts[i].TaskContext;
                eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PoisonQueueBatchWriteFail;
                eventTaskContext.TaskFailedToMoveToPoison(failReason, firstFailedException, writeDurationInMilli, count);
            }
        }

        protected override void HandleTooLargeFailure(
            AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext, 
            long maxAllowedSize)
        {
            // TODO
            // it should not happen, if it could hanppen, use blob
            // For now, move to poison
            var ioTaskContext = eventTaskContext.TaskContext;
            var outputMessage = ioTaskContext.OutputMessage;
            int messageSize = outputMessage.GetOutputMessageSize();

            Logger.LogCritical("PoisonQueue Message is too large. size: {size}", messageSize);
            ioTaskContext.TaskDrop(DropReason.LargeSizeOutput.FastEnumToString(), "MessageSize: " + messageSize, IOComponent.PoisonQueueWriter.FastEnumToString());
        }

        private class PoisonBufferedTaskProcessor : BufferedIOTaskProcessor
        {
            public PoisonBufferedTaskProcessor(AbstractBufferedIOTaskProcessorFactory<TInput, TEvent, TEventBatch> taskProcessorFactory) : 
                base(taskProcessorFactory)
            {
            }

            public override Task ProcessBufferedTasksAsync(IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts)
            {
                for(int i = 0, size = eventTaskContexts.Count; i < size; i++)
                {
                    var ioEventTaskContext = eventTaskContexts[i].TaskContext;
                    var taskActivity = ioEventTaskContext.EventTaskActivity;
                    var dataSource = ioEventTaskContext.DataSourceType;
                    if (dataSource == DataSourceType.ServiceBus || dataSource == DataSourceType.DeadLetterQueue)
                    {
                        // Since we are already in service bus retry flow, use short cut for poison flow
                        taskActivity.SetTag(InputOutputConstants.PoisonInsideRetry, true);
                        ioEventTaskContext.TaskMovedToPoison(0, 0, Stopwatch.GetTimestamp());
                    }
                }

                return base.ProcessBufferedTasksAsync(eventTaskContexts);
            }
        }
    }
}