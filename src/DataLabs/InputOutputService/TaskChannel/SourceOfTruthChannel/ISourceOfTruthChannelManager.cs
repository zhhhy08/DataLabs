namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.SourceOfTruthChannel
{
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;

    public interface ISourceOfTruthChannelManager<TInput> : ITaskChannelManager<IOEventTaskContext<TInput>> where TInput : IInputMessage
    {
    }
}
