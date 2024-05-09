namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.BlobPayloadRoutingChannelManager
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    public class BlobPayloadRoutingChannelManager : IOAbstractPartitionedBufferedTaskChannelManager<ARNSingleInputMessage>, IBlobPayloadRoutingChannelManager<ARNSingleInputMessage>
    {
        public BlobPayloadRoutingChannelManager() : base(IOTaskChannelType.BlobPayloadRoutingChannel.FastEnumToString(), typeof(ARNSingleInputMessage).Name)
        {
            // fajin TODO: set up config default values
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            var ioTaskContext = eventTaskContext.TaskContext;
            var inputMessage = ioTaskContext.InputMessage;

            // If from retry, trigger deserialization
            if (inputMessage == null || inputMessage.DeserializedObject == null)
            {
                eventTaskContext.TaskContext.TaskSuccess(Stopwatch.GetTimestamp());
                return ValueTask.CompletedTask;
            }

            eventTaskContext.TaskContext.IOEventTaskFlags |= IOEventTaskFlag.AddedToBlobPayloadRoutingChannel;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext, Exception ex)
        {
            eventTaskContext.TaskContext.TaskError(ex, ChannelName, 0);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            // Should not happen, ProcessNotMovedTaskAsync should not be called in this channel
            // Task should be moved to other channel
            // This is code bug..
            using (var criticalLogMonitor = CreateCriticalActivityMonitor("ProcessNotMovedTaskAsync", eventTaskContext))
            {
                criticalLogMonitor.OnError(ProcessNotMovedTaskException, true);
            }

            eventTaskContext.TaskMovingToPoison(SolutionUtils.GetExceptionTypeSimpleName(ProcessNotMovedTaskException), null, ChannelName, ProcessNotMovedTaskException);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessBeforeMovingToNextChannelAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            if (SolutionInputOutputService.UnitTestMode && SolutionInputOutputService.UnitTestBeforeMovingToNextChannelError)
            {
                throw new Exception("Exception for Unit Test");
            }

            try
            {
                var ioTaskContext = eventTaskContext.TaskContext;
                var inputMessage = ioTaskContext.InputMessage;

                // when channel succeeds, update metrics for BlobPayloadRoutingChannel
                // This should be different with regular Output SLA
                if (inputMessage?.DeserializedObject != null && eventTaskContext.EventFinalStage == EventTaskFinalStage.SUCCESS)
                {
                    var inputResourceType = inputMessage.ResourceType;
                    var inputAction = inputMessage.EventAction;

                    var inputEnqueuedTime = ioTaskContext.FirstEnqueuedTime;
                    var inputPickedUpTime = ioTaskContext.FirstPickedUpTime;
                    var inputEventTime = ioTaskContext.EventTime;
                    var retryCount = ioTaskContext.RetryCount;
                    var currentUtcTime = DateTimeOffset.UtcNow;

                    // Time since EventTime
                    if (inputEventTime != default)
                    {
                        var delayFromInputEventTime = (int)(currentUtcTime - inputEventTime).TotalMilliseconds;
                        IOServiceOpenTelemetry.ReportBlobPayloadRoutingFromEventTimeMetric(
                            delayFromInputEventTime: delayFromInputEventTime,
                            inputAction: inputAction,
                            resourceType: inputResourceType,
                            retryCount: retryCount);
                    }

                    // Time since EventHub EnqueuedTIme
                    if (inputEnqueuedTime != default)
                    {
                        var delayFromInputEnqueue = (int)(currentUtcTime - inputEnqueuedTime).TotalMilliseconds;
                        IOServiceOpenTelemetry.ReportBlobPayloadRoutingFromInputEnqueuedTimeMetric(
                            delayFromInputEnqueue: delayFromInputEnqueue,
                            inputAction: inputAction,
                            resourceType: inputResourceType,
                            retryCount: retryCount);
                    }

                    // Time since after reading EventHubMessage
                    if (inputPickedUpTime != default)
                    {
                        var delayFromInputPickedUpTime = (int)(currentUtcTime - inputPickedUpTime).TotalMilliseconds;
                        IOServiceOpenTelemetry.ReportBlobPayloadRoutingFromInputPickedUpTimeMetric(
                            delayFromInputPickedUpTime: delayFromInputPickedUpTime,
                            inputAction: inputAction,
                            resourceType: inputResourceType,
                            retryCount: retryCount);
                    }
                }
            }
            catch (Exception ex)
            {
                using var criticalLogMonitor = CreateCriticalActivityMonitor("ProcessBeforeMovingToNextChannelAsync", eventTaskContext);
                criticalLogMonitor.OnError(ex, true);
            }
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
