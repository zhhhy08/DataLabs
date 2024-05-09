namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.EventHubManagement
{
    using global::Azure.Messaging.EventHubs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement;
    using System;

    [TestClass]
    public class EventHubUtilsTests
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
        public void TestCreateEventHubWriterConnectionOptions()
        {
            var result = (EventHubConnectionOptions)PrivateFunctionAccessHelper.RunStaticMethod(typeof(EventHubWriterOptionsUtils), "CreateEventHubWriterConnectionOptions", new object[] { 10 });
            Assert.AreEqual(10, result.SendBufferSizeInBytes);
        }

        [TestMethod]
        public void TestCreateEventHubsRetryOptions()
        {
            int maxRetry = 5;
            int delayInMSec = 125;
            int maxDelayInSec = 12;
            int timeoutPerAttempInSec = 4;

            var result = (EventHubsRetryOptions)PrivateFunctionAccessHelper.RunStaticMethod(typeof(EventHubWriterOptionsUtils), "CreateEventHubsRetryOptions", new object[] { maxRetry, delayInMSec, maxDelayInSec, timeoutPerAttempInSec });
            Assert.AreEqual(maxRetry, result.MaximumRetries);
            Assert.AreEqual(TimeSpan.FromMilliseconds(delayInMSec), result.Delay);
            Assert.AreEqual(TimeSpan.FromSeconds(maxDelayInSec), result.MaximumDelay);
            Assert.AreEqual(TimeSpan.FromSeconds(timeoutPerAttempInSec), result.TryTimeout);
        }

        [TestMethod]
        public void TestEventHubExceptionHelper()
        {
            var exception = new EventHubsException(true, "testEventHub", EventHubsException.FailureReason.ServiceBusy);
            Assert.IsTrue(EventHubExceptionHelper.IsServerBusyException(exception));
            Assert.IsFalse(EventHubExceptionHelper.IsServerTimeoutException(exception));
            Assert.IsTrue(EventHubExceptionHelper.IsTransientError(exception));

            exception = new EventHubsException(true, "testEventHub", EventHubsException.FailureReason.ServiceTimeout);
            Assert.IsFalse(EventHubExceptionHelper.IsServerBusyException(exception));
            Assert.IsTrue(EventHubExceptionHelper.IsServerTimeoutException(exception));
            Assert.IsTrue(EventHubExceptionHelper.IsTransientError(exception));

            exception = new EventHubsException(false, "testEventHub", EventHubsException.FailureReason.QuotaExceeded);
            Assert.IsFalse(EventHubExceptionHelper.IsServerBusyException(exception));
            Assert.IsTrue(EventHubExceptionHelper.IsQuotaExceeded(exception));
            Assert.IsFalse(EventHubExceptionHelper.IsTransientError(exception));


            exception = new EventHubsException(false, "testEventHub", EventHubsException.FailureReason.MessageSizeExceeded);
            Assert.IsFalse(EventHubExceptionHelper.IsServerBusyException(exception));
            Assert.IsTrue(EventHubExceptionHelper.IsQueueMessageTooLargeException(exception));
            Assert.IsFalse(EventHubExceptionHelper.IsTransientError(exception));
        }

        [TestMethod]
        public void TestConfigurableFixedRetryStrategy()
        {
            var retryStrategy = new ConfigurableFixedRetryStrategy(() => 12);
            Assert.AreEqual(12, retryStrategy.MaxRetryCount);

            var result = retryStrategy.ShouldRetry(4, null, out var retryAfter);
            Assert.AreEqual(true, result);
            Assert.AreEqual(TimeSpan.Zero, retryAfter);

            retryStrategy = new ConfigurableFixedRetryStrategy(() => 24, TimeSpan.FromSeconds(1));
            Assert.AreEqual(24, retryStrategy.MaxRetryCount);

            result = retryStrategy.ShouldRetry(100, null, out retryAfter);
            Assert.AreEqual(false, result);
            Assert.AreEqual(TimeSpan.FromSeconds(1), retryAfter);
        }

        [TestMethod]
        public void TestTransientError()
        {
            var exception = new Exception("Some msg");
            var catchErrorStrategy = new EventHubCatchPermanentErrorsStrategy();
            Assert.IsTrue(catchErrorStrategy.IsTransientError(exception));
        }

        [TestMethod]
        public void TestPermanentError()
        {
            var exception = new ArgumentOutOfRangeException("offset/sequenceNumber",
                @"Ignoring out of date checkpoint with offset 1/sequence number 2 because current persisted checkpoint has higher offset 1/sequence number 2");

            var catchErrorStrategy = new EventHubCatchPermanentErrorsStrategy();
            Assert.IsFalse(catchErrorStrategy.IsTransientError(exception));
        }
    }
}