namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    
    public class PartnerAuthorizeManager : IPartnerAuthorizeManager
    {
        private static readonly ILogger<PartnerAuthorizeManager> Logger = DataLabLoggerFactory.CreateLogger<PartnerAuthorizeManager>();

        private Dictionary<string, PartnerAuthorizeConfig> _partnerAuthorizeConfigMaps;
        private const string Delimiter = "|";
        private const string OptionDelimiter = ",";
        private const string UseResourceGraph = "useResourceGraph";

        public PartnerAuthorizeManager(IConfiguration configuration)
        {
            _partnerAuthorizeConfigMaps = new Dictionary<string, PartnerAuthorizeConfig>(StringComparer.OrdinalIgnoreCase);

            var partnerNames = configuration.GetValue<string>(SolutionConstants.PartnerNames, string.Empty).ConvertToSet(caseSensitive: false);
            GuardHelper.ArgumentNotNullOrEmpty(partnerNames, nameof(partnerNames));

            foreach(string partnerName in partnerNames)
            {
                _partnerAuthorizeConfigMaps.Add(partnerName, new PartnerAuthorizeConfig(partnerName, configuration));
            }
        }

        public PartnerAuthorizeConfig? GetPartnerAuthorizeConfig(string partnerName)
        {
            if (_partnerAuthorizeConfigMaps.TryGetValue(partnerName, out var config))
            {
                return config;
            }
            return null;
        }

        public class ArmGetResourceParams
        {
            public string ApiVersion { get; }
            public bool UseResourceGraph { get; }

            public ArmGetResourceParams(string apiVersion, bool useResourceGraph)
            {
                ApiVersion = apiVersion;
                UseResourceGraph = useResourceGraph;
            }
        }

        public class PartnerAuthorizeConfig
        {
            public string PartnerName { get; }
            public HashSet<string> ClientIds { get; }

            public IReadOnlyDictionary<string, ArmGetResourceParams> ArmAllowedResourceTypeApiVersionMap => _armAllowedResourceTypesMap;
            public IReadOnlyDictionary<string, string> ArmAllowedGenericURIPathApiVersionMap => _armAllowedGenericURIPathsMap;
            public IReadOnlyDictionary<string, string> QfdAllowedResourceTypeApiVersionMap => _qfdAllowedResourceTypesMap;
            public IReadOnlyDictionary<string, string> ArmAdminAllowedCallApiVersionMap => _armAdminAllowedCallsMap;
            public IReadOnlyDictionary<string, string> CasAllowedCallApiVersionMap => _casAllowedCallsMap;
            public IReadOnlyDictionary<string, string> IdMappingAllowedCallApiVersionMap => _idMappingAllowedCallsMap;

            private Dictionary<string, ArmGetResourceParams> _armAllowedResourceTypesMap; // resourceType|APIVersion
            private Dictionary<string, string> _armAllowedGenericURIPathsMap; // URIPath|APIVersion
            private Dictionary<string, string> _qfdAllowedResourceTypesMap; // resourceType|APIVersion
            private Dictionary<string, string> _armAdminAllowedCallsMap; // callName|APIVersion
            private Dictionary<string, string> _casAllowedCallsMap; // callName|APIVersion
            private Dictionary<string, string> _idMappingAllowedCallsMap; // callName|APIVersion

            private string _armAllowedResourceTypesString;
            private string _armAllowedGenericURIPathsString;
            private string _qfdAllowedResourceTypesString;
            private string _armAdminAllowedCallsString;
            private string _casAllowedCallsString;
            private string _idMappingAllowedCallsString;

            private readonly object _updateLock = new();

            public PartnerAuthorizeConfig(string partnerName, IConfiguration configuration)
            {
                PartnerName = partnerName;

                var clientKey = partnerName + SolutionConstants.PartnerClientIdsSuffix;
                ClientIds = (configuration.GetValue<string>(clientKey, string.Empty) ?? "").ConvertToSet(caseSensitive: false);
                GuardHelper.ArgumentNotNullOrEmpty(ClientIds);
                 
                var configKey = partnerName + SolutionConstants.ArmAllowedResourceTypesSuffix;
                var configValue = configuration.GetValueWithCallBack<string>(
                    configKey,
                    UpdateArmAllowedResourceTypesMap,
                    string.Empty) ?? "";

                _armAllowedResourceTypesString = configValue;
                _armAllowedResourceTypesMap = CreateArmAllowedMap(configKey, configValue);

                configKey = partnerName + SolutionConstants.ArmAllowedGenericURIPathsSuffix;
                configValue = configuration.GetValueWithCallBack<string>(
                    configKey,
                    UpdateArmAllowedGenericURIPathsMap,
                    string.Empty) ?? "";

                _armAllowedGenericURIPathsString = configValue;
                _armAllowedGenericURIPathsMap = CreateAllowedMap(configKey, configValue);

                configKey = partnerName + SolutionConstants.QfdAllowedResourceTypesSuffix;
                configValue = configuration.GetValueWithCallBack<string>(
                    configKey,
                    UpdateQfdAllowedResourceTypesMap,
                    string.Empty) ?? "";

                _qfdAllowedResourceTypesString = configValue;
                _qfdAllowedResourceTypesMap = CreateAllowedMap(configKey, configValue);

                configKey = partnerName + SolutionConstants.ArmAdminAllowedCallsSuffix;
                configValue = configuration.GetValueWithCallBack<string>(
                    configKey,
                    UpdateArmAdminAllowedCallsMap,
                    string.Empty) ?? "";

                _armAdminAllowedCallsString = configValue;
                _armAdminAllowedCallsMap = CreateAllowedMap(configKey, configValue);

                configKey = partnerName + SolutionConstants.CasAllowedCallsSuffix;
                configValue = configuration.GetValueWithCallBack<string>(
                    configKey,
                    UpdateCasAllowedCallsMap,
                    string.Empty) ?? "";

                _casAllowedCallsString = configValue;
                _casAllowedCallsMap = CreateAllowedMap(configKey, configValue);

                configKey = partnerName + SolutionConstants.IdMappingAllowedCallsSuffix;
                configValue = configuration.GetValueWithCallBack<string>(
                    configKey,
                    UpdateIdMappingAllowedCallsMap,
                    string.Empty) ?? "";

                _idMappingAllowedCallsString = configValue;
                _idMappingAllowedCallsMap = CreateAllowedMap(configKey, configValue);
            }

            private static Dictionary<string, string> CreateAllowedMap(string configKeyName, string value)
            {
                //armAllowedGenericURIPaths:
                // "/providers/Microsoft.Authorization/policySetDefinitions|2021-06-01"

                //qfdAllowedResourceTypes:
                // "microsoft.features/featureproviders/subscriptionfeatureregistrations|2021-07-01"

                //armAdminAllowedCalls:
                // "GetManifestConfigAsync|2021-07-01"

                if (string.IsNullOrWhiteSpace(value))
                {
                    return new Dictionary<string, string>(0);
                }
                
                var rows = value.ConvertToList();
                if (rows.Count == 0)
                {
                    return new Dictionary<string, string>(0);
                }

                var allowedTypeApiVersionMap = new Dictionary<string, string>(rows.Count, StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows) {
                    var typeAndVersion = row.ConvertToList(delimiter: Delimiter);

                    GuardHelper.ArgumentConstraintCheck(typeAndVersion.Count == 2, configKeyName + " should have format ABC|Version");

                    var allowedType = typeAndVersion[0];
                    var apiVersion = typeAndVersion[1];

                    allowedTypeApiVersionMap.Add(allowedType, apiVersion);
                }

                return allowedTypeApiVersionMap;
            }

            private static Dictionary<string, ArmGetResourceParams> CreateArmAllowedMap(string configKeyName, string value)
            {
                //armAllowedResourceTypes:
                // "microsoft.resources/subscriptions/resourcegroups|2021-04-01"
                // "microsoft.compute/disks|2021-04-01,useResourceGraph"

                if (string.IsNullOrWhiteSpace(value))
                {
                    return new Dictionary<string, ArmGetResourceParams>(0);
                }

                var rows = value.ConvertToList();
                if (rows.Count == 0)
                {
                    return new Dictionary<string, ArmGetResourceParams>(0);
                }

                var allowedTypeApiVersionMap = new Dictionary<string, ArmGetResourceParams>(rows.Count, StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    var typeAndVersion = row.ConvertToList(delimiter: Delimiter);

                    GuardHelper.ArgumentConstraintCheck(typeAndVersion.Count == 2, configKeyName + " should have format ABC|Version");

                    var allowedType = typeAndVersion[0];
                    var versionAndOptions = typeAndVersion[1].ConvertToList(delimiter: OptionDelimiter);
                    var apiVersion = versionAndOptions[0];
                    var useResourceGraph = versionAndOptions.Count > 1 && versionAndOptions[1].Trim().EqualsInsensitively(UseResourceGraph);
                    allowedTypeApiVersionMap.Add(allowedType, new ArmGetResourceParams(apiVersion, useResourceGraph));
                }

                return allowedTypeApiVersionMap;
            }

            public Task UpdateArmAllowedResourceTypesMap(string newValue)
            {
                var configKey = PartnerName + SolutionConstants.ArmAllowedResourceTypesSuffix;
                var oldValue = _armAllowedResourceTypesString;

                if (string.IsNullOrWhiteSpace(newValue))
                {
                    Logger.LogError("{config} need non empty string", configKey);
                    return Task.CompletedTask;
                }

                if (newValue.EqualsInsensitively(oldValue))
                {
                    // Nothing change
                    return Task.CompletedTask;
                }

                try
                {
                    var oldMap = _armAllowedResourceTypesMap;
                    var newMap = CreateArmAllowedMap(configKey, newValue);

                    lock (_updateLock)
                    {
                        if (Interlocked.CompareExchange(ref _armAllowedResourceTypesMap, newMap, oldMap) == oldMap)
                        {
                            _armAllowedResourceTypesString = newValue;

                            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                                configKey, oldValue, newValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{config} is invalid. value: {value}", configKey, newValue);
                }

                return Task.CompletedTask;
            }

            public Task UpdateArmAllowedGenericURIPathsMap(string newValue)
            {
                var configKey = PartnerName + SolutionConstants.ArmAllowedGenericURIPathsSuffix;
                var oldValue = _armAllowedGenericURIPathsString;

                if (string.IsNullOrWhiteSpace(newValue))
                {
                    Logger.LogError("{config} need non empty string", configKey);
                    return Task.CompletedTask;
                }

                if (newValue.EqualsInsensitively(oldValue))
                {
                    // Nothing change
                    return Task.CompletedTask;
                }

                try
                {
                    var oldMap = _armAllowedGenericURIPathsMap;
                    var newMap = CreateAllowedMap(configKey, newValue);

                    lock (_updateLock)
                    {
                        if (Interlocked.CompareExchange(ref _armAllowedGenericURIPathsMap, newMap, oldMap) == oldMap)
                        {
                            _armAllowedGenericURIPathsString = newValue;

                            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                                configKey, oldValue, newValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{config} is invalid. value: {value}", configKey, newValue);
                }

                return Task.CompletedTask;
            }

            public Task UpdateQfdAllowedResourceTypesMap(string newValue)
            {
                var configKey = PartnerName + SolutionConstants.QfdAllowedResourceTypesSuffix;
                var oldValue = _qfdAllowedResourceTypesString;

                if (string.IsNullOrWhiteSpace(newValue))
                {
                    Logger.LogError("{config} need non empty string", configKey);
                    return Task.CompletedTask;
                }

                if (newValue.EqualsInsensitively(oldValue))
                {
                    // Nothing change
                    return Task.CompletedTask;
                }

                try
                {
                    var oldMap = _qfdAllowedResourceTypesMap;
                    var newMap = CreateAllowedMap(configKey, newValue);

                    lock (_updateLock)
                    {
                        if (Interlocked.CompareExchange(ref _qfdAllowedResourceTypesMap, newMap, oldMap) == oldMap)
                        {
                            _qfdAllowedResourceTypesString = newValue;

                            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                                configKey, oldValue, newValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{config} is invalid. value: {value}", configKey, newValue);
                }

                return Task.CompletedTask;
            }

            public Task UpdateArmAdminAllowedCallsMap(string newValue)
            {
                var configKey = PartnerName + SolutionConstants.ArmAdminAllowedCallsSuffix;
                var oldValue = _armAdminAllowedCallsString;

                if (string.IsNullOrWhiteSpace(newValue))
                {
                    Logger.LogError("{config} need non empty string", configKey);
                    return Task.CompletedTask;
                }

                if (newValue.EqualsInsensitively(oldValue))
                {
                    // Nothing change
                    return Task.CompletedTask;
                }

                try
                {
                    var oldMap = _armAdminAllowedCallsMap;
                    var newMap = CreateAllowedMap(configKey, newValue);

                    lock (_updateLock)
                    {
                        if (Interlocked.CompareExchange(ref _armAdminAllowedCallsMap, newMap, oldMap) == oldMap)
                        {
                            _armAdminAllowedCallsString = newValue;

                            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                                configKey, oldValue, newValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{config} is invalid. value: {value}", configKey, newValue);
                }

                return Task.CompletedTask;
            }

            public Task UpdateCasAllowedCallsMap(string newValue)
            {
                var configKey = PartnerName + SolutionConstants.CasAllowedCallsSuffix;
                var oldValue = _casAllowedCallsString;

                if (string.IsNullOrWhiteSpace(newValue))
                {
                    Logger.LogError("{config} need non empty string", configKey);
                    return Task.CompletedTask;
                }

                if (newValue.EqualsInsensitively(oldValue))
                {
                    // Nothing change
                    return Task.CompletedTask;
                }

                try
                {
                    var oldMap = _casAllowedCallsMap;
                    var newMap = CreateAllowedMap(configKey, newValue);

                    lock (_updateLock)
                    {
                        if (Interlocked.CompareExchange(ref _casAllowedCallsMap, newMap, oldMap) == oldMap)
                        {
                            _casAllowedCallsString = newValue;

                            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                                configKey, oldValue, newValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{config} is invalid. value: {value}", configKey, newValue);
                }

                return Task.CompletedTask;
            }

            public Task UpdateIdMappingAllowedCallsMap(string newValue)
            {
                var configKey = PartnerName + SolutionConstants.IdMappingAllowedCallsSuffix;
                var oldValue = _idMappingAllowedCallsString;

                if (string.IsNullOrWhiteSpace(newValue))
                {
                    Logger.LogError("{config} need non empty string", configKey);
                    return Task.CompletedTask;
                }

                if (newValue.EqualsInsensitively(oldValue))
                {
                    // Nothing change
                    return Task.CompletedTask;
                }

                try
                {
                    var oldMap = _idMappingAllowedCallsMap;
                    var newMap = CreateAllowedMap(configKey, newValue);

                    lock (_updateLock)
                    {
                        if (Interlocked.CompareExchange(ref _idMappingAllowedCallsMap, newMap, oldMap) == oldMap)
                        {
                            _idMappingAllowedCallsString = newValue;

                            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                                configKey, oldValue, newValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{config} is invalid. value: {value}", configKey, newValue);
                }

                return Task.CompletedTask;
            }
        }
    }
}
