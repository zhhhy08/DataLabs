namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArmThrottle
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IArmThrottleManager
    {
        public int ARMSubscriptionBackoffMilliseconds { get; }
        public int ARMSubscriptionMinReadsRemaining { get; }

        public bool TryParseSubscriptionRateLimitHeader(HttpHeaders responseHeaders, out int remainingRead);
        public Task<bool> IsSubscriptionRateLimitExistAsync(string subscriptionId, IActivity? activity, CancellationToken cancellationToken);
        public Task<long?> GetAddedTimeStampForSubscriptionRateLimitAsync(string subscriptionId, CancellationToken cancellationToken);
        public Task<bool> AddSubscriptionRateLimitAsync(string subscriptionId, int currentRemainingRead, CancellationToken cancellationToken);
    }
}
