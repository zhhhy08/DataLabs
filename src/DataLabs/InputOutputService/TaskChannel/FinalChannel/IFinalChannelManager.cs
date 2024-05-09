namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.FinalChannel
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public interface IFinalChannelManager<TInput> : ITaskChannelManager<IOEventTaskContext<TInput>> where TInput : IInputMessage
    {
    }
}
