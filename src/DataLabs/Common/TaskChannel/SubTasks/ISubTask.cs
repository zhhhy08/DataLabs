namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel
{
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;

    public interface ISubTask<T>
    {
        public bool UseValueTask { get; }
        public ValueTask ProcessEventTaskContextValueAsync(AbstractEventTaskContext<T> eventTaskContext);
        public Task ProcessEventTaskContextAsync(AbstractEventTaskContext<T> eventTaskContext);
    }
}
