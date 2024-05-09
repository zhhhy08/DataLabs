namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public interface IPartnerChannelManager<TInput> : ITaskChannelManager<IOEventTaskContext<TInput>> where TInput : IInputMessage
    {
    }
}
