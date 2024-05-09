namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /// <summary>
    /// ChainedRetryStrategy
    /// </summary>
    /// <seealso cref="Microsoft.WindowsAzure.Governance.ResourcesCache.Shared.RetryPolicy.Contracts.IRetryStrategy" />
    public class ChainedRetryStrategy : IRetryStrategy
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

        #endregion

        #region Members

        /// <summary>
        /// The retry strategies
        /// </summary>
        private readonly IList<IRetryStrategy> _retryStrategies;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="ChainedRetryStrategy"/> class.
        /// </summary>
        /// <param name="retryStrategies">The retry strategies.</param>
        public ChainedRetryStrategy(params IRetryStrategy[] retryStrategies)
        {
            GuardHelper.ArgumentNotNull(retryStrategies);

            this._retryStrategies = retryStrategies;
            this.MaxRetryCount = retryStrategies.Sum(strategy => strategy.MaxRetryCount);
        }

        /// <summary>
        /// Should retry the current retry count.
        /// </summary>
        /// <param name="currentRetryCount">The current retry count.</param>
        /// <param name="lastException">The last exception.</param>
        /// <param name="retryAfter">The retry after.</param>
        public bool ShouldRetry(int currentRetryCount, Exception lastException, out TimeSpan retryAfter)
        {
            GuardHelper.IsArgumentGreaterThanOrEqual(currentRetryCount, 0);

            var accumulatedMaxRetryCount = 0;

            for(int i = 0; i < _retryStrategies.Count; i++)
            {
                var retriesStrategy = _retryStrategies[i];
            
                if (currentRetryCount < retriesStrategy.MaxRetryCount + accumulatedMaxRetryCount)
                {
                    return retriesStrategy.ShouldRetry(
                        currentRetryCount - accumulatedMaxRetryCount, lastException, out retryAfter);
                }

                accumulatedMaxRetryCount += retriesStrategy.MaxRetryCount;
            }

            retryAfter = default;
            return false;
        }
    }
}