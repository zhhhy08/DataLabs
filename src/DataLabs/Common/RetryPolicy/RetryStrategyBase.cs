namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    public abstract class RetryStrategyBase : IRetryStrategy
    {
        private static readonly ILogger<RetryStrategyBase> Logger =
            DataLabLoggerFactory.CreateLogger<RetryStrategyBase>();

        // MaxRetry;InitialBackOff;MaxBackOff;DeltaBackoff;MaxRetry;InitialBackOff;MaxBackOff;DeltaBackoff
        // Each BackOff should be TimeSpan string format
        private const string defaultChainedRetryBackOffInfo =
            "6;00:00:01;1.00:00:00;00:00:30;6;01:00:00;05:20:00;01:00:00";

        private static IRetryStrategy _defaultRetryStrategy;
        private static string _retryBackOffInfo;

        static RetryStrategyBase()
        {
            _retryBackOffInfo = ConfigMapUtil.Configuration.GetValueWithCallBack<string>(
                SolutionConstants.ChainedRetryBackOffInfo,
                UpdateRetryStrategy, defaultChainedRetryBackOffInfo)!;

            _defaultRetryStrategy = CreateDefaultRetryStrategy(_retryBackOffInfo);
        }

        private static IRetryStrategy CreateDefaultRetryStrategy(string info)
        {
            var elements = info.ConvertToList();
            if (elements.Count != 8)
            {
                throw new ArgumentException(SolutionConstants.ChainedRetryBackOffInfo +
                    " should have 8 elements. " +
                    "MaxRetry;InitialBackOff;MaxBackOff;DeltaBackoff;MaxRetry;InitialBackOff;MaxBackOff;DeltaBackoff");
            }

            return new ChainedRetryStrategy(
                new ExponentialBackoff(
                    Int32.Parse(elements[0]),
                    TimeSpan.Parse(elements[1]),    
                    TimeSpan.Parse(elements[2]),
                    TimeSpan.Parse(elements[3])),
                new ExponentialBackoff(
                    Int32.Parse(elements[4]),
                    TimeSpan.Parse(elements[5]),
                    TimeSpan.Parse(elements[6]),
                    TimeSpan.Parse(elements[7])));
        }


        public int MaxRetryCount => _defaultRetryStrategy.MaxRetryCount;
        public IRetryStrategy GetDefaultRetryStrategy() => this;

        public bool ShouldRetry(int currentRetryCount, Exception lastException, out TimeSpan retryAfter)
        {
            // TODO
            // in the future, we could add exception specfic retry filtering. 
            // That is, some specific exception, return false
            if (!CanRetry(lastException))
            {
                retryAfter = TimeSpan.Zero;
                return false;
            }

            return _defaultRetryStrategy.ShouldRetry(currentRetryCount, lastException, out retryAfter);
        }

        public abstract bool CanRetry(Exception exception);
        public abstract IRetryStrategy GetRetryStrategy(string resourceType);
        
        private static Task UpdateRetryStrategy(string newRetryBackOffInfo)
        {
            if (string.IsNullOrEmpty(newRetryBackOffInfo) || 
                newRetryBackOffInfo.Equals(_retryBackOffInfo))
            {
                return Task.CompletedTask;
            }

            var newDefaultRetryStrategy = CreateDefaultRetryStrategy(newRetryBackOffInfo);
            var oldStrategy = _defaultRetryStrategy;
            if (Interlocked.CompareExchange(ref _defaultRetryStrategy, newDefaultRetryStrategy, oldStrategy) == oldStrategy)
            {
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.ChainedRetryBackOffInfo, _retryBackOffInfo, newRetryBackOffInfo);
                _retryBackOffInfo = newRetryBackOffInfo;
            }

            return Task.CompletedTask;
        }

    }
}