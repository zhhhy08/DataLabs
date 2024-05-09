namespace Microsoft.WindowsAzure.Governance.DataLabs.GarnetServer
{
    using System.Diagnostics.Metrics;

    public static class GarnetConstants
    {
        public const string GarnetTraceSource = "ARG.DataLabs.Garnet";
        public const string GarnetMeter = GarnetTraceSource;
        public static readonly Meter GarnetServerMeter = new(GarnetMeter, "1.0");
        public const string MeterName_SecSinceLastCheckPoint = "GarnetSecSinceLastCheckPoint";

        public const string ThreadPoolNumThreads = "ThreadPoolNumThreads";
        public const string NetworkSendThrottleMax = "NetworkSendThrottleMax";
        public const string AofSizeLimit = "AofSizeLimit";
        public const string FullCheckpointLogInterval = "FullCheckpointLogInterval";
        public const string CheckPointIntervalDuration = "CheckPointIntervalDuration";
        public const string UseBackgroundSave = "UseBackgroundSave";
        public const string LastSaveInfoIntervalDuration = "LastSaveInfoIntervalDuration";
        public const string ServerMonitorPollingPeriod = "ServerMonitorPollingPeriod";
        public const string ServerMonitorResettingPeriod = "ServerMonitorResettingPeriod";
        public const string EnableLatencyMonitor = "EnableLatencyMonitor";
        public const string MetricsSamplingFrequencyInSec = "MetricsSamplingFrequencyInSec";
        public const string CACHE_SERVICE_PORT = "CACHE_SERVICE_PORT";
        public const string HotConfigActions = "HotConfigActions";

        public const int DefaultThreadPoolNumThreads = 8192;
        public const int DefaultNetworkSendThrottleMax = 64;
        public const string DefaultAofSizeLimit = "8g"; //Checkpoint every 8G - approx 80 minutes
        public const long DefaultFullCheckpointLogInterval = 1L << 33; // 8G approx 80 minutes

        public static readonly TimeSpan DefaultCheckPointDuration = TimeSpan.FromMinutes(30); // 30 min
        public static readonly TimeSpan DefaultLastSaveInfoDuration = TimeSpan.FromMinutes(1); // 1 min

        public static readonly TimeSpan TriggerMetricsLogging = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan TriggerResetLatencyMetrics = TimeSpan.FromMinutes(3); // 3 min
        public const int DefaultMetricsSamplingFrequencyInSeconds = 10;
    }
}
