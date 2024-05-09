namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ServiceBusManagement
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement;

    [TestClass]
    public class ServiceBusOptionsUtilsTests
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
        public void TestServiceBusClientOptions()
        {
            int maxRetry = 5;
            int delayInMSec = 125;
            int maxDelayInSec = 12;
            int timeoutPerAttempInSec = 4;

            var clientoptions = ServiceBusOptionsUtils.CreateServiceBusClientOptions(maxRetry,
                delayInMSec,
                maxDelayInSec,
                timeoutPerAttempInSec);

            Assert.AreEqual(maxRetry, clientoptions.RetryOptions.MaxRetries);
            Assert.AreEqual(TimeSpan.FromMilliseconds(delayInMSec), clientoptions.RetryOptions.Delay);
            Assert.AreEqual(TimeSpan.FromSeconds(maxDelayInSec), clientoptions.RetryOptions.MaxDelay);
            Assert.AreEqual(TimeSpan.FromSeconds(timeoutPerAttempInSec), clientoptions.RetryOptions.TryTimeout);
        }

        [TestMethod]
        public void TestServiceBusProcessorOptions()
        {
            int concurrency = 5;
            int prefetchCount = 300;

            var processorOptions = ServiceBusOptionsUtils.CreateServiceBusProcessorOptions(TimeSpan.FromMinutes(1), concurrency, prefetchCount);

            Assert.AreEqual(false, processorOptions.AutoCompleteMessages);
            Assert.AreEqual(concurrency, processorOptions.MaxConcurrentCalls);
            Assert.AreEqual(prefetchCount, processorOptions.PrefetchCount);
            Assert.AreEqual(ServiceBusReceiveMode.PeekLock, processorOptions.ReceiveMode);
        }

        [TestMethod]
        public void TestCreateQueueOptions()
        {
            string queueName = "testQueue";
            int maxDeliveryCount = 10;
            int lockDurationInSec = 60;
            bool enableBatchedOperations = true;
            bool deadLetteringOnMessageExpiration = true;
            int ttlInDays = 7;
            long maxSizeInMegabytes = 81920;

            var queueOptions = ServiceBusOptionsUtils.CreateQueueOptions(
                queueName,
                maxDeliveryCount,
                lockDurationInSec,
                enableBatchedOperations,
                deadLetteringOnMessageExpiration,
                ttlInDays,
                maxSizeInMegabytes);

            Assert.AreEqual(queueName, queueOptions.Name);
            Assert.AreEqual(maxDeliveryCount, queueOptions.MaxDeliveryCount);
            Assert.AreEqual(TimeSpan.FromSeconds(lockDurationInSec), queueOptions.LockDuration);
            Assert.AreEqual(enableBatchedOperations, queueOptions.EnableBatchedOperations);
            Assert.AreEqual(deadLetteringOnMessageExpiration, queueOptions.DeadLetteringOnMessageExpiration);
            Assert.AreEqual(TimeSpan.FromDays(ttlInDays), queueOptions.DefaultMessageTimeToLive);
            Assert.AreEqual(maxSizeInMegabytes, queueOptions.MaxSizeInMegabytes);
        }
    }
}