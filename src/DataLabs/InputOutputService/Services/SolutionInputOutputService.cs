namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services
{
    using global::Azure.Identity;
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Producer;
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TestEmulator;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputDataProvider;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputDataProvider.EventHub;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputOutputService.RetryStrategy;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.FinalChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.InputChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.InputChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.InputResourceCacheChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.InputResourceCacheChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputCacheChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputCacheChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PoisonChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PoisonChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RawInputChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RawInputChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RetryChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RetryChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.SourceOfTruthChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.SourceOfTruthChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.SubJobChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TrafficTuner;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputDataProvider.ServiceBus;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.BlobPayloadRoutingChannelManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Cache;

    [ExcludeFromCodeCoverage]
    public class SolutionInputOutputService : IHostedService
    {
        #region Fields & Properties
        private static readonly ILogger<SolutionInputOutputService> Logger = DataLabLoggerFactory.CreateLogger<SolutionInputOutputService>();

        private static readonly ActivityMonitorFactory SolutionInputOutputServiceStartAsync = new("SolutionInputOutputService.StartAsync");
        private static readonly ActivityMonitorFactory SolutionInputOutputServiceStopAsync = new("SolutionInputOutputService.StopAsync");

        #region Unit Test Mode
        /* For Unit Tests */
        public static bool UnitTestMode { get; private set; }
        public static bool UnitTestBeforeMovingToNextChannelError { get; set; }
        
        public static bool UseTestMemoryWriter { get; private set; }
        public static TestEventWriter[] TestEventHubWriters;
        public static TestEventWriter[] TestSubJobQueueWriters;
        public static TestEventWriter[] TestRetryQueueWriters;
        public static TestEventWriter[] TestPoisonQueueWriters;
        #endregion

        public static bool UseSourceOfTruth { get; set; }
        public static bool PublishOutputToARN { get; set; }
        public static bool DropPoisonMessage { get; set; }


        #region BCDR
        public static string BackupInputChannelActiveConfigString { get; set; }
        public static string InputChannelActiveConfigString { get; set; }
        public static bool StartBackupInputProvidersAtStartup { get; set; }
        public static int DelaySecsForEHLoadBalancing {  get; set; }
        #endregion

        public static CacheChannelCollection CacheChannels { get; private set; }
        public static ARNMessageChannelCollection ARNMessageChannels { get; private set; }

        internal static IServiceProvider ServiceProvider { get; private set; }

        // TODO
        // Change to internal but for now, public is required for unit tests
        public static IConcurrencyManager GlobalConcurrencyManager { get; private set; }
        public static ConfigurableConcurrencyManager RawInputChannelConcurrencyManager { get; private set; }
        public static ConfigurableConcurrencyManager InputChannelConcurrencyManager { get; private set; }
        public static ConfigurableConcurrencyManager InputCacheChannelConcurrencyManager { get; private set; }
        public static ConfigurableConcurrencyManager SourceOfTruthChannelConcurrencyManager { get; private set; }
        public static ConfigurableConcurrencyManager OutputCacheChannelConcurrencyManager { get; private set; }
        // END TODO

        internal static List<IEventhubInputProvider> InputProviders { get; private set; }
        internal static List<IEventhubInputProvider> BackupInputProviders { get; private set; }
        internal static List<IInputProvider> RetryInputProviders { get; private set; }
        internal static List<IInputProvider> SubJobInputProviders { get; private set; }

        internal static List<EventHubWriter> EventHubWriters { get; private set; }
        internal static List<ServiceBusAdminManager> ServiceBusAdminManagers { get; private set; }

        internal static IORetryStrategy RetryStrategy { get; private set; }

        internal static InputProviderTrafficTunerConfiguration InputEventhubsTrafficTuner { get; private set; }
        internal static InputProviderTrafficTunerConfiguration BackupEventhubsTrafficTuner { get; private set; }

        private static IResourceCacheClient ResourceCacheClient { get; set; }
        private static IPartnerBlobClient PartnerBlobClient { get; set; }

        internal static IArnNotificationClient ArnNotificationClient { get; private set; }

        private static object _updateLock = new();

        #endregion

        #region Service initialization code and lifecycle methods

        public SolutionInputOutputService(IHostApplicationLifetime appLifetime)
        {
            appLifetime.ApplicationStarted.Register(OnStarted);
            appLifetime.ApplicationStopping.Register(OnStopping);
            appLifetime.ApplicationStopped.Register(OnStopped);

            // TODO test
            // detect SINGTERM to support graceful shutdown
        }

        private static void InitializeConcurrencyManagers()
        {
            // Global Concurrency
            var maxConcurrency = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(InputOutputConstants.GlobalConcurrency,
                UpdateGlobalConcurrency, 1000);

            GlobalConcurrencyManager = new ConcurrencyManager(InputOutputConstants.GlobalConcurrency, maxConcurrency);
            
            // Input Channel
            RawInputChannelConcurrencyManager =
                new ConfigurableConcurrencyManager(InputOutputConstants.RawInputChannelConcurrency, 50);
            InputChannelConcurrencyManager =
                new ConfigurableConcurrencyManager(InputOutputConstants.InputChannelConcurrency, 60);
            InputCacheChannelConcurrencyManager =
                new ConfigurableConcurrencyManager(InputOutputConstants.InputCacheChannelConcurrency, ConfigurableConcurrencyManager.NO_CONCURRENCY_CONTROL);
            SourceOfTruthChannelConcurrencyManager =
                new ConfigurableConcurrencyManager(InputOutputConstants.SourceOfTruthChannelConcurrency, ConfigurableConcurrencyManager.NO_CONCURRENCY_CONTROL);
            OutputCacheChannelConcurrencyManager =
                new ConfigurableConcurrencyManager(InputOutputConstants.OutputCacheChannelConcurrency, 30);

            // Below channels uses Buffered Writer. So Channel Level concurrent are not necessary
            // EventHub Writer
            // Retry Writer
            // Poison Writer 
            // Drop Writer 
        }

        private static async Task UpdateGlobalConcurrency(int newConcurrency)
        {
            if (GlobalConcurrencyManager == null)
            {
                return;
            }

            await GlobalConcurrencyManager.SetNewMaxConcurrencyAsync(newConcurrency).ConfigureAwait(false);
        }

        private static Task UpdateDropPoisonMessage(bool newVal)
        {
            var oldVal = DropPoisonMessage;

            lock(_updateLock)
            {
                if (oldVal == newVal)
                {
                    return Task.CompletedTask;
                }

                DropPoisonMessage = newVal;

                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    InputOutputConstants.DropPoisonMessage, oldVal, newVal);
            }
            return Task.CompletedTask;
        }

        private static void InitializeChannels()
        {
            // Cache Channels
            CacheChannels = new CacheChannelCollection();

            // Message Channels
            ARNMessageChannels = new ARNMessageChannelCollection();
        }

        private static void DisposeChannels()
        {
            GlobalConcurrencyManager?.Dispose();
            RawInputChannelConcurrencyManager?.Dispose();
            InputChannelConcurrencyManager?.Dispose();
            InputCacheChannelConcurrencyManager?.Dispose();
            SourceOfTruthChannelConcurrencyManager?.Dispose();
            OutputCacheChannelConcurrencyManager?.Dispose();

            ARNMessageChannels?.Dispose();
            CacheChannels?.Dispose();
        }

        public static async Task InitializeServiceAsync(IServiceProvider serviceProvider, bool unitTestMode = false)
        {

            UnitTestMode = unitTestMode;

            if (MonitoringConstants.IsLocalDevelopment)
            {
                UseTestMemoryWriter = ConfigMapUtil.Configuration.GetValue<bool>(SolutionConstants.UseTestMemoryWriter, false);

                if (UseTestMemoryWriter)
                {
                    var numTestMemoryWriter = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.NumTestMemoryWriter, 1);
                    numTestMemoryWriter = numTestMemoryWriter <= 0 ? 1 : numTestMemoryWriter;

                    TestEventHubWriters = new TestEventWriter[numTestMemoryWriter];
                    TestRetryQueueWriters = new TestEventWriter[numTestMemoryWriter];
                    TestPoisonQueueWriters = new TestEventWriter[numTestMemoryWriter];
                    TestSubJobQueueWriters = new TestEventWriter[numTestMemoryWriter];
                    for (int i = 0; i < numTestMemoryWriter; i++)
                    {
                        TestEventHubWriters[i] = new TestEventWriter();
                        TestRetryQueueWriters[i] = new TestEventWriter();
                        TestPoisonQueueWriters[i] = new TestEventWriter();
                        TestSubJobQueueWriters[i] = new TestEventWriter();
                    }
                }
            }

            int initializeServiceTimeout = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.InitializeServiceTimeoutInSec, 5*60); // 5 min
            int randomDelayInSec = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.InitializeServiceRandomDelayInSec, 2); // 2 sec

            // We don't expect neighbor's noise at this time because we do one after one rolling update.
            // However just in case (like uninstall/install) or something else, 
            // To reduce possible neighbor's noise during deployment just in case, Let's put some random Delay here
            Random random = new Random(Guid.NewGuid().GetHashCode());
            int sleepMSec = random.Next(randomDelayInSec * 1000);
            if (sleepMSec > 0) {
                await Task.Delay(sleepMSec).ConfigureAwait(false);
            }

            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.CancelAfter(TimeSpan.FromSeconds(initializeServiceTimeout));
            var cancellationToken = cancellationSource.Token;

            Logger.LogInformation("0. InitializeService has been called.");

            UseSourceOfTruth = ConfigMapUtil.Configuration.GetValue<bool>(InputOutputConstants.UseSourceOfTruth, true);
            DelaySecsForEHLoadBalancing = ConfigMapUtil.Configuration.GetValue<int>(InputOutputConstants.DelaySecsForEHLoadBalancing, 120);

            ServiceProvider = serviceProvider;

            InputProviders = new List<IEventhubInputProvider>();
            BackupInputProviders = new List<IEventhubInputProvider>();
            RetryInputProviders = new List<IInputProvider>();
            SubJobInputProviders = new List<IInputProvider>();

            EventHubWriters = new List<EventHubWriter>();
            ServiceBusAdminManagers = new List<ServiceBusAdminManager>();

            // Initialize RetryStrategyManager
            RetryStrategy = new IORetryStrategy();

            //Traffic tuners 
            InputEventhubsTrafficTuner = new InputProviderTrafficTunerConfiguration(InputOutputConstants.TrafficTunerRuleKey, InputOutputConstants.InputTrafficTunerCounterName, InputOutputConstants.PartnerTrafficTunerRuleKey, InputOutputConstants.PartnerTrafficTunerCounterName);

            BackupEventhubsTrafficTuner = new InputProviderTrafficTunerConfiguration(InputOutputConstants.BackupProviderInputTrafficTunerRuleKey, InputOutputConstants.BackupProviderInputTrafficTunerCounterName, InputOutputConstants.BackupProviderPartnerTrafficTunerRuleKey, InputOutputConstants.BackupProviderPartnerTrafficTunerCounterName);

            //BCDR
            InputChannelActiveConfigString = ConfigMapUtil.Configuration.GetValueWithCallBack<string>(
                InputOutputConstants.InputChannelActive,
                ActivateInputChannel,
                string.Empty, // we do not want to accidentally execute this path on service startup. Only meant to be executed for BCDR scenarios
                true);

            BackupInputChannelActiveConfigString = ConfigMapUtil.Configuration.GetValueWithCallBack<string>(
               InputOutputConstants.BackupInputChannelActive,
               ActivateBackupInputChannel,
               string.Empty, // we do not want to accidentally execute this path on service startup. Only meant to be executed for BCDR scenarios
               true);

            // Initialize CacheClient
            ResourceCacheClient = ServiceProvider.GetService<IResourceCacheClient>();
            GuardHelper.ArgumentNotNull(ResourceCacheClient);

            // Initialize PartnerBlobClient
            PartnerBlobClient = ServiceProvider.GetService<IPartnerBlobClient>();
            GuardHelper.ArgumentNotNull(PartnerBlobClient);

            //Initialize RegionConfigManager 
            RegionConfigManager.Initialize(ConfigMapUtil.Configuration, cancellationToken);

            // Initialize Arn publish client
            ArnNotificationClient = ServiceProvider.GetService<IArnNotificationClient>();
            PublishOutputToARN = ConfigMapUtil.Configuration.GetValue<bool>(InputOutputConstants.PublishOutputToArn, false);

            // Initialize Poison To Drop flag with callback
            DropPoisonMessage = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(InputOutputConstants.DropPoisonMessage, UpdateDropPoisonMessage, false);

            //BCDR
            StartBackupInputProvidersAtStartup = ConfigMapUtil.Configuration.GetValue<bool>(InputOutputConstants.StartBackupInputProvidersAtStartup, false);

            // Initialize Concurrency Managers
            InitializeConcurrencyManagers();

            if (!UnitTestMode && !UseTestMemoryWriter)
            {
                // Add Input Providers
                AddInputProviders();

                // Add Output EventHubs
                if (!PublishOutputToARN)
                {
                    await AddOutputEventHubsAsync(cancellationToken).ConfigureAwait(false);
                }

                // Add Service Bus Queues for retry, poison and subJob
                await CreateServiceBusAdminManagersAsync(cancellationToken).ConfigureAwait(false); ;

                // Add Retry Input Providers
                AddRetryInputProviders();

                // Add SubJob Queue Providers
                AddSubJobInputProviders();
            }

            // Initialize Channels
            InitializeChannels();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("1. StartAsync has been called.");
            using var monitor = SolutionInputOutputServiceStartAsync.ToMonitor();
            monitor.Activity.Properties["InputProvidersCount"] = InputProviders.Count;
            monitor.Activity.Properties["BackupInputProvidersCount"] = BackupInputProviders.Count;
            monitor.Activity.Properties["RetryInputProvidersCount"] = RetryInputProviders.Count;
            monitor.Activity.Properties["SubJobInputProvidersCount"] = SubJobInputProviders.Count;
            monitor.Activity.Properties["ServiceBusAdminManagersCount"] = ServiceBusAdminManagers.Count;
            monitor.Activity.Properties["EventHubWritersCount"] = EventHubWriters.Count;
            
            monitor.OnStart();

            // Start ServiceBusAdminManagers
            foreach (var serviceBusAdminManager in ServiceBusAdminManagers)
            {
                await serviceBusAdminManager.StartAsync(cancellationToken).ConfigureAwait(false);
                Logger.LogInformation("serviceBusAdminManager: {namespace} is started", serviceBusAdminManager.NameSpace);
            }

            // Start eventHubWriters
            foreach (var eventHubWriter in EventHubWriters)
            {
                await eventHubWriter.StartAsync(cancellationToken).ConfigureAwait(false);
                Logger.LogInformation("OutputEventHubWriter: {name} is started", eventHubWriter.EventHubName);
            }

            await InitializeInputProvidersAsync(cancellationToken).ConfigureAwait(false);

            // Start RetryProviders
            foreach (var provider in RetryInputProviders)
            {
                await provider.StartAsync(cancellationToken).ConfigureAwait(false);
                Logger.LogInformation("RetryProviders: {name} is started", provider.Name);
            }

            // Start SubJobProviders
            foreach (var provider in SubJobInputProviders)
            {
                await provider.StartAsync(cancellationToken).ConfigureAwait(false);
                Logger.LogInformation("SubJobProviders: {name} is started", provider.Name);
            }

            monitor.OnCompleted();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("4. StopAsync has been called.");

            using var monitor = SolutionInputOutputServiceStopAsync.ToMonitor();
            monitor.Activity.Properties["InputProvidersCount"] = InputProviders.Count;
            monitor.Activity.Properties["BackupInputProvidersCount"] = BackupInputProviders.Count;
            monitor.Activity.Properties["RetryInputProvidersCount"] = RetryInputProviders.Count;
            monitor.Activity.Properties["SubJobInputProvidersCount"] = SubJobInputProviders.Count;
            monitor.Activity.Properties["ServiceBusAdminManagersCount"] = ServiceBusAdminManagers.Count;
            monitor.Activity.Properties["EventHubWritersCount"] = EventHubWriters.Count;

            ConfigMapUtil.Reset();

            // Stop SubJobProviders
            foreach (var provider in SubJobInputProviders)
            {
                try
                {
                    await provider.StopAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogInformation("SbuJobProviders: {name} is stopped", provider.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "SubJobProviders: {name} failed to StopAsync. {exception}", provider.Name, ex.ToString());
                }
            }

            // Stop RetryProviders
            foreach (var provider in RetryInputProviders)
            {
                try
                {
                    await provider.StopAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogInformation("RetryProviders: {name} is stopped", provider.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "RetryProviders: {name} failed to StopAsync. {exception}", provider.Name, ex.ToString());
                }
            }

            // Stop InputProviders
            await StopInputProviderAsync(cancellationToken).ConfigureAwait(false);

            if(StartBackupInputProvidersAtStartup)
            {
                await StopBackupInputProviderAsync(cancellationToken).ConfigureAwait(false);
            }

            // Stop eventHubWriters
            foreach (var eventHubWriter in EventHubWriters)
            {
                try
                {
                    await eventHubWriter.StopAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogInformation("OutputEventHubWriter: {name} is stopped", eventHubWriter.EventHubName);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "OutputEventHubWriter: {name} failed to StopAsync. {exception}", eventHubWriter.EventHubName, ex.ToString());
                }
            }

            // Stop serviceBusAdminManager
            foreach (var serviceBusAdminManager in ServiceBusAdminManagers)
            {
                try
                {
                    await serviceBusAdminManager.StopAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "ServiceBusAdminManager: {namespaceame} failed to StopAsync. {exception}", serviceBusAdminManager.NameSpace, ex.ToString());
                }
            }

            monitor.OnCompleted();
        }

        private void OnStarted()
        {
            Logger.LogInformation("2. OnStarted has been called.");
        }

        private void OnStopping()
        {
            Logger.LogInformation("3. OnStopping has been called.");
        }

        private void OnStopped()
        {
            Logger.LogInformation("5. OnStopped has been called.");

            Logger.LogInformation("Disposing all Channels");

            DisposeChannels();
            ConfigMapUtil.Configuration.Dispose();
        }

        #endregion

        #region Input

        private async Task InitializeInputProvidersAsync(CancellationToken cancellationToken)
        {
            /*
             * Check if service start is happening while any BCDR action is happening
             * Node restart scenarios can be due to infrastructure issue or auto upgrades on the cluster.
             * On service restart and while the BCDR is happening, restart the BCDR process.
             */

            var defaultStartupForInputChannel = IsDefaultStartupForInputChannel();
            var defaultStartupForBackupChannel = IsDefaultStartupForBackupInputChannel();

            if (defaultStartupForInputChannel)
            {
                await StartInputProviderAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Do not start i.e do nothing , equivalent to stop
                Logger.LogWarning("BCDR scenario: Stopped Input channel on service restart.");
            }

            // default startup scenario
            if (defaultStartupForBackupChannel && defaultStartupForInputChannel)
            {
                if (StartBackupInputProvidersAtStartup) //Cache enabled partners
                {
                    await StartBackupInputProviderAsync(cancellationToken).ConfigureAwait(false);
                }
                // non cache enabled partners - do nothing
            } 
            //  BCDR startup scenario for input channel
            else if(defaultStartupForBackupChannel && !defaultStartupForInputChannel)
            {
                // Do not start i.e do nothing , equivalent to stop
                Logger.LogWarning("BCDR scenario:Stop Backup Input channel on service restart.");
            }
            else
            {
                // cache enabled partners and non cache enabled partners for BCDR scenario startup of backup input channel 
                // start with the secondary consumer group  
                Logger.LogWarning("BCDR scenario:Started Backup Input channel on secondary consumer group on service restart.");
                await StartBackupInputProviderAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        #region InputProviders
        public static async Task StartInputProviderAsync(CancellationToken cancellationToken)
        {
            foreach (var provider in InputProviders)
            {
                try
                {
                    await provider.StartAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogInformation("InputProvider: {name} is started", provider.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogCritical(ex, "InputProvider: {name} failed to start. {exception}", provider.Name, ex.ToString());
                    throw;
                }
            }
        }

        public static async Task StopInputProviderAsync(CancellationToken cancellationToken)
        {
            foreach (var provider in InputProviders)
            {
                try
                {
                    await provider.StopAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogInformation("InputProvider: {name} is stopped", provider.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "InputProvider: {name} failed to StopAsync. {exception}", provider.Name, ex.ToString());
                }
            }
        }

        public static async Task<bool> DeleteInputProviderCheckpointsAsync(CancellationToken cancellationToken)
        {
            Logger.LogWarning("DeleteInputProviderCheckpointsAsync is started as a result of config update");

            var returnResult = true;
            foreach (var provider in InputProviders)
            {
                try
                {
                    var result = await provider.DeleteCheckpointsAsync(InputOutputConstants.DefaultConsumerGroupName,cancellationToken).ConfigureAwait(false);
                    if(!result)
                    {
                        returnResult = false;
                    }
                    Logger.LogWarning("InputProviders: {name} has checkpoints deleted", provider.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogCritical(ex, "InputProviders: {name} failed to delete checkpoints - {exception}", provider.Name, ex.ToString());
                    throw;
                }
            }
            return returnResult;
        }

        #endregion

        #region BackupInputProviders
        public static async Task StartBackupInputProviderAsync(CancellationToken cancellationToken)
        {
            // Start BackupInputProviders
            foreach (var provider in BackupInputProviders)
            {
                try
                {
                    await provider.StartAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogWarning("BackupInputProviders: {name} is started", provider.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogCritical(ex, "BackupInputProviders: {name} failed to start. {exception}", provider.Name, ex.ToString());
                    throw;
                }
            }
        }

        public static async Task StopBackupInputProviderAsync(CancellationToken cancellationToken)
        {
            // Stop BackupInputProviders
            foreach (var provider in BackupInputProviders)
            {
                try
                {
                    await provider.StopAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogWarning("BackupInputProviders: {name} is stopped", provider.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogCritical(ex, "BackupInputProviders: {name} failed to stop. {exception}", provider.Name, ex.ToString());
                    throw;
                }
            }
        }

        public static async Task<bool> DeleteBackupInputProviderCheckpointsAsync(bool isDefaultConsumerGroup, CancellationToken cancellationToken)
        {
            Logger.LogWarning("DeleteBackupInputProviderCheckpointsAsync is started as a result of config update");

            var returnResult = true;

            // Stop BackupInputProviders
            foreach (var provider in BackupInputProviders)
            {
                try
                {
                    string consumerGroupName = isDefaultConsumerGroup ? InputOutputConstants.DefaultConsumerGroupName : InputOutputConstants.SecondaryConsumerGroupName;
                    var result = await provider.DeleteCheckpointsAsync(consumerGroupName, cancellationToken).ConfigureAwait(false);
                    if (!result)
                    {
                        returnResult = false;
                    }
                    Logger.LogWarning("BackupInputProviders: {name} has checkpoints deleted", provider.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogCritical(ex, "BackupInputProviders: {name} failed to delete checkpoints - {exception}", provider.Name, ex.ToString());
                    throw;
                }
            }
            return returnResult;
        }

        #endregion

        #endregion

        #region BCDR methods

        #region InputChannel
        /*
         * InputProviders processing need to start or stop during BCDR, this method helps in the same.
         * Default value is empty
         * Change to InputChannelActiveConfigString calls ActivateInputChannel
         * To stop Input Channel in case of disaster InputChannelActiveConfigString should be false, offset values are disregarded
         * To start Input Channel in case of disaster InputChannelActiveConfigString should be true, offset values, if any, are respected.
         * Method accepts offset values, in case of no value provided,starts with the latest timestamp.
         */
        private static async Task ActivateInputChannel(string newActivateValue)
        {
            try
            {
                string pathwayName = "Input Channel";
                if(ShouldReturn(pathwayName, InputChannelActiveConfigString, newActivateValue))
                {
                    return;
                }

                InputChannelActiveConfigString = newActivateValue;

                await EvaluateInputChannelConfigAndPerformActions(newActivateValue).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "BCDR scenario: Input Channel failed to start or stop on time with error - {exception}", ex.ToString());
            }
        }

        private static async Task EvaluateInputChannelConfigAndPerformActions(string newActivateValue)
        {
            var (inputChannelActive, initialOffsetOverride) = ParseConfigs(newActivateValue);

            Logger.LogWarning($"Input channel activated with config update - {inputChannelActive}, {initialOffsetOverride.GetValueOrDefault().TotalSeconds} seconds");

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10)); // timeout can be updated based on actual numbers
            var cancellationToken = cancellationTokenSource.Token;

            var inputTrafficTunerRule = ConfigMapUtil.Configuration.GetValue<string>(InputOutputConstants.TrafficTunerRuleKey);
            var partnerTrafficTunerRule = ConfigMapUtil.Configuration.GetValue<string>(InputOutputConstants.PartnerTrafficTunerRuleKey);
            var backupInputTrafficTunerRule = ConfigMapUtil.Configuration.GetValue<string>(InputOutputConstants.BackupProviderInputTrafficTunerRuleKey);

            if (inputChannelActive)
            {
                //Summary: restart processing of InputProviders and if started, BackupInputProviders
                await InputChannelIsRestarted(inputTrafficTunerRule, partnerTrafficTunerRule, backupInputTrafficTunerRule, initialOffsetOverride, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Summary: stop processing of InputProviders and if started, BackupInputProviders. This part can fail depending on the extent of the outage
                await InputChannelIsStopped(inputTrafficTunerRule, partnerTrafficTunerRule, backupInputTrafficTunerRule, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task DeleteInputEventhubConsumerGroupCheckpointsInBackground()
        {
            try
            {
                await Task.Delay(CalculateDelayInMs());
                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                var cancellationToken = cancellationTokenSource.Token;
                await DeleteInputProviderCheckpointsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.LogCritical($"DeleteInputEventhubConsumerGroupCheckpointsInBackground had exception {e}");
                // do not throw as this is called via bg thread
            }
        }

        private static async Task InputChannelIsRestarted(string inputTrafficTunerRule, string partnerTrafficTunerRule, string backupInputTrafficTunerRule, TimeSpan? initialOffsetOverride, CancellationToken cancellationToken)
        {
            //1. delete checkpoints of default consumer group to make sure offset is respected
            await DeleteInputProviderCheckpointsAsync(cancellationToken).ConfigureAwait(false);
            if (StartBackupInputProvidersAtStartup)
            {
                await DeleteBackupInputProviderCheckpointsAsync(isDefaultConsumerGroup: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            // 2. artificial wait time to make sure concurrent deletes do not happen i.e checkpoints created by one node should not be deleted by another.
            //    Also to make sure the load balancing cycle does not throw errors at this point
            Logger.LogWarning("Start wait time to give time between checkpoint delete  and start eventhubs");
            await Task.Delay(CalculateDelayInMs(), cancellationToken);
            Logger.LogWarning("Stop wait time to give time between checkpoint delete and start eventhubs");

            //3. clear and add the input eventhubs provider array, backup eventhub provider array(if needed)
            //4. update the traffic tuner values
            //      A.cache NOT ENABLED and BackupInputProviders are NOT STARTED - input eventhub providers :InputTrafficTuner - get updated
            //      B.cache ENABLED and BackupInputProviders are STARTED - input eventhub providers : InputTrafficTuner,PartnerTrafficTuner AND backup eventhub providers : InputTrafficTuner - get updated
            InputProviders.Clear();
            EventHubInputProvider.AddEventHubInputProviders(initialOffsetOverride);
            InputEventhubsTrafficTuner.InputTrafficTuner.UpdateTrafficTunerRuleValue(inputTrafficTunerRule);

            if (StartBackupInputProvidersAtStartup)
            {
                //Due to pending cache refill code work item, updating the InputEventhubsTrafficTuner.PartnerTrafficTuner has
                //to be done as a separate hot config update 
                //after manual evaluation to put the region back 

                BackupInputProviders.Clear();
                EventHubInputProvider.AddBackupEventHubInputProviders(initialOffsetOverride, InputOutputConstants.DefaultConsumerGroupName);

                InputEventhubsTrafficTuner.PartnerTrafficTuner.UpdateTrafficTunerRuleValue(partnerTrafficTunerRule); // not expecting InputEventhubsTrafficTuner.PartnerTrafficTuner to get updated for BCDR when partner does not have cache enabled
                BackupEventhubsTrafficTuner.InputTrafficTuner.UpdateTrafficTunerRuleValue(backupInputTrafficTunerRule);
            }

            //5. start read from eventhub providers
            await StartInputProviderAsync(cancellationToken).ConfigureAwait(false);
            if (StartBackupInputProvidersAtStartup)
            {
                await StartBackupInputProviderAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task InputChannelIsStopped(string inputTrafficTunerRule, string partnerTrafficTunerRule, string backupInputTrafficTunerRule, CancellationToken cancellationToken)
        {
            // 1. Update traffic tuner value 
            //      A.cache NOT ENABLED and BackupInputProviders are NOT STARTED - input eventhub providers :InputTrafficTuner - get updated
            //      B.cache ENABLED and BackupInputProviders are STARTED - input eventhub providers : InputTrafficTuner,PartnerTrafficTuner AND backup eventhub providers : InputTrafficTuner - get updated
            InputEventhubsTrafficTuner.InputTrafficTuner.UpdateTrafficTunerRuleValue(inputTrafficTunerRule);

            if (StartBackupInputProvidersAtStartup)
            {
                InputEventhubsTrafficTuner.PartnerTrafficTuner.UpdateTrafficTunerRuleValue(partnerTrafficTunerRule); // not expecting partner traffic tuner to get updated for BCDR when partner does not have cache enabled
                BackupEventhubsTrafficTuner.InputTrafficTuner.UpdateTrafficTunerRuleValue(backupInputTrafficTunerRule);
            }

            // 2. Stop read from InputProviders 
            await StopInputProviderAsync(cancellationToken).ConfigureAwait(false);
            if (StartBackupInputProvidersAtStartup)
            {
                await StopBackupInputProviderAsync(cancellationToken).ConfigureAwait(false);
            }

            // 3. Delete checkpoints of default consumer group to make sure offset is respected for restart in background
            // discard result of background task, we don't care about result of this delete
            _ = Task.Run(async () => {
                    await DeleteInputEventhubConsumerGroupCheckpointsInBackground().ConfigureAwait(false);
                    if (StartBackupInputProvidersAtStartup)
                    {
                        await DeleteBackupEventhubConsumerGroupCheckpointsInBackground(isDefaultConsumerGroup: true).ConfigureAwait(false);
                    }
                }
            );
        }
        #endregion

        #region BackupChannel
        /*
        *  
        *  Change to BackupInputChannelActiveConfigString calls ActivateBackupInputChannel
         * To stop backup input Channel processing to store in case of disaster BackupInputChannelActiveConfigString should be false, offset values are respected
         * To start backup input Channel processing to store in case of disaster BackupInputChannelActiveConfigString should be true, offset values are respected.
         * Method accepts offset values, in case of no value provided,starts with the latest timestamp.
        */
        private static async Task ActivateBackupInputChannel(string newActivateValue)
        {
            try
            {
                string pathwayName = "Backup Input Channel";
                if (ShouldReturn(pathwayName, BackupInputChannelActiveConfigString, newActivateValue))
                {
                    return;
                }

                BackupInputChannelActiveConfigString = newActivateValue;

                await EvaluateBackupInputChannelConfigAndPerformActions(newActivateValue).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "BCDR scenario: Backup Input Channel failed to start or stop on time with error - {exception}", ex.ToString());
            }
        }

        private static async Task EvaluateBackupInputChannelConfigAndPerformActions(string newActivateValue)
        {
            var (backupInputChannelActive, initialOffsetOverride) = ParseConfigs(newActivateValue);

            Logger.LogWarning($"ActivateBackupInputChannel with config update - {backupInputChannelActive}, {initialOffsetOverride.GetValueOrDefault().TotalSeconds} seconds");

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10)); // timeout can be updated based on actual numbers
            var cancellationToken = cancellationTokenSource.Token;

            // CASE A: cache ENABLED and BackupInputProviders are STARTED
            // Backup channel is restarted to fill cache and START processing traffic
            if (StartBackupInputProvidersAtStartup && backupInputChannelActive)
            {
                // Summary: Stop read from default consumer group, update partner traffic tuner value, start read from secondary consumer group with offset override

                string backupProviderTrafficTunerRule = ConfigMapUtil.Configuration.GetValue<string>(InputOutputConstants.BackupProviderPartnerTrafficTunerRuleKey);

                await PerformBackupInputChannelActivationActions(
                    startReadFromDefaultConsumerGroup: false,
                    initialOffsetOverride: initialOffsetOverride,
                    partnerTrafficTunerValue: backupProviderTrafficTunerRule,
                    cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            // CASE A: cache ENABLED and BackupInputProviders are STARTED
            // Backup channel is restarted to fill cache and STOP processing traffic 
            else if (StartBackupInputProvidersAtStartup && !backupInputChannelActive)
            {
                //Summary: stop read from secondary consumer group, update partner traffic tuner value, start read from default consumer group with offset override

                string backupProviderTrafficTunerRule = ConfigMapUtil.Configuration.GetValue<string>(InputOutputConstants.BackupProviderPartnerTrafficTunerRuleKey);

                await PerformBackupInputChannelActivationActions(
                    startReadFromDefaultConsumerGroup: true,
                    initialOffsetOverride: initialOffsetOverride,
                    partnerTrafficTunerValue: backupProviderTrafficTunerRule,
                    cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            // CASE B: cache NOT ENABLED and BackupInputProviders are NOT STARTED
            // Backup channel is started
            else if (!StartBackupInputProvidersAtStartup && backupInputChannelActive)
            {
                // Summary: update Input traffic tuner value, Start processing traffic from secondary consumer group with offset override

                string backupProviderTrafficTunerRule = ConfigMapUtil.Configuration.GetValue<string>(InputOutputConstants.BackupProviderInputTrafficTunerRuleKey);

                await BackupChannelIsStartedForSecondaryConsumerGroup(
                    inputTrafficTunerRuleValue: backupProviderTrafficTunerRule,
                    initialOffsetOverride: initialOffsetOverride,
                    cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            // CASE B: cache NOT ENABLED and BackupInputProviders are NOT STARTED
            // Backup channel is stopped
            else
            {
                // Summary: update Input traffic tuner value, stop processing traffic from secondary consumer group

                string backupProviderTrafficTunerRule = ConfigMapUtil.Configuration.GetValue<string>(InputOutputConstants.BackupProviderInputTrafficTunerRuleKey);

                await BackupChannelIsStoppedForSecondaryConsumerGroup(
                    inputTrafficTunerRuleValue: backupProviderTrafficTunerRule,
                    cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }

        private static async Task PerformBackupInputChannelActivationActions(bool startReadFromDefaultConsumerGroup, TimeSpan? initialOffsetOverride, string partnerTrafficTunerValue, CancellationToken cancellationToken)
        {
            // 1. stop the backup input provider
            await StopBackupInputProviderAsync(cancellationToken).ConfigureAwait(false);

            // 1.5. artificial wait time of 30 seconds to make sure every node stops, other nodes cannot stop properly if checkpoints are deleted and some nodes are still stopping.
            // it is not a consistent repro , so this is added as a prevention measure
            Logger.LogWarning("Start wait time to give time between stop and checkpoint delete");
            await Task.Delay(CalculateDelayInMs(), cancellationToken);
            Logger.LogWarning("Stop wait time to give time between stop and checkpoint delete");

            // 2. to double check, call delete checkpoints again on appropriate consumer group
            await DeleteBackupInputProviderCheckpointsAsync(isDefaultConsumerGroup: startReadFromDefaultConsumerGroup, cancellationToken: cancellationToken).ConfigureAwait(false);

            // 3. clear and readd the backup input providers to respect the initialoffset
            BackupInputProviders.Clear();
            var consumerGroupName = startReadFromDefaultConsumerGroup ? InputOutputConstants.DefaultConsumerGroupName : InputOutputConstants.SecondaryConsumerGroupName;
            EventHubInputProvider.AddBackupEventHubInputProviders(initialOffsetOverride, consumerGroupName);

            // 4. artificial wait time to make sure concurrent deletes do not happen i.e checkpoints created by one node should not be deleted by another.
            //      Also to make sure the load balancing cycle does not throw errors at this point
            Logger.LogWarning("Start wait time to give time between checkpoint delete  and start eventhubs");
            await Task.Delay(CalculateDelayInMs(), cancellationToken);
            Logger.LogWarning("Stop wait time to give time between checkpoint delete and start eventhubs");

            //5. PartnerTrafficTuner rule update on backup eventhubs
            BackupEventhubsTrafficTuner.PartnerTrafficTuner.UpdateTrafficTunerRuleValue(partnerTrafficTunerValue);

            // 6.restart backup input providers
            await StartBackupInputProviderAsync(cancellationToken).ConfigureAwait(false);

            // 7. delete checkpoints  on appropriate consumer group
            // discard result of background task, we don't care about result of this delete
            _ = Task.Run(async () => await DeleteBackupEventhubConsumerGroupCheckpointsInBackground(!startReadFromDefaultConsumerGroup).ConfigureAwait(false));
        }

        private static async Task BackupChannelIsStartedForSecondaryConsumerGroup(string inputTrafficTunerRuleValue, TimeSpan? initialOffsetOverride, CancellationToken cancellationToken)
        {
            // 1. to double check, call delete checkpoints again on secondary consumer group
            await DeleteBackupInputProviderCheckpointsAsync(isDefaultConsumerGroup: false, cancellationToken: cancellationToken).ConfigureAwait(false);

            // 2. clear and readd the backup input providers to respect the initialoffset
            BackupInputProviders.Clear();
            EventHubInputProvider.AddBackupEventHubInputProviders(initialOffsetOverride, InputOutputConstants.SecondaryConsumerGroupName);

            // 3. artificial wait time to make sure concurrent deletes do not happen i.e checkpoints created by one node should not be deleted by another.
            //      Also to make sure the load balancing cycle does not throw errors at this point
            Logger.LogWarning("Start wait time to give time between checkpoint delete  and start eventhubs");
            await Task.Delay(CalculateDelayInMs(), cancellationToken);
            Logger.LogWarning("Stop wait time to give time between checkpoint delete and start eventhubs");

            // 4. Update InputTrafficTuner value
            BackupEventhubsTrafficTuner.InputTrafficTuner.UpdateTrafficTunerRuleValue(inputTrafficTunerRuleValue);

            // 5. start backup input providers
            await StartBackupInputProviderAsync(cancellationToken).ConfigureAwait (false);
        }

        private static async Task BackupChannelIsStoppedForSecondaryConsumerGroup(string inputTrafficTunerRuleValue, CancellationToken cancellationToken)
        {
            // 1. InputTrafficTuner value is updated
            BackupEventhubsTrafficTuner.InputTrafficTuner.UpdateTrafficTunerRuleValue(inputTrafficTunerRuleValue);
            // 2. Stop backup input providers
            await StopBackupInputProviderAsync(cancellationToken).ConfigureAwait(false);

            // 7. delete checkpoints  on secondary consumer group
            // discard result of background task, we don't care about result of this delete
            _ = Task.Run(async () => await DeleteBackupEventhubConsumerGroupCheckpointsInBackground(isDefaultConsumerGroup:false).ConfigureAwait(false));

        }

        private static async Task DeleteBackupEventhubConsumerGroupCheckpointsInBackground(bool isDefaultConsumerGroup)
        {
            try
            {
                await Task.Delay(CalculateDelayInMs());
                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                var cancellationToken = cancellationTokenSource.Token;
                await DeleteBackupInputProviderCheckpointsAsync(isDefaultConsumerGroup: isDefaultConsumerGroup, cancellationToken: cancellationToken).ConfigureAwait(false);
            } catch(Exception e)
            {
                Logger.LogCritical($"DeleteBackupEventhubConsumerGroupCheckpointsInBackground had exception {e}");
                // do not throw as this is called via bg thread
            }
        }

        #endregion

        #region Helper methods

        private static bool ShouldReturn(string pathwayName, string oldActivateValue, string newActivateValue)
        {
            if (string.IsNullOrWhiteSpace(newActivateValue))
            {
                Logger.LogWarning($"{pathwayName} received empty string, returning with no action");
                return true;
            }

            Logger.LogWarning($"{pathwayName} received the signal to activate - {newActivateValue}");

            if (oldActivateValue == newActivateValue)
            {
                Logger.LogWarning($"{pathwayName} is already in state - {newActivateValue}, returning without doing any action");
                return true;
            }

            return false;
        }

        private static (bool activateValue,TimeSpan? offsetOverride) ParseConfigs(string newActivateValue)
        {
            var configs = newActivateValue.Split('/');
            var boolActivateValue = bool.Parse(configs[0]);
            TimeSpan? initialOffsetOverride = null;

            if (configs.Length > 1)
            {
                var startTimeSpan = TimeSpan.Parse(configs[1]);
                if (startTimeSpan.TotalSeconds > 0)
                {
                    initialOffsetOverride = startTimeSpan;
                }
            }

            return (boolActivateValue, initialOffsetOverride);
        }

        private static int CalculateDelayInMs()
        {
           return (DelaySecsForEHLoadBalancing + ThreadSafeRandom.Next(1, 15)) * 1000;
        }


        private static bool IsDefaultStartupForBackupInputChannel()
        {
            var defaultStartup = string.IsNullOrWhiteSpace(BackupInputChannelActiveConfigString) ?
               true : //For the backupInputChannelActive - default value "" , it is equal to normal startup.
               ParseConfigs(BackupInputChannelActiveConfigString).activateValue ?
               false : //For the backupInputChannelActive - true indicates BCDR action is applied.
               true;  //For the backupInputChannelActive -  false value, it is equal to normal startup.

            return defaultStartup;
        }

        private static bool IsDefaultStartupForInputChannel()
        {
            var defaultStartup = string.IsNullOrWhiteSpace(InputChannelActiveConfigString) ?
              true :   //For the inputChannelActive, - default value "" , it is equal to normal startup.
              ParseConfigs(InputChannelActiveConfigString).activateValue ?
              true : //For the inputChannelActive, - true value, it is equal to normal startup.
              false; //For the inputChannelActive - false indicates BCDR action is applied.

            return defaultStartup;
        }

        #endregion

        #endregion

        #region Create methods
        private static void AddInputProviders()
        {
            AddEventHubProviders();
        }

        private static void AddEventHubProviders()
        {
            EventHubInputProvider.AddEventHubInputProviders();

            //Consider BCDR scenario during service start, BCDR scenarios process from secondary consumer group.
            var consumerGroupName = IsDefaultStartupForBackupInputChannel() ? InputOutputConstants.DefaultConsumerGroupName : InputOutputConstants.SecondaryConsumerGroupName;
            Logger.LogWarning($"Choosing {consumerGroupName} for AddBackupEventHubInputProviders at startup");
            EventHubInputProvider.AddBackupEventHubInputProviders(consumerGroupOverride: consumerGroupName);
        }
       
        private static void AddRetryInputProviders()
        {
            ServiceBusQueueInputProvider.AddRetryQueueInputProviders();
        }

        private static void AddSubJobInputProviders()
        {
            ServiceBusQueueInputProvider.AddSubJobQueueInputProviders();
        }

        private static async Task<EventHubWriter> CreatEventHubWriterAsync(
            string outputEventHubNameSpaceAndName, 
            string outputEHConnectionString, 
            CancellationToken cancellationToken)
        {
            var names = outputEventHubNameSpaceAndName.Split('/');
            var outputEventHubNameSpace = names[0];
            var outputEHName = names[1];
            var fullyQualifiedOutputNamespace = $"{outputEventHubNameSpace}.servicebus.windows.net";

            EventHubWriter eventHubWriter;
            if (outputEHConnectionString != null)
            {
                eventHubWriter = new EventHubWriter(
                    outputEHConnectionString,
                    outputEHName);
            }
            else
            {
                eventHubWriter = new EventHubWriter(
                    fullyQualifiedOutputNamespace,
                    outputEHName,
                    new DefaultAzureCredential());
            }
            
            await eventHubWriter.ValidateConnectionAsync(cancellationToken).ConfigureAwait(false);
            return eventHubWriter;
        }

        private static async Task AddOutputEventHubsAsync(CancellationToken cancellationToken)
        {
            // TODO
            // For production, we will not use eventHubOutput.
            // Instead. we will have to send through EventGrid

            // make hotconfig for below config
            // So that we can remove / add eventHub through hotconfig
            var configuration = ConfigMapUtil.Configuration;

            var outputEventHubNameSpaceAndNames = configuration
                .GetValue<string>(InputOutputConstants.OutputEventHubNameSpaceAndName)
                .ConvertToSet(false);

            var outputEHConnectionString = configuration.GetValue<string>(InputOutputConstants.OutputEventHubConnectionString);
            if (outputEHConnectionString != null && outputEventHubNameSpaceAndNames.Count > 1)
            {
                // Using connection string is only for testing
                throw new InvalidOperationException("OutputEHConnectionString can only be used with one output EventHub");
            }

            foreach (var outputEventHubNameSpaceAndName in outputEventHubNameSpaceAndNames)
            {
                var eventHubWriter = await CreatEventHubWriterAsync(outputEventHubNameSpaceAndName, outputEHConnectionString, cancellationToken).ConfigureAwait(false);
                EventHubWriters.Add(eventHubWriter);
            }
        }

        private static async Task<ServiceBusAdminManager> CreateServiceBusAdminManagerAsync(
            string serviceBusNameSpaceAndName, 
            string subJobQueueName,
            CancellationToken cancellationToken,
            string serviceBusQueueConnectionString = null)
        {
            var names = serviceBusNameSpaceAndName.Split('/');
            var serviceBusNameSpace = names[0];
            var serviceBusRetryQueueName = names[1];
            var fullyQualifiedNamespace = $"{serviceBusNameSpace}.servicebus.windows.net";

            // Create ServiceBusClientWrapper
            ServiceBusAdminManager serviceBusAdminManager;
            if (serviceBusQueueConnectionString != null)
            {
                serviceBusAdminManager = new ServiceBusAdminManager(serviceBusQueueConnectionString);
            }
            else
            {
                serviceBusAdminManager = new ServiceBusAdminManager(fullyQualifiedNamespace, new DefaultAzureCredential());
            }

            // Create SubJobQueue
            await serviceBusAdminManager.CreateSubJobQueueWriterAsync(subJobQueueName, cancellationToken).ConfigureAwait(false);

            // Create Retry Queue
            await serviceBusAdminManager.CreateRetryQueueWriter(serviceBusRetryQueueName, cancellationToken).ConfigureAwait(false);

            return serviceBusAdminManager;
        }

        private static async Task CreateServiceBusAdminManagersAsync(CancellationToken cancellationToken)
        {
            var configuration = ConfigMapUtil.Configuration;

            var subJobQueueName = configuration.GetValue<string>(InputOutputConstants.ServiceBusSubJobQueueName);
            GuardHelper.ArgumentNotNullOrEmpty(subJobQueueName, InputOutputConstants.ServiceBusSubJobQueueName);

            // TODO: make hotconfig for below config
            // So that we can remove / add servicebus through hotconfig
            var serviceBusNameSpaceAndNames = configuration
                .GetValue<string>(InputOutputConstants.ServiceBusNameSpaceAndName)
                .ConvertToSet(false);

            var serviceBusQueueConnectionString = configuration.GetValue<string>(InputOutputConstants.ServiceBusQueueConnectionString);
            if (serviceBusQueueConnectionString != null && serviceBusNameSpaceAndNames.Count > 1)
            {
                // Using connection string is only for testing
                throw new InvalidOperationException("ServiceBusQueueConnectionString can only be used with one ServiceBus");
            }

            foreach (var serviceBusNameSpaceAndName in serviceBusNameSpaceAndNames)
            {
                var serviceBusAdminManager = await CreateServiceBusAdminManagerAsync(
                    serviceBusNameSpaceAndName: serviceBusNameSpaceAndName, 
                    subJobQueueName: subJobQueueName,
                    cancellationToken: cancellationToken,
                    serviceBusQueueConnectionString: serviceBusQueueConnectionString).ConfigureAwait(false);
                ServiceBusAdminManagers.Add(serviceBusAdminManager);
            }
        }

        #endregion

        #region NextChannel methods
        // CacheChannels
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetNextChannelToInputCacheChannel(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            eventTaskContext.SetNextChannel(CacheChannels.InputCacheChannelManager);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask SetNextChannelToPartnerChannelAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            return ARNMessageChannels.PartnerChannelRoutingManager.SetNextChannelAsync(eventTaskContext);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetNextChannelToSourceOfTruthChannel(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            if (UseSourceOfTruth)
            {
                eventTaskContext.SetNextChannel(ARNMessageChannels.SourceOfTruthChannelManager);
            }else
            {
                SetNextChannelToOutputChannel(eventTaskContext);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetNextChannelToSubJobChannel(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            eventTaskContext.SetNextChannel(ARNMessageChannels.SubJobChannelManager);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetNextChannelToOutputChannel(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            eventTaskContext.SetNextChannel(ARNMessageChannels.OutputChannelManager);
        }

        #endregion

        #region internal classes
        public class CacheChannelCollection : IDisposable
        {
            public IInputCacheChannelManager<ARNSingleInputMessage> InputCacheChannelManager;
            public IOutputCacheChannelManager OutputCacheChannelManager;

            public CacheChannelCollection()
            {
                InputCacheChannelManager = ServiceProvider.GetService<IInputCacheChannelManager<ARNSingleInputMessage>>();
                GuardHelper.ArgumentNotNull(InputCacheChannelManager);

                OutputCacheChannelManager = ServiceProvider.GetService<IOutputCacheChannelManager>();
                GuardHelper.ArgumentNotNull(OutputCacheChannelManager);

                AddSubTasks();
                AddConcurrencyManager();
            }

            private void AddSubTasks()
            {
                // Input Cache
                InputCacheChannelManager.AddSubTaskFactory(new InputCacheTaskFactory(ResourceCacheClient));
                // OutputCache
                OutputCacheChannelManager.AddSubTaskFactory(new OutputCacheTaskFactory(ResourceCacheClient));
            }

            private void AddConcurrencyManager()
            {
                // Input Channel
                InputCacheChannelConcurrencyManager.RegisterObject(InputCacheChannelManager.SetExternalConcurrencyManager);
                OutputCacheChannelConcurrencyManager.RegisterObject(OutputCacheChannelManager.SetExternalConcurrencyManager);
            }

            public void Dispose()
            {
                InputCacheChannelManager?.Dispose();
                OutputCacheChannelManager?.Dispose();
            }
        }

        public class ARNMessageChannelCollection : IDisposable
        {
            public IInputChannelManager<ARNSingleInputMessage> InputChannelManager { get; }
            public IPartnerChannelRoutingManager PartnerChannelRoutingManager { get; }
            public ISourceOfTruthChannelManager<ARNSingleInputMessage> SourceOfTruthChannelManager { get; }
            public ISubJobChannelManager<ARNSingleInputMessage> SubJobChannelManager { get; }
            public IOutputChannelManager<ARNSingleInputMessage> OutputChannelManager { get; }
            public IBlobPayloadRoutingChannelManager<ARNSingleInputMessage> BlobPayloadRoutingChannelManager { get; }
            public IRetryChannelManager<ARNSingleInputMessage> RetryChannelManager { get; }
            public IPoisonChannelManager<ARNSingleInputMessage> PoisonChannelManager { get; }
            public IFinalChannelManager<ARNSingleInputMessage> FinalChannelManager { get; }

            public IRawInputChannelManager<ARNRawInputMessage> RawInputChannelManager { get; }
            public IRetryChannelManager<ARNRawInputMessage> RawInputRetryChannelManager { get; }
            public IPoisonChannelManager<ARNRawInputMessage> RawInputPoisonChannelManager { get; }
            public IFinalChannelManager<ARNRawInputMessage> RawInputFinalChannelManager { get; }

            public ARNMessageChannelCollection()
            {
                InputChannelManager = ServiceProvider.GetService<IInputChannelManager<ARNSingleInputMessage>>();
                GuardHelper.ArgumentNotNull(InputChannelManager, SolutionUtils.GetTypeName(typeof(IInputChannelManager<ARNSingleInputMessage>)));

                PartnerChannelRoutingManager = ServiceProvider.GetService<IPartnerChannelRoutingManager>();
                GuardHelper.ArgumentNotNull(PartnerChannelRoutingManager, SolutionUtils.GetTypeName(typeof(IPartnerChannelRoutingManager)));

                SourceOfTruthChannelManager = ServiceProvider.GetService<ISourceOfTruthChannelManager<ARNSingleInputMessage>>();
                GuardHelper.ArgumentNotNull(SourceOfTruthChannelManager, SolutionUtils.GetTypeName(typeof(ISourceOfTruthChannelManager<ARNSingleInputMessage>)));

                SubJobChannelManager = ServiceProvider.GetService<ISubJobChannelManager<ARNSingleInputMessage>>();
                GuardHelper.ArgumentNotNull(SubJobChannelManager, SolutionUtils.GetTypeName(typeof(ISubJobChannelManager<ARNSingleInputMessage>)));

                OutputChannelManager = ServiceProvider.GetService<IOutputChannelManager<ARNSingleInputMessage>>();
                GuardHelper.ArgumentNotNull(OutputChannelManager, SolutionUtils.GetTypeName(typeof(IOutputChannelManager<ARNSingleInputMessage>)));

                BlobPayloadRoutingChannelManager = ServiceProvider.GetService<IBlobPayloadRoutingChannelManager<ARNSingleInputMessage>>();
                GuardHelper.ArgumentNotNull(BlobPayloadRoutingChannelManager, SolutionUtils.GetTypeName(typeof(IBlobPayloadRoutingChannelManager<ARNSingleInputMessage>)));

                RetryChannelManager = ServiceProvider.GetService<IRetryChannelManager<ARNSingleInputMessage>>();
                GuardHelper.ArgumentNotNull(RetryChannelManager, SolutionUtils.GetTypeName(typeof(IRetryChannelManager<ARNSingleInputMessage>)));

                PoisonChannelManager = ServiceProvider.GetService<IPoisonChannelManager<ARNSingleInputMessage>>();
                GuardHelper.ArgumentNotNull(PoisonChannelManager, SolutionUtils.GetTypeName(typeof(IPoisonChannelManager<ARNSingleInputMessage>)));

                FinalChannelManager = ServiceProvider.GetService<IFinalChannelManager<ARNSingleInputMessage>>();
                GuardHelper.ArgumentNotNull(FinalChannelManager, SolutionUtils.GetTypeName(typeof(IFinalChannelManager<ARNSingleInputMessage>)));

                /* For ARN RwaInputMessage */
                RawInputChannelManager = ServiceProvider.GetService<IRawInputChannelManager<ARNRawInputMessage>>();
                GuardHelper.ArgumentNotNull(RawInputChannelManager, SolutionUtils.GetTypeName(typeof(IRawInputChannelManager<ARNRawInputMessage>)));

                RawInputRetryChannelManager = ServiceProvider.GetService<IRetryChannelManager<ARNRawInputMessage>>();
                GuardHelper.ArgumentNotNull(RawInputRetryChannelManager, SolutionUtils.GetTypeName(typeof(IRetryChannelManager<ARNRawInputMessage>)));

                RawInputPoisonChannelManager = ServiceProvider.GetService<IPoisonChannelManager<ARNRawInputMessage>>();
                GuardHelper.ArgumentNotNull(RawInputPoisonChannelManager, SolutionUtils.GetTypeName(typeof(IPoisonChannelManager<ARNRawInputMessage>)));

                RawInputFinalChannelManager = ServiceProvider.GetService<IFinalChannelManager<ARNRawInputMessage>>();
                GuardHelper.ArgumentNotNull(RawInputFinalChannelManager, SolutionUtils.GetTypeName(typeof(IFinalChannelManager<ARNRawInputMessage>)));

                AddSubTasks();
                AddConcurrencyManager();
            }

            private void AddSubTasks()
            {
                // RawInputMessage
                RawInputChannelManager.AddSubTaskFactory(new PayloadDisassemblyTaskFactory(PartnerBlobClient));

                // Input
                InputChannelManager.AddSubTaskFactory(new InputResourceCheckTaskFactory());

                // SourceOfTruth
                if (UseSourceOfTruth)
                {
                    SourceOfTruthChannelManager.AddSubTaskFactory(new OutputBlobUploadTaskFactory());
                }

                // SubJobChannel
                if (UnitTestMode || UseTestMemoryWriter)
                {
                    List<IEventWriter<TestEventData, TestEventBatchData>> eventWriters = new(TestSubJobQueueWriters);
                    SubJobChannelManager.SetBufferedTaskProcessorFactory(new SubJobBufferedWriterTaskProcessorFactory<TestEventData, TestEventBatchData>(eventWriters));
                }
                else
                {
                    List<IEventWriter<ServiceBusMessage, ServiceBusMessageBatch>> eventWriters = new(ServiceBusAdminManagers.Count);
                    foreach (var serviceBusAdminManager in ServiceBusAdminManagers)
                    {
                        eventWriters.Add(serviceBusAdminManager.SubJobQueueServiceBusWriter);
                    }

                    SubJobChannelManager.SetBufferedTaskProcessorFactory(new SubJobBufferedWriterTaskProcessorFactory<ServiceBusMessage, ServiceBusMessageBatch>(eventWriters));
                }

                // Output
                if (UnitTestMode || UseTestMemoryWriter)
                {
                    List<IEventWriter<TestEventData, TestEventBatchData>> eventWriters = new(TestEventHubWriters);

                    OutputChannelManager.SetBufferedTaskProcessorFactory(
                        PublishOutputToARN 
                        ? new ArnPublishTaskProcessorFactory<ARNSingleInputMessage>(ArnNotificationClient) 
                        : new EventHubBufferedWriterTaskProcessorFactory<TestEventData, TestEventBatchData>(eventWriters));
                }
                else
                {
                    if (PublishOutputToARN)
                    {
                        OutputChannelManager.SetBufferedTaskProcessorFactory(new ArnPublishTaskProcessorFactory<ARNSingleInputMessage>(ArnNotificationClient));
                    }
                    else
                    {
                        List<IEventWriter<EventData, EventDataBatch>> eventWriters = new(EventHubWriters.Count);
                        foreach (var eventHubWriter in EventHubWriters)
                        {
                            eventWriters.Add(eventHubWriter);
                        }

                        OutputChannelManager.SetBufferedTaskProcessorFactory(new EventHubBufferedWriterTaskProcessorFactory<EventData, EventDataBatch>(eventWriters));
                    }
                }

                // Blob payload routing
                if (ArnNotificationClient != null)
                {
                    BlobPayloadRoutingChannelManager.SetBufferedTaskProcessorFactory(new BlobPayloadRoutingTaskProcessorFactory(ArnNotificationClient));
                }

                // Retry
                if (UnitTestMode || UseTestMemoryWriter)
                {
                    List<IEventWriter<TestEventData, TestEventBatchData>> eventWriters = new(TestRetryQueueWriters);

                    RetryChannelManager.SetBufferedTaskProcessorFactory(
                        new RetryBufferedWriterTaskProcessorFactory<ARNSingleInputMessage, TestEventData, TestEventBatchData>(eventWriters));

                    RawInputRetryChannelManager.SetBufferedTaskProcessorFactory(
                        new RetryBufferedWriterTaskProcessorFactory<ARNRawInputMessage, TestEventData, TestEventBatchData>(eventWriters));
                }
                else
                {
                    List<IEventWriter<ServiceBusMessage, ServiceBusMessageBatch>> eventWriters = new(ServiceBusAdminManagers.Count);
                    foreach (var serviceBusAdminManager in ServiceBusAdminManagers)
                    {
                        eventWriters.Add(serviceBusAdminManager.RetryQueueServiceBusWriter);
                    }

                    RetryChannelManager.SetBufferedTaskProcessorFactory(
                        new RetryBufferedWriterTaskProcessorFactory<ARNSingleInputMessage, ServiceBusMessage, ServiceBusMessageBatch>(eventWriters));

                    RawInputRetryChannelManager.SetBufferedTaskProcessorFactory(
                        new RetryBufferedWriterTaskProcessorFactory<ARNRawInputMessage, ServiceBusMessage, ServiceBusMessageBatch>(eventWriters));
                }

                // Poison
                if (UnitTestMode || UseTestMemoryWriter)
                {
                    List<IEventWriter<TestEventData, TestEventBatchData>> eventWriters = new(TestPoisonQueueWriters);

                    PoisonChannelManager.SetBufferedTaskProcessorFactory(
                        new PoisonBufferedWriterTaskProcessorFactory<ARNSingleInputMessage, TestEventData, TestEventBatchData>(eventWriters));

                    RawInputPoisonChannelManager.SetBufferedTaskProcessorFactory(
                        new PoisonBufferedWriterTaskProcessorFactory<ARNRawInputMessage, TestEventData, TestEventBatchData>(eventWriters));
                }
                else
                {
                    List<IEventWriter<ServiceBusMessage, ServiceBusMessageBatch>> eventWriters = new(ServiceBusAdminManagers.Count);
                    foreach (var serviceBusAdminManager in ServiceBusAdminManagers)
                    {
                        eventWriters.Add(serviceBusAdminManager.PoisonQueueServiceBusWriter);
                    }

                    PoisonChannelManager.SetBufferedTaskProcessorFactory(
                        new PoisonBufferedWriterTaskProcessorFactory<ARNSingleInputMessage, ServiceBusMessage, ServiceBusMessageBatch>(eventWriters));

                    RawInputPoisonChannelManager.SetBufferedTaskProcessorFactory(
                        new PoisonBufferedWriterTaskProcessorFactory<ARNRawInputMessage, ServiceBusMessage, ServiceBusMessageBatch>(eventWriters));
                }
            }

            private void AddConcurrencyManager()
            {
                // Input Channel
                RawInputChannelConcurrencyManager.RegisterObject(RawInputChannelManager.SetExternalConcurrencyManager);
                InputChannelConcurrencyManager.RegisterObject(InputChannelManager.SetExternalConcurrencyManager);
                SourceOfTruthChannelConcurrencyManager.RegisterObject(SourceOfTruthChannelManager.SetExternalConcurrencyManager);

                // Don't need any concurrency control for OutputChannelManager, RetryChannelManager, PoisonChannelManager because they are using buffered writer
                // Don't need concurrency control for FinalChannelManager
            }

            public void Dispose()
            {
                RawInputChannelManager?.Dispose();
                InputChannelManager?.Dispose();
                PartnerChannelRoutingManager?.Dispose();
                SourceOfTruthChannelManager?.Dispose();
                OutputChannelManager?.Dispose();

                RetryChannelManager?.Dispose();
                RawInputRetryChannelManager?.Dispose();

                PoisonChannelManager?.Dispose();
                RawInputPoisonChannelManager?.Dispose();

                FinalChannelManager?.Dispose();
                RawInputFinalChannelManager?.Dispose();
            }
        }

        #endregion
    }
}