namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using global::Azure.Core;
    using global::Azure.Storage.Blobs;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient;

    [ExcludeFromCodeCoverage]
    public class EventHubBatchReader
    {
        #region Logging

        private static readonly ILogger<EventHubBatchReader> Logger =
            DataLabLoggerFactory.CreateLogger<EventHubBatchReader>();

        private ActivityMonitorFactory EventHubBatchReaderStartProcessingAsync = new("EventHubBatchReader.StartProcessingAsync");
        private ActivityMonitorFactory EventHubBatchReaderStopProcessingAsync = new("EventHubBatchReader.StopProcessingAsync");
        private ActivityMonitorFactory EventHubBatchReaderDeleteCheckpointsAsync = new("EventHubBatchReader.DeleteCheckpointsAsync");

        #endregion

        public string EventHubName => _eventHubBatchMessageProcessor.EventHubName;
        public string FullyQualifiedNamespace => _eventHubBatchMessageProcessor.FullyQualifiedNamespace;

        private const string CheckpointFormatVersionSuffix = "-v2";

        private readonly BlobContainerClient _checkpointStore;
        private readonly IEventHubTaskManager _eventHubTaskManager;
        private readonly EventHubBatchMessageProcessor _eventHubBatchMessageProcessor;

        private int _started;
        private int _stopping;

        public EventHubBatchReader(
            IEventHubTaskManager eventHubTaskManager,
            string eventHubConnectionString,
            string eventHubName,
            string storageConnectionString,
            string eventHubLeaseContainerName,
            EventHubProcessorOptions options)
        {
            _eventHubTaskManager = eventHubTaskManager;

            var containerName = eventHubLeaseContainerName + CheckpointFormatVersionSuffix;
            _checkpointStore = new BlobContainerClient(storageConnectionString, containerName);

            _eventHubBatchMessageProcessor = new EventHubBatchMessageProcessor(
                _checkpointStore,
                eventHubConnectionString,
                eventHubName,
                options,
                _eventHubTaskManager);
        }

        public EventHubBatchReader(
            IEventHubTaskManager eventHubTaskManager,
            string fullyQualifiedNamespace,
            string eventHubName,
            TokenCredential credential,
            string storageAccountName,
            string eventHubLeaseContainerName,
            EventHubProcessorOptions options)
        {
            _eventHubTaskManager = eventHubTaskManager;

            var checkpointStoreUri = new Uri(string.Format("https://{0}.blob.core.windows.net/{1}{2}",
                                storageAccountName,
                                eventHubLeaseContainerName,
                                CheckpointFormatVersionSuffix));

            var blobStorageLogsEnabled = ConfigMapUtil.Configuration.GetValue(SolutionConstants.BlobStorageLogsEnabled, false);
            var blobStorageTraceEnabled = ConfigMapUtil.Configuration.GetValue(SolutionConstants.BlobStorageTraceEnabled, false);

            var blobClientOptions = new BlobClientOptions();
            blobClientOptions.Diagnostics.IsLoggingEnabled = blobStorageLogsEnabled;
            blobClientOptions.Diagnostics.IsDistributedTracingEnabled = blobStorageTraceEnabled;

            _checkpointStore = new BlobContainerClient(checkpointStoreUri, credential, blobClientOptions);

            _eventHubBatchMessageProcessor = new EventHubBatchMessageProcessor(
                _checkpointStore,
                fullyQualifiedNamespace,
                eventHubName,
                credential,
                options,
                _eventHubTaskManager);
        }

        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            using var monitor = EventHubBatchReaderStartProcessingAsync.ToMonitor();
            monitor.Activity[SolutionConstants.EventHubName] = _eventHubBatchMessageProcessor.EventHubName;
            monitor.Activity[SolutionConstants.FullyQualifiedNamespace] = _eventHubBatchMessageProcessor.FullyQualifiedNamespace;
            monitor.OnStart();

            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            {
                monitor.Activity["AlreadyProcessing"] = true;
                monitor.OnCompleted();
                // Task is already starting
                return;
            }

            try
            {
                await _checkpointStore.CreateIfNotExistsAsync(cancellationToken: cancellationToken).IgnoreContext();
                await _eventHubBatchMessageProcessor.StartProcessingAsync(cancellationToken).IgnoreContext();

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _started, 0);

                monitor.OnError(ex, true);
                throw;
            }
        }

        public async Task StopProcessingAsync(CancellationToken cancellationToken)
        {

            using var monitor = EventHubBatchReaderStopProcessingAsync.ToMonitor();
            monitor.Activity[SolutionConstants.EventHubName] = _eventHubBatchMessageProcessor.EventHubName;
            monitor.Activity[SolutionConstants.FullyQualifiedNamespace] = _eventHubBatchMessageProcessor.FullyQualifiedNamespace;
            monitor.OnStart();

            if (_started == 0 || Interlocked.CompareExchange(ref _stopping, 1, 0) != 0)
            {
                monitor.Activity["AlreadyProcessing"] = true;
                monitor.OnCompleted();
                return;
            }

            try
            {
                await _eventHubBatchMessageProcessor.StopProcessingAsync(cancellationToken).IgnoreContext();
                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex, true);
                throw;

            }
            finally
            {
                Interlocked.Exchange(ref _stopping, 0);
                Interlocked.Exchange(ref _started, 0);
            }
        }

        public async Task<bool> DeleteCheckpointsAsync(string consumerGroupName, CancellationToken cancellationToken)
        {
            using var monitor = EventHubBatchReaderDeleteCheckpointsAsync.ToMonitor();
            monitor.Activity[SolutionConstants.EventHubName] = _eventHubBatchMessageProcessor.EventHubName;
            monitor.Activity[SolutionConstants.FullyQualifiedNamespace] = _eventHubBatchMessageProcessor.FullyQualifiedNamespace;
            monitor.OnStart();

            try
            {
                // path for checkpoints of a single partition is of format eg: abcinteuspremipehns0.servicebus.windows.net/abcinteuspremipeh/$default/checkpoint/0
                // we delete the entire folder for consumer group i.e. $default for example.
                // hence blobPrefix should be "abcinteuspremipehns0.servicebus.windows.net/abcinteuspremipeh/$default"
                var blobPrefix = $"{_eventHubBatchMessageProcessor.FullyQualifiedNamespace}/{_eventHubBatchMessageProcessor.EventHubName}/{consumerGroupName.ToLower()}";

                monitor.Activity["BlobPrefix"] = blobPrefix;
                monitor.Activity["ContainerName"] = _checkpointStore.Name;

                var result = await BlobUtils.DeleteBlobsWithPrefixAsync(container: _checkpointStore, blobPrefix: blobPrefix, exceptionOnDeleteFailed: false, cancellationToken: cancellationToken, logger:Logger).ConfigureAwait(false);
                
                monitor.OnCompleted();
                return result;

            } 
            catch(Exception ex)
            {
                monitor.OnError(ex, true);
                throw;
            }
        }

    }
}
