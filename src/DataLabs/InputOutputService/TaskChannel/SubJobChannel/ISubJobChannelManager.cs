namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.SubJobChannel
{
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;

    public interface ISubJobChannelManager<TInput> : ITaskChannelManager<IOEventTaskContext<TInput>> where TInput : IInputMessage
    {
    }
}
