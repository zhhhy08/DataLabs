namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    public class ResourceProxyAllowedConfigInfo
    {
        private static readonly ILogger<ResourceProxyAllowedConfigInfo> Logger = DataLabLoggerFactory.CreateLogger<ResourceProxyAllowedConfigInfo>();

        public ReadOnlyDictionary<string, ClientProviderConfigList> AllowedTypesMap => _allowedTypesMap;
        public ResourceProxyAllowedConfigType ConfigType { get; }

        private readonly string _configKey;
        private readonly HashSet<ClientProviderType> _allowedTypesProviders;
        private readonly ResourceProxyAllowedTypesConfigManager _configManager;

        private ReadOnlyDictionary<string, ClientProviderConfigList> _allowedTypesMap;
        private string _allowedTypesString;

        private readonly object _updateLock = new();

        public ResourceProxyAllowedConfigInfo(
            ResourceProxyAllowedConfigType configType,
            string configKey,
            HashSet<ClientProviderType> allowedTypesProviders,
            ResourceProxyAllowedTypesConfigManager configManager, 
            IConfiguration configuration)
        {
            ConfigType = configType;
            _configKey = configKey;
            _allowedTypesProviders = allowedTypesProviders;
            _configManager = configManager;

            _allowedTypesString = configuration.GetValueWithCallBack<string>(configKey, UpdateAllowedTypesMap, string.Empty) ?? "";
            _allowedTypesMap = _configManager.ParseAllowedTypeConfigValue(
                value: _allowedTypesString,
                allowedProviderTypes: _allowedTypesProviders);
        }

        public Task UpdateAllowedTypesMap(string newValue)
        {
            var oldValue = _allowedTypesString;

            if (string.IsNullOrWhiteSpace(newValue))
            {
                Logger.LogError("{config} need non empty string", _configKey);
                return Task.CompletedTask;
            }

            if (newValue.EqualsInsensitively(oldValue))
            {
                // Nothing change
                return Task.CompletedTask;
            }

            try
            {
                var oldMap = _allowedTypesMap;
                var newMap = _configManager.ParseAllowedTypeConfigValue(
                    value: newValue,
                    allowedProviderTypes: _allowedTypesProviders);

                lock (_updateLock)
                {
                    if (Interlocked.CompareExchange(ref _allowedTypesMap, newMap, oldMap) == oldMap)
                    {
                        _allowedTypesString = newValue;

                        Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                            _configKey, oldValue, newValue);

                        _configManager.NotifyUpdateListeners(ConfigType);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{config} is invalid. value: {value}", _configKey, newValue);
            }

            return Task.CompletedTask;
        }
    }
}