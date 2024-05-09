namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.EventWriter
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TestEmulator;

    [TestClass]
    public class BufferedEventWriterTest
    {
        private const string TestEventWriterName = "testEventWriter";

        [TestInitialize]
        public void TestInitialize()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public void TestBatchWriterUpdateCallback()
        {
            int curVal = 10;

            var configName = TestEventWriterName + SolutionConstants.EventBatchWriterConcurrencySuffix;
            ConfigMapUtil.Configuration[configName] = curVal.ToString();

            var testEventWriter = new TestEventWriter(4096);
            var testWriterCallBack = new TestEventWriterCallBack();

            var bufferedEventWriter =
                new BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>(testEventWriter, TestEventWriterName);
            bufferedEventWriter.EventWriterCallBack = testWriterCallBack;

            var fieldValue = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>),
                               "_maxBatchWriters", bufferedEventWriter);

            Assert.AreEqual(curVal, fieldValue);

            // Increase the value
            var newVal = curVal + 10;
            ConfigMapUtil.Configuration[configName] = newVal.ToString();
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            fieldValue = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>),
                               "_maxBatchWriters", bufferedEventWriter);

            Assert.AreEqual(newVal, fieldValue);
            curVal = newVal;

            // Decrease the value
            newVal = curVal - 15;
            ConfigMapUtil.Configuration[configName] = newVal.ToString();
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            fieldValue = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>),
                               "_maxBatchWriters", bufferedEventWriter);

            Assert.AreEqual(newVal, fieldValue);
        }

        [TestMethod]
        public void TestMaxBatchSizeUpdateCallback()
        {
            int curVal = 10;

            var configName = TestEventWriterName + SolutionConstants.EventBatchMaxSizeSuffix;
            ConfigMapUtil.Configuration[configName] = curVal.ToString();

            var testEventWriter = new TestEventWriter(4096);
            var testWriterCallBack = new TestEventWriterCallBack();

            var bufferedEventWriter =
                new BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>(testEventWriter, TestEventWriterName);
            bufferedEventWriter.EventWriterCallBack = testWriterCallBack;

            var fieldValue = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>),
                               "_maxBatchSize", bufferedEventWriter);

            Assert.AreEqual(curVal, fieldValue);

            // Increase the value
            var newVal = curVal + 10;
            ConfigMapUtil.Configuration[configName] = newVal.ToString();
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            fieldValue = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>),
                               "_maxBatchSize", bufferedEventWriter);

            Assert.AreEqual(newVal, fieldValue);
            curVal = newVal;

            // Decrease the value
            newVal = curVal - 15;
            ConfigMapUtil.Configuration[configName] = newVal.ToString();
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            fieldValue = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>),
                               "_maxBatchSize", bufferedEventWriter);

            Assert.AreEqual(newVal, fieldValue);
        }

        [TestMethod]
        public void TestBatchWriterTimeoutUpdateCallback()
        {
            int curVal = 10;

            var configName = TestEventWriterName + SolutionConstants.EventBatchWriterTimeOutInSecSuffix;
            ConfigMapUtil.Configuration[configName] = curVal.ToString();

            var testEventWriter = new TestEventWriter(4096);
            var testWriterCallBack = new TestEventWriterCallBack();

            var bufferedEventWriter =
                new BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>(testEventWriter, TestEventWriterName);
            bufferedEventWriter.EventWriterCallBack = testWriterCallBack;

            var fieldValue = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>),
                               "_batchWriteTimeout", bufferedEventWriter);
            fieldValue = (int)((TimeSpan)fieldValue).TotalSeconds;

            Assert.AreEqual(curVal, fieldValue);

            // Increase the value
            var newVal = curVal + 10;
            ConfigMapUtil.Configuration[configName] = newVal.ToString();
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            fieldValue = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>),
                               "_batchWriteTimeout", bufferedEventWriter);
            fieldValue = (int)((TimeSpan)fieldValue).TotalSeconds;

            Assert.AreEqual(newVal, fieldValue);
            curVal = newVal;

            // Decrease the value
            newVal = curVal - 15;
            ConfigMapUtil.Configuration[configName] = newVal.ToString();
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            fieldValue = PrivateFunctionAccessHelper.GetPrivateField(
                               typeof(BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>),
                               "_batchWriteTimeout", bufferedEventWriter);
            fieldValue = (int)((TimeSpan)fieldValue).TotalSeconds;

            Assert.AreEqual(newVal, fieldValue);
        }

        [TestMethod]
        public async Task TestAddEventMessage()
        {
            var testEventWriter = new TestEventWriter(4096);
            var testWriterCallBack = new TestEventWriterCallBack();

            var bufferedEventWriter =
                new BufferedEventWriter<TestEventOutputContext, TestEventData, TestEventBatchData>(testEventWriter, TestEventWriterName);
            bufferedEventWriter.EventWriterCallBack = testWriterCallBack;

            var data = new BinaryData(new byte[] { 1, 2, 3, 4, 5 });
            var testEventOutputContext = new TestEventOutputContext();
            testEventOutputContext.OutputData = data;

            var isSyncWrite = await bufferedEventWriter.AddEventMessageAsync(testEventOutputContext).ConfigureAwait(false);
            Assert.IsFalse(isSyncWrite);

            Thread.Sleep(50);

            Assert.AreEqual(1, testEventWriter.NumEventDataCreated);
            Assert.AreEqual(1, testEventWriter.NumEventBatchCreated);
        }
    }
}

