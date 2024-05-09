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
    public class PartnerChannelRoutingManagerTests
    {
        public const string PartnerChannelConcurrency = "localhost1:3;localhost2:3;";
        private TestInputOutputService testInputOutputService;
        private int TEST_PARTITION_ID = 10;

        [TestInitialize]
        public void TestInitialize()
        {
            ConfigMapUtil.Reset();
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[InputOutputConstants.AllowedOutputTypes] = ResourcesConstants.AllowedSampleOutputResourceType;
            ConfigMapUtil.Configuration[InputOutputConstants.PartnerChannelConcurrency] = PartnerChannelConcurrency;
            ConfigMapUtil.Configuration[SolutionConstants.PrimaryRegionName] = "p-eus";
            ConfigMapUtil.Configuration[SolutionConstants.BackupRegionName] = "b-eus";
            ConfigMapUtil.Configuration[SolutionConstants.UseOutputCache] = "true";
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = false;
        }

        [TestMethod]
        [DataRow(true, "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "")]
        [DataRow(false, "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "")]
        [DataRow(true, "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"eventTypes\": \"Microsoft.TestPartner/TestSolution/write\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute.Test/*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"eventTypes\": \"Microsoft.TestPartner/TestSolution/write\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute.Test/*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(true, "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute.Test/*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute.Test/*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(true, "{ \"eventTypes\" : \"Microsoft.Compute/virtualMachineScaleSets/write\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.TestPartner/*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(false, "{ \"eventTypes\" : \"Microsoft.Compute/virtualMachineScaleSets/write\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.TestPartner*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        public async Task PartnerChannelRoutingManagerSingleResponseTestAsync(bool rawInput, string partnerSingleResponseResourcesRouting, string partnerMultiResponseResourcesRouting)
        {
            TestReset();

            ConfigMapUtil.Configuration[SolutionConstants.PartnerSingleResponseResourcesRouting] = partnerSingleResponseResourcesRouting;
            ConfigMapUtil.Configuration[SolutionConstants.PartnerMultiResponseResourcesRouting] = partnerMultiResponseResourcesRouting;

            testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);

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
        }

        [TestMethod]
        [DataRow(true, "", "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(false, "", "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(true, "{ \"resourceTypes\" : \"Microsoft.Compute.Test/*\",\"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"eventTypes\": \"Microsoft.TestPartner/TestSolution/write\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"Microsoft.Compute.Test/*\",\"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"eventTypes\": \"Microsoft.TestPartner/TestSolution/write\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(true, "{ \"resourceTypes\" : \"Microsoft.Compute.Test/*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\",\"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"Microsoft.Compute.Test/*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(true, "{ \"resourceTypes\" : \"Microsoft.Compute.Test/*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"eventTypes\" : \"Microsoft.Compute/virtualMachineScaleSets/write\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"Microsoft.Compute.Test/*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"eventTypes\" : \"Microsoft.Compute/virtualMachineScaleSets/write\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        public async Task PartnerChannelRoutingManagerMultiResponsesTestAsync(bool rawInput, string partnerSingleResponseResourcesRouting, string partnerMultiResponseResourcesRouting)
        {
            TestReset();

            ConfigMapUtil.Configuration[SolutionConstants.PartnerSingleResponseResourcesRouting] = partnerSingleResponseResourcesRouting;
            ConfigMapUtil.Configuration[SolutionConstants.PartnerMultiResponseResourcesRouting] = partnerMultiResponseResourcesRouting;

            testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);

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
        }

        [TestMethod]
        [DataRow(true, "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(true, "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        [DataRow(true, "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        [DataRow(true, "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        [DataRow(true, "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"Microsoft.Compute/virtualMachineScaleSets\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};")]
        [DataRow(true, "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        [DataRow(true, "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"eventTypes\" : \"Microsoft.Compute/virtualMachineScaleSets/write|delete\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        [DataRow(false, "{ \"resourceTypes\" : \"Microsoft.Compute*\", \"partnerChannelAddress\":\"http://localhost1:5072\", \"partnerChannelName\":\"localhost1\"};", "{ \"eventTypes\" : \"Microsoft.Compute/virtualMachineScaleSets/write|delete\", \"partnerChannelAddress\":\"http://localhost2:5072\", \"partnerChannelName\":\"localhost2\"};")]
        public async Task PartnerChannelRoutingManagerAllResponsesTestAsync(bool rawInput, string partnerSingleResponseResourcesRouting, string partnerMultiResponseResourcesRouting)
        {
            TestReset();

            ConfigMapUtil.Configuration[SolutionConstants.PartnerSingleResponseResourcesRouting] = partnerSingleResponseResourcesRouting;
            ConfigMapUtil.Configuration[SolutionConstants.PartnerMultiResponseResourcesRouting] = partnerMultiResponseResourcesRouting;

            testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);

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

            Assert.AreEqual(2, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(2, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(2, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.IsTrue(SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated > 0);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.IsTrue(SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count > 0);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);
        }

        private void TestReset()
        {
            if (SolutionInputOutputService.TestEventHubWriters != null)
            {
                foreach(var testEventWriter in SolutionInputOutputService.TestEventHubWriters)
                {
                    testEventWriter?.Clear();
                }
            }
            if (SolutionInputOutputService.TestRetryQueueWriters != null)
            {
                foreach (var testEventWriter in SolutionInputOutputService.TestRetryQueueWriters)
                {
                    testEventWriter?.Clear();
                }
            }
            if (SolutionInputOutputService.TestPoisonQueueWriters != null)
            {
                foreach (var testEventWriter in SolutionInputOutputService.TestPoisonQueueWriters)
                {
                    testEventWriter?.Clear();
                }
            }

            SolutionInputOutputService.UseSourceOfTruth = true;

            if (testInputOutputService != null)
            {
                testInputOutputService.testPartnerDispatcherTaskFactory?.Clear();
                testInputOutputService.testOutputBlobClient?.Clear();
                testInputOutputService.testCacheClient?.Clear();
            }
        }
    }
}
