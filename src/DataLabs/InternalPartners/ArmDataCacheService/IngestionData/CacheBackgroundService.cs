namespace Microsoft.Azure.ARMDataInsights.ArmDataCacheService.IngestionData
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;

    public class CacheBackgroundService
    {
        private static readonly ActivityMonitorFactory RemoveDeletedSubscriptionsFromCacheAsyncMonitor = new ActivityMonitorFactory("CacheBackgroundService.RemoveDeletedSubscriptionsFromCacheAsync");
        private readonly ICacheClient cacheClient;
        public CacheBackgroundService(ICacheClient cacheClient)
        {
            this.cacheClient = cacheClient;
        }

        public async Task RemoveDeletedSubscriptionsFromCacheAsync()
        {
            using var tokenSource = new CancellationTokenSource();
            var cancellationToken = tokenSource.Token;
            var monitor = RemoveDeletedSubscriptionsFromCacheAsyncMonitor.ToMonitor();
            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    monitor.OnStart();

                    // TODO : Get buckets from config 
                    for (int idx = 0; idx < 2; idx++)
                    {
                        // get scores older than 60 hours
                        TimeSpan timeSpan = DateTime.UtcNow.AddHours(-60) - Constants.offSetStartTime;
                        double maxTimeStampScore = timeSpan.TotalMinutes;

                        var count = await cacheClient.SortedSetRemoveRangeByScoreAsync($"{SkuService.Common.Utilities.Constants.SubscriptionsCacheKeyPrefix}{idx}", 0, maxTimeStampScore, cancellationToken);
                        if (count < 0)
                        {
                            throw new Exception("Cache is not available");
                        }
                    }
                    monitor.OnCompleted();
                }
                catch (Exception ex)
                {
                    monitor.OnError(ex);
                }
            }
        }
    }
}
