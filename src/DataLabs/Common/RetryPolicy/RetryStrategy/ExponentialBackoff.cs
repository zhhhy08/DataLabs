namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    using System;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /// <summary>
    ///  ExponentialBackoff
    /// </summary>
    public class ExponentialBackoff : IRetryStrategy
    {
        #region Constants

        /// <summary>
        /// The default backoff variance
        /// </summary>
        private const double DefaultBackoffVariance = 0.1;

        #endregion

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
        /// Gets the min backoff.
        /// </summary>
        /// <value>
        /// The min backoff.
        /// </value>
        public TimeSpan MinBackoff
        {
            get;
        }

        /// <summary>
        /// Gets the max backoff.
        /// </summary>
        /// <value>
        /// The max backoff.
        /// </value>
        public TimeSpan MaxBackoff
        {
            get;
        }

        /// <summary>
        /// Gets the delta backoff.
        /// </summary>
        /// <value>
        /// The delta backoff.
        /// </value>
        public TimeSpan DeltaBackoff
        {
            get;
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="ExponentialBackoff"/> class.
        /// </summary>
        /// <param name="maxRetryCount">The maximum retry count.</param>
        /// <param name="minBackoff">The minimum backoff.</param>
        /// <param name="maxBackoff">The maximum backoff.</param>
        /// <param name="deltaBackoff">The delta backoff.</param>
        public ExponentialBackoff(int maxRetryCount,
            TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff)
        {
            this.MinBackoff = minBackoff;
            this.MaxBackoff = maxBackoff;
            this.DeltaBackoff = deltaBackoff;
            this.MaxRetryCount = maxRetryCount;
        }

        /// <summary>
        /// Should retry the current retry count.
        /// </summary>
        /// <param name="currentRetryCount">The current retry count.</param>
        /// <param name="lastException">The last exception.</param>
        /// <param name="retryAfter">The retry after.</param>
        public virtual bool ShouldRetry(
            int currentRetryCount, Exception lastException, out TimeSpan retryAfter)
        {
            if (currentRetryCount < this.MaxRetryCount)
            {
                var deltaMilliseconds = (Math.Pow(2.0, currentRetryCount) - 1.0)
                    * this.DeltaBackoff.TotalMilliseconds
                    * ThreadSafeRandom.NextDouble(1 - DefaultBackoffVariance, 1 + DefaultBackoffVariance);

                retryAfter = deltaMilliseconds < (this.MaxBackoff - this.MinBackoff).TotalMilliseconds
                    ? this.MinBackoff + TimeSpan.FromMilliseconds(deltaMilliseconds)
                    : TimeSpan.FromMilliseconds(this.MaxBackoff.TotalMilliseconds*(ThreadSafeRandom.NextDouble(1 - DefaultBackoffVariance, 1)));

                return true;
            }

            retryAfter = default;
            return false;
        }
    }
}