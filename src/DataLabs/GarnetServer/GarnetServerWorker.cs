namespace Microsoft.WindowsAzure.Governance.DataLabs.GarnetServer
{
    using Garnet;
    using Garnet.server;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.WindowsAzure.Governance.DataLabs.GarnetServer.HotConfigUtils;

    [ExcludeFromCodeCoverage]
    public class GarnetServerWorker : IHostedService
    {
        private static readonly ILogger<GarnetServerWorker> Logger = DataLabLoggerFactory.CreateLogger<GarnetServerWorker>();
        public static string[] CommandArgs;

        private GarnetServer _garnetServer;
        private CheckPointManager _checkPointManager;

        private readonly int _threadPoolNumThreads;
        private readonly int _networkSendThrottleMax;
        private readonly string _aofSizeLimit;
        private readonly long _fullCheckpointLogInterval;

        private readonly TimeSpan _pollingPeriod;
        private readonly TimeSpan _resettingPeriod;

        private readonly int _metricsSamplingFrequency;
        private readonly bool _enableLatencyMonitor;

        public GarnetServerWorker(IHostApplicationLifetime appLifetime)
        {
            appLifetime.ApplicationStarted.Register(OnStarted);
            appLifetime.ApplicationStopping.Register(OnStopping);
            appLifetime.ApplicationStopped.Register(OnStopped);

            _threadPoolNumThreads = ConfigMapUtil.Configuration.GetValue(GarnetConstants.ThreadPoolNumThreads, GarnetConstants.DefaultThreadPoolNumThreads);
            _networkSendThrottleMax = ConfigMapUtil.Configuration.GetValue(GarnetConstants.NetworkSendThrottleMax, GarnetConstants.DefaultNetworkSendThrottleMax);
            _aofSizeLimit = ConfigMapUtil.Configuration.GetValue(GarnetConstants.AofSizeLimit, GarnetConstants.DefaultAofSizeLimit);
            _fullCheckpointLogInterval = ConfigMapUtil.Configuration.GetValue(GarnetConstants.FullCheckpointLogInterval, GarnetConstants.DefaultFullCheckpointLogInterval);

            // ServerMonitor
            _pollingPeriod = ConfigMapUtil.Configuration.GetValue<TimeSpan>(GarnetConstants.ServerMonitorPollingPeriod, GarnetConstants.TriggerMetricsLogging);
            _resettingPeriod = ConfigMapUtil.Configuration.GetValue<TimeSpan>(GarnetConstants.ServerMonitorResettingPeriod, GarnetConstants.TriggerResetLatencyMetrics);

            _enableLatencyMonitor = ConfigMapUtil.Configuration.GetValue(GarnetConstants.EnableLatencyMonitor, true);
            _metricsSamplingFrequency = ConfigMapUtil.Configuration.GetValue(GarnetConstants.MetricsSamplingFrequencyInSec, GarnetConstants.DefaultMetricsSamplingFrequencyInSeconds);

            InitializeServer();
        }

        private void SetServerParams(Options serverSettings)
        {
            // checkpointing settings
            if (serverSettings.EnableAOF.GetValueOrDefault() && string.IsNullOrWhiteSpace(serverSettings.AofSizeLimit))
            {
                serverSettings.AofSizeLimit = _aofSizeLimit;
            }

            if (!string.IsNullOrWhiteSpace(serverSettings.CheckpointDir) && !Directory.Exists(serverSettings.CheckpointDir))
                Directory.CreateDirectory(serverSettings.CheckpointDir);

            if (!string.IsNullOrWhiteSpace(serverSettings.LogDir) && !Directory.Exists(serverSettings.LogDir))
                Directory.CreateDirectory(serverSettings.LogDir);

            serverSettings.NetworkSendThrottleMax = _networkSendThrottleMax;
            serverSettings.ThreadPoolMaxThreads = _threadPoolNumThreads;

            serverSettings.LatencyMonitor = _enableLatencyMonitor;
            serverSettings.MetricsSamplingFrequency = _metricsSamplingFrequency;
        }

        private void InitializeServer()
        {
            ILoggerFactory loggerFactory = DataLabLoggerFactory.GetLoggerFactory();
            ServerSettingsManager.TryParseCommandLineArguments(CommandArgs, out var serverSettings, out _, loggerFactory.CreateLogger("CommandLineArgs"));
            if (serverSettings == null) return;
            SetServerParams(serverSettings);

            // Assign values to GarnetServerOptions
            var serverOptions = serverSettings.GetServerOptions(loggerFactory.CreateLogger("Options"));

            // set full checkpoint interval
            serverOptions.FullCheckpointLogInterval = _fullCheckpointLogInterval;

            var server = new GarnetServer(serverOptions, loggerFactory);
            
            // Register custom command on raw strings (SETIFPM = "set if prefix match")
            server.Register.NewCommand("SETIFPM", 2, CommandType.ReadModifyWrite, new SetIfPMCustomCommand());

            // Register custom command on raw strings (SETWPIFPGT = "set with prefix, if prefix greater than")
            server.Register.NewCommand("SETWPIFPGT", 2, CommandType.ReadModifyWrite, new SetWPIFPGTCustomCommand());

            // Register custom command on raw strings (DELIFM = "delete if value matches")
            server.Register.NewCommand("DELIFM", 1, CommandType.ReadModifyWrite, new DeleteIfMatchCustomCommand());

            // Register custom commands on objects
            var factory = new MyDictFactory();
            server.Register.NewCommand("MYDICTSET", 2, CommandType.ReadModifyWrite, factory);
            server.Register.NewCommand("MYDICTGET", 1, CommandType.Read, factory);

            // Register stored transactional procedure
            server.Register.NewTransactionProc("READWRITETX", 3, () => new ReadWriteTxn());

            // Register ARG procedures
            server.Register.NewTransactionProc("ARGUPDATETX", 12, () => new ARGPacificUpdateResourceTxn());
            server.Register.NewTransactionProc("ARGDELETETX", 10, () => new ARGPacificDeleteResourceTxn());

         
            _garnetServer = server;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Start the server
            _garnetServer.Start();
            ILoggerFactory loggerFactory = DataLabLoggerFactory.GetLoggerFactory();
            
            // Start CheckPoint Manager
            _checkPointManager = new CheckPointManager();
            _checkPointManager.Start();

            var monitor = new ServerMonitor(server: _garnetServer, pollingPeriod: _pollingPeriod, resettingPeriod: _resettingPeriod, logger: loggerFactory.CreateLogger("servermonitor"));
            monitor.Start();
            Logger.LogInformation("1. StartAsync has been called.");

            // Create Temporary HotConfigManager
            new GarnetHotConfigManager();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("4. StopAsync has been called.");

            _checkPointManager.Dispose();
            _garnetServer.Dispose();

            return Task.CompletedTask;
        }

        private void OnStarted()
        {
            Logger.LogInformation("2. OnStarted has been called.");
        }

        private void OnStopping()
        {
            Logger.LogInformation("3. OnStopping has been called.");
        }

        private void OnStopped()
        {
            Logger.LogInformation("5. OnStopped has been called.");
        }
    }
}