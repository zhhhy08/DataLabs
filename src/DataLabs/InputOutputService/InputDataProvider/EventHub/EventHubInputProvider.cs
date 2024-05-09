namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputDataProvider.EventHub
{
    using global::Azure.Core;
    using global::Azure.Identity;
    using global::Azure.Messaging.EventHubs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;

    [ExcludeFromCodeCoverage]
    internal class EventHubInputProvider : IEventhubInputProvider
    {
        private static readonly int NumPartitions;

        static EventHubInputProvider()
        {
            NumPartitions = ConfigMapUtil.Configuration.GetValue<int>(InputOutputConstants.InputEventHubPartitions, 60);
        }

        private static readonly ILogger<EventHubInputProvider> Logger =
            DataLabLoggerFactory.CreateLogger<EventHubInputProvider>();

        private static readonly ActivityMonitorFactory EventHubInputProviderStartAsync =
            new ActivityMonitorFactory("EventHubInputProvider.StartAsync");

        private readonly EventHubTaskManager _eventHubTaskManager;
        private readonly EventHubBatchReader _eventHubBatchReader;

        private int _started;
        private int _stopping;

        public string Name { get; }

        public EventHubInputProvider(
            string solutionName,
            string inputEHConnectionString,
            string inputEHName,
            string storageConnectionString,
            RegionConfig regionConfig,
            TimeSpan? initialOffsetFromCurrent = null,
            string consumerGroup = InputOutputConstants.DefaultConsumerGroupName)
        {
            GuardHelper.ArgumentNotNull(solutionName);
            GuardHelper.ArgumentNotNull(inputEHConnectionString);
            GuardHelper.ArgumentNotNull(inputEHName);
            GuardHelper.ArgumentNotNull(storageConnectionString);
            GuardHelper.ArgumentNotNull(consumerGroup);

            var connectionStringProperties = EventHubsConnectionStringProperties.Parse(inputEHConnectionString);
            var fullyQualifiedNamespace = connectionStringProperties.FullyQualifiedNamespace;
            var ehNameSpace = fullyQualifiedNamespace.FastSplitAndReturnFirst('.');
            Name = ehNameSpace + '/' + inputEHName;

            Logger.LogInformation($"EventHubName: {Name} with consumerGroup : {consumerGroup},initialOffsetFromCurrent : {initialOffsetFromCurrent}");

            var options = EventHubReaderOptionsUtils.CreateDefaultOptions(consumerGroup, initialOffsetFromCurrent);
            var eventHubLeaseContainerName = GetEventHubLeaseContainerName(solutionName);

            _eventHubTaskManager = new EventHubTaskManager(ehNameSpace, inputEHName,consumerGroup, NumPartitions, regionConfig);
            _eventHubBatchReader = new EventHubBatchReader(
                _eventHubTaskManager,
                inputEHConnectionString,
                inputEHName,
                storageConnectionString,
                eventHubLeaseContainerName,
                options);
        }

        public EventHubInputProvider(
            string solutionName,
            TokenCredential credential,
            string fullyQualifiedInputEHNamespace,
            string inputEHName,
            string storageAccountName,
            RegionConfig regionConfig,
            TimeSpan? initialOffsetFromCurrent = null,
            string consumerGroup = InputOutputConstants.DefaultConsumerGroupName)
        {
            GuardHelper.ArgumentNotNull(solutionName);
            GuardHelper.ArgumentNotNull(fullyQualifiedInputEHNamespace);
            GuardHelper.ArgumentNotNull(inputEHName);
            GuardHelper.ArgumentNotNull(storageAccountName);
            GuardHelper.ArgumentNotNull(consumerGroup);

            var ehNameSpace = fullyQualifiedInputEHNamespace.FastSplitAndReturnFirst('.');
            Name = ehNameSpace + '/' + inputEHName;

            Logger.LogInformation($"EventHubName: {Name} with consumerGroup : {consumerGroup},initialOffsetFromCurrent : {initialOffsetFromCurrent}");

            var options = EventHubReaderOptionsUtils.CreateDefaultOptions(consumerGroup, initialOffsetFromCurrent);
            var eventHubLeaseContainerName = GetEventHubLeaseContainerName(solutionName);

            _eventHubTaskManager = new EventHubTaskManager(ehNameSpace, inputEHName, consumerGroup, NumPartitions, regionConfig);
            _eventHubBatchReader = new EventHubBatchReader(
                _eventHubTaskManager,
                fullyQualifiedInputEHNamespace,
                inputEHName,
                credential,
                storageAccountName,
                eventHubLeaseContainerName,
                options);
        }

        private static string GetEventHubLeaseContainerName(string solutionName)
        {
            return solutionName.ToLowerInvariant() + "-input";
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var monitor = EventHubInputProviderStartAsync.ToMonitor();
            monitor.Activity["EventHubName"] = Name;
            monitor.OnStart();

            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            {
                // Task is already starting
                monitor.Activity["AlreadyStarted"] = true;
                monitor.OnCompleted();
                return;
            }

            try
            {
                Logger.LogWarning("EventHubInputProvider is started. EventHubName: {EventHubName}", Name);
                await _eventHubBatchReader.StartProcessingAsync(cancellationToken).IgnoreContext();

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _started, 0);

                Logger.LogCritical(ex, "EventHubInputProvider StartAsync got Exception. EventHubName: {EventHubName}. {exception}",
                    Name, ex.ToString());
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_started == 0 || Interlocked.CompareExchange(ref _stopping, 1, 0) != 0)
            {
                return;
            }

            try
            {
                Logger.LogWarning("EventHubInputProvider is stopped. EventHubName: {name}", Name);
                await _eventHubBatchReader.StopProcessingAsync(cancellationToken).IgnoreContext();
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "EventHubInputProvider StopAsync got Exception. EventHubName: {EventHubName}. {exception}",
                    Name, ex.ToString());
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
            try
            {
                Logger.LogWarning("Deleting checkpoints for eventhub name {Name}", Name);
                var result = await _eventHubBatchReader.DeleteCheckpointsAsync(consumerGroupName, cancellationToken).IgnoreContext();
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "EventHubInputProvider DeleteCheckpoints got Exception. EventHubName: {EventHubName}. {exception}",
                    Name, ex.ToString());
                throw;
            }
        }

        private static EventHubInputProvider CreateEventHubInputProvider(string eventHubNameSpaceAndName, string storageAccountName, string consumerGroup, string solutionName, RegionConfig regionConfig,
            string inputEHConnectionString = null, string storageConnectionString = null, TimeSpan? initialOffsetFromCurrentOverride = null)
        {
            var names = eventHubNameSpaceAndName.Split('/');
            var inputEventHubNameSpace = names[0];
            var inputEHName = names[1];

            TimeSpan? initialOffsetFromCurrent = null;

            if (names.Length > 2 && initialOffsetFromCurrentOverride == null)
            {
                var startTimeSpan = TimeSpan.Parse(names[2]);
                if (startTimeSpan.TotalSeconds > 0)
                {
                    initialOffsetFromCurrent = startTimeSpan;
                }
            } else
            {
                initialOffsetFromCurrent = initialOffsetFromCurrentOverride;
            }

            var fullyQualifiedInputEhNamespace = $"{inputEventHubNameSpace}.servicebus.windows.net";

            if (!string.IsNullOrEmpty(inputEHConnectionString) && !string.IsNullOrEmpty(storageConnectionString) && !string.IsNullOrEmpty(storageConnectionString))
            {
                return new EventHubInputProvider(solutionName,
                    inputEHConnectionString,
                    inputEHName,
                    storageConnectionString,
                    regionConfig,
                    initialOffsetFromCurrent,
                    consumerGroup);
            }
            else
            {
                return new EventHubInputProvider(solutionName,
                        new DefaultAzureCredential(),
                        fullyQualifiedInputEhNamespace,
                        inputEHName,
                        storageAccountName,
                        regionConfig,
                        initialOffsetFromCurrent,
                        consumerGroup);
            }
        }

        public static void AddEventHubInputProviders(TimeSpan? initialOffsetFromCurrent = null)
        {
            var configuration = ConfigMapUtil.Configuration;

            var configString = configuration.GetValue<string>(InputOutputConstants.InputEventHubNameSpaceAndName);
            if (string.IsNullOrWhiteSpace(configString))
            {
                return;
            }

            // TODO: make hotconfig for below config
            // So that we can remove / add EventHubInputProvider through hotconfig
            var eventHubNameSpaceAndNames = configString.ConvertToSet(false);
            if (eventHubNameSpaceAndNames == null || eventHubNameSpaceAndNames.Count == 0)
            {
                return;
            }

            var inputEHConnectionString = configuration.GetValue<string>(InputOutputConstants.InputEventHubConnectionString);
            if (inputEHConnectionString != null && eventHubNameSpaceAndNames.Count > 1)
            {
                // Using connection string is only for testing
                throw new InvalidOperationException("InputEventHubConnectionString can only be used with one EventHub");
            }

            var storageConnectionString = configuration.GetValue<string>(InputOutputConstants.EventHubStorageAccountConnectionString);
            var storageAccountName = configuration.GetValue<string>(InputOutputConstants.EventHubStorageAccountName);
            var solutionName = configuration.GetValue<string>(InputOutputConstants.SolutionName);
            var consumerGroup = configuration.GetValue<string>(InputOutputConstants.InputEventHubConsumerGroup, InputOutputConstants.DefaultConsumerGroupName);
            var primaryRegionName = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.PrimaryRegionName);


            foreach (var eventHubNameSpaceAndName in eventHubNameSpaceAndNames)
            {
                var provider = CreateEventHubInputProvider(
                    eventHubNameSpaceAndName: eventHubNameSpaceAndName,
                    storageAccountName: storageAccountName,
                    consumerGroup: consumerGroup,
                    solutionName: solutionName,
                    inputEHConnectionString: inputEHConnectionString,
                    storageConnectionString: storageConnectionString,
                    regionConfig: RegionConfigManager.GetRegionConfig(primaryRegionName),
                    initialOffsetFromCurrentOverride: initialOffsetFromCurrent);
                SolutionInputOutputService.InputProviders.Add(provider);
            }
        }

        public static void AddBackupEventHubInputProviders(TimeSpan? initialOffsetFromCurrent = null, string consumerGroupOverride = null)
        {
            var configuration = ConfigMapUtil.Configuration;

            var eventHubNameSpaceAndNames = configuration
                .GetValue<string>(InputOutputConstants.BackupInputEventHubNameSpaceAndName)
                .ConvertToSet(false);

            var inputEHConnectionString = configuration.GetValue<string>(InputOutputConstants.BackupInputEventHubConnectionString);
            if (inputEHConnectionString != null && eventHubNameSpaceAndNames.Count > 1)
            {
                // Using connection string is only for testing
                throw new InvalidOperationException("BackupInputEventHubConnectionString can only be used with one EventHub");
            }

            var storageConnectionString = configuration.GetValue<string>(InputOutputConstants.EventHubStorageAccountConnectionString);
            var storageAccountName = configuration.GetValue<string>(InputOutputConstants.EventHubStorageAccountName);
            var solutionName = configuration.GetValue<string>(InputOutputConstants.SolutionName);
            var backupRegionName = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.BackupRegionName);
            var consumerGroupConfig = configuration.GetValue<string>(InputOutputConstants.InputEventHubConsumerGroup, InputOutputConstants.DefaultConsumerGroupName);
            var consumerGroup = consumerGroupOverride ?? consumerGroupConfig;


            foreach (var eventHubNameSpaceAndName in eventHubNameSpaceAndNames)
            {
                var provider = CreateEventHubInputProvider(
                    eventHubNameSpaceAndName: eventHubNameSpaceAndName,
                    storageAccountName: storageAccountName,
                    consumerGroup: consumerGroup,
                    solutionName: solutionName,
                    inputEHConnectionString: inputEHConnectionString,
                    storageConnectionString: storageConnectionString,
                    regionConfig: RegionConfigManager.GetRegionConfig(backupRegionName),
                    initialOffsetFromCurrentOverride: initialOffsetFromCurrent);
                SolutionInputOutputService.BackupInputProviders.Add(provider);
            }
        }
    }
}