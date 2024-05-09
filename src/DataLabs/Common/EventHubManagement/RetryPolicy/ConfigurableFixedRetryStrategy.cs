namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using System;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy;

    public class ConfigurableFixedRetryStrategy : IRetryStrategy
    {
        /// <summary>
        /// Total try count is retry count + 1.
        /// </summary>
        private readonly Func<int> _retryCountFn;

        private readonly TimeSpan _retryInterval;

        public int MaxRetryCount => _retryCountFn();

        public ConfigurableFixedRetryStrategy(Func<int> retryCountFn)
            : this(retryCountFn, TimeSpan.Zero)
        {
        }

        public ConfigurableFixedRetryStrategy(Func<int> retryCountFn, TimeSpan retryInternval)
        {
            _retryCountFn = retryCountFn;
            _retryInterval = retryInternval;
        }

        public bool ShouldRetry(int currentRetryCount, Exception lastException, out TimeSpan retryAfter)
        {
            retryAfter = _retryInterval;
            return currentRetryCount < this.MaxRetryCount;
        }
    }
}
