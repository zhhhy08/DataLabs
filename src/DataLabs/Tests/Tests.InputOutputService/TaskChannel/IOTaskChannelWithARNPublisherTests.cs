namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.TaskChannel
{
    using global::Azure.Messaging.EventHubs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TestEmulator;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PoisonChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.RetryChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.Services;

    [TestClass]
    public class IOTaskChannelWithARNPublisherTests
    {
        private int TEST_PARTITION_ID = 10;

        [TestInitialize]
        public void TestInitializeAsync()
        {
            ConfigMapUtil.Reset();
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[InputOutputConstants.AllowedOutputTypes] = ResourcesConstants.AllowedSampleOutputResourceType;
            ConfigMapUtil.Configuration[SolutionConstants.PartnerSingleResponseResourcesRouting]=ResourcesConstants.PartnerSingleResponseResourcesRouting;
            ConfigMapUtil.Configuration[InputOutputConstants.PartnerChannelConcurrency] = ResourcesConstants.PartnerChannelConcurrency;
            ConfigMapUtil.Configuration[InputOutputConstants.InputCacheTypes] = "Microsoft.Compute/virtualMachineScaleSets";
            ConfigMapUtil.Configuration[SolutionConstants.PrimaryRegionName] = "p-eus";
            ConfigMapUtil.Configuration[SolutionConstants.BackupRegionName] = "b-eus";
            ConfigMapUtil.Configuration[SolutionConstants.UseOutputCache] = "true";

            // Enable ARN Publish
            ConfigMapUtil.Configuration[InputOutputConstants.PublishOutputToArn] = "true";

        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = false;
        }

        private async Task ConcurrencyCheck(TestInputOutputService testInputOutputService)
        {
            await Task.Delay(100).ConfigureAwait(false);
            Assert.AreEqual(0, SolutionInputOutputService.GlobalConcurrencyManager.NumRunning);
            Assert.AreEqual(0, SolutionInputOutputService.RawInputChannelConcurrencyManager.GetCurrentNumRunning());
            Assert.AreEqual(0, SolutionInputOutputService.InputChannelConcurrencyManager.GetCurrentNumRunning());
            Assert.AreEqual(0, SolutionInputOutputService.InputCacheChannelConcurrencyManager.GetCurrentNumRunning());
            Assert.AreEqual(0, SolutionInputOutputService.SourceOfTruthChannelConcurrencyManager.GetCurrentNumRunning());
            Assert.AreEqual(0, SolutionInputOutputService.OutputCacheChannelConcurrencyManager.GetCurrentNumRunning());

            int count = testInputOutputService.taskInfoQueuePerPartition.Length;
            for (int i = 0; i < count; i++)
            {
                if (i == TEST_PARTITION_ID)
                {
                    Assert.AreEqual(1, testInputOutputService.taskInfoQueuePerPartition[i].TaskInfoQueueLength);
                    testInputOutputService.taskInfoQueuePerPartition[i].UpdateCompletedTasks();
                    Assert.AreEqual(0, testInputOutputService.taskInfoQueuePerPartition[i].TaskInfoQueueLength);
                }
                else
                {
                    Assert.AreEqual(0, testInputOutputService.taskInfoQueuePerPartition[i].TaskInfoQueueLength);
                }
            }
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestArnPublishErrorAndRetryErrorAndPoison(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var testArnNotificationClient = testInputOutputService.testArnNotificationClient;
            testArnNotificationClient.ReturnException = true;

            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnException = true;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.POISON_QUEUE, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient._cache.Count);

            Assert.AreEqual(1, testArnNotificationClient.NumPublishToArnCalls);
            Assert.AreEqual(1, testArnNotificationClient.NumExceptionCalls);
            Assert.AreEqual(0, testArnNotificationClient.NumPublishSuccess);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }


        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestArnPublishErrorAndRetryErrorAndRetryErrorAndPoison(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var testArnNotificationClient = testInputOutputService.testArnNotificationClient;
            testArnNotificationClient.ReturnException = true;

            SolutionInputOutputService.TestRetryQueueWriters[0].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[1];
            SolutionInputOutputService.TestRetryQueueWriters[1].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[0];

            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnExceptionOnCreateBatch = true;
            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnException = false;
            SolutionInputOutputService.TestRetryQueueWriters[0].ResetNextWriteException = false;

            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnExceptionOnCreateBatch = false;
            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnException = true;
            SolutionInputOutputService.TestRetryQueueWriters[1].ResetNextWriteException = false;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.POISON_QUEUE, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient._cache.Count);

            Assert.AreEqual(1, testArnNotificationClient.NumPublishToArnCalls);
            Assert.AreEqual(1, testArnNotificationClient.NumExceptionCalls);
            Assert.AreEqual(0, testArnNotificationClient.NumPublishSuccess);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[1].testEventDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated + SolutionInputOutputService.TestRetryQueueWriters[1].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated + SolutionInputOutputService.TestRetryQueueWriters[1].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count + SolutionInputOutputService.TestRetryQueueWriters[1].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestArnPublishErrorAndRetryErrorAndAnotherRetryQueue(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var testArnNotificationClient = testInputOutputService.testArnNotificationClient;
            testArnNotificationClient.ReturnException = true;

            SolutionInputOutputService.TestRetryQueueWriters[0].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[1];
            SolutionInputOutputService.TestRetryQueueWriters[1].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[0];

            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnExceptionOnCreateBatch = false;
            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnException = true;
            SolutionInputOutputService.TestRetryQueueWriters[0].ResetNextWriteException = true;

            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnExceptionOnCreateBatch = false;
            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnException = true;
            SolutionInputOutputService.TestRetryQueueWriters[1].ResetNextWriteException = true;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.RETRY_QUEUE, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient._cache.Count);

            Assert.AreEqual(1, testArnNotificationClient.NumPublishToArnCalls);
            Assert.AreEqual(1, testArnNotificationClient.NumExceptionCalls);
            Assert.AreEqual(0, testArnNotificationClient.NumPublishSuccess);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[1].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[1].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[1].testEventDataList.Count);

            // Due to random, one of the retry queue will be selected
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count + SolutionInputOutputService.TestRetryQueueWriters[1].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestArnPublishErrorAndRetryQueueBatchErrorAndAnotherRetryQueue(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var testArnNotificationClient = testInputOutputService.testArnNotificationClient;
            testArnNotificationClient.ReturnException = true;

            SolutionInputOutputService.TestRetryQueueWriters[0].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[1];
            SolutionInputOutputService.TestRetryQueueWriters[1].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[0];

            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnExceptionOnCreateBatch = true;
            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnException = false;
            SolutionInputOutputService.TestRetryQueueWriters[0].ResetNextWriteException = true;

            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnExceptionOnCreateBatch = true;
            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnException = false;
            SolutionInputOutputService.TestRetryQueueWriters[1].ResetNextWriteException = true;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.RETRY_QUEUE, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient._cache.Count);

            Assert.AreEqual(1, testArnNotificationClient.NumPublishToArnCalls);
            Assert.AreEqual(1, testArnNotificationClient.NumExceptionCalls);
            Assert.AreEqual(0, testArnNotificationClient.NumPublishSuccess);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[1].testEventDataList.Count);

            // Due to random, one of the retry queue will be selected
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated + SolutionInputOutputService.TestRetryQueueWriters[1].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated + SolutionInputOutputService.TestRetryQueueWriters[1].NumEventBatchCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count + SolutionInputOutputService.TestRetryQueueWriters[1].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestArnPublishErrorAndRetryErrorAndRetryErrorAndPoisonErrorAndPoison(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2, numPoisonQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var testArnNotificationClient = testInputOutputService.testArnNotificationClient;
            testArnNotificationClient.ReturnException = true;

            SolutionInputOutputService.TestRetryQueueWriters[0].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[1];
            SolutionInputOutputService.TestRetryQueueWriters[1].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[0];

            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnExceptionOnCreateBatch = true;
            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnException = false;
            SolutionInputOutputService.TestRetryQueueWriters[0].ResetNextWriteException = false;

            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnExceptionOnCreateBatch = false;
            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnException = true;
            SolutionInputOutputService.TestRetryQueueWriters[1].ResetNextWriteException = false;

            SolutionInputOutputService.TestPoisonQueueWriters[0].NextTestEventWriter = SolutionInputOutputService.TestPoisonQueueWriters[1];
            SolutionInputOutputService.TestPoisonQueueWriters[1].NextTestEventWriter = SolutionInputOutputService.TestPoisonQueueWriters[0];

            SolutionInputOutputService.TestPoisonQueueWriters[0].ReturnExceptionOnCreateBatch = true;
            SolutionInputOutputService.TestPoisonQueueWriters[0].ReturnException = false;
            SolutionInputOutputService.TestPoisonQueueWriters[0].ResetNextWriteException = true;

            SolutionInputOutputService.TestPoisonQueueWriters[1].ReturnExceptionOnCreateBatch = true;
            SolutionInputOutputService.TestPoisonQueueWriters[1].ReturnException = false;
            SolutionInputOutputService.TestPoisonQueueWriters[1].ResetNextWriteException = true;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.POISON_QUEUE, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient._cache.Count);

            Assert.AreEqual(1, testArnNotificationClient.NumPublishToArnCalls);
            Assert.AreEqual(1, testArnNotificationClient.NumExceptionCalls);
            Assert.AreEqual(0, testArnNotificationClient.NumPublishSuccess);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[1].testEventDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated + SolutionInputOutputService.TestRetryQueueWriters[1].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated + SolutionInputOutputService.TestRetryQueueWriters[1].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count + SolutionInputOutputService.TestRetryQueueWriters[1].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated + SolutionInputOutputService.TestPoisonQueueWriters[1].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated + SolutionInputOutputService.TestPoisonQueueWriters[1].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count + SolutionInputOutputService.TestPoisonQueueWriters[1].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestArnPublishErrorAndRetryErrorAndRetryErrorAndPoisonErrorAndPoisonErrorAndDrop(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2, numPoisonQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var testArnNotificationClient = testInputOutputService.testArnNotificationClient;
            testArnNotificationClient.ReturnException = true;

            SolutionInputOutputService.TestRetryQueueWriters[0].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[1];
            SolutionInputOutputService.TestRetryQueueWriters[1].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[0];

            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnExceptionOnCreateBatch = true;
            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnException = false;
            SolutionInputOutputService.TestRetryQueueWriters[0].ResetNextWriteException = false;

            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnExceptionOnCreateBatch = false;
            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnException = true;
            SolutionInputOutputService.TestRetryQueueWriters[1].ResetNextWriteException = false;

            SolutionInputOutputService.TestPoisonQueueWriters[0].NextTestEventWriter = SolutionInputOutputService.TestPoisonQueueWriters[1];
            SolutionInputOutputService.TestPoisonQueueWriters[1].NextTestEventWriter = SolutionInputOutputService.TestPoisonQueueWriters[0];

            SolutionInputOutputService.TestPoisonQueueWriters[0].ReturnExceptionOnCreateBatch = false;
            SolutionInputOutputService.TestPoisonQueueWriters[0].ReturnException = true;
            SolutionInputOutputService.TestPoisonQueueWriters[0].ResetNextWriteException = false;

            SolutionInputOutputService.TestPoisonQueueWriters[1].ReturnExceptionOnCreateBatch = false;
            SolutionInputOutputService.TestPoisonQueueWriters[1].ReturnException = true;
            SolutionInputOutputService.TestPoisonQueueWriters[1].ResetNextWriteException = false;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.DROP, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient._cache.Count);

            Assert.AreEqual(1, testArnNotificationClient.NumPublishToArnCalls);
            Assert.AreEqual(1, testArnNotificationClient.NumExceptionCalls);
            Assert.AreEqual(0, testArnNotificationClient.NumPublishSuccess);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[1].testEventDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated + SolutionInputOutputService.TestRetryQueueWriters[1].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated + SolutionInputOutputService.TestRetryQueueWriters[1].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count + SolutionInputOutputService.TestRetryQueueWriters[1].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[1].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[1].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestSuccess(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            var testArnNotificationClient = testInputOutputService.testArnNotificationClient;
            Assert.AreEqual(1, testArnNotificationClient.NumPublishToArnCalls);
            Assert.AreEqual(0, testArnNotificationClient.NumExceptionCalls);
            Assert.AreEqual(1, testArnNotificationClient.NumPublishSuccess);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestSuccessAfterOutputChannelHotConfig(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            ConfigMapUtil.Configuration["OutputChannelPartitionKey"] = "SCOPEID";
            ConfigMapUtil.Configuration["OutputChannelNumBufferQueue"] = "5";
            ConfigMapUtil.Configuration["OutputChannelBufferQueueLength"] = "1500";
            ConfigMapUtil.Configuration["OutputChannelMaxBufferedSize"] = "400";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(default);
            Thread.Sleep(50);

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            var testArnNotificationClient = testInputOutputService.testArnNotificationClient;
            Assert.AreEqual(1, testArnNotificationClient.NumPublishToArnCalls);
            Assert.AreEqual(0, testArnNotificationClient.NumExceptionCalls);
            Assert.AreEqual(1, testArnNotificationClient.NumPublishSuccess);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        public void TestBufferedTaskProcessorFactoryInitTests()
        {
            int numEventHubWriter = 2;
            var testEventHubWriters = new TestEventWriter[numEventHubWriter];
            for (int i = 0; i < numEventHubWriter; i++)
            {
                testEventHubWriters[i] = new TestEventWriter();
            }
            List<IEventWriter<TestEventData, TestEventBatchData>> eventWriters = new(testEventHubWriters);

            var arnPublishTaskProcessorFactory = new ArnPublishTaskProcessorFactory<ARNSingleInputMessage>(new TestArnNotificationClient());
            var eventHubBufferedWriterTaskProcessorFactory = new EventHubBufferedWriterTaskProcessorFactory<TestEventData, TestEventBatchData>(eventWriters);
            var retryBufferedWriterTaskProcessorFactory = new RetryBufferedWriterTaskProcessorFactory<ARNRawInputMessage, TestEventData, TestEventBatchData>(eventWriters);
            var poisonBufferedWriterTaskProcessorFactory = new PoisonBufferedWriterTaskProcessorFactory<ARNRawInputMessage, TestEventData, TestEventBatchData>(eventWriters);
            var subJobBufferedWriterTaskProcessorFactory = new SubJobBufferedWriterTaskProcessorFactory<TestEventData, TestEventBatchData>(eventWriters);
            var blobPayloadRoutingTaskProcessorFactory = new BlobPayloadRoutingTaskProcessorFactory(new TestArnNotificationClient());

            for (int i = 0; i < 4; i++)
            {
                arnPublishTaskProcessorFactory.CreateBufferedTaskProcessor();
                eventHubBufferedWriterTaskProcessorFactory.CreateBufferedTaskProcessor();
                retryBufferedWriterTaskProcessorFactory.CreateBufferedTaskProcessor();
                poisonBufferedWriterTaskProcessorFactory.CreateBufferedTaskProcessor();
                subJobBufferedWriterTaskProcessorFactory.CreateBufferedTaskProcessor();
                blobPayloadRoutingTaskProcessorFactory.CreateBufferedTaskProcessor();
            }
        }
    }
}