namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputChannel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;

    public class OutputChannelManager<TInput> : IOAbstractPartitionedBufferedTaskChannelManager<TInput>, IOutputChannelManager<TInput> where TInput : IInputMessage
    {
        private static Counter<long> NotAllowedOutputResourceType = MetricLogger.CommonMeter.CreateCounter<long>("NotAllowedOutputResourceType");
        private static readonly NotAllowedOutputResourceTypeException NotAllowedOutputResourceTypeException = new("NotAllowedOutputResourceType");

        private readonly HashSet<string> _allowedOutputTypes;

        public OutputChannelManager() : base(IOTaskChannelType.OutputChannel.FastEnumToString(), typeof(TInput).Name)
        {
            // For security reason, we don't allow hotconfig for below config
            // Only allowed types will be published to output Channel
            _allowedOutputTypes = ConfigMapUtil.Configuration.GetValue<string>(InputOutputConstants.AllowedOutputTypes).ConvertToSet(false);

            GuardHelper.ArgumentConstraintCheck(_allowedOutputTypes?.Count > 0,
                InputOutputConstants.AllowedOutputTypes + " is a mandatory config key. We should have at least one allowed output type");
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            var ioTaskContext = eventTaskContext.TaskContext;
            var outputMessage = ioTaskContext.OutputMessage;

            // Empty Response
            if (outputMessage == null || outputMessage.GetOutputMessageSize() == 0)
            {
                eventTaskContext.TaskContext.TaskSuccess(Stopwatch.GetTimestamp());
                return ValueTask.CompletedTask;
            }

            // Internal Response check
            // Mostly this check is already done in SourceOfTruth channel but SourceOfTruth channel is optional
            if (SolutionUtils.IsInternalResponse(outputMessage?.RespProperties))
            {
                eventTaskContext.EventTaskActivity.SetTag(InputOutputConstants.InternalResponse, true);
                eventTaskContext.TaskContext.TaskSuccess(Stopwatch.GetTimestamp());
                return ValueTask.CompletedTask;
            }

            var resourceType = outputMessage.ResourceType;
            if (string.IsNullOrWhiteSpace(resourceType) || !_allowedOutputTypes.Contains(resourceType))
            {
                NotAllowedOutputResourceType.Add(1, new KeyValuePair<string, object?>(SolutionConstants.ResourceType, resourceType));
                eventTaskContext.TaskMovingToPoison(PoisonReason.NotAllowedOutputResourceType.FastEnumToString(), null, ChannelName, NotAllowedOutputResourceTypeException);
                return ValueTask.CompletedTask;
            }

            eventTaskContext.TaskContext.IOEventTaskFlags |= IOEventTaskFlag.AddedToOutputChannel;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext, Exception ex)
        {
            eventTaskContext.TaskContext.TaskError(ex, ChannelName, 0);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
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

        protected override ValueTask ProcessBeforeMovingToNextChannelAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            if (SolutionInputOutputService.UnitTestMode && SolutionInputOutputService.UnitTestBeforeMovingToNextChannelError)
            {
                throw new Exception("Exception for Unit Test");
            }

            try
            {
                var ioTaskContext = eventTaskContext.TaskContext;
                var outputMessage = ioTaskContext.OutputMessage;

                // when Output Channel succeeds, Let's update SLA
                if (outputMessage?.Data != null && eventTaskContext.EventFinalStage == EventTaskFinalStage.SUCCESS)
                {
                    // output type
                    var outputResourceType = outputMessage.ResourceType;
                    var outputAction = ArmUtils.GetAction(outputMessage.EventType);
                    var inputAction = ioTaskContext.EventTaskActivity.EventAction;

                    var inputEnqueuedTime = ioTaskContext.FirstEnqueuedTime;
                    var inputPickedUpTime = ioTaskContext.FirstPickedUpTime;
                    var inputEventTime = ioTaskContext.EventTime;
                    var retryCount = ioTaskContext.RetryCount;
                    var currentUtcTime = DateTimeOffset.UtcNow;

                    // Time since EventTime
                    if (inputEventTime != default)
                    {
                        var delayFromInputEventTime = (int)(currentUtcTime - inputEventTime).TotalMilliseconds;
                        IOServiceOpenTelemetry.ReportSLOFromEventTimeMetric(
                            delayFromInputEventTime: delayFromInputEventTime,
                            inputAction: inputAction,
                            outputResourceType: outputResourceType,
                            outputAction: outputAction,
                            retryCount: retryCount);
                    }

                    // Time since EventHub EnqueuedTIme
                    if (inputEnqueuedTime != default)
                    {
                        var delayFromInputEnqueue = (int)(currentUtcTime - inputEnqueuedTime).TotalMilliseconds;
                        IOServiceOpenTelemetry.ReportSLOFromInputEnqueuedTimeMetric(
                            delayFromInputEnqueue: delayFromInputEnqueue,
                            inputAction: inputAction,
                            outputResourceType: outputResourceType,
                            outputAction: outputAction,
                            retryCount: retryCount);
                    }

                    // Time since after reading EventHubMessage
                    if (inputPickedUpTime != default)
                    {
                        var delayFromInputPickedUpTime = (int)(currentUtcTime - inputPickedUpTime).TotalMilliseconds;
                        IOServiceOpenTelemetry.ReportSLOFromInputPickedUpTimeMetric(
                            delayFromInputPickedUpTime: delayFromInputPickedUpTime,
                            inputAction: inputAction,
                            outputResourceType: outputResourceType,
                            outputAction: outputAction,
                            retryCount: retryCount);

                        // Add Elapsed Time excluding Partner Spent Time
                        var partnerSpentTime = ioTaskContext.GetPartnerMaxTotalSpentTimeIncludingChildTasks();
                        if (partnerSpentTime > 0)
                        {
                            var elapsedTimeExcludingPartnerTime = delayFromInputPickedUpTime - (int)partnerSpentTime;
                            IOServiceOpenTelemetry.ReportSLOExcludingPartnerTimeMetric(
                                elapsedTimeExcludingPartnerTime: elapsedTimeExcludingPartnerTime,
                                inputAction: inputAction,
                                outputResourceType: outputResourceType,
                                outputAction: outputAction,
                                retryCount: retryCount);
                        }
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
