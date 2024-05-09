namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SecretProviderManager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /* 
     * For Security
     * This SecretProviderManager will read the SecretProviderFolder
     * And It doesn't expose the internal IConfiguration so resetting Key/Value is not allowed
     * That is, it only expose read-Only access to the IConfiguration through explicit method GetSecretValueWithCallBack
     */
    public class SecretProviderManager
    {
        private static readonly ILogger<SecretProviderManager> Logger =
            DataLabLoggerFactory.CreateLogger<SecretProviderManager>();

        private static volatile SecretProviderManager? _instance;
        private static readonly object SyncRoot = new object();

        #region Singleton Impl

        public static SecretProviderManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new SecretProviderManager();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        private readonly Dictionary<string, CertificateListeners>? _certificateListenersMap;
        private readonly ConfigurationWithCallBack? _configuration;
        private readonly ConfigurableTimer? _valueChangeCheckerTimer;
        private readonly TimeSpan _callBackTimeout = TimeSpan.FromMinutes(1);

        private SecretProviderManager()
        {
            var secretDir = Environment.GetEnvironmentVariable(SolutionConstants.SECRETS_STORE_DIR);
            if (string.IsNullOrWhiteSpace(secretDir))
            {
                return;
            }

            // For better security, don't print secret directory
            var hasSecretDir = Directory.Exists(secretDir);
            Logger.LogWarning($"SecretStoreFolder exists: {hasSecretDir}");

            if (!hasSecretDir)
            {
                return;
            }

            var fileProvider = new PhysicalFileProvider(secretDir);
            Logger.LogWarning("Adding configurations from SecretStoreFolder");

            _certificateListenersMap = new(StringComparer.OrdinalIgnoreCase);

            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddKeyPerFile(
                    (source =>
                    {
                        source.FileProvider = fileProvider;
                        source.Optional = false;
                        source.ReloadOnChange = true;
                    }));

            var config = configBuilder.Build();
            _configuration = new ConfigurationWithCallBack(config);

            var token = config.GetReloadToken();
            token.RegisterChangeCallback(ConfigReloadedCallBack, null);

            // By default, we don't use the timer based checker, Default value is Zero
            _valueChangeCheckerTimer = new ConfigurableTimer(SolutionConstants.SecretProviderRefreshDuration, TimeSpan.Zero);
            _valueChangeCheckerTimer.Elapsed += ValueChangeCheckTimerHandler;
            _valueChangeCheckerTimer.Start();
        }

        public X509Certificate2? GetCertificateWithListener(string certificateName, ICertificateListener listener, bool allowMultiListeners)
        {
            if (_certificateListenersMap == null || _configuration == null)
            {
                return null;
            }

            lock(this)
            {
                if (_certificateListenersMap.TryGetValue(certificateName, out var certificateListeners))
                {
                    if (!allowMultiListeners)
                    {
                        // check if previous callback exist
                        throw new ArgumentException(certificateName + " is already registered with other certificate listener ");
                    }

                    certificateListeners.AddListener(listener);
                }
                else
                {
                    // New Key
                    certificateListeners = new CertificateListeners(certificateName, listener, _configuration);
                    _certificateListenersMap[certificateName] = certificateListeners;
                }

                return certificateListeners._certificate;
            }
        }

        public void RemoveListener(string certificateName, ICertificateListener listener)
        {
            if (_certificateListenersMap == null || _configuration == null)
            {
                return;
            }

            lock (this)
            {
                if (_certificateListenersMap.TryGetValue(certificateName, out var certificateListeners))
                {
                    certificateListeners.RemoveListener(listener);
                }
            }
        }

        private void ConfigReloadedCallBack(object? state)
        {
            if (_configuration == null)
            {
                return;
            }

            Logger.LogWarning("SecretStoreFolder Has change!");

            using CancellationTokenSource tokenSource = new();
            tokenSource.CancelAfter(_callBackTimeout);
            _configuration.CheckChangeAndCallBack(tokenSource.Token);

            var token = _configuration.GetReloadToken();
            token.RegisterChangeCallback(ConfigReloadedCallBack, null);
        }

        private void ValueChangeCheckTimerHandler(object? sender, ElapsedEventArgs e)
        {
            if (_configuration == null)
            {
                return;
            }

            try
            {
                using CancellationTokenSource tokenSource = new();
                tokenSource.CancelAfter(_callBackTimeout);
                _configuration.CheckChangeAndCallBack(tokenSource.Token);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "CheckChangeAndCallBack got exception. {exception}", ex.ToString());
            }
        }

        private class CertificateListeners
        {
            private static readonly ActivityMonitorFactory CertificateListenersUpdateClientCertificate = new("CertificateListeners.UpdateClientCertificate");
            private static readonly ActivityMonitorFactory CertificateListenersCriticalError = new ("CertificateListeners.CriticalError", LogLevel.Critical);

            public readonly string _certificateName;
            public string _certificateValue;
            public X509Certificate2 _certificate;
            public readonly LinkedList<ICertificateListener> _certificateListeners;

            public CertificateListeners(string certificateName, ICertificateListener firstListener, IConfiguration configuration)
            {
                lock(this)
                {
                    GuardHelper.ArgumentNotNull(firstListener);
                    _certificateListeners = new LinkedList<ICertificateListener>();
                    _certificateListeners.AddLast(firstListener);

                    _certificateName = certificateName;
                    _certificateValue = configuration.GetValueWithCallBack<string>(certificateName, UpdateClientCertificate, String.Empty)!;
                    GuardHelper.ArgumentNotNullOrEmpty(_certificateValue);

                    _certificate = CertificateUtils.CreateCertificate(_certificateValue);
                    GuardHelper.ArgumentNotNull(_certificate);
                }
            }

            public void AddListener(ICertificateListener listener)
            {
                if (listener == null)
                {
                    return;
                }

                lock (this)
                {
                    _certificateListeners.AddLast(listener);
                }
            }

            public void RemoveListener(ICertificateListener listener)
            {
                if (listener == null)
                {
                    return;
                }

                lock (this)
                {
                    _certificateListeners.Remove(listener);
                }
            }

            private async Task ExecuteListenerAsync(ICertificateListener listener)
            {
                try
                {
                    await listener.CertificateChangedAsync(_certificate).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    using var criticalLogMonitor = CertificateListenersCriticalError.ToMonitor();
                    criticalLogMonitor.Activity[SolutionConstants.MethodName] = "ExecuteListenerAsync";
                    criticalLogMonitor.OnError(ex);
                }
            }

            private void ExecuteListeners()
            {
                foreach(var listener in _certificateListeners)
                {
                    _ = Task.Run(() => ExecuteListenerAsync(listener)); //background task
                }
            }

            private Task UpdateClientCertificate(string newVal)
            {
                lock (this)
                {
                    var oldVal = _certificateValue;
                    var oldCertificate = _certificate;

                    if (string.IsNullOrWhiteSpace(newVal) || newVal.Equals(oldVal))
                    {
                        // no change
                        return Task.CompletedTask;
                    }

                    using var methodMonitor = CertificateListenersUpdateClientCertificate.ToMonitor();
                    try
                    {
                        methodMonitor.OnStart(false);

                        var newClientCertificate = CertificateUtils.CreateCertificate(newVal);
                        GuardHelper.ArgumentNotNull(newClientCertificate);

                        // Replace old with new
                        _certificateValue = newVal;
                        _certificate = newClientCertificate;

                        Logger.LogWarning("{config} is changed", _certificateName);

                        // For security, we should not log detail information about certificate.
                        // Let's print thumbprint only
                        methodMonitor.Activity.Properties[SolutionConstants.CertificateName] = _certificateName;
                        methodMonitor.Activity.Properties[SolutionConstants.OldCertificateThumbprint] = oldCertificate?.Thumbprint;
                        methodMonitor.Activity.Properties[SolutionConstants.NewCertificateThumbprint] = newClientCertificate.Thumbprint;

                        ExecuteListeners();

                        methodMonitor.OnCompleted();
                    }
                    catch (Exception ex)
                    {
                        methodMonitor.OnError(ex);
                    }

                    return Task.CompletedTask;
                }
            }
        }
    }
}
