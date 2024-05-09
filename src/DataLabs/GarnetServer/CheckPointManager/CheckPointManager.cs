namespace Microsoft.WindowsAzure.Governance.DataLabs.GarnetServer
{
    using StackExchange.Redis;
    using System;
    using System.Threading.Tasks;
    using System.Timers;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    public class CheckPointManager : IDisposable
    {
        private static readonly ILogger<CheckPointManager> Logger =
            DataLabLoggerFactory.CreateLogger<CheckPointManager>();

        private static readonly ActivityMonitorFactory CheckPointManagerSendCheckPointCommandAsync = 
            new("CheckPointManager.SendCheckPointCommandAsync");

        private static readonly ActivityMonitorFactory CheckPointManagerSendLastSaveInfoCommandAsync =
            new("CheckPointManager.SendLastSaveInfoCommandAsync");

        public const string BACKGROUND_CHECKPOINT_COMMAND = "BGSAVE";
        public const string SYNC_CHECKPOINT_COMMAND = "SAVE";
        public const string LAST_CHECKPOINT_INFO_COMMAND = "LASTSAVE";

        public const string COMMAND = "command";
        public const string RESPONSE = "response";
        public const string NumOfCheckPoint = "NumOfCheckPoint";

        private readonly int _cachePort;

        private readonly double _checkPointTimerIntervalAfterInitial;
        private readonly double _initCheckPointDelay;
        private long _numOfCheckPoints;
        private readonly ConfigurableTimer _checkPointTimer;
        private readonly ConfigurableTimer _lastSaveInfoTimer;
        private long _lastSavedCheckPointUnixSec;

        private bool _useBackgroundSave;
        private volatile bool _disposed;

        private readonly object _lock = new();

        public CheckPointManager()
        {
            GarnetConstants.GarnetServerMeter.CreateObservableGauge<long>(GarnetConstants.MeterName_SecSinceLastCheckPoint, GetSecSinceLastCheckPoint);

            var cachePortEnv = Environment.GetEnvironmentVariable(GarnetConstants.CACHE_SERVICE_PORT) ?? "3278";
            _cachePort = int.Parse(cachePortEnv);
            GuardHelper.ArgumentConstraintCheck(_cachePort > 0);

            //UseBackgroundSave
            _useBackgroundSave = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(GarnetConstants.UseBackgroundSave,
                UpdateUseBackgroundSave, defaultValue: true);

            // CheckPointTimer
            _checkPointTimer = new ConfigurableTimer(GarnetConstants.CheckPointIntervalDuration, GarnetConstants.DefaultCheckPointDuration);
            _checkPointTimer.Elapsed += async (sender, e) => await CheckPointTimerHandler(sender, e);

            // Let's set random minutes for the first interval
            _checkPointTimerIntervalAfterInitial = _checkPointTimer.CurrentIntervalConfigValue.TotalMilliseconds;
            if (_checkPointTimerIntervalAfterInitial > 0)
            {
                _initCheckPointDelay = (int)TimeSpan.FromMinutes(3).TotalMilliseconds;
                _initCheckPointDelay += new Random(Guid.NewGuid().GetHashCode()).Next((int)_checkPointTimerIntervalAfterInitial);
                _checkPointTimer.Interval = _initCheckPointDelay;
            }

            // LastSaveInfoTImer
            _lastSaveInfoTimer = new ConfigurableTimer(GarnetConstants.LastSaveInfoIntervalDuration, GarnetConstants.DefaultLastSaveInfoDuration);
            _lastSaveInfoTimer.AddTimeEventHandlerAsyncSafely(LastSaveInfoTimerHandlerAsync);
        }

        public void Start()
        {
            _checkPointTimer.Start();
            _lastSaveInfoTimer.Start();
        }

        public async Task SendCheckPointCommandAsync()
        {
            using var monitor = CheckPointManagerSendCheckPointCommandAsync.ToMonitor();
            try
            {
                monitor.OnStart(false);

                var command = _useBackgroundSave ? BACKGROUND_CHECKPOINT_COMMAND : SYNC_CHECKPOINT_COMMAND;
                monitor.Activity[COMMAND] = command;
                monitor.Activity[NumOfCheckPoint] = _numOfCheckPoints;

                using var connection = ConnectionMultiplexer.Connect("127.0.0.1:" + _cachePort);
                var database = connection.GetDatabase();
                var resp = await database.ExecuteAsync(command).ConfigureAwait(false);

                //Encoding.ASCII.GetBytes("Background saving started")
                //Encoding.ASCII.GetBytes("-ERR checkpoint already in progress\r\n"));

                monitor.Activity[RESPONSE] = resp.ToString();
                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
            }
        }

        public async Task SendLastSaveInfoCommandAsync()
        {
            using var monitor = CheckPointManagerSendLastSaveInfoCommandAsync.ToMonitor();
            try
            {
                monitor.OnStart(false);

                var command = LAST_CHECKPOINT_INFO_COMMAND;
                monitor.Activity[COMMAND] = command;

                using var connection = ConnectionMultiplexer.Connect("127.0.0.1:" + _cachePort);
                var database = connection.GetDatabase();
                var resp = await database.ExecuteAsync(command).ConfigureAwait(false);

                //var seconds = storeWrapper.lastSaveTime.ToUnixTimeSeconds();

                var respString = resp.ToString();
                if (int.TryParse(respString, out int unixTimeSec))
                {
                    _lastSavedCheckPointUnixSec = unixTimeSec;
                    monitor.Activity[RESPONSE] = DateTimeOffset.FromUnixTimeSeconds(unixTimeSec).ToString();
                }
                else
                {
                    monitor.Activity[RESPONSE] = respString;
                }

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
            }
        }

        private long GetSecSinceLastCheckPoint()
        {
            if (_lastSavedCheckPointUnixSec <= 0)
            {
                return 0;
            }

            var currentUnixTimeSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long sec = currentUnixTimeSec - _lastSavedCheckPointUnixSec;
            return sec < 0 ? 0 : sec;
        }

        private async Task CheckPointTimerHandler(object? sender, ElapsedEventArgs e)
        {
            try
            {
                await SendCheckPointCommandAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Log is already done in SendCheckPointCommandAsync
            }
            finally
            {
                if (_initCheckPointDelay > 0 && _numOfCheckPoints++ == 0)
                {
                    // this is first time
                    _checkPointTimer.Interval = _checkPointTimerIntervalAfterInitial;
                }
            }
        }

        private async Task LastSaveInfoTimerHandlerAsync(object? sender, ElapsedEventArgs e)
        {
            try
            {
                await SendLastSaveInfoCommandAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Log is already done in LastSaveInfoTimerHandler
            }
        }

        private Task UpdateUseBackgroundSave(bool newValue)
        {
            var oldValue = _useBackgroundSave;
            if (oldValue != newValue)
            {
                lock(_lock)
                {
                    _useBackgroundSave = newValue;
                }

                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    GarnetConstants.UseBackgroundSave, oldValue, newValue);
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _checkPointTimer.Stop();
            _lastSaveInfoTimer.Stop();
            _disposed = true;
        }
    }
}
