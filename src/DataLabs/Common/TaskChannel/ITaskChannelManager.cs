namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;

    public interface ITaskChannelManager<T> : IDisposable
    {
        public string ChannelName { get; }
        public Task ExecuteEventTaskContextAsync(AbstractEventTaskContext<T> eventTaskContext);
        public void AddSubTaskFactory(ISubTaskFactory<T> subTaskFactory);
        public void SetBufferedTaskProcessorFactory(IBufferedTaskProcessorFactory<T> bufferedTaskProcessorFactory);
        public void SetExternalConcurrencyManager(IConcurrencyManager channelConcurrencyManager);
    }
}
