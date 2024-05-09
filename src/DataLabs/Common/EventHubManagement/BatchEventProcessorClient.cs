namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using global::Azure.Core;
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Consumer;
    using global::Azure.Messaging.EventHubs.Primitives;
    using global::Azure.Storage.Blobs;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    
    [ExcludeFromCodeCoverage]
    internal class BatchEventProcessorClient : EventProcessorClient
    {
        private static readonly ActivityMonitorFactory BatchEventProcessorClientUnexpectedProcessingStateFactory =
            new("BatchEventProcessorClient.UnexpectedProcessingState");

        public string EventHubNamespace { get; }
        public EventHubProcessorOptions ProcessorOptions { get; }
        private readonly Func<IEnumerable<EventData>, EventProcessorPartition, LastEnqueuedEventProperties, CancellationToken, Task> _processingEventBatchFunc;

        internal BatchEventProcessorClient(
            BlobContainerClient checkpointStore,
            string eventHubConnectionString,
            string eventHubName,
            EventHubProcessorOptions processorOptions,
            Func<IEnumerable<EventData>, EventProcessorPartition, LastEnqueuedEventProperties, CancellationToken, Task> processingEventBatchFunc) :
            base(checkpointStore, processorOptions.ConsumerGroup, eventHubConnectionString, eventHubName, processorOptions.ProcessorClientOptions)
        {
            EventHubNamespace = this.FullyQualifiedNamespace.SplitAndRemoveEmpty('.').FirstOrDefault()!;
            ProcessorOptions = processorOptions;
            _processingEventBatchFunc = processingEventBatchFunc;
        }

        internal BatchEventProcessorClient(
            BlobContainerClient checkpointStore,
            string fullyQualifiedNamespace,
            string eventHubName,
            TokenCredential credential,
            EventHubProcessorOptions processorOptions,
            Func<IEnumerable<EventData>, EventProcessorPartition, LastEnqueuedEventProperties, CancellationToken, Task> processingEventBatchFunc) :
            base(checkpointStore, processorOptions.ConsumerGroup, fullyQualifiedNamespace, eventHubName, credential, processorOptions.ProcessorClientOptions)
        {
            EventHubNamespace = this.FullyQualifiedNamespace.SplitAndRemoveEmpty('.').FirstOrDefault()!;
            ProcessorOptions = processorOptions;
            _processingEventBatchFunc = processingEventBatchFunc;
        }

        #region Public Method

        public LastEnqueuedEventProperties GetLastEnqueuedEventProperties(string PartitionId)
        {
            return this.ReadLastEnqueuedEventProperties(PartitionId);
        }

        public Task DoCheckpointAsync(string partitionId, long offset, long? sequenceNumber, CancellationToken cancellationToken)
        {
            return this.UpdateCheckpointAsync(partitionId, offset, sequenceNumber, cancellationToken);
        }

        #endregion

        #region EventProcessorClient overrides

        protected override Task OnProcessingEventBatchAsync(IEnumerable<EventData> events,
            EventProcessorPartition partition,
            CancellationToken cancellationToken)
        {
            var lastEnqueuedEventProperties = ReadLastEnqueuedEventProperties(partition.PartitionId);
            return _processingEventBatchFunc.Invoke(events, partition, lastEnqueuedEventProperties, cancellationToken);
        }

        // Handling for next issue (recommended by EH team):
        // - EH processor tried to read the checkpoint blob
        // - [xstore BUG] Xstore returned a false 404 for checkpoint for partition 30
        // - EH processor fell back to reading from DefaultStartingPoint
        // - EH processor rewind the cursor accordingly
        // Fix: retry during reading of checkpoint
        protected override async Task<EventProcessorCheckpoint?> GetCheckpointAsync(string partitionId,
            CancellationToken cancellationToken)
        {
            var checkpoint = await base.GetCheckpointAsync(partitionId, cancellationToken).ConfigureAwait(false);
            var currentRetry = 1;

            var startPositionWhenNoCheckpoint = this.ProcessorOptions.GetStartPositionWhenNoCheckpoint(this.EventHubNamespace, this.EventHubName, partitionId, parentActivity: null);

            while ((checkpoint == null ||
                    checkpoint.StartingPosition == EventPosition.Earliest ||
                    checkpoint.StartingPosition == startPositionWhenNoCheckpoint)
                   && currentRetry < ProcessorOptions.MaxRetryCountForCheckpoint)
            {
                checkpoint = await base.GetCheckpointAsync(partitionId, cancellationToken).ConfigureAwait(false);
                currentRetry++;
            }

            if (currentRetry > 1)
            {
                using var warningMonitor = BatchEventProcessorClientUnexpectedProcessingStateFactory.ToMonitor();
                warningMonitor.Activity["State"] = "Default checkpoint was return and retry was done";
                warningMonitor.Activity["PartitionId"] = partitionId;
                warningMonitor.Activity["EventHubNamespace"] = this.EventHubNamespace;
                warningMonitor.Activity["CurrentRetryCount"] = currentRetry;
                warningMonitor.Activity["Checkpoint"] =
                    checkpoint == null ? null : SerializationHelper.SerializeToString(checkpoint);
                warningMonitor.OnCompleted();
            }

            // If after all retries, we still have a default checkpoint, we will return it.
            // It will cause replay from a place defined in config (set in PartitionInitializingHandlerAsync).
            // If config StartPositionWhenNoCheckpoint wasn't present during initialization, replay will start with the earliest available event in the EventHub.
            return checkpoint;
        }

        #endregion // EventProcessorClient overrides
    }
}
