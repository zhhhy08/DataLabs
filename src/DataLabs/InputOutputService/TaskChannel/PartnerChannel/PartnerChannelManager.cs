namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public class PartnerChannelManager<TInput> : AbstractConcurrentTaskChannelManager<IOEventTaskContext<TInput>>, IPartnerChannelManager<TInput> where TInput : IInputMessage
    {
        public PartnerChannelManager(string channelName) : base(channelName, typeof(TInput).Name)
        {
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            eventTaskContext.TaskContext.IOEventTaskFlags |= IOEventTaskFlag.AddedToPartnerChannel;
            eventTaskContext.TaskContext.PartnerChannelName = ChannelName;
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

        protected override void Dispose(bool disposing)
        {
        }
    }
}
