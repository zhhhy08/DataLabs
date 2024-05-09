namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    /* 
     * Since this class is not thread safe for now,
     * RegisterObject need to be called in advance to avoid concurrent modification 
     */
    public class ConfigurableConcurrencyManager : IDisposable
    {
        public const int NO_CONCURRENCY_CONTROL = 0;

        private static readonly ILogger<ConfigurableConcurrencyManager> Logger = DataLabLoggerFactory.CreateLogger<ConfigurableConcurrencyManager>();

        private IConcurrencyManager? _concurrencyManager;
        private readonly List<Action<IConcurrencyManager?>> _registeredObjects;
        private readonly string _configName;
        private int _maxConcurrency;

        public ConfigurableConcurrencyManager(string configName, int initMaxConcurrency, bool useConfigValue = true)
        {
            _configName = configName;
            _registeredObjects = new();
            var maxConcurrency = !useConfigValue ? initMaxConcurrency : ConfigMapUtil.Configuration.GetValueWithCallBack(configName, UpdateConcurrencyAsync, initMaxConcurrency);
            if (maxConcurrency > 0)
            {
                UpdateConcurrencyAsync(maxConcurrency).GetAwaiter().GetResult();
            }
        }

        public int GetCurrentNumRunning()
        {
            var concurrencyManager = _concurrencyManager;
            return (concurrencyManager == null) ? 0 : concurrencyManager.NumRunning;
        }

        public void RegisterObject(Action<IConcurrencyManager?> action)
        {
            action(_concurrencyManager);
            _registeredObjects.Add(action);
        }

        private void UpdateRegisteredObjects()
        {
            for (int i = 0; i < _registeredObjects.Count; i++)
            {
                _registeredObjects[i](_concurrencyManager);
            }
        }

        public async Task UpdateConcurrencyAsync(int newConcurrency)
        {
            if (newConcurrency != NO_CONCURRENCY_CONTROL && newConcurrency <= 0)
            {
                Logger.LogError("Concurrency must be larger than 0");
                return;
            }

            var oldConcurrency = _maxConcurrency;
            var oldConcurrencyManager = _concurrencyManager;

            if (newConcurrency == oldConcurrency)
            {
                return;
            }

            if (newConcurrency == NO_CONCURRENCY_CONTROL)
            {
                if (oldConcurrencyManager != null)
                {
                    // No concurrency control
                    // Set _concurrentManager to null so we can avoid call
                    if (Interlocked.CompareExchange(ref _concurrencyManager, null, oldConcurrencyManager) == oldConcurrencyManager)
                    {
                        _maxConcurrency = newConcurrency;

                        UpdateRegisteredObjects();

                        oldConcurrencyManager.Dispose();

                        Logger.LogWarning("MaxConcurrency is changed, ConfigName: {concurrencyConfig}, Old: {oldVal}, New: {newVal}",
                            _configName, oldConcurrency, newConcurrency);
                        return;
                    }
                }
            }
            else
            {
                if (oldConcurrencyManager == null)
                {
                    var newConcurrencyManager = new ConcurrencyManager(_configName, newConcurrency);

                    if (Interlocked.CompareExchange(ref _concurrencyManager, newConcurrencyManager, null) == null)
                    {
                        _maxConcurrency = newConcurrency;

                        UpdateRegisteredObjects();

                        Logger.LogWarning("MaxConcurrency is changed, ConfigName: {concurrencyConfig}, Old: {oldVal}, New: {newVal}",
                            _configName, oldConcurrency, newConcurrency);
                        return;
                    }
                    else
                    {
                        // Failed to replace. it is already replaced with other call
                        // Destory just created concurrencyManager
                        newConcurrencyManager.Dispose();
                        return;
                    }
                }
                else
                {
                    if (await oldConcurrencyManager.SetNewMaxConcurrencyAsync(newConcurrency).ConfigureAwait(false))
                    {
                        _maxConcurrency = newConcurrency;
                        Logger.LogWarning("MaxConcurrency is changed, ConfigName: {concurrencyConfig}, Old: {oldVal}, New: {newVal}",
                            _configName, oldConcurrency, newConcurrency);
                        return;
                    }
                }
            }
        }

        private bool _disposed = false;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                var oldConcurrencyManager = _concurrencyManager;
                if (oldConcurrencyManager != null)
                {
                    if (Interlocked.CompareExchange(ref _concurrencyManager, null, oldConcurrencyManager) == oldConcurrencyManager)
                    {
                        UpdateRegisteredObjects();
                        oldConcurrencyManager.Dispose();
                    }
                }
                _registeredObjects.Clear();
            }
        }
    }
}