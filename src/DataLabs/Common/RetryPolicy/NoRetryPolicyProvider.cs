namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    /// <summary>
    /// Retry policy provider.
    /// </summary>
    public class NoRetryPolicyProvider : IRetryPolicyProvider
    {
        /// <summary>
        /// Provider for no retry policy.
        /// </summary>
        private static readonly IRetryPolicy NoRetryPolicy = new RetryPolicy(
            new ThrowAllErrorStrategy(),
            new NoRetry());

        /// <summary>
        /// The retry policy.
        /// </summary>
        public IRetryPolicy RetryPolicy => NoRetryPolicy;
    }
}
