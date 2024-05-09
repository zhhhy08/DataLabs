namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;

    public interface IBufferedTaskProcessorFactory<T> : IDisposable
    {
        public IBufferedTaskProcessor<T> CreateBufferedTaskProcessor();
    }

    public interface IBufferedTaskProcessor<T>
    {
        public Task ProcessBufferedTasksAsync(IReadOnlyList<AbstractEventTaskContext<T>> eventTaskContexts);
    }
}
