namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RetryChannel
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public class RetryChannelManager<TInput> : AbstractPartitionedBufferedTaskChannelManager<IOEventTaskContext<TInput>>, IRetryChannelManager<TInput> where TInput : IInputMessage
    {
        public RetryChannelManager() : base(IOTaskChannelType.RetryChannel.FastEnumToString(), typeof(TInput).Name)
        {
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            var ioEventTaskContext = eventTaskContext.TaskContext;
            ioEventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.AddedToRetryChannel;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext, Exception ex)
        {
            // Internal error inside RetryChannel
            eventTaskContext.TaskContext.TaskFailedToMoveToRetry(SolutionUtils.GetExceptionTypeSimpleName(ex), ex, 0, 0);
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

        protected override void Dispose(bool disposing)
        {
        }
    }
}
