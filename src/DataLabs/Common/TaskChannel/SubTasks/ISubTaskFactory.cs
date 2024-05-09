namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel
{
    using System;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;

    public interface ISubTaskFactory<T> : IDisposable
    {
        public string SubTaskName { get; }
        public bool CanContinueToNextTaskOnException { get; }
        public ISubTask<T> CreateSubTask(AbstractEventTaskContext<T> eventTaskContext);
    }
}
