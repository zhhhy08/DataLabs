namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    using System;

    /// <summary>
    /// FixedInterval
    /// </summary>
    public class FixedInterval : IRetryStrategy
    {
        #region Properties

        /// <summary>
        /// Gets the max retry count.
        /// </summary>
        /// <value>
        /// The max retry count.
        /// </value>
        public int MaxRetryCount
        {
            get;
        }

        /// <summary>
        /// Gets the retry interval.
        /// </summary>
        /// <value>
        /// The retry interval.
        /// </value>
        public TimeSpan RetryInterval
        {
            get;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedInterval"/> class.
        /// </summary>
        /// <param name="maxRetryCount">The max retry count.</param>
        public FixedInterval(int maxRetryCount)
            : this(maxRetryCount, TimeSpan.Zero)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedInterval"/> class.
        /// </summary>
        /// <param name="maxRetryCount">The max retry count.</param>
        /// <param name="retryInterval">The retry interval.</param>
        public FixedInterval(int maxRetryCount, TimeSpan retryInterval)
        {
            this.MaxRetryCount = maxRetryCount;
            this.RetryInterval = retryInterval;
        }

        #endregion

        /// <summary>
        /// Should retry the current retry count.
        /// </summary>
        /// <param name="currentRetryCount">The current retry count.</param>
        /// <param name="lastException">The last exception.</param>
        /// <param name="retryAfter">The retry after.</param>
        public virtual bool ShouldRetry(int currentRetryCount, Exception lastException, out TimeSpan retryAfter)
        {
            if (currentRetryCount < this.MaxRetryCount)
            {
                retryAfter = this.RetryInterval;
                return true;
            }

            retryAfter = default;
            return false;
        }
    }
}