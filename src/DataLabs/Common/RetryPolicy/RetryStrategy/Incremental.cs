namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    using System;

    /// <summary>
    /// Incremental
    /// </summary>
    public class Incremental : IRetryStrategy
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
        /// Gets the increment.
        /// </summary>
        /// <value>
        /// The increment.
        /// </value>
        public TimeSpan DeltaIncrement
        {
            get;
        }

        /// <summary>
        /// Gets the initial interval.
        /// </summary>
        /// <value>
        /// The initial interval.
        /// </value>
        public TimeSpan InitialInterval
        {
            get;
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="Incremental" /> class.
        /// </summary>
        /// <param name="maxRetryCount">The max retry count.</param>
        /// <param name="initialInterval">The initial interval.</param>
        /// <param name="increment">The increment.</param>
        public Incremental(int maxRetryCount, TimeSpan initialInterval, TimeSpan increment)
        {
            this.DeltaIncrement = increment;
            this.MaxRetryCount = maxRetryCount;
            this.InitialInterval = initialInterval;
        }

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
                retryAfter = TimeSpan.FromMilliseconds(this.InitialInterval.
                    TotalMilliseconds + (this.DeltaIncrement.TotalMilliseconds * currentRetryCount));
                return true;
            }

            retryAfter = default(TimeSpan);
            return false;
        }
    }
}