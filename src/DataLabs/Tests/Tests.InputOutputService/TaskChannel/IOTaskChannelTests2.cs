namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.TaskChannel
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.Services;

    [TestClass]
    public class IOTaskChannelTests2
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
        public async Task TestNoResourceNotificatoin()
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var inputData = ResourcesConstants.NoResourceEventGridEventBinaryData;
            var rawInputEventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);

            Assert.AreEqual(EventTaskFinalStage.POISON_QUEUE, rawInputEventTaskContext.EventFinalStage);

            var parentTaskCallBack = (EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack;
            Assert.AreEqual(false, parentTaskCallBack.IsTaskCancelled);
            Assert.AreEqual(true, parentTaskCallBack.IsCompleted);
            Assert.AreEqual(false, parentTaskCallBack.IsTaskSuccess);
            Assert.AreEqual(1, parentTaskCallBack.numCleanupCalled);
            Assert.AreEqual(true, rawInputEventTaskContext.HasTaskDisposed);

            // When parent is finished, we set childEventCallback to null
            var childEventCallBack = rawInputEventTaskContext.ChildEventTaskCallBack;
            Assert.IsNull(childEventCallBack);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestBlobURLNotificationSerializationError()
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            testInputOutputService.testPartnerBlobClient.ReturnSerialziationError = true;

            var inputData = ResourcesConstants.BlobURLEventGridEventBinaryData;
            var rawInputEventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);

            Assert.AreEqual(EventTaskFinalStage.POISON_QUEUE, rawInputEventTaskContext.EventFinalStage);

            var parentTaskCallBack = (EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack;
            Assert.AreEqual(false, parentTaskCallBack.IsTaskCancelled);
            Assert.AreEqual(true, parentTaskCallBack.IsCompleted);
            Assert.AreEqual(false, parentTaskCallBack.IsTaskSuccess);
            Assert.AreEqual(1, parentTaskCallBack.numCleanupCalled);
            Assert.AreEqual(true, rawInputEventTaskContext.HasTaskDisposed);

            // When parent is finished, we set childEventCallback to null
            var childEventCallBack = rawInputEventTaskContext.ChildEventTaskCallBack;
            Assert.IsNull(childEventCallBack);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestBlobURLNotificationRetryableNetworkError()
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            testInputOutputService.testPartnerBlobClient.ThrowNetworkException = true;
            testInputOutputService.testPartnerBlobClient.StatusCode = System.Net.HttpStatusCode.InternalServerError;

            var inputData = ResourcesConstants.BlobURLEventGridEventBinaryData;
            var rawInputEventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);

            Assert.AreEqual(EventTaskFinalStage.RETRY_QUEUE, rawInputEventTaskContext.EventFinalStage);

            var parentTaskCallBack = (EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack;
            Assert.AreEqual(false, parentTaskCallBack.IsTaskCancelled);
            Assert.AreEqual(true, parentTaskCallBack.IsCompleted);
            Assert.AreEqual(false, parentTaskCallBack.IsTaskSuccess);
            Assert.AreEqual(1, parentTaskCallBack.numCleanupCalled);
            Assert.AreEqual(true, rawInputEventTaskContext.HasTaskDisposed);

            // When parent is finished, we set childEventCallback to null
            var childEventCallBack = rawInputEventTaskContext.ChildEventTaskCallBack;
            Assert.IsNull(childEventCallBack);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestBlobURLNotificationNonRetryableNetworkError()
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            testInputOutputService.testPartnerBlobClient.ThrowNetworkException = true;
            testInputOutputService.testPartnerBlobClient.StatusCode = System.Net.HttpStatusCode.Forbidden;

            var inputData = ResourcesConstants.BlobURLEventGridEventBinaryData;
            var rawInputEventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);

            Assert.AreEqual(EventTaskFinalStage.POISON_QUEUE, rawInputEventTaskContext.EventFinalStage);

            var parentTaskCallBack = (EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack;
            Assert.AreEqual(false, parentTaskCallBack.IsTaskCancelled);
            Assert.AreEqual(true, parentTaskCallBack.IsCompleted);
            Assert.AreEqual(false, parentTaskCallBack.IsTaskSuccess);
            Assert.AreEqual(1, parentTaskCallBack.numCleanupCalled);
            Assert.AreEqual(true, rawInputEventTaskContext.HasTaskDisposed);

            // When parent is finished, we set childEventCallback to null
            var childEventCallBack = rawInputEventTaskContext.ChildEventTaskCallBack;
            Assert.IsNull(childEventCallBack);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestBlobURLNotificationSuccess()
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var inputData = ResourcesConstants.BlobURLEventGridEventBinaryData;
            var rawInputEventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);

            Assert.AreEqual(EventTaskFinalStage.SUCCESS, rawInputEventTaskContext.EventFinalStage);

            var parentTaskCallBack = (EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack;
            Assert.AreEqual(false, parentTaskCallBack.IsTaskCancelled);
            Assert.AreEqual(true, parentTaskCallBack.IsCompleted);
            Assert.AreEqual(true, parentTaskCallBack.IsTaskSuccess);
            Assert.AreEqual(1, parentTaskCallBack.numCleanupCalled);
            Assert.AreEqual(true, rawInputEventTaskContext.HasTaskDisposed);

            // When parent is finished, we set childEventCallback to null
            var childEventCallBack = rawInputEventTaskContext.ChildEventTaskCallBack;
            Assert.IsNull(childEventCallBack);

            Assert.AreEqual(3, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(3, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(3, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(3, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);

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
        public async Task TestRawMultiResourcesInput()
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var inputData = ResourcesConstants.SingleEventGridEventMultiResourcesBinaryData;
            var rawInputEventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);

            Assert.AreEqual(EventTaskFinalStage.SUCCESS, rawInputEventTaskContext.EventFinalStage);

            var parentTaskCallBack = (EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack;
            Assert.AreEqual(false, parentTaskCallBack.IsTaskCancelled);
            Assert.AreEqual(true, parentTaskCallBack.IsCompleted);
            Assert.AreEqual(true, parentTaskCallBack.IsTaskSuccess);
            Assert.AreEqual(1, parentTaskCallBack.numCleanupCalled);
            Assert.AreEqual(true, rawInputEventTaskContext.HasTaskDisposed);

            // When parent is finished, we set childEventCallback to null
            var childEventCallBack = rawInputEventTaskContext.ChildEventTaskCallBack;
            Assert.IsNull(childEventCallBack);

            Assert.AreEqual(3, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(3, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(3, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(3, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);

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
        public async Task TestLargeBatchedResources()
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            // Update the value
            ConfigMapUtil.Configuration[InputOutputConstants.MaxBatchedChildBeforeMoveToRetryQueue] = "1";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            var inputData = ResourcesConstants.SingleEventGridEventMultiResourcesBinaryData;
            var rawInputEventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);

            Assert.AreEqual(EventTaskFinalStage.SUCCESS, rawInputEventTaskContext.EventFinalStage);

            var parentTaskCallBack = (EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack;
            Assert.AreEqual(false, parentTaskCallBack.IsTaskCancelled);
            Assert.AreEqual(true, parentTaskCallBack.IsCompleted);
            Assert.AreEqual(true, parentTaskCallBack.IsTaskSuccess);
            Assert.AreEqual(1, parentTaskCallBack.numCleanupCalled);
            Assert.AreEqual(true, rawInputEventTaskContext.HasTaskDisposed);

            // When parent is finished, we set childEventCallback to null
            var childEventCallBack = rawInputEventTaskContext.ChildEventTaskCallBack;
            Assert.IsNull(childEventCallBack);

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient.NumUploadCall);
            
            // OutputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);

            Assert.AreEqual(2, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.IsTrue(SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated > 0);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.IsTrue(SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count > 0);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestSubJobSingleResponses(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            testInputOutputService.testPartnerDispatcherTaskFactory.UseSubJob = true;

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

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(1, SolutionInputOutputService.TestSubJobQueueWriters[0].NumEventDataCreated);
            Assert.IsTrue(SolutionInputOutputService.TestSubJobQueueWriters[0].NumEventBatchCreated > 0);
            Assert.AreEqual(0, SolutionInputOutputService.TestSubJobQueueWriters[0].testEventDataList.Count);
            Assert.IsTrue(SolutionInputOutputService.TestSubJobQueueWriters[0].testEventBatchDataList.Count > 0);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);

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
        public async Task TestSubJobMultiStreamResponses(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            var numResponses = 4;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseMultiResponses = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.NumStreamResponses = numResponses;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseSubJob = true;

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

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(numResponses, SolutionInputOutputService.TestSubJobQueueWriters[0].NumEventDataCreated);
            Assert.IsTrue(SolutionInputOutputService.TestSubJobQueueWriters[0].NumEventBatchCreated > 0);
            Assert.AreEqual(0, SolutionInputOutputService.TestSubJobQueueWriters[0].testEventDataList.Count);
            Assert.IsTrue(SolutionInputOutputService.TestSubJobQueueWriters[0].testEventBatchDataList.Count > 0);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);

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
        public async Task TestEventHubWriteErrorAndRetryErrorAndPoison(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            SolutionInputOutputService.TestEventHubWriters[0].ReturnException = true;
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

            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
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

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);
        }


        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestEventHubWriteErrorAndRetryErrorAndRetryErrorAndPoison(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            SolutionInputOutputService.TestEventHubWriters[0].ReturnException = true;

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

            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

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
        public async Task TestEventHubWriteErrorAndRetryErrorAndAnotherRetryQueue(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;
        
            SolutionInputOutputService.TestEventHubWriters[0].ReturnException = true;

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

            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

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
        public async Task TestEventHubWriteErrorAndRetryQueueBatchErrorAndAnotherRetryQueue(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            SolutionInputOutputService.TestEventHubWriters[0].ReturnException = true;

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

            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

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
        public async Task TestEventHubWriteErrorAndRetryErrorAndRetryErrorAndPoisonErrorAndPoison(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2, numPoisonQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            SolutionInputOutputService.TestEventHubWriters[0].ReturnException = true;

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

            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

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
        public async Task TestEventHubWriteErrorAndRetryErrorAndRetryErrorAndPoisonErrorAndPoisonErrorAndDrop(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2, numPoisonQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            SolutionInputOutputService.TestEventHubWriters[0].ReturnException = true;

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

            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

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
        public async Task TestPoisonWithDropPoisonMessage(bool rawInput)
        {
            var testInputOutputService = new TestInputOutputService(numRetryQueueWriter: 2, numPoisonQueueWriter: 2);
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;

            SolutionInputOutputService.TestEventHubWriters[0].ReturnException = true;

            SolutionInputOutputService.TestRetryQueueWriters[0].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[1];
            SolutionInputOutputService.TestRetryQueueWriters[1].NextTestEventWriter = SolutionInputOutputService.TestRetryQueueWriters[0];

            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnExceptionOnCreateBatch = true;
            SolutionInputOutputService.TestRetryQueueWriters[0].ReturnException = false;
            SolutionInputOutputService.TestRetryQueueWriters[0].ResetNextWriteException = false;

            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnExceptionOnCreateBatch = false;
            SolutionInputOutputService.TestRetryQueueWriters[1].ReturnException = true;
            SolutionInputOutputService.TestRetryQueueWriters[1].ResetNextWriteException = false;

            // Enable drop poison message
            ConfigMapUtil.Configuration[InputOutputConstants.DropPoisonMessage] = "true";
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
                Assert.AreEqual(EventTaskFinalStage.DROP, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient._cache.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[1].testEventDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated + SolutionInputOutputService.TestRetryQueueWriters[1].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated + SolutionInputOutputService.TestRetryQueueWriters[1].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count + SolutionInputOutputService.TestRetryQueueWriters[1].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[1].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[1].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck(testInputOutputService).ConfigureAwait(false);

            SolutionInputOutputService.DropPoisonMessage = false;
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

            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

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
    }
}