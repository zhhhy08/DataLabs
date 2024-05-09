namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RawInputChannel
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public interface IRawInputChannelManager<TInput> : ITaskChannelManager<IOEventTaskContext<TInput>> where TInput : IInputMessage
    {
    }
}
