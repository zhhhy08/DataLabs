namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.BlobPayloadRoutingChannelManager
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public interface IBlobPayloadRoutingChannelManager<TInput> : ITaskChannelManager<IOEventTaskContext<TInput>> where TInput : IInputMessage
    {
    }
}
