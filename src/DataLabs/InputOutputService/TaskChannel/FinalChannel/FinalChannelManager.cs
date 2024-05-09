namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.FinalChannel
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;

    public class FinalChannelManager<TInput> : AbstractTaskChannelManager<IOEventTaskContext<TInput>>, IFinalChannelManager<TInput> where TInput : IInputMessage
    {
        private static readonly ActivityMonitorFactory FinalChannelManagerProcessEventTaskContextAsync = new("FinalChannelManager.ProcessEventTaskContextAsync");
        private static readonly InvalidFinalStageException InvalidFinalStageException = new ("IOEventFinalStage is still NONE");

        public FinalChannelManager() : base(IOTaskChannelType.FinalChannel.FastEnumToString(), typeof(TInput).Name)
        {
        }
        
        protected override Task ProcessEventTaskContextAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            SetCurrentEventTaskContext(eventTaskContext);

            using var monitor = CreateActivityMonitor(
                    FinalChannelManagerProcessEventTaskContextAsync,
                    eventTaskContext);
            try
            {
                var ioEventTaskContext = eventTaskContext.TaskContext;
                var eventTaskName = ioEventTaskContext.EventTaskActivity.ActivityName;
                var inputEventAction = ioEventTaskContext.EventTaskActivity.EventAction;

                monitor.OnStart(false);

                eventTaskContext.TaskContext.IOEventTaskFlags |= IOEventTaskFlag.AddedToFinalChannel;

                monitor.Activity[SolutionConstants.TaskFinalStage] = eventTaskContext.EventFinalStage.FastEnumToString();
                monitor.Activity[SolutionConstants.TaskChannelBeforeSuccess] = eventTaskContext.PrevTaskChannel?.ChannelName;
                monitor.Activity[SolutionConstants.PartnerResponseFlags] = eventTaskContext.TaskContext.IOEventTaskFlags.GetPartnerResponseFlags();

                if (ioEventTaskContext.EventFinalStage == EventTaskFinalStage.NONE)
                {
                    
                }else
                {
                    switch(ioEventTaskContext.EventFinalStage)
                    {
                        case EventTaskFinalStage.RETRY_QUEUE:
                            {
                                if (IOServiceOpenTelemetry.IsIndividualResourceTask(eventTaskName))
                                {
                                    IOServiceOpenTelemetry.ReportMovedToRetryIndividualResourceCounter(inputEventAction);
                                }
                            }
                            break;

                        case EventTaskFinalStage.POISON_QUEUE:
                            {
                                if (IOServiceOpenTelemetry.IsIndividualResourceTask(eventTaskName))
                                {
                                    IOServiceOpenTelemetry.ReportMovedToPoisonIndividualResourceCounter(inputEventAction);

                                    var inputEnqueuedTime = ioEventTaskContext.FirstEnqueuedTime;
                                    var inputPickedUpTime = ioEventTaskContext.FirstPickedUpTime;
                                    // Input Message could be null for retry flow
                                    var inputEventTime = ioEventTaskContext.EventTime;
                                    var retryCount = ioEventTaskContext.RetryCount;
                                    var currentUtcTime = DateTimeOffset.UtcNow;
                                    var isPartnerDecision = (ioEventTaskContext.IOEventTaskFlags & IOEventTaskFlag.PartnerPoisonResponse) != 0L;

                                    // Time since EventTime
                                    if (inputEventTime != default)
                                    {
                                        var delayFromInputEventTime = (int)(currentUtcTime - inputEventTime).TotalMilliseconds;
                                        IOServiceOpenTelemetry.ReportSLOToPoisonFromEventTimeMetric(
                                            delayFromInputEventTime: delayFromInputEventTime,
                                            inputAction: inputEventAction,
                                            isPartnerDecision: isPartnerDecision,
                                            retryCount: retryCount);
                                    }

                                    // Time since EventHub EnqueuedTIme
                                    if (inputEnqueuedTime != default)
                                    {
                                        var delayFromInputEnqueue = (int)(currentUtcTime - inputEnqueuedTime).TotalMilliseconds;
                                        IOServiceOpenTelemetry.ReportSLOToPoisonFromInputEnqueuedTimeMetric(
                                            delayFromInputEnqueue: delayFromInputEnqueue,
                                            inputAction: inputEventAction,
                                            isPartnerDecision: isPartnerDecision,
                                            retryCount: retryCount);
                                    }

                                    // Time since after reading EventHubMessage
                                    if (inputPickedUpTime != default)
                                    {
                                        var delayFromInputPickedUpTime = (int)(currentUtcTime - inputPickedUpTime).TotalMilliseconds;
                                        IOServiceOpenTelemetry.ReportSLOToPoisonFromInputPickedUpTimeMetric(
                                            delayFromInputPickedUpTime: delayFromInputPickedUpTime,
                                            inputAction: inputEventAction,
                                            isPartnerDecision: isPartnerDecision,
                                            retryCount: retryCount);

                                        // Add Elapsed Time excluding Partner Spent Time
                                        var partnerSpentTime = ioEventTaskContext.GetPartnerMaxTotalSpentTimeIncludingChildTasks();
                                        if (partnerSpentTime > 0)
                                        {
                                            var elapsedTimeExcludingPartnerTime = delayFromInputPickedUpTime - (int)partnerSpentTime;
                                            IOServiceOpenTelemetry.ReportSLOToPoisonExcludingPartnerTimeMetric(
                                                elapsedTimeExcludingPartnerTime: elapsedTimeExcludingPartnerTime,
                                                inputAction: inputEventAction,
                                                isPartnerDecision: isPartnerDecision,
                                                retryCount: retryCount);
                                        }
                                    }
                                }
                            }
                            break;

                        case EventTaskFinalStage.DROP:
                            {
                                if (IOServiceOpenTelemetry.IsIndividualResourceTask(eventTaskName))
                                {
                                    IOServiceOpenTelemetry.ReportDroppedIndividualResourceCounter(inputEventAction);
                                }
                            }
                            break;

                        case EventTaskFinalStage.NONE:
                            {
                                // This is critical error because FinalStage should be set before in FinalStage but we are still ok to continue
                                // So let's create separate critical error here for logging and continue
                                using var criticalLogMonitor = CreateCriticalActivityMonitor("ProcessEventTaskContextAsync", eventTaskContext);
                                criticalLogMonitor.OnError(InvalidFinalStageException, true);
                            }
                            break;

                        default:
                            break;
                    }

                }

                // This is really last step. It should not have any thing after this line
                // Do not call dispose part of TaskProcessCompleted because we still have monitor.OnCompleted() or onError() called which need current Activity
                // If we call dispose part of TaskProcessCompleted, OpenTelementry activity's dispose will be called and current Activity will be changed
                // And it causes incorrect traceId for OnCompleted() and onError()
                ioEventTaskContext.TaskProcessCompleted(false);

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                // This is very critical exception because we should not have any exception in FinalChannel
                monitor.OnError(ex, true);
            }
            finally
            {
                eventTaskContext.Dispose();
            }

            return Task.CompletedTask;
        }

        public override void AddSubTaskFactory(ISubTaskFactory<IOEventTaskContext<TInput>> subTaskFactory)
        {
            throw new InvalidOperationException();
        }

        public override void SetBufferedTaskProcessorFactory(IBufferedTaskProcessorFactory<IOEventTaskContext<TInput>> bufferedTaskProcessorFactory)
        {
            throw new InvalidOperationException();
        }

        public override void SetExternalConcurrencyManager(IConcurrencyManager channelConcurrencyManager)
        {
            throw new InvalidOperationException();
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext, Exception ex)
        {
            throw new NotImplementedException();
        }

        protected override ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
        }
    }
}