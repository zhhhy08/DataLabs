namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.TaskChannel
{
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.Services;
    
    [TestClass]
    public class IOTaskChannelTests
    {
        private TestInputOutputService testInputOutputService;
        private int TEST_PARTITION_ID = 10;

        [TestInitialize]
        public async Task TestInitializeAsync()
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
            testInputOutputService = new TestInputOutputService();
            await testInputOutputService.InitializeAndStartAsync().ConfigureAwait(false);
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = true;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
            ActivityMonitorFactory.UseTaskAwareActivityMonitor = false;
        }

        private async Task ConcurrencyCheck()
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
        public void TestEmptyTraceId()
        {
            TestReset();

            //LogRecord.TraceId:                 10000000000000000000000000000001
            //LogRecord.SpanId:                  1000000000000001
            //LogRecord.TraceFlags:              None

            var traceIdPattern = "LogRecord.TraceId:\\s+" + MissingTraceIdLogProcessor.EmptyTraceIdString;
            var spanIdPattern = "LogRecord.SpanId:\\s+" + MissingTraceIdLogProcessor.EmptySpanIdString;
            var traceFlagsPattern = "LogRecord.TraceFlags:\\s+" + ActivityTraceFlags.None.ToString();
            Regex regexTraceId = new Regex(traceIdPattern);
            Regex regexSpanId = new Regex(spanIdPattern);
            Regex regexTraceFlag = new Regex(traceFlagsPattern);

            var originalConsoleOut = Console.Out; // preserve the original stream
            using (var writer = new StringWriter())
            {
                Console.SetOut(writer);

                var currentVal = ConfigMapUtil.Configuration[InputOutputConstants.DeleteCacheAfterETagConflict];
                ConfigMapUtil.Configuration[InputOutputConstants.DeleteCacheAfterETagConflict] = "true";
                ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
                Thread.Sleep(50);

                ConfigMapUtil.Configuration[InputOutputConstants.DeleteCacheAfterETagConflict] = "false";
                ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
                Thread.Sleep(50);

                ConfigMapUtil.Configuration[InputOutputConstants.DeleteCacheAfterETagConflict] = currentVal;
                ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
                Thread.Sleep(50);

                writer.Flush();

                var consoleOutput = writer.GetStringBuilder().ToString();

                Match match = regexTraceId.Match(consoleOutput);
                Assert.AreEqual(true, match.Success);

                match = regexSpanId.Match(consoleOutput);
                Assert.AreEqual(true, match.Success);

                match = regexTraceFlag.Match(consoleOutput);
                Assert.AreEqual(true, match.Success);
            }

            Console.SetOut(originalConsoleOut); // restore Console.Out
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestHardTimeout(bool rawInput)
        {
            TestReset();

            testInputOutputService.testPartnerDispatcherTaskFactory.DelayInMilliSecForTimeOutTest = 200;
            var timeOutInMilli = 1000;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID, timeOutInMilli, false).ConfigureAwait(false);

                await Task.Delay(50).ConfigureAwait(false);

                var eventHubAsyncTaskInfo = (EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack;
                var maxDurationExpireTaskInfo = eventHubAsyncTaskInfo.CreateAndSetMaxDurationExpireTaskInfo();
                if (maxDurationExpireTaskInfo != null)
                {
                    // This Task has been too long on Queue. 
                    // It mean that processing task might be in some stuck state or unexpected state
                    // Let's create RetryMessage and move original eventHub Message to retryQueue
                    // Then we can checkpoint this message and continue to read eventHub messages
                    _ = Task.Run(() => EventHubAsyncTaskInfoQueue.MoveMessageToRetryAsync(
                        originalEventHubAsyncTaskInfo: eventHubAsyncTaskInfo,
                        maxDurationExpireTaskInfo: maxDurationExpireTaskInfo));
                }

                await Task.Delay(500).ConfigureAwait(false);

                Assert.AreEqual(EventTaskFinalStage.DROP, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(true, eventHubAsyncTaskInfo.IsTaskCancelled);
                Assert.AreEqual(true, eventHubAsyncTaskInfo.IsCompleted);
                Assert.AreEqual(false, eventHubAsyncTaskInfo.IsTaskSuccess);
                Assert.AreEqual(1, eventHubAsyncTaskInfo.numCleanupCalled);

                Assert.AreEqual(false, maxDurationExpireTaskInfo.IsTaskCancelled);
                Assert.AreEqual(true, maxDurationExpireTaskInfo.IsCompleted);
                Assert.AreEqual(false, maxDurationExpireTaskInfo.IsTaskSuccess);
                Assert.AreEqual(1, maxDurationExpireTaskInfo.numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID, timeOutInMilli, false).ConfigureAwait(false);

                await Task.Delay(50).ConfigureAwait(false);

                var eventHubAsyncTaskInfo = (EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack;
                var maxDurationExpireTaskInfo = eventHubAsyncTaskInfo.CreateAndSetMaxDurationExpireTaskInfo();
                if (maxDurationExpireTaskInfo != null)
                {
                    // This Task has been too long on Queue. 
                    // It mean that processing task might be in some stuck state or unexpected state
                    // Let's create RetryMessage and move original eventHub Message to retryQueue
                    // Then we can checkpoint this message and continue to read eventHub messages
                    _ = Task.Run(() => EventHubAsyncTaskInfoQueue.MoveMessageToRetryAsync(
                        originalEventHubAsyncTaskInfo: eventHubAsyncTaskInfo,
                        maxDurationExpireTaskInfo: maxDurationExpireTaskInfo));
                }

                await Task.Delay(500).ConfigureAwait(false);

                Assert.AreEqual(EventTaskFinalStage.DROP, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(true, eventHubAsyncTaskInfo.IsTaskCancelled);
                Assert.AreEqual(true, eventHubAsyncTaskInfo.IsCompleted);
                Assert.AreEqual(false, eventHubAsyncTaskInfo.IsTaskSuccess);
                Assert.AreEqual(1, eventHubAsyncTaskInfo.numCleanupCalled);

                Assert.AreEqual(false, maxDurationExpireTaskInfo.IsTaskCancelled);
                Assert.AreEqual(true, maxDurationExpireTaskInfo.IsCompleted);
                Assert.AreEqual(false, maxDurationExpireTaskInfo.IsTaskSuccess);
                Assert.AreEqual(1, maxDurationExpireTaskInfo.numCleanupCalled);
            }

            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestTimeout(bool rawInput)
        {
            TestReset();

            testInputOutputService.testPartnerDispatcherTaskFactory.DelayInMilliSecForTimeOutTest = 200;
            var timeOutInMilli = 50;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID, timeOutInMilli).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID, timeOutInMilli).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.RETRY_QUEUE, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestSuccess(bool rawInput)
        {
            TestReset();
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

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestNotAllowedOutput(bool rawInput)
        {
            TestReset();

            testInputOutputService.testPartnerDispatcherTaskFactory.ReturnNotAllowedResponse = true;

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

            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestNoSourceOfTrue(bool rawInput)
        {
            TestReset();

            SolutionInputOutputService.UseSourceOfTruth = false;
            
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

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
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

            await ConcurrencyCheck().ConfigureAwait(false);

            SolutionInputOutputService.UseSourceOfTruth = true;
        }

        [TestMethod]
        public async Task TestRawParentWithChildCallBack()
        {
            TestReset();

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            var eventTaskContexts = await testInputOutputService.AddParentTaskWithRawInputCallBackAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);
            
            var parentEvenTaskContext = eventTaskContexts.taskContext1;
            var childEventTaskContext = eventTaskContexts.taskContext2;

            await Task.Delay(500).ConfigureAwait(false);

            Assert.AreEqual(false, ((EventHubAsyncTaskInfo)parentEvenTaskContext.EventTaskCallBack).IsTaskCancelled);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)parentEvenTaskContext.EventTaskCallBack).IsCompleted);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)parentEvenTaskContext.EventTaskCallBack).IsTaskSuccess);
            Assert.AreEqual(1, ((EventHubAsyncTaskInfo)parentEvenTaskContext.EventTaskCallBack).numCleanupCalled);

            Assert.AreEqual(false, ((RawInputChildEventTaskCallBack)childEventTaskContext.EventTaskCallBack).IsTaskCancelled);

            Assert.AreEqual(EventTaskFinalStage.SUCCESS, parentEvenTaskContext.EventFinalStage);
            Assert.AreEqual(EventTaskFinalStage.SUCCESS, childEventTaskContext.EventFinalStage);

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

            Assert.AreEqual(true, parentEvenTaskContext.HasTaskDisposed);
            Assert.AreEqual(true, childEventTaskContext.HasTaskDisposed);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestChainedTasks()
        {
            TestReset();

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;
            var eventTaskContexts = await testInputOutputService.AddChainedTaskInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);

            var eventTaskContext1 = eventTaskContexts.taskContext1;
            var eventTaskContext2 = eventTaskContexts.taskContext2;

            await Task.Delay(500).ConfigureAwait(false);

            Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext1.EventTaskCallBack).IsTaskCancelled);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext1.EventTaskCallBack).IsCompleted);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext1.EventTaskCallBack).IsTaskSuccess);

            Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext2.EventTaskCallBack).IsTaskCancelled);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext2.EventTaskCallBack).IsCompleted);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext2.EventTaskCallBack).IsTaskSuccess);

            Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext1.EventFinalStage);
            Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext2.EventFinalStage);

            Assert.AreEqual(2, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(2, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(2, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(2, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            //Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            //Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(true, eventTaskContext1.HasTaskDisposed);
            Assert.AreEqual(true, eventTaskContext2.HasTaskDisposed);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestMultiStreamResponses(bool rawInput)
        {
            TestReset();

            var numResponses = 4;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseMultiResponses = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.NumStreamResponses = numResponses;

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

            Assert.AreEqual(numResponses, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(numResponses, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(numResponses, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
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

            await ConcurrencyCheck().ConfigureAwait(false); 

        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestMultiStreamResponsesHardTimeout(bool rawInput)
        {
            TestReset();

            var numResponses = 4;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseMultiResponses = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.NumStreamResponses = numResponses;
            testInputOutputService.testPartnerDispatcherTaskFactory.DelayInMilliSecForTimeOutTest = 200;
            var timeOutInMilli = 50;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;

            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID, timeOutInMilli, false).ConfigureAwait(false);

                await Task.Delay(50).ConfigureAwait(false);

                var eventHubAsyncTaskInfo = (EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack;
                var maxDurationExpireTaskInfo = eventHubAsyncTaskInfo.CreateAndSetMaxDurationExpireTaskInfo();
                if (maxDurationExpireTaskInfo != null)
                {
                    // This Task has been too long on Queue. 
                    // It mean that processing task might be in some stuck state or unexpected state
                    // Let's create RetryMessage and move original eventHub Message to retryQueue
                    // Then we can checkpoint this message and continue to read eventHub messages
                    _ = Task.Run(() => EventHubAsyncTaskInfoQueue.MoveMessageToRetryAsync(
                        originalEventHubAsyncTaskInfo: eventHubAsyncTaskInfo,
                        maxDurationExpireTaskInfo: maxDurationExpireTaskInfo));
                }

                await Task.Delay(500).ConfigureAwait(false);

                Assert.AreEqual(EventTaskFinalStage.DROP, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(true, eventHubAsyncTaskInfo.IsTaskCancelled);
                Assert.AreEqual(true, eventHubAsyncTaskInfo.IsCompleted);
                Assert.AreEqual(false, eventHubAsyncTaskInfo.IsTaskSuccess);
                Assert.AreEqual(1, eventHubAsyncTaskInfo.numCleanupCalled);

                Assert.AreEqual(false, maxDurationExpireTaskInfo.IsTaskCancelled);
                Assert.AreEqual(true, maxDurationExpireTaskInfo.IsCompleted);
                Assert.AreEqual(false, maxDurationExpireTaskInfo.IsTaskSuccess);
                Assert.AreEqual(1, maxDurationExpireTaskInfo.numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID, timeOutInMilli, false).ConfigureAwait(false);

                await Task.Delay(50).ConfigureAwait(false);

                var eventHubAsyncTaskInfo = (EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack;
                var maxDurationExpireTaskInfo = eventHubAsyncTaskInfo.CreateAndSetMaxDurationExpireTaskInfo();
                if (maxDurationExpireTaskInfo != null)
                {
                    // This Task has been too long on Queue. 
                    // It mean that processing task might be in some stuck state or unexpected state
                    // Let's create RetryMessage and move original eventHub Message to retryQueue
                    // Then we can checkpoint this message and continue to read eventHub messages
                    _ = Task.Run(() => EventHubAsyncTaskInfoQueue.MoveMessageToRetryAsync(
                        originalEventHubAsyncTaskInfo: eventHubAsyncTaskInfo,
                        maxDurationExpireTaskInfo: maxDurationExpireTaskInfo));
                }

                await Task.Delay(500).ConfigureAwait(false);

                Assert.AreEqual(EventTaskFinalStage.DROP, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(true, eventHubAsyncTaskInfo.IsTaskCancelled);
                Assert.AreEqual(true, eventHubAsyncTaskInfo.IsCompleted);
                Assert.AreEqual(false, eventHubAsyncTaskInfo.IsTaskSuccess);
                Assert.AreEqual(1, eventHubAsyncTaskInfo.numCleanupCalled);

                Assert.AreEqual(false, maxDurationExpireTaskInfo.IsTaskCancelled);
                Assert.AreEqual(true, maxDurationExpireTaskInfo.IsCompleted);
                Assert.AreEqual(false, maxDurationExpireTaskInfo.IsTaskSuccess);
                Assert.AreEqual(1, maxDurationExpireTaskInfo.numCleanupCalled);
            }

            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);

        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestMultiStreamResponsesTimeout(bool rawInput)
        {
            TestReset();

            var numResponses = 4;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseMultiResponses = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.NumStreamResponses = numResponses;
            testInputOutputService.testPartnerDispatcherTaskFactory.DelayInMilliSecForTimeOutTest = 200;
            var timeOutInMilli = 50;

            var inputData = ResourcesConstants.SingleEventGridEventBinaryData;

            if (rawInput)
            {
                var eventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID, timeOutInMilli).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.SUCCESS, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }
            else
            {
                var eventTaskContext = await testInputOutputService.AddSingleInputAsync(inputData, TEST_PARTITION_ID, timeOutInMilli).ConfigureAwait(false);
                Assert.AreEqual(EventTaskFinalStage.RETRY_QUEUE, eventTaskContext.EventFinalStage);
                Assert.AreEqual(true, eventTaskContext.HasTaskDisposed);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskCancelled);
                Assert.AreEqual(true, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsCompleted);
                Assert.AreEqual(false, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).IsTaskSuccess);
                Assert.AreEqual(1, ((EventHubAsyncTaskInfo)eventTaskContext.EventTaskCallBack).numCleanupCalled);
            }

            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);

        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestSameGroupMultiStreamResponses(bool rawInput)
        {
            TestReset();

            var numResponses = 4;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseMultiResponses = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseSameGroups = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.NumStreamResponses = numResponses;

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

            Assert.AreEqual(numResponses, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(numResponses, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(numResponses, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(numResponses, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(numResponses, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestMultiStreamInternalResponses(bool rawInput)
        {
            TestReset();

            var numResponses = 4;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseMultiResponses = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseSameGroups = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseFirstInternal = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.NumStreamResponses = numResponses;

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

            Assert.AreEqual(numResponses, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(numResponses, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(numResponses - 1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(numResponses - 1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(numResponses - 1, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestMultiStreamResponsesWithParentError(bool rawInput)
        {
            TestReset();

            var numResponses = 4;
            var returnParentErrorAfterNum = 2;

            testInputOutputService.testPartnerDispatcherTaskFactory.UseMultiResponses = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.NumStreamResponses = numResponses;
            testInputOutputService.testPartnerDispatcherTaskFactory.ReturnParentErrorAfterNum = returnParentErrorAfterNum;

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
            
            //Assert.AreEqual(returnParentErrorAfterNum, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            //Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            //Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            //Assert.AreEqual(1, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestSameGroupMultiStreamWithChildError(bool rawInput)
        {
            TestReset();

            ConfigMapUtil.Configuration[InputOutputConstants.DeleteCacheAfterETagConflict] = "true";
            ConfigMapUtil.Configuration[InputOutputConstants.SourceOfTruthConflictRetryDelayInMsec] = "0";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            var numResponses = 4;
            var returnChildErrorAfterNum = 2;

            testInputOutputService.testPartnerDispatcherTaskFactory.UseMultiResponses = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.NumStreamResponses = numResponses;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseSameGroups = true;
            testInputOutputService.testOutputBlobClient.ReturnETagConflictAfterNum = returnChildErrorAfterNum;

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

            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(returnChildErrorAfterNum, testInputOutputService.testOutputBlobClient.NumUploadCall);
            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumDownloadCall);
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumDeleteCall);  // cache reset after Etag
            Assert.AreEqual(returnChildErrorAfterNum - 1, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);

            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(returnChildErrorAfterNum - 1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(returnChildErrorAfterNum - 1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(returnChildErrorAfterNum - 1, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestSameGroupMultiStreamWithChildErrorCacheInsertError(bool rawInput)
        {
            TestReset();

            ConfigMapUtil.Configuration[InputOutputConstants.DeleteCacheAfterETagConflict] = "true";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            var numResponses = 4;
            var returnChildErrorAfterNum = 2;

            testInputOutputService.testPartnerDispatcherTaskFactory.UseMultiResponses = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.NumStreamResponses = numResponses;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseSameGroups = true;
            testInputOutputService.testOutputBlobClient.ReturnETagConflictAfterNum = returnChildErrorAfterNum;
            testInputOutputService.testCacheClient.ReturnInsertErrorAfterNum = returnChildErrorAfterNum;

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

            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(returnChildErrorAfterNum, testInputOutputService.testOutputBlobClient.NumUploadCall);

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumDownloadCall);
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumDeleteCall);  // cache reset after Etag
            Assert.AreEqual(returnChildErrorAfterNum - 1, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumDeleteCall);

            Assert.AreEqual(returnChildErrorAfterNum - 1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(returnChildErrorAfterNum - 1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(returnChildErrorAfterNum - 1, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestSameGroupMultiStreamWithChildErrorNoOutputCache(bool rawInput)
        {
            TestReset();

            // Update the value
            ConfigMapUtil.Configuration[SolutionConstants.UseOutputCache] = "false";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            var numResponses = 4;
            var returnChildErrorAfterNum = 2;

            testInputOutputService.testPartnerDispatcherTaskFactory.UseMultiResponses = true;
            testInputOutputService.testPartnerDispatcherTaskFactory.NumStreamResponses = numResponses;
            testInputOutputService.testPartnerDispatcherTaskFactory.UseSameGroups = true;
            testInputOutputService.testOutputBlobClient.ReturnETagConflictAfterNum = returnChildErrorAfterNum;

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

            Assert.AreEqual(returnChildErrorAfterNum, testInputOutputService.testOutputBlobClient.NumUploadCall);
            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumDownloadCall); // no cache
            
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(1, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(returnChildErrorAfterNum - 1, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(returnChildErrorAfterNum - 1, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(returnChildErrorAfterNum - 1, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestPartnerEmptyResponse(bool rawInput)
        {
            TestReset();

            testInputOutputService.testPartnerDispatcherTaskFactory.ReturnEmpty = true;
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

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient._cache.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestPartnerException(bool rawInput)
        {
            TestReset();
            testInputOutputService.testPartnerDispatcherTaskFactory.ReturnError = true;
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

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient._cache.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestPartnerExceptionRetryError(bool rawInput)
        {
            TestReset();
            testInputOutputService.testPartnerDispatcherTaskFactory.ReturnError = true;
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

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient._cache.Count);

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

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestEventHubWriteError(bool rawInput)
        {
            TestReset();

            SolutionInputOutputService.TestEventHubWriters[0].ReturnException = true;
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
            Assert.AreEqual(1, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestEventHubWriteErrorAndMovingToNextChannelError(bool rawInput)
        {
            TestReset();

            SolutionInputOutputService.TestEventHubWriters[0].ReturnException = true;
            SolutionInputOutputService.UnitTestBeforeMovingToNextChannelError = true;

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

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(1, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);

            SolutionInputOutputService.UnitTestBeforeMovingToNextChannelError = false;
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestEventHubWriterErrorRetryError(bool rawInput)
        {
            // EventHubWriter Fail -> Retry Error
            TestReset();
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

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestRawInput()
        {
            TestReset();
            var inputData = ResourcesConstants.SingleEventGridEventWithEventArrayBinaryData;
            var rawInputEventTaskContext = await testInputOutputService.AddRawInputAsync(inputData, TEST_PARTITION_ID).ConfigureAwait(false);

            Assert.AreEqual(EventTaskFinalStage.SUCCESS, rawInputEventTaskContext.EventFinalStage);
            Assert.AreEqual(false, ((EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack).IsTaskCancelled);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack).IsCompleted);
            Assert.AreEqual(true, ((EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack).IsTaskSuccess);
            Assert.AreEqual(1, ((EventHubAsyncTaskInfo)rawInputEventTaskContext.EventTaskCallBack).numCleanupCalled);

            Assert.AreEqual(1, testInputOutputService.testOutputBlobClient.NumUploadCall);

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

            Assert.AreEqual(true, rawInputEventTaskContext.HasTaskDisposed);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestAllowedTrafficTuner(bool rawInput)
        {
            TestReset();

            var tunerRule = $"includedsubscriptions: {ResourcesConstants.MicrosoftTenantId}={ResourcesConstants.TestSubscriptionId};messageretrycutoffcount: 5";
            ConfigMapUtil.Configuration[InputOutputConstants.TrafficTunerRuleKey] = tunerRule;
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
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

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestNoTrafficTuner(bool rawInput)
        {
            TestReset();

            var tunerRule = $"stopalltenants: true";
            ConfigMapUtil.Configuration[InputOutputConstants.TrafficTunerRuleKey] = tunerRule;
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
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

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "RawInput")]
        [DataRow(false, DisplayName = "SingleInput")]
        public async Task TestNotAllowedResourceTypeTrafficTuner(bool rawInput)
        {
            TestReset();

            var sub1 = Guid.NewGuid().ToString();
            var sub2 = Guid.NewGuid().ToString();
            var tunerRule = $"includedSubscriptions: {ResourcesConstants.MicrosoftTenantId}={sub1},{sub2};messageretrycutoffcount: 5;excludedResourceTypes: Microsoft.Compute/virtualMachineScaleSets";
            ConfigMapUtil.Configuration[InputOutputConstants.TrafficTunerRuleKey] = tunerRule;
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
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

            Assert.AreEqual(0, testInputOutputService.testOutputBlobClient.NumUploadCall);
            // OutputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueWithExpiryAsync);
            // InputCache
            Assert.AreEqual(0, testInputOutputService.testCacheClient.NumSetValueIfGreaterThanWithExpiryAsync);

            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestEventHubWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestRetryQueueWriters[0].testEventBatchDataList.Count);

            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventDataCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].NumEventBatchCreated);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventDataList.Count);
            Assert.AreEqual(0, SolutionInputOutputService.TestPoisonQueueWriters[0].testEventBatchDataList.Count);

            await ConcurrencyCheck().ConfigureAwait(false);
        }

        private void TestReset()
        {
            if (SolutionInputOutputService.TestEventHubWriters != null)
            {
                foreach (var testEventWriter in SolutionInputOutputService.TestEventHubWriters)
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

            SolutionInputOutputService.UnitTestBeforeMovingToNextChannelError = false;
            SolutionInputOutputService.UseSourceOfTruth = true;

            if (testInputOutputService != null)
            {
                testInputOutputService.testPartnerDispatcherTaskFactory.Clear();
                testInputOutputService.testOutputBlobClient.Clear();
                testInputOutputService.testCacheClient.Clear();
            }
        }
    }
}

