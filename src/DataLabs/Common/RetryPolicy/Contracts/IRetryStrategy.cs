namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    using System;

    /// <summary>
    /// IRetryStrategy
    /// </summary>
    public interface IRetryStrategy
    {
        /// <summary>
        /// Gets the max retry count.
        /// </summary>
        /// <value>
        /// The max retry count.
        /// </value>
        int MaxRetryCount
        {
            get;
        }

        /// <summary>
        /// Should retry the current retry count.
        /// </summary>
        /// <param name="currentRetryCount">The current retry count.</param>
        /// <param name="lastException">The last exception.</param>
        /// <param name="retryAfter">The retry after.</param>
        bool ShouldRetry(int currentRetryCount,
            Exception lastException, out TimeSpan retryAfter);
    }
}