namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Threading.Tasks;
    using System.Timers;
    using Microsoft.Extensions.FileSystemGlobbing;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    /// <summary>
    /// A base class for tasks that execute on scheduled intervals. 
    /// This class extends .net Timer and the interval can be updated from configuration(hotconfig)
    /// 
    /// Because this class extends .NET Timer class, 
    ///   in addition to extend all .NET Timer functionality, 
    ///   this class also provides methods which safely runs timer function where exception is automatically caught 
    ///
    /// 
    /// .NET Description
    /// https://learn.microsoft.com/en-us/dotnet/api/system.timers.timer?view=net-8.0
    //  The Timer component catches and suppresses all exceptions thrown by event handlers for the Elapsed event.
    //  Note, however, that this is not true of event handlers that execute asynchronously and include the await operator (in C#).
    //  Exceptions thrown in these event handlers are propagated back to the calling thread,
    //

    /// </summary>
    public class ConfigurableTimer : System.Timers.Timer
    {
        public delegate Task ElapsedEventHandlerAsync(object? sender, ElapsedEventArgs e);
        public delegate void ElapsedEventHandler(object? sender, ElapsedEventArgs e);

        private static readonly ILogger<ConfigurableTimer> Logger =
            DataLabLoggerFactory.CreateLogger<ConfigurableTimer>();

        public new double Interval
        {
            get => base.Interval;
            set 
            {
                base.Interval = value;
                _timeInterval = TimeSpan.FromMilliseconds(value);
            }
        }

        public TimeSpan CurrentIntervalConfigValue => _timeInterval;

        private TimeSpan _timeInterval;
        private readonly string _configName;
        private readonly object _lock = new ();

        private ElapsedEventHandler? _safeEventHandler;
        private ElapsedEventHandlerAsync? _safeEventHandlerAsync;
        
        public ConfigurableTimer(string configName, TimeSpan initInterval, bool allowMultiCallBacks = true) : base()
        {
            // config should have TimeSpan format
            _configName = configName;
            _timeInterval = ConfigMapUtil.Configuration.GetValueWithCallBack<TimeSpan>(configName, UpdateInterval, initInterval, allowMultiCallBacks: allowMultiCallBacks);
            AutoReset = true;
        }

        /* 
         * This method doesn't support multiple handler add
         * If you want to use multiple handlers add, please use timer's Elapsed directly
         */
        public void AddTimeEventHandlerSafely(ElapsedEventHandler elapsedEventHandler)
        {
            lock (_lock)
            {
                if (_safeEventHandler != null || _safeEventHandlerAsync != null)
                {
                    throw new InvalidOperationException("This method doesn't support multiple handler add");
                }

                _safeEventHandler = elapsedEventHandler;
                Elapsed += SafeInnerTimerHandler;
            }
        }

        /* 
         * This method doesn't support multiple handler add
         * If you want to use multiple handlers add, please use timer's Elapsed directly
         */
        public void AddTimeEventHandlerAsyncSafely(ElapsedEventHandlerAsync elapsedEventHandlerAsync)
        {
            lock (_lock)
            {
                if (_safeEventHandler != null || _safeEventHandlerAsync != null)
                {
                    throw new InvalidOperationException("This method doesn't support multiple handler add");
                }

                _safeEventHandlerAsync = elapsedEventHandlerAsync;
                Elapsed += SafeInnerTimerHandlerAsync;
            }
        }

        public void RemoveTimeEventHandlerSafely(ElapsedEventHandler elapsedEventHandler)
        {
            lock(_lock)
            {
                if (_safeEventHandler != elapsedEventHandler)
                {
                    // Something wrong, registered handler is not the same as given handler
                    return;
                }

                // Remove Timer
                Elapsed -= SafeInnerTimerHandler;
                _safeEventHandler = null;
            }
        }

        public void RemoveTimeEventHandlerAsyncSafely(ElapsedEventHandlerAsync elapsedEventHandlerAsync)
        {
            lock (_lock)
            {
                if (_safeEventHandlerAsync != elapsedEventHandlerAsync)
                {
                    // Something wrong, registered handler is not the same as given handler
                    return;
                }

                // Remove Timer
                Elapsed -= SafeInnerTimerHandlerAsync;
                _safeEventHandlerAsync = null;
            }
        }

        private void SafeInnerTimerHandler(object? sender, ElapsedEventArgs e)
        {
            var safeEventHandler = _safeEventHandler;
            if (safeEventHandler == null)
            {
                return;
            }

            try
            {
                safeEventHandler.Invoke(sender, e);
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, "Timer {TimerName} Got Exception: {ErrorMessage}", _configName, ex.Message);
            }
        }

        private async void SafeInnerTimerHandlerAsync(object? sender, ElapsedEventArgs e)
        {
            var safeEventHandlerAsync = _safeEventHandlerAsync;
            if (safeEventHandlerAsync == null)
            {
                return;
            }

            try
            {
                await safeEventHandlerAsync.Invoke(sender, e).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Timer {TimerName} Got Exception: {ErrorMessage}", _configName, ex.Message);
            }
        }

        public new void Start()
        {
            if (_timeInterval.TotalMilliseconds <= 0)
            {
                Stop();
                return;
            }

            base.Interval = _timeInterval.TotalMilliseconds;
            base.Start();
            Logger.LogInformation("Time {TimerName} is started with interval {Interval}", _configName, _timeInterval);
        }

        public new void Stop()
        {
            base.Stop();
            Logger.LogInformation("Time {TimerName} is stopped", _configName);
        }

        public Task UpdateInterval(TimeSpan newInterval)
        {
            if (newInterval.TotalMilliseconds <= 0)
            {
                Logger.LogError("{config} must be larger than 0", _configName);
                return Task.CompletedTask;
            }

            lock (_lock)
            {
                var oldInterval = _timeInterval;

                if (newInterval.TotalMilliseconds == oldInterval.TotalMilliseconds)
                {
                    return Task.CompletedTask;
                }

                _timeInterval = newInterval;

                Logger.LogWarning("TimerInterval is changed, Config: {config}, Old: {oldVal}, New: {newVal}",
                    _configName, oldInterval, newInterval);
                Start();
            }

            return Task.CompletedTask;
        }
    }
}
