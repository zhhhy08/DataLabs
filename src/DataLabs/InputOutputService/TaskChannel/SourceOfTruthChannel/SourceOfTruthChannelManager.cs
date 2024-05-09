namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.SourceOfTruthChannel
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputChannel;

    public class SourceOfTruthChannelManager<TInput> : AbstractConcurrentTaskChannelManager<IOEventTaskContext<TInput>>, ISourceOfTruthChannelManager<TInput> where TInput : IInputMessage
    {
        private readonly IOutputChannelManager<TInput> _outputChannelManager;

        public SourceOfTruthChannelManager(IOutputChannelManager<TInput> outputChannelManager) :
            base(IOTaskChannelType.SourceOfTruthChannel.FastEnumToString(), typeof(TInput).Name)
        {
            _outputChannelManager = outputChannelManager;
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            var ioEventTaskContext = eventTaskContext.TaskContext;
            ioEventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.AddedToSourceOfTruthChannel;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext, Exception ex)
        {
            eventTaskContext.TaskContext.TaskError(ex, ChannelName, 0);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            // This could be called when SourceOfTruth is disabled
            eventTaskContext.SetNextChannel(_outputChannelManager);
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
