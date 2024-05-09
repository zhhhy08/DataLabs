namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /* 
     * Sempahore in this class is readonly to make sure acquire and release in the same semaphore instance
     * If you want to use hotconfig-enabled concurrencyManager, 
     * consider to use ConfigurableConcurrencyManager
     */
    public class ConcurrencyManager : IConcurrencyManager
    {
        private static readonly ILogger<ConcurrencyManager> Logger = DataLabLoggerFactory.CreateLogger<ConcurrencyManager>();

        private const string ConcurrencyWaitTimeMetric = "ConcurrencyWaitTimeMetric";
        private static readonly Histogram<int> ConcurrencyWaitTimeMetricDuration = MetricLogger.CommonMeter.CreateHistogram<int>(ConcurrencyWaitTimeMetric);

        #region Members

        private readonly SemaphoreSlim _semaphoreSlim;
        private readonly string _name;

        private int _maxConcurrency;
        private int _pendingParallelismChange;

        #endregion

        #region Properties

        public SemaphoreSlim MaxConcurrentJobsSemaphoreSlim => _semaphoreSlim;
        public int MaxConcurrency => _maxConcurrency;
        public int NumAvailables => _semaphoreSlim.CurrentCount;
        public int NumRunning => _maxConcurrency - _semaphoreSlim.CurrentCount;

        #endregion

        public ConcurrencyManager(string name, int maxConcurrency)
        {
            GuardHelper.IsArgumentPositive(maxConcurrency);

            _name = name;
            _maxConcurrency = maxConcurrency;
            _semaphoreSlim = new SemaphoreSlim(maxConcurrency, maxCount: 20000);
            MetricLogger.CommonMeter.CreateObservableGauge<int>(name + MonitoringConstants.CONCURRENCY_MANAGER_RUNNING_PREFIX, () => NumRunning);
            MetricLogger.CommonMeter.CreateObservableGauge<int>(name + MonitoringConstants.CONCURRENCY_MANAGER_AVAILABLE_PREFIX, () => NumAvailables);
        }

        public async Task<bool> SetNewMaxConcurrencyAsync(int maxConcurrency)
        {
            if (maxConcurrency <= 0)
            {
                Logger.LogError("Concurrency must be larger than 0");
                return false;
            }

            if (Interlocked.CompareExchange(ref _pendingParallelismChange, 1, 0) == 0)
            {
                try
                {
                    var diff = maxConcurrency - _maxConcurrency;
                    if (diff > 0)
                    {
                        _semaphoreSlim.Release(diff);
                    }
                    else if (maxConcurrency > 0 && diff < 0)
                    {
                        diff = -diff;
                        for (var i = 0; i < diff; i++)
                        {
                            await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
                        }
                    }

                    Interlocked.Exchange(ref _maxConcurrency, maxConcurrency);
                }
                finally
                {
                    Interlocked.CompareExchange(ref _pendingParallelismChange, 0, 1);
                }

                return true;
            }

            return false;
        }

        public async Task AcquireResourceAsync(CancellationToken cancellationToken)
        {
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();
            var isSuccess = false;

            try
            {
                await _semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
                isSuccess = true;
                return;
            }
            finally
            {
                var concurrencyWaitElapsed = Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                RecordConcurrencyWaitTime((int)concurrencyWaitElapsed, isSuccess);
            }
        }

        public async Task<bool> AcquireResourceAsync(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            var startStopWatchTimeStamp = Stopwatch.GetTimestamp();
            var isSuccess = false;

            try
            {
                isSuccess = await _semaphoreSlim.WaitAsync(millisecondsTimeout, cancellationToken).ConfigureAwait(false);
                return isSuccess;
            }
            finally
            {
                var concurrencyWaitElapsed = Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                RecordConcurrencyWaitTime((int)concurrencyWaitElapsed, isSuccess);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordConcurrencyWaitTime(int concurrencyWaitElapsed, bool isSuccess)
        {
            ConcurrencyWaitTimeMetricDuration.Record(concurrencyWaitElapsed,
                new KeyValuePair<string, object?>(MonitoringConstants.NameDimension, _name),
                MonitoringConstants.GetSuccessDimension(success: isSuccess));
        }

        public int ReleaseResource()
        {
            return _semaphoreSlim.Release();
        }

        public void Dispose()
        {
            _semaphoreSlim.Dispose();
        }
    }
}