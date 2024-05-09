namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.EventHubManagement
{
    using global::Azure.Messaging.EventHubs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement;
    using System;

    [TestClass]
    public class EventHubProcessorOptionsTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(config, false);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public void TestConstructor()
        {
            var consumerGroup = "testConsumerGroup";
            var eventProcessorClientOptions = new EventProcessorClientOptions();
            var eventHubProcessorOptions = new EventHubProcessorOptions(eventProcessorClientOptions, consumerGroup);

            Assert.AreEqual(eventProcessorClientOptions, eventHubProcessorOptions.ProcessorClientOptions);
            Assert.AreEqual(consumerGroup, eventHubProcessorOptions.ConsumerGroup);
            Assert.IsNull(eventHubProcessorOptions.GetStartPositionWhenNoCheckpoint(null, null, null, null));


            var eventHubProcessorOptions2 = new EventHubProcessorOptions(eventHubProcessorOptions);
            Assert.AreEqual(eventProcessorClientOptions, eventHubProcessorOptions2.ProcessorClientOptions);
            Assert.AreEqual(consumerGroup, eventHubProcessorOptions2.ConsumerGroup);
            Assert.IsNull(eventHubProcessorOptions2.GetStartPositionWhenNoCheckpoint(null, null, null, null));
        }

        [TestMethod]
        public void TestSetStartPositionWhenNoCheckpoint()
        {
            var consumerGroup = "testConsumerGroup";
            var eventProcessorClientOptions = new EventProcessorClientOptions();
            var eventHubProcessorOptions = new EventHubProcessorOptions(eventProcessorClientOptions, consumerGroup);

            eventHubProcessorOptions.SetStartPositionWhenNoCheckpoint(TimeSpan.FromSeconds(10), DateTimeOffset.UtcNow, null);

            Assert.AreEqual(eventProcessorClientOptions, eventHubProcessorOptions.ProcessorClientOptions);
            Assert.AreEqual(consumerGroup, eventHubProcessorOptions.ConsumerGroup);
            Assert.IsNotNull(eventHubProcessorOptions.GetStartPositionWhenNoCheckpoint("a", "b", "1", null));
        }
    }
}