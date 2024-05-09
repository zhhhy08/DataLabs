namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.InputResourceCacheChannel
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public class InputCacheChannelManager<TInput> : AbstractConcurrentTaskChannelManager<IOEventTaskContext<TInput>>, IInputCacheChannelManager<TInput> where TInput : IInputMessage
    {
        public InputCacheChannelManager() : base(IOTaskChannelType.InputCacheChannel.FastEnumToString(), typeof(TInput).Name)
        {
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            eventTaskContext.TaskContext.IOEventTaskFlags |= IOEventTaskFlag.AddedToInputCacheChannel;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext, Exception ex)
        {
            //inputcache fail should be considered as a error and need to retry input so that InputCache will be guaranteed for future use
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
