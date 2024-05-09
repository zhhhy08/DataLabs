namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArmThrottle
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class ARMThrottleManager : IArmThrottleManager
    {
        private static readonly ILogger<ARMThrottleManager> Logger =
            DataLabLoggerFactory.CreateLogger<ARMThrottleManager>();

        private static readonly ActivityMonitorFactory ArmThrottleManagerFailedToGetSubscriptionRateLimit = 
            new ("ArmThrottleManager.FailedToGetSubscriptionRateLimit");

        private static readonly ActivityMonitorFactory ArmThrottleManagerAddSubscriptionRateLimit =
            new ("ArmThrottleManager.AddSubscriptionRateLimit");

        private static readonly CacheInsertionFailException _cacheInsertionFailException = new("Failed to Add to Cache");

        public int ARMSubscriptionBackoffMilliseconds => _armSubscriptionBackoffMilliseconds;
        public int ARMSubscriptionMinReadsRemaining => _armSubscriptionMinReadsRemaining;

        private readonly IResourceCacheClient _inputCacheClient;

        private int _armSubscriptionBackoffMilliseconds;
        private int _armSubscriptionMinReadsRemaining;

        public ARMThrottleManager(IResourceCacheClient inputCacheClient, IConfiguration configuration)
        {
            _inputCacheClient = inputCacheClient;

            _armSubscriptionMinReadsRemaining = configuration.GetValueWithCallBack<int>(
                SolutionConstants.SubscriptionARMReadSafeLimit, Update_SubscriptionARMReadSafeLimit, 5000, true);
            GuardHelper.IsArgumentPositive(_armSubscriptionMinReadsRemaining, SolutionConstants.SubscriptionARMReadSafeLimit);

            _armSubscriptionBackoffMilliseconds = configuration.GetValueWithCallBack<int>(
                SolutionConstants.SubscriptionARMReadSafeLimit_BackoffMilliseconds,
                Update_SubscriptionARMReadSafeLimit_BackoffMilliseconds, 600000, true);
            GuardHelper.IsArgumentPositive(_armSubscriptionBackoffMilliseconds, SolutionConstants.SubscriptionARMReadSafeLimit_BackoffMilliseconds);
        }

        public bool TryParseSubscriptionRateLimitHeader(HttpHeaders responseHeaders, out int remainingRead)
        {
            remainingRead = int.MaxValue;

            if (responseHeaders.TryGetValues(CommonHttpHeaders.ARMRemainingSubscriptionReads, out var headerValues))
            {
                // ARMRemainingSubscriptionReads header is present
                var armSubscriptionRemainingRead = headerValues.FirstOrDefault();
                return armSubscriptionRemainingRead?.Length > 0 && int.TryParse(armSubscriptionRemainingRead, out remainingRead);
            }
            return false;
        }

        // Check to input cache to see if subscription is throttling
        public async Task<bool> IsSubscriptionRateLimitExistAsync(string subscriptionId, IActivity? activity, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subscriptionId))
            {
                return false;
            }

            var addedTimeStamp = await GetAddedTimeStampForSubscriptionRateLimitAsync(subscriptionId: subscriptionId, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (addedTimeStamp.HasValue)
            {
                if (activity != null)
                {
                    activity[SolutionConstants.SubscriptionThrottledAddedTimeStamp] = addedTimeStamp.Value;
                }

                // cacheExpiry might not work sometimes. Let's compare the time here again just in case
                var timeElapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - addedTimeStamp.Value;
                return timeElapsed < _armSubscriptionBackoffMilliseconds;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<long?> GetAddedTimeStampForSubscriptionRateLimitAsync(string subscriptionId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subscriptionId))
            {
                return null;
            }

            try
            {
                var throttlingResourceId = GenerateArmSubscriptionRateLimitingResourceId(subscriptionId);
                return await _inputCacheClient.GetLongValueAsync(
                    key: throttlingResourceId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                using var method = ArmThrottleManagerFailedToGetSubscriptionRateLimit.ToMonitor();
                method.OnError(ex);
            }
            return null;
        }

        public async Task<bool> AddSubscriptionRateLimitAsync(string subscriptionId, int currentRemainingRead, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subscriptionId))
            {
                return false;
            }

            using var monitor = ArmThrottleManagerAddSubscriptionRateLimit.ToMonitor();
            
            try
            {
                monitor.OnStart(false);

                var throttlingResourceId = GenerateArmSubscriptionRateLimitingResourceId(subscriptionId);

                monitor.Activity[SolutionConstants.SubscriptionId] = subscriptionId;
                monitor.Activity[SolutionConstants.SubscriptionARMReadSafeLimit_BackoffMilliseconds] = _armSubscriptionBackoffMilliseconds;
                monitor.Activity[SolutionConstants.SubscriptionARMReadRemaining] = currentRemainingRead;
                monitor.Activity[SolutionConstants.SubscriptionARMReadSafeLimit] = _armSubscriptionMinReadsRemaining;

                var cacheSuccess = await _inputCacheClient.SetLongValueAsync(
                    key: throttlingResourceId,
                    value: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    expiry: TimeSpan.FromMilliseconds(_armSubscriptionBackoffMilliseconds),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                monitor.Activity[SolutionConstants.SubscriptionThrottledAddedToCache] = cacheSuccess;

                if (cacheSuccess)
                {
                    monitor.OnCompleted();
                    return true;
                }
                else
                {
                    monitor.OnError(_cacheInsertionFailException);
                    return false;
                }
            }
            catch (Exception ex)
            {
                monitor.Activity[SolutionConstants.SubscriptionThrottledAddedToCache] = false;
                monitor.OnError(ex);
                return false; // no throw exception if it fails to add to cache
            }
        }

        /// <summary>
        /// Generates ResourceId for subscriptions that exceed the ArmSubscriptionReadLimit at a given moment. Since they
        /// need to be added to the input cache as a resourceId, this util function generates a common one to use and check
        /// for.
        /// 
        /// Example Result: /subscriptions/00000000-0000-0000-0000-000000000000/SubscriptionRateLimit
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GenerateArmSubscriptionRateLimitingResourceId(string subscriptionId)
        {
            var sb = new StringBuilder(100);
            sb.Append("/subscriptions/")
                .Append(subscriptionId)
                .Append('/')
                .Append(SolutionConstants.SubscriptionRateLimit);
            return sb.ToString();
        }

        private Task Update_SubscriptionARMReadSafeLimit(int newConfigVal)
        {
            if (newConfigVal < 0)
            {
                return Task.CompletedTask;
            }

            var oldConfigVal = _armSubscriptionMinReadsRemaining;
            if (oldConfigVal != newConfigVal)
            {
                if (Interlocked.CompareExchange(ref _armSubscriptionMinReadsRemaining, newConfigVal, oldConfigVal) == oldConfigVal)
                {
                    var configKey = SolutionConstants.SubscriptionARMReadSafeLimit;
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                        configKey, oldConfigVal, newConfigVal);
                }
            }
            return Task.CompletedTask;
        }

        private Task Update_SubscriptionARMReadSafeLimit_BackoffMilliseconds(int newConfigVal)
        {
            if (newConfigVal < 0)
            {
                return Task.CompletedTask;
            }

            var oldConfigVal = _armSubscriptionBackoffMilliseconds;
            if (oldConfigVal != newConfigVal)
            {
                if (Interlocked.CompareExchange(ref _armSubscriptionBackoffMilliseconds, newConfigVal, oldConfigVal) == oldConfigVal)
                {
                    var configKey = SolutionConstants.SubscriptionARMReadSafeLimit_BackoffMilliseconds;
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                        configKey, oldConfigVal, newConfigVal);
                }
            }
            return Task.CompletedTask;
        }
    }
}
