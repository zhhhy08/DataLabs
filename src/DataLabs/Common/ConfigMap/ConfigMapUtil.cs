// <copyright file="ConfigMapUtil.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Threading;
    using System.Timers;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    [ExcludeFromCodeCoverage]
    public class ConfigMapUtil
    {
        private static readonly ILogger<ConfigMapUtil> Logger =
            DataLabLoggerFactory.CreateLogger<ConfigMapUtil>();

        public const string LIST_DELIMITER = ";";

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. 
        private static ConfigurationWithCallBack _configuration;
        private static ConfigurableTimer? _valueChangeCheckerTimer;

        private static TimeSpan _callBackTimeout = TimeSpan.FromSeconds(30);
        private static int _configInitialized = 0;

        public static ConfigurationWithCallBack Configuration => _configuration;
        public static bool RunningInContainer { get; private set; }
        

        public static void Initialize(IConfigurationBuilder configBuilder, bool loadLocalFile=true)
        {
            if (_configInitialized > 0 ||
                Interlocked.CompareExchange(ref _configInitialized, 1, 0) != 0)
            {
                // Already initialized
#pragma warning disable CS8774 // Member must have a non-null value when exiting.
                return;
#pragma warning disable CS8774 //  Member must have a non-null value when exiting.
            }

            RunningInContainer = bool.TryParse(Environment.GetEnvironmentVariable(SolutionConstants.DOTNET_RUNNING_IN_CONTAINER),
              out var isRunningInContainer) && isRunningInContainer;

            Console.WriteLine($"Running in Container: '{RunningInContainer}'");

            if (RunningInContainer)
            {
                var configDir = Environment.GetEnvironmentVariable(SolutionConstants.CONFIGMAP_DIR);
                Console.WriteLine($"ConfigurationFolder set to: '{configDir}'");
                Console.WriteLine($"ConfigurationFolder exists: {Directory.Exists(configDir)}");

                if (!string.IsNullOrWhiteSpace(configDir))
                {
                    var fileProvider = new PhysicalFileProvider(configDir);
                    Console.WriteLine($"Adding configurations from configDir: {configDir}");
                    configBuilder.AddKeyPerFile(
                           (source =>
                           {
                               source.FileProvider = fileProvider;
                               source.Optional = false;
                               source.ReloadOnChange = true;
                               source.ReloadDelay = 1000;
                           }));
                }
            }
            else
            {
                Console.WriteLine("Relying on default configuration");
                // Add local json configuration
                string localJson = "LocalTestParameters.json";
                if (loadLocalFile && File.Exists(localJson))
                {
                    string currentDirectory = Directory.GetCurrentDirectory();
                    var fileProvider = new PhysicalFileProvider(currentDirectory);
                    configBuilder.AddJsonFile(
                        (source =>
                        {
                            source.FileProvider = fileProvider;
                            source.Path = localJson;
                            source.Optional = false;
                            source.ReloadOnChange = true;
                            source.ReloadDelay = 1000;
                        }));
                }
            }

            var config = configBuilder.Build();
            var token = config.GetReloadToken();
            token.RegisterChangeCallback(ConfigReloadedCallBack, null);

            var configWithCallBack = new ConfigurationWithCallBack(config);
            Interlocked.Exchange(ref _configuration, configWithCallBack);

            // Timer should start in the end because it is using Configuration
            InitializeAndStartTimer();
        }

        public static void Reset()
        {
            _configInitialized = 0;
            StopReloadTimer();
        }

        private static void ConfigReloadedCallBack(object? state)
        {
            Logger.LogWarning("ConfigMap is changed!");

            if (Configuration == null)
            {
                return;
            }

            using CancellationTokenSource tokenSource = new();
            tokenSource.CancelAfter(_callBackTimeout);
            Configuration.CheckChangeAndCallBack(tokenSource.Token);

            var token = Configuration.GetReloadToken();
            token.RegisterChangeCallback(ConfigReloadedCallBack, null);
        }

        private static void InitializeAndStartTimer()
        {
            if (_valueChangeCheckerTimer != null)
            {
                return;
            }

            // By default, we don't use the timer based checker, Default value is Zero
            _valueChangeCheckerTimer = new ConfigurableTimer(SolutionConstants.ConfigMapRefreshDuration, TimeSpan.Zero);
            _valueChangeCheckerTimer.Elapsed += ValueChangeCheckTimerHandler;
            _valueChangeCheckerTimer.Start();
        }

        private static void StopReloadTimer()
        {
            if (_valueChangeCheckerTimer != null)
            {
                _valueChangeCheckerTimer.Stop();
                _valueChangeCheckerTimer.Dispose();
                _valueChangeCheckerTimer = null;
            }
        }

        private static void ValueChangeCheckTimerHandler(object? sender, ElapsedEventArgs e)
        {
            if (_configInitialized == 0)
            {
                return;
            }

            var config = Configuration;
            if (config != null)
            {
                using CancellationTokenSource tokenSource = new();
                tokenSource.CancelAfter(_callBackTimeout);
                config.CheckChangeAndCallBack(tokenSource.Token);
            }
        }

    }
}
