namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement
{
    using global::Azure.Messaging.ServiceBus;
    using System.Threading.Tasks;

    public interface IServiceBusTaskManager
    {
        public Task ProcessMessageAsync(ProcessMessageEventArgs args);
    }
}
