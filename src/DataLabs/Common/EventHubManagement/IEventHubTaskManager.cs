namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using global::Azure.Messaging.EventHubs.Processor;
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Primitives;
    using global::Azure.Messaging.EventHubs.Consumer;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IEventHubTaskManager
    {
        public void SetUpdateCheckpointAsyncFunc(Func<string, long, long?, CancellationToken, Task> updateCheckpointAsyncFunc);
        public Task ProcessBatchEventDataAsync(IEnumerable<EventData> events, EventProcessorPartition partition, LastEnqueuedEventProperties lastEnqueuedEventProperties, CancellationToken cancellationToken);
        public Task PartitionInitializingHandlerAsync(string partitionId);
        public Task PartitionClosingHandlerAsync(PartitionClosingEventArgs args);
    }
}
