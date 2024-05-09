namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RetryChannel.SubTasks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;

    public class SubJobBufferedWriterTaskProcessorFactory<TEvent, TEventBatch>
        : AbstractBufferedIOTaskProcessorFactory<ARNSingleInputMessage, TEvent, TEventBatch>
        where TEvent : class
        where TEventBatch : class, IDisposable
    {
        private static readonly ILogger<SubJobBufferedWriterTaskProcessorFactory<TEvent, TEventBatch>> Logger =
            DataLabLoggerFactory.CreateLogger<SubJobBufferedWriterTaskProcessorFactory<TEvent, TEventBatch>>();

        public SubJobBufferedWriterTaskProcessorFactory(List<IEventWriter<TEvent, TEventBatch>> eventWriters) 
            : base(eventWriters, SolutionConstants.SubJobQueuePrefix)
        {
        }

        protected override BinaryData GetBinaryData(IIOEventTaskContext eventTaskContext)
        {
            return eventTaskContext.OutputMessage.GetOutputMessage();
        }

        protected override void AddPropertyToEventData(
            IEventWriter<TEvent, TEventBatch> eventWriter, 
            TEvent eventData, 
            IIOEventTaskContext eventTaskContext,
            ref TagList tagList)
        {
            var taskActivity = eventTaskContext.EventTaskActivity;
            var outputMessage = eventTaskContext.OutputMessage;

            // Add ActivityId
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_ActivityId, Tracer.ConvertToActivityId(taskActivity.Context)); // string

            // Add TopActivityStartTime
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_TopActivityStartTime, taskActivity.TopActivityStartTime.ToUnixTimeMilliseconds()); // long

            // Parent TraceId (Original Input) 
            var parentDifferentTraceId = taskActivity.ParentDifferentTraceId;
            if (!string.IsNullOrWhiteSpace(parentDifferentTraceId))
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_ParentTraceId, parentDifferentTraceId); // string
            }

            // Add Data Format
            eventWriter.AddProperty(eventData, SolutionConstants.DataFormat, outputMessage.OutputFormat.FastEnumToString()); // string

            // Single Resource
            eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_SingleResource, true); // bool

            // Correlation Id
            var correlationId = outputMessage.CorrelationId;
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_Input_CorrrelationId, correlationId); // string
            }

            // TenantId
            var tenantId = outputMessage.TenantId;
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_Input_Tenant_Id, tenantId); // string
            }

            // Resource Id
            var resourceId = outputMessage.ResourceId;
            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_Input_Resource_Id, resourceId); // string
            }
            
            // EventType
            var eventType = outputMessage.EventType;
            if (!string.IsNullOrWhiteSpace(eventType))
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_Input_EventType, eventType); // string
            }

            // ResourceLocation
            var resourceLocation = outputMessage.ResourceLocation;
            if (!string.IsNullOrWhiteSpace(resourceLocation))
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_Input_ResourceLocation, resourceLocation); // string
            }

            // EventTime
            var outputTimeStamp = outputMessage.OutputTimeStamp;
            if (outputTimeStamp > 0)
            {
                eventWriter.AddProperty(eventData, InputOutputConstants.PropertyTag_Input_EventTime, outputTimeStamp); // long
            }
        }

        protected override void HandleWriteSuccess(
            List<AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>>> eventTaskContexts,
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
            IOServiceOpenTelemetry.SubJobWriteSuccessDuration.Record((int)writeDurationInMilli, namePair);
            IOServiceOpenTelemetry.SubJobSuccessBatchSizeMetric.Record(batchSize, namePair);
            IOServiceOpenTelemetry.SubJobQueueWriteSuccessCounter.Add(batchSize, namePair);

            for (var i = 0; i < batchSize; i++)
            {
                var eventTaskContext = eventTaskContexts[i].TaskContext;
                eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.SubJobQueueBatchWriteSuccess;
                eventTaskContext.TaskSuccess(endStopWatchTimestamp);
            }
        }

        protected override void HandleWriteFailure(
            List<AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>>> eventTaskContexts,
            long writeDurationInMilli,
            Exception firstFailedException,
            string firstWriterName)
        {
            if (writeDurationInMilli < 0)
            {
                writeDurationInMilli = 0;
            }

            int count = eventTaskContexts.Count;

            var namePair = new KeyValuePair<string, object?>(MonitoringConstants.FirstFailedWriterNameDimension, firstWriterName);
            IOServiceOpenTelemetry.SubJobWriteFailDuration.Record((int)writeDurationInMilli, namePair);
            IOServiceOpenTelemetry.SubJobQueueWriteFinalFailCounter.Add(count, namePair);

            for (var i = 0; i < count; i++)
            {
                var eventTaskContext = eventTaskContexts[i].TaskContext;
                eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.SubJobQueueBatchWriteFail;
                eventTaskContext.TaskError(firstFailedException, IOComponent.SubJobQueueWriter.FastEnumToString(), 0);
            }
        }

        protected override void HandleTooLargeFailure(
            AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext,
            long maxAllowedSize)
        {
            // TODO
            // it should not happen, if it could hanppen, use blob
            // For now, move to poison
            var ioTaskContext = eventTaskContext.TaskContext;
            var outputMessage = ioTaskContext.OutputMessage;
            int messageSize = outputMessage.GetOutputMessageSize();

            Logger.LogCritical("SubJobQueue Message is too large. size: {size}", messageSize);
            ioTaskContext.TaskMovingToPoison(PoisonReason.LargeSizeOutput.FastEnumToString(), "MessageSize: " + messageSize, IOComponent.SubJobQueueWriter.FastEnumToString(), null);
        }
    }
}