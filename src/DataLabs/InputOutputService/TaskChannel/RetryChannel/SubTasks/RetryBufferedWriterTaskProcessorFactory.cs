namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RetryChannel.SubTasks
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;

    public class RetryBufferedWriterTaskProcessorFactory<TInput, TEvent, TEventBatch>
        : AbstractBufferedIOTaskProcessorFactory<TInput, TEvent, TEventBatch>
        where TInput : IInputMessage
        where TEvent : class
        where TEventBatch : class, IDisposable
    {
        private static readonly ILogger<RetryBufferedWriterTaskProcessorFactory<TInput, TEvent, TEventBatch>> Logger =
            DataLabLoggerFactory.CreateLogger<RetryBufferedWriterTaskProcessorFactory<TInput, TEvent, TEventBatch>>();

        public RetryBufferedWriterTaskProcessorFactory(List<IEventWriter<TEvent, TEventBatch>> eventWriters) 
            : base(eventWriters, SolutionConstants.RetryQueuePrefix)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool UseOutputMessage(IIOEventTaskContext eventTaskContext)
        {
            var inputMessage = eventTaskContext.BaseInputMessage;
            return (eventTaskContext.OutputMessage != null) && 
                (inputMessage == null || !SolutionInputOutputService.UseSourceOfTruth || eventTaskContext.IOEventTaskFlags.HasFlag(IOEventTaskFlag.SuccessUploadToSourceOfTruth));
        }

        protected override BinaryData GetBinaryData(IIOEventTaskContext eventTaskContext)
        {
            var inputMessage = eventTaskContext.BaseInputMessage;
            if (inputMessage == null)
            {
                // InputMessage could be null if it comes from RetryServiceBus or Stream ChildTask
                GuardHelper.ArgumentConstraintCheck(eventTaskContext.OutputMessage != null);
            }

            // We successfully posted to Source of Truth, so we can retry output directly
            bool useOutput = UseOutputMessage(eventTaskContext);
            return useOutput ? eventTaskContext.OutputMessage.GetOutputMessage() : inputMessage.SerializedData;
        }

        protected override void AddPropertyToEventData(
            IEventWriter<TEvent, TEventBatch> eventWriter, 
            TEvent eventData, 
            IIOEventTaskContext eventTaskContext,
            ref TagList tagList)
        {
            // Add Delay
            var delay = eventTaskContext.RetryDelay;
            if (delay.Ticks > 0)
            {
                eventWriter.AddDelayMessageTime(eventData, delay);
            }

            bool useOutputChannel = UseOutputMessage(eventTaskContext);

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
            IOServiceOpenTelemetry.RetryWriteSuccessDuration.Record((int)writeDurationInMilli, namePair);
            IOServiceOpenTelemetry.RetrySuccessBatchSizeMetric.Record(batchSize, namePair);
            IOServiceOpenTelemetry.RetryQueueWriteSuccessCounter.Add(batchSize, namePair);

            for (var i = 0; i < batchSize; i++)
            {
                var eventTaskContext = eventTaskContexts[i].TaskContext;
                eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.RetryQueueBatchWriteSuccess;
                eventTaskContext.TaskMovedToRetry(writeDurationInMilli, batchSize, endStopWatchTimestamp);
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
            IOServiceOpenTelemetry.RetryWriteFailDuration.Record((int)writeDurationInMilli, namePair);
            IOServiceOpenTelemetry.RetryQueueWriteFinalFailCounter.Add(count, namePair);

            for (var i = 0; i < count; i++)
            {
                var eventTaskContext = eventTaskContexts[i].TaskContext;
                eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.RetryQueueBatchWriteFail;
                eventTaskContext.TaskFailedToMoveToRetry(
                    failReason,
                    firstFailedException, writeDurationInMilli, count);
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

            Logger.LogCritical("RetryQueue Message is too large. size: {size}", messageSize);
            ioTaskContext.TaskMovingToPoison(PoisonReason.LargeSizeOutput.FastEnumToString(), "MessageSize: " + messageSize, IOComponent.RetryQueueWriter.FastEnumToString(), null);
        }
    }
}