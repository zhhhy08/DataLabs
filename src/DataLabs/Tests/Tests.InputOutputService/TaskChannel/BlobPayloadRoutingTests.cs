namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.TaskChannel
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Common;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.Services;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Clients.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts.Data;
    using Moq;
    using System.Threading.Tasks;

    [TestClass]
    public class BlobPayloadRoutingTests
    {
        #region Fields

        private const int TEST_PARTITION_ID = 10;
        private const string VmssType = "Microsoft.Compute/virtualMachineScaleSets";
        private const string VmType = "Microsoft.Compute/virtualMachine";
        private Mock<IPartnerBlobClient> _partnerBlobClientMock;
        private Mock<IArnNotificationClient> _arnNotificationClientMock;

        #endregion

        [TestInitialize]
        public void TestInitializeAsync()
        {
            ConfigMapUtil.Reset();
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[InputOutputConstants.AllowedOutputTypes] = ResourcesConstants.AllowedSampleOutputResourceType;
            ConfigMapUtil.Configuration[SolutionConstants.PartnerSingleResponseResourcesRouting] = ResourcesConstants.PartnerSingleResponseResourcesRouting;
            ConfigMapUtil.Configuration[InputOutputConstants.PartnerChannelConcurrency] = ResourcesConstants.PartnerChannelConcurrency;
            ConfigMapUtil.Configuration[InputOutputConstants.InputCacheTypes] = $"{VmssType};{VmType}";
            ConfigMapUtil.Configuration[SolutionConstants.PrimaryRegionName] = "p-eus";
            ConfigMapUtil.Configuration[SolutionConstants.BackupRegionName] = "b-eus";
            ConfigMapUtil.Configuration[InputOutputConstants.PublishOutputToArn] = "false";
            ConfigMapUtil.Configuration[InputOutputConstants.EnableBlobPayloadRouting] = "true";
            ConfigMapUtil.Configuration[InputOutputConstants.BlobPayloadRoutingTypes] = VmssType;
            ConfigMapUtil.Configuration[SolutionConstants.UseOutputCache] = "true";

            _partnerBlobClientMock = new Mock<IPartnerBlobClient>();
            _arnNotificationClientMock = new Mock<IArnNotificationClient>();

            _arnNotificationClientMock.Setup(x => x.IsInitialized).Returns(true);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = false;
        }

        [DataRow(VmssType, "arn.routing.location", "eastus", "arn.routing.location", "westus", true, true)]
        [DataRow(VmssType, "arn.routing.location", "", "arn.routing.location", "westus", true, true)]
        [DataRow(VmssType, "arn.routing.location", null, "arn.routing.location", "westus", true, true)]
        [DataRow(VmssType, "key", "value", "arn.routing.location", "westus", true, true)]
        [DataRow(VmssType, "arn.routing.location", "westus", "arn.routing.location", "westus", false, true)]
        [DataRow(VmssType, "arn.routing.location", "westus", "arn.routing.location", "", false, true)]
        [DataRow(VmssType, "arn.routing.location", "westus", "arn.routing.location", null, false, true)]
        [DataRow(VmssType, "arn.routing.location", "westus", "key", "value", false, true)]
        [DataRow(VmType, "key", "value", "arn.routing.location", "westus", false, true)]
        [DataRow(VmssType, "key", "value", "arn.routing.location", "westus", false, false)]
        [TestMethod]
        public async Task TestFromEventHubSucceed(
            string resourceType,
            string additionalBatchPropertyKey,
            string additionalBatchPropertyValue,
            string additionalResourcePropertyKey,
            string additionalResourcePropertyValue,
            bool blobPayloadRouting,
            bool featureEnabled)
        {
            ConfigMapUtil.Configuration[InputOutputConstants.EnableBlobPayloadRouting] = featureEnabled.ToString();

            var testInputOutputService = new TestInputOutputService(
                numRetryQueueWriter: 2, 
                partnerBlobClient: _partnerBlobClientMock.Object,
                arnNotificationClient: _arnNotificationClientMock.Object);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var inlineEvent = CommonUtils.GetInlinePayloadEventString(
                type: resourceType,
                additionalResourcePropertyKey: additionalResourcePropertyKey,
                additionalResourcePropertyValue: additionalResourcePropertyValue).ToV3Event();

            var blobEvent = CommonUtils.GetBlobPayloadEventString(
                type: resourceType,
                additionalBatchPropertyKey: additionalBatchPropertyKey,
                additionalBatchPropertyValue: additionalBatchPropertyValue).ToBinaryData();

            IList<ResourceOperationBase> publishedResources = null;

            _partnerBlobClientMock
                .Setup(x => x.GetResourcesAsync<NotificationResourceDataV3<GenericResource>>(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(inlineEvent.Data.Resources.ToList()));

            _arnNotificationClientMock
                .Setup(x => x.PublishToArn(It.IsAny<IList<ResourceOperationBase>>(), It.IsAny<DataBoundary?>(), It.IsAny<FieldOverrides>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<AdditionalGroupingProperties>()))
                .Returns(
                (IList<ResourceOperationBase> resourceOperations, DataBoundary? _, FieldOverrides _, bool _, CancellationToken _, AdditionalGroupingProperties _) =>
                {
                    publishedResources = resourceOperations;
                    return Task.CompletedTask;
                });

            var eventTaskContext = await testInputOutputService
                .AddRawInputAsync(blobEvent, TEST_PARTITION_ID)
                .ConfigureAwait(false);

            // Allow cache update to complete
            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
            Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
            Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
            Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);

            Assert.AreEqual(blobPayloadRouting ? 0 : 1, testInputOutputService.testOutputBlobClient.NumUploadCall);
            Assert.AreEqual(blobPayloadRouting ? 0 : 1, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            Assert.AreEqual(blobPayloadRouting ? 0 : 1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(blobPayloadRouting ? 0 : 1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(blobPayloadRouting ? 0 : 1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(blobPayloadRouting ? 0 : 1, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(blobPayloadRouting ? 1 : 0, publishedResources?.Count ?? 0);
            Assert.AreEqual(blobPayloadRouting ? additionalResourcePropertyValue : null, publishedResources?[0]?.AdditionalArmProperties[additionalResourcePropertyKey]?.ToString());

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await testInputOutputService.ConcurrencyCheck(TEST_PARTITION_ID).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestFromEventHubPutToRetrySucceed()
        {
            var testInputOutputService = new TestInputOutputService(
                partnerBlobClient: _partnerBlobClientMock.Object);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);

            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;
            testInputOutputService.testArnNotificationClient.ReturnException = true;

            var inlineEvent = CommonUtils.GetInlinePayloadEventString(
                type: VmssType,
                additionalResourcePropertyKey: "arn.routing.location",
                additionalResourcePropertyValue: "eastus").ToV3Event();

            var blobEvent = CommonUtils.GetBlobPayloadEventString(
                type: VmssType,
                additionalBatchPropertyKey: "key",
                additionalBatchPropertyValue: "value").ToBinaryData();

            _partnerBlobClientMock
                .Setup(x => x.GetResourcesAsync<NotificationResourceDataV3<GenericResource>>(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(inlineEvent.Data.Resources.ToList()));

            var eventTaskContext = await testInputOutputService
                .AddRawInputAsync(blobEvent, TEST_PARTITION_ID)
                .ConfigureAwait(false);

            // Parent task succeeded, child task put to retry
            Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
            Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
            Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
            Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(1, testInputOutputService.testArnNotificationClient.NumPublishToArnCalls);
            Assert.AreEqual(1, testInputOutputService.testArnNotificationClient.NumExceptionCalls);
            Assert.AreEqual(0, testInputOutputService.testArnNotificationClient.NumPublishSuccess);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);
            Assert.AreEqual("BlobPayloadRoutingChannel", SolutionInputOutputService.TestRetryQueueWriters[0].PropertyList
                .Where(x => x.Key == InputOutputConstants.PropertyTag_ChannelType).First().Value);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await testInputOutputService.ConcurrencyCheck(TEST_PARTITION_ID).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestFromEventHubPutToPoisonSucceed()
        {
            var testInputOutputService = new TestInputOutputService(
                partnerBlobClient: _partnerBlobClientMock.Object);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;
            testInputOutputService.testArnNotificationClient.ReturnException = true;
            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnException = true;

            var inlineEvent = CommonUtils.GetInlinePayloadEventString(
                type: VmssType,
                additionalResourcePropertyKey: "arn.routing.location",
                additionalResourcePropertyValue: "eastus").ToV3Event();

            var blobEvent = CommonUtils.GetBlobPayloadEventString(
                type: VmssType,
                additionalBatchPropertyKey: "key",
                additionalBatchPropertyValue: "value").ToBinaryData();

            _partnerBlobClientMock
                .Setup(x => x.GetResourcesAsync<NotificationResourceDataV3<GenericResource>>(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(inlineEvent.Data.Resources.ToList()));

            var eventTaskContext = await testInputOutputService
                .AddRawInputAsync(blobEvent, TEST_PARTITION_ID)
                .ConfigureAwait(false);

            // Parent task succeeded, child task put to poison
            Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
            Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
            Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
            Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(1, testInputOutputService.testArnNotificationClient.NumPublishToArnCalls);
            Assert.AreEqual(1, testInputOutputService.testArnNotificationClient.NumExceptionCalls);
            Assert.AreEqual(0, testInputOutputService.testArnNotificationClient.NumPublishSuccess);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);
            Assert.AreEqual("BlobPayloadRoutingChannel", SolutionInputOutputService.TestPoisonQueueWriters[0].PropertyList
                .Where(x => x.Key == InputOutputConstants.PropertyTag_ChannelType).First().Value);

            await testInputOutputService.ConcurrencyCheck(TEST_PARTITION_ID).ConfigureAwait(false);
        }

        [DataRow(VmssType, "arn.routing.location", "eastus", "arn.routing.location", "westus")]
        [DataRow(VmssType, "arn.routing.location", "", "arn.routing.location", "westus")]
        [DataRow(VmssType, "arn.routing.location", null, "arn.routing.location", "westus")]
        [DataRow(VmssType, "key", "value", "arn.routing.location", "westus")]
        [DataRow(VmssType, "arn.routing.location", "westus", "arn.routing.location", "westus")]
        [DataRow(VmssType, "arn.routing.location", "westus", "arn.routing.location", "")]
        [DataRow(VmssType, "arn.routing.location", "westus", "arn.routing.location", null)]
        [DataRow(VmssType, "arn.routing.location", "westus", "key", "value")]
        [DataRow(VmType, "key", "value", "arn.routing.location", "westus")]
        [TestMethod]
        public async Task TestFromRetrySucceed(
            string resourceType,
            string additionalBatchPropertyKey,
            string additionalBatchPropertyValue,
            string additionalResourcePropertyKey,
            string additionalResourcePropertyValue)
        {
            var testInputOutputService = new TestInputOutputService(
                numRetryQueueWriter: 2,
                arnNotificationClient: _arnNotificationClientMock.Object);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var inlineEvent = CommonUtils.GetInlinePayloadEventString(
                type: resourceType,
                additionalResourcePropertyKey: additionalResourcePropertyKey,
                additionalResourcePropertyValue: additionalResourcePropertyValue,
                additionalBatchPropertyKey: additionalBatchPropertyKey,
                additionalBatchPropertyValue: additionalBatchPropertyValue).ToBinaryData();

            IList<ResourceOperationBase> publishedResources = null;

            _arnNotificationClientMock
                .Setup(x => x.PublishToArn(It.IsAny<IList<ResourceOperationBase>>(), It.IsAny<DataBoundary?>(), It.IsAny<FieldOverrides>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<AdditionalGroupingProperties>()))
                .Returns(
                (IList<ResourceOperationBase> resourceOperations, DataBoundary? _, FieldOverrides _, bool _, CancellationToken _, AdditionalGroupingProperties _) =>
                {
                    publishedResources = resourceOperations;
                    return Task.CompletedTask;
                });

            // Mimic service bus processing
            var eventTaskContext = await testInputOutputService
                .AddSingleInputAsync(inlineEvent, TEST_PARTITION_ID, startChannel: SolutionInputOutputService.ARNMessageChannels.BlobPayloadRoutingChannelManager)
                .ConfigureAwait(false);

            Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
            Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
            Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
            Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, publishedResources?.Count ?? 0);
            Assert.AreEqual(additionalResourcePropertyValue ?? "", publishedResources?[0]?.AdditionalArmProperties[additionalResourcePropertyKey]?.ToString());

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await testInputOutputService.ConcurrencyCheck(TEST_PARTITION_ID).ConfigureAwait(false);
        }
    }
}
