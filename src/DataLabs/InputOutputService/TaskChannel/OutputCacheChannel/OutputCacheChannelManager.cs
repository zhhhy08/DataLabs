namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputCacheChannel
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public class OutputCacheChannelManager : AbstractConcurrentTaskChannelManager<CacheTaskContext>, IOutputCacheChannelManager
    {
        public OutputCacheChannelManager() : base(IOTaskChannelType.OutputCacheChannel.FastEnumToString(), typeof(CacheTaskContext).Name)
        {
        }

        protected override ValueTask BeforeProcessAsync(AbstractEventTaskContext<CacheTaskContext> eventTaskContext)
        {
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessErrorAsync(AbstractEventTaskContext<CacheTaskContext> eventTaskContext, Exception ex)
        {
            // Do nothing for now
            eventTaskContext.EventFinalStage = EventTaskFinalStage.DROP;
            eventTaskContext.Dispose();
            return ValueTask.CompletedTask;
        }

        protected override ValueTask ProcessNotMovedTaskAsync(AbstractEventTaskContext<CacheTaskContext> eventTaskContext)
        {
            // There is no next channel after OutputCacheChannelManager
            eventTaskContext.Dispose();
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
