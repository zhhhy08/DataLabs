namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.RetryPolicy
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;

    /// <summary>
    /// ChainedRetryStrategyTests
    /// </summary>
    [TestClass]
    public class ChainedRetryStrategyTests
    {
        [TestMethod]
        public void TestRetriesForRetryQueue()
        {
            for (var i = 0; i < 1000; i++)
            {
                TestRetriesForRetryQueueOnce();
            }
        }

        [TestMethod]
        public void TestStrategyRetryAfterTimespan()
        {
            var chainedRetryStrategy = new ChainedRetryStrategy(
                new FixedInterval(1, TimeSpan.FromSeconds(2)),
                new FixedInterval(1, TimeSpan.FromSeconds(3)));

            var totalWait = TimeSpan.Zero;
            for (var i = 0; i < chainedRetryStrategy.MaxRetryCount; i++)
            {
                chainedRetryStrategy.ShouldRetry(i, new ArgumentException("Still need an exception text."), out var currentValue);
                totalWait += currentValue;
            }

            Assert.AreEqual(TimeSpan.FromSeconds(5), totalWait);
        }

        [TestMethod]
        public void TestStrategyWithinBounds()
        {
            var chainedRetryStrategy = new ChainedRetryStrategy(
                GetExponentialBackOffWithRandomInterval(1, 5),
                GetExponentialBackOffWithRandomInterval(1, 5),
                GetExponentialBackOffWithRandomInterval(1, 5));

            for (var i = 0; i < chainedRetryStrategy.MaxRetryCount; i++)
            {
                Assert.IsTrue(chainedRetryStrategy.ShouldRetry(i, new ArgumentException("I needed an exception."), out var output));
            }
        }

        [TestMethod]
        public void TestExceedingBoundsFail()
        {
            var strategy = GetExponentialBackOffWithRandomInterval(1, 5);
            var chainedRetryStrategy = new ChainedRetryStrategy(strategy);

            Assert.IsFalse(chainedRetryStrategy.ShouldRetry(strategy.MaxRetryCount, new ArgumentException("I needed an exception."), out var output));
        }

        [TestMethod]
        public void TestUnderBoundsFail()
        {
            var strategy = GetExponentialBackOffWithRandomInterval(1, 5);
            var chainedRetryStrategy = new ChainedRetryStrategy(strategy);

            AssertUtils.AssertThrows<ArgumentOutOfRangeException>(() => chainedRetryStrategy.ShouldRetry(-1, new ArgumentException("I needed an exception."), out var output));
        }

        #region Private methods

        [TestMethod]
        public void TestRetriesForRetryQueueOnce()
        {
            var retryStrategy = new ChainedRetryStrategy(
                new ExponentialBackoff(6, TimeSpan.Zero, TimeSpan.FromSeconds(86400), TimeSpan.FromSeconds(30)),
                new ExponentialBackoff(6, TimeSpan.FromHours(1), TimeSpan.Parse("05:20:00"), TimeSpan.FromHours(1)));

            var total = TimeSpan.Zero;

            for (var i = 0; i < retryStrategy.MaxRetryCount; i++)
            {
                Assert.IsTrue(retryStrategy.ShouldRetry(i, new ArgumentException("Some text."),
                    out var outputTimeSpan));
                total += outputTimeSpan;
            }

            Assert.IsTrue(total >= TimeSpan.FromHours(20) && total <= TimeSpan.FromHours(24));
        }

        /// <summary>
        /// Gets the exponential back off with random interval.
        /// </summary>
        /// <param name="minValue">The minimum value.</param>
        /// <param name="maxValue">The maximum value.</param>
        public ExponentialBackoff GetExponentialBackOffWithRandomInterval(int minValue, int maxValue)
        {
            return new ExponentialBackoff(ThreadSafeRandom.Next(minValue, maxValue), TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(.5));
        }

        #endregion
    }
}