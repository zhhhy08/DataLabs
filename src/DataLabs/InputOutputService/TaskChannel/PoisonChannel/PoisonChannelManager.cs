namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PoisonChannel
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public class PoisonChannelManager<TInput> : AbstractPartitionedBufferedTaskChannelManager<IOEventTaskContext<TInput>>, IPoisonChannelManager<TInput> where TInput : IInputMessage
    {
        public PoisonChannelManager() : base(IOTaskChannelType.PoisonChannel.FastEnumToString(), typeof(TInput).Name)
        {
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            var ioEventTaskContext = eventTaskContext.TaskContext;
            ioEventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.AddedToPoisonChannel;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext, Exception ex)
        {
            // Poison fails -> Drop
            eventTaskContext.TaskContext.TaskDrop(SolutionUtils.GetExceptionTypeSimpleName(ex), ex?.Message, IOComponent.PoisonChannel.FastEnumToString());
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            // Should not happen, ProcessNotMovedTaskAsync should not be called in this channel
            // Task should be moved to final channel
            // This is code bug..

            using (var criticalLogMonitor = CreateCriticalActivityMonitor("ProcessNotMovedTaskAsync", eventTaskContext))
            {
                criticalLogMonitor.OnError(ProcessNotMovedTaskException, true);
            }

            var ioEventTaskContext = eventTaskContext.TaskContext;
            ioEventTaskContext.TaskDrop(SolutionUtils.GetExceptionTypeSimpleName(ProcessNotMovedTaskException), ProcessNotMovedTaskException?.Message, IOComponent.PoisonChannel.FastEnumToString());
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
