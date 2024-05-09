namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using global::Azure.Core;
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Consumer;
    using global::Azure.Messaging.EventHubs.Primitives;
    using global::Azure.Messaging.EventHubs.Processor;
    using global::Azure.Storage.Blobs;

    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    internal class EventHubBatchMessageProcessor
    {
        private static readonly ActivityMonitorFactory EventHubBatchMessageProcessorProcessingEventBatchError = 
            new("EventHubBatchMessageProcessor.ProcessingEventBatchError", LogLevel.Critical);

        private static readonly ActivityMonitorFactory EventHubBatchMessageProcessorStartProcessingAsync =
            new("EventHubBatchMessageProcessor.StartProcessingAsync");

        private static readonly ActivityMonitorFactory EventHubBatchMessageProcessorStopProcessingAsync =
            new("EventHubBatchMessageProcessor.StopProcessingAsync");

        private ActivityMonitorFactory EventHubBatchMessageProcessorPartitionInitializingHandlerAsync =
            new("EventHubBatchMessageProcessor.PartitionInitializingHandlerAsync");

        private ActivityMonitorFactory EventHubBatchMessageProcessorPartitionClosingHandlerAsync =
            new("EventHubBatchMessageProcessor.PartitionClosingHandlerAsync");

        private ActivityMonitorFactory EventHubBatchMessageProcessorProcessErrorHandler =
            new("EventHubBatchMessageProcessor.ProcessErrorHandler");

        #region Fields

        public string EventHubName => _batchProcessorClient.EventHubName;
        public string EventHubNamespace => _batchProcessorClient.EventHubNamespace;
        public string FullyQualifiedNamespace => _batchProcessorClient.FullyQualifiedNamespace;

        internal readonly BatchEventProcessorClient _batchProcessorClient;

        private readonly EventHubProcessorOptions _options;
        private readonly IEventHubTaskManager _eventHubTaskManager;

        #endregion

        #region Constructors

        public EventHubBatchMessageProcessor(
            BlobContainerClient checkpointStore,
            string eventHubConnectionString,
            string eventHubName,
            EventHubProcessorOptions options,
            IEventHubTaskManager eventHubTaskManager)
        {
            this._options = options;
            this._eventHubTaskManager = eventHubTaskManager;
            this._batchProcessorClient = new BatchEventProcessorClient(
                checkpointStore,
                eventHubConnectionString,
                eventHubName,
                options,
                (eventData, partition, lastEnqueuedEventProperties, token) => ProcessingEventBatchAsync(eventData, partition, lastEnqueuedEventProperties, token));

            eventHubTaskManager.SetUpdateCheckpointAsyncFunc(_batchProcessorClient.DoCheckpointAsync);
        }

        public EventHubBatchMessageProcessor(
            BlobContainerClient checkpointStore,
            string fullyQualifiedNamespace,
            string eventHubName,
            TokenCredential credential,
            EventHubProcessorOptions options,
            IEventHubTaskManager eventHubTaskManager)
        {
            this._options = options;
            this._eventHubTaskManager = eventHubTaskManager;
            this._batchProcessorClient = new BatchEventProcessorClient(
                checkpointStore, 
                fullyQualifiedNamespace, 
                eventHubName, 
                credential, 
                options,
                (eventData, partition, lastEnqueuedEventProperties, token) => ProcessingEventBatchAsync(eventData, partition, lastEnqueuedEventProperties, token));

            eventHubTaskManager.SetUpdateCheckpointAsyncFunc(_batchProcessorClient.DoCheckpointAsync);
        }

        #endregion

        #region Processing handlers
        protected virtual async Task PartitionInitializingHandlerAsync(PartitionInitializingEventArgs args)
        {
            using var monitor = EventHubBatchMessageProcessorPartitionInitializingHandlerAsync.ToMonitor();

            try
            {
                var partitionId = args.PartitionId;
                monitor.Activity.Properties[SolutionConstants.PartitionId] = partitionId;
                monitor.Activity.Properties[SolutionConstants.EventHubName] = EventHubName;
                monitor.Activity.Properties[SolutionConstants.FullyQualifiedNamespace] = FullyQualifiedNamespace;

                monitor.OnStart();

                var startPositionFromConfig = this._batchProcessorClient.ProcessorOptions.GetStartPositionWhenNoCheckpoint(this.EventHubNamespace,
                    this.EventHubName, args.PartitionId, monitor.Activity);

                if (startPositionFromConfig != null)
                {
                    args.DefaultStartingPosition = startPositionFromConfig.Value;
                    monitor.Activity.Properties["PositionSource"] = "Options";
                }
                else
                {
                    monitor.Activity.Properties["PositionSource"] = "NotSet";
                }

                monitor.Activity.Properties[SolutionConstants.DefaultStartingPosition] = args.DefaultStartingPosition;

                await _eventHubTaskManager.PartitionInitializingHandlerAsync(partitionId).IgnoreContext();

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                // The processor does not have enough understanding of your code to determine the correct action to take.
                // Any exceptions from your handlers go uncaught by the processor and will NOT be redirected to
                // the error handler.
                monitor.OnError(ex, true);
            }
        }

        protected virtual async Task PartitionClosingHandlerAsync(PartitionClosingEventArgs args)
        {
            using var monitor = EventHubBatchMessageProcessorPartitionClosingHandlerAsync.ToMonitor();

            try
            {
                monitor.OnStart();

                monitor.Activity.Properties[SolutionConstants.EventHubName] = EventHubName;
                monitor.Activity.Properties[SolutionConstants.FullyQualifiedNamespace] = FullyQualifiedNamespace;
                monitor.Activity.Properties[SolutionConstants.PartitionId] = args.PartitionId;
                monitor.Activity.Properties[SolutionConstants.CloseReason] = args.Reason;

                await _eventHubTaskManager.PartitionClosingHandlerAsync(args).IgnoreContext();

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                // It is very important that you always guard against
                // exceptions in your handler code; the processor does
                // not have enough understanding of your code to
                // determine the correct action to take.  Any
                // exceptions from your handlers go uncaught by
                // the processor and will NOT be handled in any
                // way.
                monitor.OnError(ex, true);
            }
        }

        protected virtual Task ProcessErrorHandler(ProcessErrorEventArgs args)
        {

            using var monitor = EventHubBatchMessageProcessorProcessErrorHandler.ToMonitor();

            try
            {
                monitor.OnStart();

                monitor.Activity.Properties[SolutionConstants.EventHubName] = EventHubName;
                monitor.Activity.Properties[SolutionConstants.FullyQualifiedNamespace] = FullyQualifiedNamespace;
                monitor.Activity.Properties[SolutionConstants.PartitionId] = args.PartitionId;
                monitor.Activity.Properties[SolutionConstants.Operation] = args.Operation;
                monitor.Activity.Properties[SolutionConstants.Exception] = args.Exception?.ToString();

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                // It is very important that you always guard against
                // exceptions in your handler code; the processor does
                // not have enough understanding of your code to
                // determine the correct action to take.  Any
                // exceptions from your handlers go uncaught by
                // the processor and will NOT be handled in any
                // way.
                monitor.OnError(ex, true);
            }

            return Task.CompletedTask;
        }

        protected async Task ProcessingEventBatchAsync(IEnumerable<EventData> events, EventProcessorPartition partition, LastEnqueuedEventProperties lastEnqueuedEventProperties, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await _eventHubTaskManager.ProcessBatchEventDataAsync(events, partition, lastEnqueuedEventProperties, cancellationToken).IgnoreContext();
            }
            catch (Exception ex)
            {
                // It is very important that you always guard against
                // exceptions in your handler code; the processor does
                // not have enough understanding of your code to
                // determine the correct action to take.  Any
                // exceptions from your handlers go uncaught by
                // the processor and will NOT be handled in any
                // way.
                // Should not reach this line

                using var criticalLogMonitor = EventHubBatchMessageProcessorProcessingEventBatchError.ToMonitor();
                criticalLogMonitor.OnError(ex, true);
            }
        }

        private Task ProcessEventHandler(ProcessEventArgs args)
        {
            throw new NotImplementedException("ProcessEventHandler should not be called in BatchMessageProcessor");
        }

        #endregion

        #region Public methods

        public async Task StartProcessingAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using var monitor = EventHubBatchMessageProcessorStartProcessingAsync.ToMonitor();

            try
            {
                monitor.Activity[SolutionConstants.EventHubName] = EventHubName;
                monitor.Activity[SolutionConstants.FullyQualifiedNamespace] = FullyQualifiedNamespace;
                monitor.OnStart();

                this._batchProcessorClient.ProcessEventAsync += ProcessEventHandler;
                this._batchProcessorClient.ProcessErrorAsync += ProcessErrorHandler;
                this._batchProcessorClient.PartitionInitializingAsync += PartitionInitializingHandlerAsync;
                this._batchProcessorClient.PartitionClosingAsync += PartitionClosingHandlerAsync;

                await _batchProcessorClient.StartProcessingAsync(cancellationToken).IgnoreContext();

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex, true);
                throw;
            }
        }

        public async Task StopProcessingAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            using var monitor = EventHubBatchMessageProcessorStopProcessingAsync.ToMonitor();

            try
            {
                monitor.Activity[SolutionConstants.EventHubName] = EventHubName;
                monitor.Activity[SolutionConstants.FullyQualifiedNamespace] = FullyQualifiedNamespace;
                monitor.OnStart();

                await this._batchProcessorClient.StopProcessingAsync(cancellationToken).IgnoreContext();

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex, true);
                throw;
            }
            finally
            {

                this._batchProcessorClient.ProcessEventAsync -= ProcessEventHandler;
                this._batchProcessorClient.ProcessErrorAsync -= ProcessErrorHandler;
                this._batchProcessorClient.PartitionInitializingAsync -= PartitionInitializingHandlerAsync;
                this._batchProcessorClient.PartitionClosingAsync -= PartitionClosingHandlerAsync;
            }
        }

        public LastEnqueuedEventProperties GetLastEnqueuedEventProperties(string PartitionId)
        {
            return _batchProcessorClient.GetLastEnqueuedEventProperties(PartitionId);
        }

        #endregion
    }
}
