namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;

    public interface IPartnerChannelRoutingManager : IDisposable
    {
        public ValueTask SetNextChannelAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext);
        public Dictionary<string, IPartnerChannelManager<ARNSingleInputMessage>> PartnerChannelsDictionary { get; }
    }
}
