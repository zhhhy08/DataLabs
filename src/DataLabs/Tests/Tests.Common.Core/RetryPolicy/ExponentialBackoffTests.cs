namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.RetryPolicy
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy;
    using System;

    /// <summary>
    /// Exponential back-off tests
    /// </summary>
    [TestClass]
    public class ExponentialBackoffTests
    {
        [TestMethod]
        public void TestDefaultFirstRetryConfigValue()
        {
            //6;00:00:01;1.00:00:00;00:00:30
            var strategy = new ExponentialBackoff(6,
                TimeSpan.Parse("00:00:01"),
                TimeSpan.Parse("1.00:00:00"),
                TimeSpan.Parse("00:00:30"));
            for (int i = 0; i < 6; i++)
            {
                int retryCount = i;
                var shouldRetry = strategy.ShouldRetry(retryCount, null, out var retryAfter);
                Assert.IsTrue(shouldRetry);
                Console.WriteLine($"Retry Count: {retryCount}, Retry After: {retryAfter}");
            }
        }

        [TestMethod]
        public void TestDefaultSecondRetryConfigValue()
        {
            //6;01:00:00;05:20:00;01:00:00"
            var strategy = new ExponentialBackoff(6,
                TimeSpan.Parse("01:00:00"),
                TimeSpan.Parse("05:20:00"),
                TimeSpan.Parse("01:00:00"));
            for (int i = 0; i < 6; i++)
            {
                int retryCount = i;
                var shouldRetry = strategy.ShouldRetry(retryCount, null, out var retryAfter);
                Assert.IsTrue(shouldRetry);
                Console.WriteLine($"Retry Count: {retryCount}, Retry After: {retryAfter}");
            }
        }

        /// <summary>
        /// Tests the zeroth retry.
        /// </summary>
        [TestMethod]
        public void TestZerothRetry()
        {
            var minBackoff = TimeSpan.FromSeconds(1);
            var strategy = new ExponentialBackoff(
                100, minBackoff, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            var shouldRetry = strategy.ShouldRetry(0, null, out var retryAfter);

            Assert.IsTrue(shouldRetry);
            Assert.AreEqual(minBackoff, retryAfter);
        }

        /// <summary>
        /// Tests the minimum back-off.
        /// </summary>
        [TestMethod]
        public void TestMinBackoff()
        {
            var minBackoff = TimeSpan.FromMinutes(1);
            var strategy = new ExponentialBackoff(
                100, minBackoff, TimeSpan.FromHours(1), TimeSpan.FromSeconds(1));
            var shouldRetry = strategy.ShouldRetry(0, null, out var retryAfter);

            Assert.IsTrue(shouldRetry);
            Assert.AreEqual(minBackoff, retryAfter);
        }

        /// <summary>
        /// Tests the exponential back-off on the 1st retry.
        /// </summary>
        [TestMethod]
        public void TestExponentialBackoffFirstRetry()
        {
            var strategy = new ExponentialBackoff(
                100, TimeSpan.Zero, TimeSpan.FromHours(1), TimeSpan.FromSeconds(60));
            var shouldRetry = strategy.ShouldRetry(1, null, out var retryAfter);

            Assert.IsTrue(shouldRetry);
            Assert.IsTrue(retryAfter >= TimeSpan.FromSeconds(54));
            Assert.IsTrue(retryAfter <= TimeSpan.FromSeconds(66));
        }

        /// <summary>
        /// Tests the exponential back-off on the 2nd retry.
        /// </summary>
        [TestMethod]
        public void TestExponentialBackoffSecondRetry()
        {
            var strategy = new ExponentialBackoff(
                100, TimeSpan.Zero, TimeSpan.FromHours(1), TimeSpan.FromSeconds(60));
            var shouldRetry = strategy.ShouldRetry(2, null, out var retryAfter);

            Assert.IsTrue(shouldRetry);
            Assert.IsTrue(retryAfter >= TimeSpan.FromSeconds(162));
            Assert.IsTrue(retryAfter <= TimeSpan.FromSeconds(198));
        }

        /// <summary>
        /// Tests the maximum back-off.
        /// </summary>
        [TestMethod]
        public void TestMaxBackoff()
        {
            var strategy = new ExponentialBackoff(
                100, TimeSpan.Zero, TimeSpan.FromSeconds(60), TimeSpan.FromHours(1));
            var shouldRetry = strategy.ShouldRetry(5, null, out var retryAfter);

            Assert.IsTrue(shouldRetry);
            Assert.IsTrue(retryAfter >= TimeSpan.FromSeconds(54));
            Assert.IsTrue(retryAfter <= TimeSpan.FromSeconds(60));
        }

        /// <summary>
        /// Tests the retry count large enough to cause int32 overflow.
        /// </summary>
        [TestMethod]
        public void TestLargeRetryCount()
        {
            var strategy = new ExponentialBackoff(
                100, TimeSpan.Zero, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(1));
            var shouldRetry = strategy.ShouldRetry(99, null, out var retryAfter);

            Assert.IsTrue(shouldRetry);
            Assert.IsTrue(retryAfter >= TimeSpan.FromMinutes(54));
            Assert.IsTrue(retryAfter <= TimeSpan.FromMinutes(60));
        }

        /// <summary>
        /// Tests the retry count large enough to cause double overflow.
        /// </summary>
        [TestMethod]
        public void TestVeryLargeRetryCount()
        {
            var strategy = new ExponentialBackoff(
                10000, TimeSpan.Zero, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(1));
            var shouldRetry = strategy.ShouldRetry(9999, null, out var retryAfter);

            Assert.IsTrue(shouldRetry);
            Assert.IsTrue(retryAfter >= TimeSpan.FromMinutes(54));
            Assert.IsTrue(retryAfter <= TimeSpan.FromMinutes(60));
        }

        /// <summary>
        /// Tests the retry count exceeded.
        /// </summary>
        [TestMethod]
        public void TestRetryCountExceeded()
        {
            var strategy = new ExponentialBackoff(
                1, TimeSpan.Zero, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1));
            var shouldRetry = strategy.ShouldRetry(2, null, out var _);

            Assert.IsFalse(shouldRetry);
        }
    }
}
