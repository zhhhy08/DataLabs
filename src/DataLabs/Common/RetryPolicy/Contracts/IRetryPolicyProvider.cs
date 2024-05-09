namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    /// <summary>
    /// IRetryPolicyProvider
    /// </summary>
    public interface IRetryPolicyProvider
    {
        /// <summary>
        /// The retry policy.
        /// </summary>
        IRetryPolicy RetryPolicy { get; }
    }
}