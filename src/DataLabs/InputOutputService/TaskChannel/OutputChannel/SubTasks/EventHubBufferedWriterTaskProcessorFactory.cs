namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputChannel.SubTasks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;

    public class EventHubBufferedWriterTaskProcessorFactory<TEvent, TEventBatch>
        : AbstractBufferedIOTaskProcessorFactory<ARNSingleInputMessage, TEvent, TEventBatch>
        where TEvent : class
        where TEventBatch : class, IDisposable
    {
        private static readonly ILogger<EventHubBufferedWriterTaskProcessorFactory<TEvent, TEventBatch>> Logger =
            DataLabLoggerFactory.CreateLogger<EventHubBufferedWriterTaskProcessorFactory<TEvent, TEventBatch>>();

        private readonly string _outputDataSet;
        private int _eventHubWriteFailRetryDelayInMsec; // msecs

        public EventHubBufferedWriterTaskProcessorFactory(List<IEventWriter<TEvent, TEventBatch>> eventWriters)
            : base(eventWriters, SolutionConstants.EventHubPrefix)
        {
            _outputDataSet = ConfigMapUtil.Configuration.GetValue(SolutionConstants.OutputDataset, "newpartner");

            _eventHubWriteFailRetryDelayInMsec =
                ConfigMapUtil.Configuration.GetValueWithCallBack<int>(InputOutputConstants.EventHubWriteFailRetryDelayInMsec,
                UpdateFailRetryDelay, 1000);
        }

        protected override BinaryData GetBinaryData(IIOEventTaskContext eventTaskContext)
        {
            return eventTaskContext.OutputMessage?.GetOutputMessage();
        }

        protected override void AddPropertyToEventData(
            IEventWriter<TEvent, TEventBatch> eventWriter,
            TEvent eventData,
            IIOEventTaskContext eventTaskContext,
            ref TagList tagList)
        {
            eventWriter.AddProperty(eventData, SolutionConstants.OutputEventHubDataSetName, _outputDataSet);
        }

        protected override void HandleWriteSuccess(
            List<AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>>> eventTaskContexts,
            long writeDurationInMilli,
            long endStopWatchTimestamp,
            string eventHubName)
        {
            if (writeDurationInMilli < 0)
            {
                writeDurationInMilli = 0;
            }

            int batchSize = eventTaskContexts.Count;

            var namePair = new KeyValuePair<string, object?>(MonitoringConstants.EventHubNameDimension, eventHubName);
            IOServiceOpenTelemetry.EventHubWriteSuccessDuration.Record((int)writeDurationInMilli, namePair);
            IOServiceOpenTelemetry.EventHubSuccessBatchSizeMetric.Record(batchSize, namePair);
            IOServiceOpenTelemetry.EventHubWriteSuccessCounter.Add(batchSize, namePair);

            for (var i = 0; i < batchSize; i++)
            {
                var eventTaskContext = eventTaskContexts[i].TaskContext;
                eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.EventHubBatchWriteSuccess;
                eventTaskContext.TaskMovedToEventHub(writeDurationInMilli, batchSize, endStopWatchTimestamp);
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
            IOServiceOpenTelemetry.EventHubWriteFailDuration.Record((int)writeDurationInMilli, namePair);
            IOServiceOpenTelemetry.EventHubWriteFinalFailCount.Add(count, namePair);

            for (var i = 0; i < count; i++)
            {
                var eventTaskContext = eventTaskContexts[i].TaskContext;
                eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.EventHubBatchWriteFail;
                eventTaskContext.TaskFailedToMoveToEventHub(firstFailedException, writeDurationInMilli, count, _eventHubWriteFailRetryDelayInMsec);
            }
        }

        protected override void HandleTooLargeFailure(
            AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext,
            long maxAllowedSize)
        {
            // TODO use blob upload
            var ioTaskContext = eventTaskContext.TaskContext;
            var outputMessage = ioTaskContext.OutputMessage;
            int messageSize = outputMessage.GetOutputMessageSize();

            Logger.LogCritical("OutputMessage is too large. OutputMessage Size: {osize}, MaximumSizeInBytes: {MaximumSizeInBytes}",
                messageSize, maxAllowedSize);
            ioTaskContext.TaskMovingToPoison(PoisonReason.LargeSizeOutput.FastEnumToString(), "MessageSize: " + messageSize, IOComponent.EventHubWriter.FastEnumToString(), null);
        }

        private Task UpdateFailRetryDelay(int newDelayInMs)
        {
            if (newDelayInMs <= 0)
            {
                Logger.LogError("{config} must be larger than 0", InputOutputConstants.EventHubWriteFailRetryDelayInMsec);
                return Task.CompletedTask;
            }

            //ConnectErrorDelaymsForNextTry 
            var oldDelay = _eventHubWriteFailRetryDelayInMsec;
            if (Interlocked.CompareExchange(ref _eventHubWriteFailRetryDelayInMsec, newDelayInMs, oldDelay) == oldDelay)
            {
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    InputOutputConstants.EventHubWriteFailRetryDelayInMsec, oldDelay, newDelayInMs);
            }

            return Task.CompletedTask;
        }
    }
}
