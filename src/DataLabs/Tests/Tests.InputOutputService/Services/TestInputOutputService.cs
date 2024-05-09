using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
using Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub;
using Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputOutputService.Services;
using Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputOutputService.RetryStrategy;
using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common;
using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Constants;
using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TestEmulator;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
using Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputDataProvider;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.Services
{
    public class TestInputOutputService
    {
        public const string TEST_EVENT_HUB_NAME = "testeventhub";
        public const int NUM_PARTITIONS = 60;
        public readonly EventHubAsyncTaskInfoQueue[] taskInfoQueuePerPartition;
        public SolutionInputOutputService solutionInputOutputService;
        public TestPartnerDispatcherTaskFactory testPartnerDispatcherTaskFactory;
        public TestOutputBlobClient testOutputBlobClient;
        public TestCacheClient testCacheClient;
        public TestPartnerBlobClient testPartnerBlobClient;
        public TestArnNotificationClient testArnNotificationClient;
        public RegionConfig primaryRegionConfig;

        internal List<IEventhubInputProvider> inputProviders;
        internal List<IEventhubInputProvider> backupInputProviders;

        private IORetryStrategy _retryStrategy = new IORetryStrategy();
        private TestEventWriter[] TestEventHubWriters;
        private TestEventWriter[] TestRetryQueueWriters;
        private TestEventWriter[] TestPoisonQueueWriters;
        private TestEventWriter[] TestSubJobQueueWriters;

        #region Mocks

        IPartnerBlobClient _partnerBlobClient;
        IArnNotificationClient _arnNotificationClient;

        #endregion

        public TestInputOutputService(
            int numEventHubWriter = 1,
            int numRetryQueueWriter = 1,
            int numPoisonQueueWriter = 1,
            int numSubJobQueueWriter = 1,
            IPartnerBlobClient partnerBlobClient = null,
            IArnNotificationClient arnNotificationClient = null,
            int numInputProviders = 1,
            int numBackupInputProviders = 1
            )
        {
            TestEventHubWriters = new TestEventWriter[numEventHubWriter];
            for (int i = 0; i < numEventHubWriter; i++)
            {
                TestEventHubWriters[i] = new TestEventWriter();
            }
            TestRetryQueueWriters = new TestEventWriter[numRetryQueueWriter];
            for (int i = 0; i < numRetryQueueWriter; i++)
            {
                TestRetryQueueWriters[i] = new TestEventWriter();
            }
            TestPoisonQueueWriters = new TestEventWriter[numPoisonQueueWriter];
            for (int i = 0; i < numPoisonQueueWriter; i++)
            {
                TestPoisonQueueWriters[i] = new TestEventWriter();
            }
            TestSubJobQueueWriters = new TestEventWriter[numSubJobQueueWriter];
            for (int i = 0; i < numSubJobQueueWriter; i++)
            {
                TestSubJobQueueWriters[i] = new TestEventWriter();
            }

            taskInfoQueuePerPartition = new EventHubAsyncTaskInfoQueue[NUM_PARTITIONS];

            for (int i = 0; i < NUM_PARTITIONS; i++)
            {
                taskInfoQueuePerPartition[i] = new EventHubAsyncTaskInfoQueue(TEST_EVENT_HUB_NAME, i.ToString(), UpdateCheckpointAsyncFunc, 30, 10);
            }

            _partnerBlobClient = partnerBlobClient;
            _arnNotificationClient = arnNotificationClient;

            inputProviders = new List<IEventhubInputProvider>();
            for (int i = 0; i < numInputProviders; i++)
            {
                inputProviders.Add(new TestEventHubInputProvider("ipeh" + i));
            }

            backupInputProviders = new List<IEventhubInputProvider>();
            for (int i = 0; i < numBackupInputProviders; i++)
            {
                backupInputProviders.Add(new TestEventHubInputProvider("bipeh" + i));
            }
        }

        public async Task InitializeAndStartAsync()
        {
            SolutionUtils.InitializeProgram(ConfigMapUtil.Configuration, minWorkerThreads: 1000, minCompletionThreads: 1000);

            // LoggerFactory
            DataLabLoggerFactory.GetLoggerFactory();

            // Init Tracer and Metric Providers
            Tracer.CreateDataLabsTracerProvider(IOServiceOpenTelemetry.IOServiceTraceSource);
            MetricLogger.CreateDataLabsMeterProvider(IOServiceOpenTelemetry.IOServiceMeter);

            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(ConfigMapUtil.Configuration);
            services.AddSingleton<IConfigurationWithCallBack>(ConfigMapUtil.Configuration);

            // Add BlobClient Emulator
            testOutputBlobClient = new TestOutputBlobClient();
            testCacheClient = new TestCacheClient();
            testPartnerBlobClient = new TestPartnerBlobClient();

            services.AddSingleton<IOutputBlobClient>(testOutputBlobClient);
            services.AddSingleton<ICacheClient>(testCacheClient);
            services.AddSingleton<IPartnerBlobClient>(_partnerBlobClient ?? testPartnerBlobClient);
            services.AddIOResourceCacheClient();

            testArnNotificationClient = new TestArnNotificationClient();
            services.AddSingleton<IArnNotificationClient>(_arnNotificationClient ?? testArnNotificationClient);

            services = services
             .AddRawInputChannelManager()
             .AddInputChannelManager()
             .AddInputCacheChannelManager()
             .AddPartnerChannelManager()
             .AddSourceOfTruthChannelManager()
             .AddSubJobChannelManager()
             .AddOutputCacheChannelManager()
             .AddOutputChannelManager()
             .AddBlobPayloadRoutingChannelManager()
             .AddRetryChannelManager()
             .AddPoisonChannelManager();

            var serviceProvider = services.BuildServiceProvider();
            // Add Test Partner SubTaskFactory
            testPartnerDispatcherTaskFactory = new TestPartnerDispatcherTaskFactory();
            PartnerChannelRoutingManager.SetUnitTestsProperties(testPartnerDispatcherTaskFactory);

            SolutionInputOutputService.TestEventHubWriters = TestEventHubWriters;
            SolutionInputOutputService.TestRetryQueueWriters = TestRetryQueueWriters;
            SolutionInputOutputService.TestPoisonQueueWriters = TestPoisonQueueWriters;
            SolutionInputOutputService.TestSubJobQueueWriters = TestSubJobQueueWriters;

            await SolutionInputOutputService.InitializeServiceAsync(serviceProvider, unitTestMode: true).ConfigureAwait(false);

            CommonUtils.SetupRegionConfigManager(testOutputBlobClient);
            string primaryRegionName = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.PrimaryRegionName);

            primaryRegionConfig = RegionConfigManager.GetRegionConfig(primaryRegionName);


            PrivateFunctionAccessHelper.SetPrivateProperty(typeof(SolutionInputOutputService), "InputProviders", null, inputProviders, isStaticProperty: true);
            PrivateFunctionAccessHelper.SetPrivateProperty(typeof(SolutionInputOutputService), "BackupInputProviders", null, backupInputProviders, isStaticProperty: true);

            solutionInputOutputService = new SolutionInputOutputService(new TestHostApplication());
            await solutionInputOutputService.StartAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public async Task<(IOEventTaskContext<ARNRawInputMessage> taskContext1,
            IOEventTaskContext<ARNSingleInputMessage> taskContext2)>
            AddParentTaskWithRawInputCallBackAsync(BinaryData eventData, int partitionId, int taskTimeOutInMillSec = 10*1000)
        {
            var enqueuedTime = DateTimeOffset.UtcNow;
            var eventReadTime = DateTimeOffset.UtcNow;

            var taskInfo = new EventHubAsyncTaskInfo(
                    TEST_EVENT_HUB_NAME,
                    "MessageId",
                    DateTimeOffset.UtcNow,
                    partitionId.ToString(),
                    1,
                    1, null, false, default,primaryRegionConfig.RegionLocationName);

            taskInfoQueuePerPartition[partitionId].AddTaskInfo(taskInfo);

            var rawInputMessage = new ARNRawInputMessage(
                eventData,
                "testCorrelationId",
                default,
                null,
                null,
                null,
                false);

            var parentTaskContext = new IOEventTaskContext<ARNRawInputMessage>(
                InputOutputConstants.EventHubSingleInputEventTask,
                DataSourceType.InputEventHub,
                TEST_EVENT_HUB_NAME,
                firstEnqueuedTime: enqueuedTime,
                firstPickedUpTime: eventReadTime,
                dataEnqueuedTime: enqueuedTime,
                eventTime: rawInputMessage.EventTime,
                rawInputMessage,
                taskInfo,
                retryCount: 0,
                _retryStrategy,
                default,
                default,
                true,
                primaryRegionConfig,
                CancellationToken.None,
                SolutionInputOutputService.ARNMessageChannels.RawInputRetryChannelManager,
                SolutionInputOutputService.ARNMessageChannels.RawInputPoisonChannelManager,
                SolutionInputOutputService.ARNMessageChannels.RawInputFinalChannelManager,
                SolutionInputOutputService.GlobalConcurrencyManager);

            var childInputMessage = new ARNSingleInputMessage(
                eventData,
                null,
                "testCorrelationId",
                eventType: null,
                ResourcesConstants.SampleResourceId,
                ResourcesConstants.MicrosoftTenantId,
                ResourcesConstants.TestResourceLocation,
                default,
                false);

            var childTaskCallBack = new RawInputChildEventTaskCallBack(parentTaskContext);

            var childEventTaskContext = new IOEventTaskContext<ARNSingleInputMessage>(
                InputOutputConstants.EventHubSingleInputEventTask,
                DataSourceType.InputEventHub,
                TEST_EVENT_HUB_NAME,
                firstEnqueuedTime: enqueuedTime,
                firstPickedUpTime: eventReadTime,
                dataEnqueuedTime: enqueuedTime,
                eventTime: childInputMessage.EventTime,
                childInputMessage,
                childTaskCallBack,
                retryCount: 0,
                _retryStrategy,
                default,
                default,
                true,
                primaryRegionConfig,
                CancellationToken.None,
                SolutionInputOutputService.ARNMessageChannels.RetryChannelManager,
                SolutionInputOutputService.ARNMessageChannels.PoisonChannelManager,
                SolutionInputOutputService.ARNMessageChannels.FinalChannelManager,
                SolutionInputOutputService.GlobalConcurrencyManager);

            // Start Task
            childTaskCallBack.StartAddChildEvent();
            childTaskCallBack.IncreaseChildEventCount();
            childEventTaskContext.SetTaskTimeout(TimeSpan.FromMilliseconds(taskTimeOutInMillSec));

            await childEventTaskContext.StartEventTaskAsync(SolutionInputOutputService.ARNMessageChannels.InputChannelManager, true, null).ConfigureAwait(false);

            childTaskCallBack.FinishAddChildEvent();

            var waitingAction = parentTaskContext.StartWaitingChildTasksAction;
            waitingAction();

            return (parentTaskContext, childEventTaskContext);
        }

        public async Task<IOEventTaskContext<ARNSingleInputMessage>> AddSingleInputAsync(
            BinaryData eventData,
            int partitionId,
            int taskTimeOutInMiilSec = 10*1000, 
            bool waitForCompletion = true,
            ITaskChannelManager<IOEventTaskContext<ARNSingleInputMessage>> startChannel = null)
        {
            var eventTaskContext = CreateSingleInput(eventData, partitionId);

            // Start Task

            eventTaskContext.SetTaskTimeout(TimeSpan.FromMilliseconds(taskTimeOutInMiilSec));
            await eventTaskContext.StartEventTaskAsync(startChannel ?? SolutionInputOutputService.ARNMessageChannels.InputChannelManager, waitForCompletion, null).ConfigureAwait(false);
            return eventTaskContext;
        }

        public IOEventTaskContext<ARNSingleInputMessage> CreateSingleInput(BinaryData eventData, int partitionId)
        {
            var enqueuedTime = DateTimeOffset.UtcNow;
            var eventReadTime = DateTimeOffset.UtcNow;
            var taskInfo = new EventHubAsyncTaskInfo(
                    TEST_EVENT_HUB_NAME,
                    "MessageId",
                    DateTimeOffset.UtcNow,
                    partitionId.ToString(),
                    1,
                    1, eventData, false, default, primaryRegionConfig.RegionLocationName);

            taskInfoQueuePerPartition[partitionId].AddTaskInfo(taskInfo);

            var inputMessage = new ARNSingleInputMessage(
                eventData,
                null,
                "testCorrelationId",
                eventType: null,
                ResourcesConstants.SampleResourceId,
                ResourcesConstants.MicrosoftTenantId,
                ResourcesConstants.TestResourceLocation,
                default,
                false);

            var eventTaskContext = new IOEventTaskContext<ARNSingleInputMessage>(
                InputOutputConstants.EventHubSingleInputEventTask,
                DataSourceType.InputEventHub,
                TEST_EVENT_HUB_NAME,
                firstEnqueuedTime: enqueuedTime,
                firstPickedUpTime: eventReadTime,
                dataEnqueuedTime: enqueuedTime,
                eventTime: inputMessage.EventTime,
                inputMessage,
                taskInfo,
                retryCount: 0,
                _retryStrategy,
                default,
                default,
                true,
                primaryRegionConfig,
                CancellationToken.None,
                SolutionInputOutputService.ARNMessageChannels.RetryChannelManager,
                SolutionInputOutputService.ARNMessageChannels.PoisonChannelManager,
                SolutionInputOutputService.ARNMessageChannels.FinalChannelManager,
                SolutionInputOutputService.GlobalConcurrencyManager);

            taskInfo.SetCancellableTask(eventTaskContext);

            return eventTaskContext;
        }

        public async Task<IOEventTaskContext<ARNRawInputMessage>> AddRawInputAsync(BinaryData eventData, int partitionId, int taskTimeoutInMill = 10*1000, bool waitForCompletion = true)
        {
            var enqueuedTime = DateTimeOffset.UtcNow;
            var eventReadTime = DateTimeOffset.UtcNow;
            var taskInfo = new EventHubAsyncTaskInfo(
                    TEST_EVENT_HUB_NAME,
                    "MessageId",
                    DateTimeOffset.UtcNow,
                    partitionId.ToString(),
                    1,
                    1, eventData, false, default, primaryRegionConfig.RegionLocationName);

            taskInfoQueuePerPartition[partitionId].AddTaskInfo(taskInfo);

            var inputMessage = ARNRawInputMessage.CreateRawInputMessage(
                binaryData: eventData,
                rawInputCorrelationId: "testCorrelationId",
                eventTime: default,
                eventType: null,
                tenantId: null,
                resourceLocation: null,
                deserialize: true,
                hasCompressed: false,
                null);

            var eventTaskContext = new IOEventTaskContext<ARNRawInputMessage>(
                InputOutputConstants.EventHubRawInputEventTask,
                DataSourceType.InputEventHub,
                TEST_EVENT_HUB_NAME,
                firstEnqueuedTime: enqueuedTime,
                firstPickedUpTime: eventReadTime,
                dataEnqueuedTime: enqueuedTime,
                eventTime: inputMessage.EventTime,
                inputMessage,
                taskInfo,
                retryCount: 0,
                _retryStrategy,
                default,
                default,
                true,
                primaryRegionConfig,
                CancellationToken.None,
                SolutionInputOutputService.ARNMessageChannels.RawInputRetryChannelManager,
                SolutionInputOutputService.ARNMessageChannels.RawInputPoisonChannelManager,
                SolutionInputOutputService.ARNMessageChannels.RawInputFinalChannelManager,
                SolutionInputOutputService.GlobalConcurrencyManager);

            // Start Task
            taskInfo.SetCancellableTask(eventTaskContext);
            eventTaskContext.SetTaskTimeout(TimeSpan.FromMilliseconds(taskTimeoutInMill));

            await eventTaskContext.StartEventTaskAsync(SolutionInputOutputService.ARNMessageChannels.RawInputChannelManager, waitForCompletion, null).ConfigureAwait(false);
            return eventTaskContext;
        }

        public async Task<(IOEventTaskContext<ARNSingleInputMessage> taskContext1,
           IOEventTaskContext<ARNSingleInputMessage> taskContext2)>
           AddChainedTaskInputAsync(BinaryData eventData, int partitionId)
        {
            var enqueuedTime = DateTimeOffset.UtcNow;
            var eventReadTime = DateTimeOffset.UtcNow;
            var taskInfo = new EventHubAsyncTaskInfo(
                    TEST_EVENT_HUB_NAME,
                    "MessageId",
                    DateTimeOffset.UtcNow,
                    partitionId.ToString(),
                    1,
                    1, null, false, default, primaryRegionConfig.RegionLocationName);

            taskInfoQueuePerPartition[partitionId].AddTaskInfo(taskInfo);

            var inputMessage = new ARNSingleInputMessage(
                eventData,
                null,
                "testCorrelationId",
                eventType: null,
                ResourcesConstants.SampleResourceId,
                ResourcesConstants.MicrosoftTenantId,
                ResourcesConstants.TestResourceLocation,
                default,
                false);

            var eventTaskContext = new IOEventTaskContext<ARNSingleInputMessage>(
                InputOutputConstants.EventHubSingleInputEventTask,
                DataSourceType.InputEventHub,
                TEST_EVENT_HUB_NAME,
                firstEnqueuedTime: enqueuedTime,
                firstPickedUpTime: eventReadTime,
                dataEnqueuedTime: enqueuedTime,
                eventTime: inputMessage.EventTime,
                inputMessage,
                taskInfo,
                retryCount: 0,
                _retryStrategy,
                default,
                default,
                true,
                primaryRegionConfig,
                CancellationToken.None,
                SolutionInputOutputService.ARNMessageChannels.RetryChannelManager,
                SolutionInputOutputService.ARNMessageChannels.PoisonChannelManager,
                SolutionInputOutputService.ARNMessageChannels.FinalChannelManager,
                SolutionInputOutputService.GlobalConcurrencyManager);

            var eventTaskContext2 = new IOEventTaskContext<ARNSingleInputMessage>(
                InputOutputConstants.EventHubSingleInputEventTask,
                DataSourceType.InputEventHub,
                TEST_EVENT_HUB_NAME,
                firstEnqueuedTime: enqueuedTime,
                firstPickedUpTime: eventReadTime,
                dataEnqueuedTime: enqueuedTime,
                eventTime: inputMessage.EventTime,
                inputMessage,
                taskInfo,
                retryCount: 0,
                _retryStrategy,
                default,
                default,
                true,
                primaryRegionConfig,
                CancellationToken.None,
                SolutionInputOutputService.ARNMessageChannels.RetryChannelManager,
                SolutionInputOutputService.ARNMessageChannels.PoisonChannelManager,
                SolutionInputOutputService.ARNMessageChannels.FinalChannelManager,
                SolutionInputOutputService.GlobalConcurrencyManager);

            eventTaskContext.SetChainedNextEventTaskContext(eventTaskContext2, SolutionInputOutputService.ARNMessageChannels.InputChannelManager);

            // Start Task
            eventTaskContext.SetTaskTimeout(TimeSpan.FromSeconds(10));
            eventTaskContext2.SetTaskTimeout(TimeSpan.FromSeconds(10));

            await eventTaskContext.StartEventTaskAsync(SolutionInputOutputService.ARNMessageChannels.InputChannelManager, true, null).ConfigureAwait(false);
            return (eventTaskContext, eventTaskContext2);
        }

        private static Task UpdateCheckpointAsyncFunc(string partitionId, long offset, long? sequenceNumber, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public List<TestEventHubInputProvider> GetInputProviders()
        {
            return GetProviders("InputProviders");
        }

        public List<TestEventHubInputProvider> GetBackupInputProviders()
        {
            return GetProviders("BackupInputProviders");
        }

        private List<TestEventHubInputProvider> GetProviders(string privatePropertyName)
        {
            var result = new List<TestEventHubInputProvider>();
            var tempList = (List<IEventhubInputProvider>)PrivateFunctionAccessHelper.GetPrivateProperty(typeof(SolutionInputOutputService), privatePropertyName, null, isStaticProperty: true);
            foreach (var item in tempList)
            {
                result.Add((TestEventHubInputProvider)item);
            }
            return result;
        }

        public class TestHostApplication : IHostApplicationLifetime
        {
            public CancellationToken ApplicationStarted => new CancellationToken();

            public CancellationToken ApplicationStopping => new CancellationToken();

            public CancellationToken ApplicationStopped => new CancellationToken();

            public void StopApplication()
            {
            }
        }
        public class TestEventHubInputProvider : IEventhubInputProvider
        {
            public string Name { get; private set; }

            public int numStartAsyncCalled { get; private set; }
            public int numDeleteCheckpointAsyncCalled { get; private set; }

            public int numStopAsyncCalled { get; private set; }

            public string consumerGroupDeleted { get; private set; }

            public TestEventHubInputProvider(string name)
            {
                Name = name;
                numStartAsyncCalled = 0;
                numDeleteCheckpointAsyncCalled = 0;
                numStopAsyncCalled = 0;
            }

            public Task<bool> DeleteCheckpointsAsync(string consumerGroupName, CancellationToken cancellationToken)
            {
                consumerGroupDeleted = consumerGroupName;
                numDeleteCheckpointAsyncCalled++;
                return Task.FromResult(true);
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                numStartAsyncCalled++;
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                numStopAsyncCalled++;
                return Task.CompletedTask;
            }
        }
    }
}