namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    using System;

    /// <summary>
    /// NoRetry
    /// </summary>
    public class NoRetry : IRetryStrategy
    {
        #region Properties

        /// <summary>
        /// Gets the max retry count.
        /// </summary>
        /// <value>
        /// The max retry count.
        /// </value>
        public int MaxRetryCount => 0;

        #endregion

        /// <summary>
        /// Should retry the current retry count.
        /// </summary>
        /// <param name="currentRetryCount">The current retry count.</param>
        /// <param name="lastException">The last exception.</param>
        /// <param name="retryAfter">The retry after.</param>
        public virtual bool ShouldRetry(int currentRetryCount, Exception lastException, out TimeSpan retryAfter)
        {
            retryAfter = default(TimeSpan);
            return false;
        }
    }
}