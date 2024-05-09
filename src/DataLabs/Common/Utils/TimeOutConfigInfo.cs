namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    public class TimeOutConfigInfo
    {
        private static readonly ILogger<TimeOutConfigInfo> Logger = DataLabLoggerFactory.CreateLogger<TimeOutConfigInfo>();

        public TimeSpan NonRetryFlowTimeOut => _nonRetryFlowTimeOut;
        public TimeSpan RetryFlowTimeOut => _retryFlowTimeOut;

        private TimeSpan _nonRetryFlowTimeOut;
        private TimeSpan _retryFlowTimeOut;
        private string _timeOutConfigValue;

        private readonly object _updatelock = new ();
        private readonly string _configkey;

        public TimeOutConfigInfo(string configKey, string defaultTimeOutString, IConfiguration configuration, bool allowMultiCallBacks = true)
        {
            GuardHelper.ArgumentNotNullOrEmpty(configKey, nameof(configKey));
            GuardHelper.ArgumentNotNullOrEmpty(defaultTimeOutString, nameof(defaultTimeOutString));

            _configkey = configKey;

            var configVal = configuration.GetValueWithCallBack<string>(configKey, UpdateTimeOutDuration, defaultTimeOutString, allowMultiCallBacks);
            if (!SetNewTimeOut(configVal))
            {
                throw new ArgumentException($"Invalid config key: {configKey}, value: {configVal}");
            }
            _timeOutConfigValue = configVal!; // This is not necessary because SetNewTimeout will set it but <Nullable>enable</Nullable> complains it
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan GetTimeOut(int retryFlowCount)
        {
            return retryFlowCount > 0 ? _retryFlowTimeOut : _nonRetryFlowTimeOut;
        }

        private bool SetNewTimeOut(string? timeoutStr)
        {
            var oldValue = _timeOutConfigValue;
            if (string.IsNullOrWhiteSpace(timeoutStr))
            {
                return false;
            }

            var (nonRetryFlowTimeout, retryFlowTimeout) = SolutionConfigMapKeyValueUtils.ParseConfigTimeOut(timeoutStr);

            lock(_updatelock)
            {
                if (Interlocked.CompareExchange(ref _timeOutConfigValue, timeoutStr, oldValue) == oldValue)
                {
                    _nonRetryFlowTimeOut = nonRetryFlowTimeout;
                    _retryFlowTimeOut = retryFlowTimeout;
                    return true;
                }
            }

            return false;
        }

        private Task UpdateTimeOutDuration(string timeoutStr)
        {
            var oldValue = _timeOutConfigValue;
            if (string.IsNullOrWhiteSpace(timeoutStr) || timeoutStr.Equals(oldValue))
            {
                // no change
                return Task.CompletedTask;
            }

            try
            {
                if (SetNewTimeOut(timeoutStr))
                {
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", _configkey, oldValue, timeoutStr);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Timeout has invalid format. value: {timeoutStr}");
            }

            return Task.CompletedTask;
        }
    }
}
