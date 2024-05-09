namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.InputChannel
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public interface IInputChannelManager<TInput> : ITaskChannelManager<IOEventTaskContext<TInput>> where TInput : IInputMessage
    {
    }
}
