namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;

    public class ResourceProxyAllowedTypesConfigManager : IResourceProxyAllowedTypesConfigManager, ICacheTTLManagerUpdateListener
    {
        /* Pubilc Methods */
        public ICacheTTLManager CacheTTLManager => _cacheTTLManager;

        public ReadOnlyDictionary<string, ClientProviderConfigList> GetAllowedTypesMap(ResourceProxyAllowedConfigType configType)
        {
            return _resourceProxyAllowedConfigInfos[(int)configType].AllowedTypesMap;
        }

        public ResourceProxyAllowedConfigInfo GetAllowedTypesConfigInfo(ResourceProxyAllowedConfigType configType)
        {
            return _resourceProxyAllowedConfigInfos[(int)configType];
        }

        public void AddUpdateListener(IResourceProxyAllowedTypesUpdateListener updateListener)
        {
            _updateListeners.Add(updateListener);
        }

        private readonly static HashSet<ClientProviderType> GetResourceAllowedTypesProviders = new()
        {
            // GetResource can be fetched through 
            // cache, arm, qfd, resourcefetcher_arm, resourcefetcher_qfd, outputSourceoftruth
            ClientProviderType.Cache,
            ClientProviderType.Arm,
            ClientProviderType.Qfd,
            ClientProviderType.ResourceFetcher_Arm,
            ClientProviderType.ResourceFetcher_Qfd,
            ClientProviderType.OutputSourceoftruth
        };

        private readonly static HashSet<ClientProviderType> CallARMGenericRequestAllowedTypesProviders = new()
        {
            // callARMGenericRequest can be fetched through 
            // cache, arm, resourcefetcher_arm
            ClientProviderType.Cache,
            ClientProviderType.Arm,
            ClientProviderType.ResourceFetcher_Arm
        };

        private readonly static HashSet<ClientProviderType> GetCollectionAllowedTypesProviders = new()
        {
            // getCollection can be fetched through 
            // cache, qfdclient, resourcefetcher_qfd
            ClientProviderType.Cache,
            ClientProviderType.Qfd,
            ClientProviderType.ResourceFetcher_Qfd
        };

        private readonly static HashSet<ClientProviderType> GetManifestConfigAllowedTypesProviders = new()
        {
            // getManifestConfig can be fetched through 
            // cache, armadmin, resourcefetcher_armadmin
            ClientProviderType.Cache,
            ClientProviderType.ArmAdmin,
            ClientProviderType.ResourceFetcher_ArmAdmin
        };

        private readonly static HashSet<ClientProviderType> GetConfigSpecsAllowedTypesProviders = new()
        {
            // getConfigSpecs can be fetched through 
            // cache, armadmin, resourcefetcher_armadmin
            ClientProviderType.Cache,
            ClientProviderType.ArmAdmin,
            ClientProviderType.ResourceFetcher_ArmAdmin
        };

        private readonly static HashSet<ClientProviderType> GetCasResponseAllowedTypesProviders = new()
        {
            // getCasResponse can be fetched through 
            // cache, cas, resourcefetcher_cas
            ClientProviderType.Cache,
            ClientProviderType.Cas,
            ClientProviderType.ResourceFetcher_Cas
        };

        private readonly static HashSet<ClientProviderType> GetIdMappingAllowedTypesProviders = new()
        {
            // getIdMapping can be fetched through 
            // qfdclient
            ClientProviderType.Qfd
        };

        private readonly ResourceProxyAllowedConfigInfo[] _resourceProxyAllowedConfigInfos;

        private const string InvalidFormatMessage = "value should have format <allowedType>:Provider(|option),Provider(|option)";

        private const char TypeAndProvidersDelimiter = ':';
        private const char ProvidersDelimiter = ',';
        private const char ProviderInfoDelimiter = '|';
        private readonly static StringSplitOptions _splitOptions = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;

        private const string CacheOption_Read = "read";
        private const string CacheOption_Write = "write";
        private const string CacheOption_AddNotFound = "addNotFound";

        private readonly ICacheTTLManager _cacheTTLManager;
        private readonly List<IResourceProxyAllowedTypesUpdateListener> _updateListeners = new();

        private readonly bool _useSourceOfTruth;
        private readonly object _updateLock = new();

        public ResourceProxyAllowedTypesConfigManager(ICacheTTLManager cacheTTLManager, IConfiguration configuration)
        {
            _cacheTTLManager = cacheTTLManager;
            _useSourceOfTruth = configuration.GetValue<bool>(SolutionConstants.UseSourceOfTruth);

            int totalEnumValues = Enum.GetValues(typeof(ResourceProxyAllowedConfigType)).Length;
            _resourceProxyAllowedConfigInfos = new ResourceProxyAllowedConfigInfo[totalEnumValues];

            //GetResourceAllowedTypes
            _resourceProxyAllowedConfigInfos[(int)ResourceProxyAllowedConfigType.GetResourceAllowedTypes] = 
                new ResourceProxyAllowedConfigInfo(
                    configType: ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                    configKey: SolutionConstants.GetResourceAllowedTypes,
                    allowedTypesProviders: GetResourceAllowedTypesProviders,
                    configManager: this, 
                    configuration: configuration);
            //CallARMGenericRequestAllowedTypes
            _resourceProxyAllowedConfigInfos[(int)ResourceProxyAllowedConfigType.CallARMGenericRequestAllowedTypes] =
                new ResourceProxyAllowedConfigInfo(
                    configType: ResourceProxyAllowedConfigType.CallARMGenericRequestAllowedTypes,
                    configKey: SolutionConstants.CallARMGenericRequestAllowedTypes,
                    allowedTypesProviders: CallARMGenericRequestAllowedTypesProviders,
                    configManager: this,
                    configuration: configuration);
            //GetCollectionAllowedTypes
            _resourceProxyAllowedConfigInfos[(int)ResourceProxyAllowedConfigType.GetCollectionAllowedTypes] =
                new ResourceProxyAllowedConfigInfo(
                    configType: ResourceProxyAllowedConfigType.GetCollectionAllowedTypes,
                    configKey: SolutionConstants.GetCollectionAllowedTypes,
                    allowedTypesProviders: GetCollectionAllowedTypesProviders,
                    configManager: this,
                    configuration: configuration);
            //GetManifestConfigAllowedTypes
            _resourceProxyAllowedConfigInfos[(int)ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes] =
                new ResourceProxyAllowedConfigInfo(
                    configType: ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes,
                    configKey: SolutionConstants.GetManifestConfigAllowedTypes,
                    allowedTypesProviders: GetManifestConfigAllowedTypesProviders,
                    configManager: this,
                    configuration: configuration);
            //GetConfigSpecsAllowedTypes
            _resourceProxyAllowedConfigInfos[(int)ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes] =
                new ResourceProxyAllowedConfigInfo(
                    configType: ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes,
                    configKey: SolutionConstants.GetConfigSpecsAllowedTypes,
                    allowedTypesProviders: GetConfigSpecsAllowedTypesProviders,
                    configManager: this,
                    configuration: configuration);
            //GetCasResponseAllowedTypes
            _resourceProxyAllowedConfigInfos[(int)ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes] =
                new ResourceProxyAllowedConfigInfo(
                    configType: ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes,
                    configKey: SolutionConstants.GetCasResponseAllowedTypes,
                    allowedTypesProviders: GetCasResponseAllowedTypesProviders,
                    configManager: this,
                    configuration: configuration);
            //GetIdMappingAllowedTypes
            _resourceProxyAllowedConfigInfos[(int)ResourceProxyAllowedConfigType.GetIdMappingAllowedTypes] =
                new ResourceProxyAllowedConfigInfo(
                    configType: ResourceProxyAllowedConfigType.GetIdMappingAllowedTypes,
                    configKey: SolutionConstants.GetIdMappingAllowedTypes,
                    allowedTypesProviders: GetIdMappingAllowedTypesProviders,
                    configManager: this,
                    configuration: configuration);

            _cacheTTLManager.AddUpdateListener(this);
        }

        internal void NotifyUpdateListeners(ResourceProxyAllowedConfigType configType)
        {
            lock (_updateLock)
            {
                foreach (var Listener in _updateListeners)
                {
                    Listener.NotifyUpdatedConfig(configType);
                }
            }
        }

        /*
        * getResourceAllowedTypes:
        *   "<resourceType>:(cache|<cacheOptions>),(outputsourceoftruth, arm,qfd,resourcefetcher_arm, resourcefetcher_qfd)|<optional apiVersion>"  
        *
        * callARMGenericRequestAllowedTypes
        *   "<URIPath>:(cache|<cacheOptions>),(arm,resourcefetcher_arm)|<optional apiVersion>"
        *
        * getCollectionAllowedTypes:
        *   "<resourceType>:(cache|<cacheOptions>),(qfd,resourcefetcher_qfd)|<optional apiVersion>"  
        *   
        * getManifestConfigAllowedTypes:
        *   "*:(cache|<cacheOptions>),(armadmin,resourcefetcher_armadmin)|<optional apiVersion>"
        *
        * getConfigSpecsAllowedTypes:
        *   "*:(cache|<cacheOptions>),(armadmin,resourcefetcher_armadmin)|<optional apiVersion>"
        *   
        * getCasResponseAllowedTypes:
        *   "*:(cache|<cacheOptions>),(cas,resourcefetcher_cas)|<optional apiVersion>"  
        *              
        *   cacheOptions format:
        *     read/00:10:00|write/00:30:00|addNotFound/00:10:00
        *     1. write TTL is used when new cache entry is added. If write TTL is not specified, then Cache TTL config for IO(resourceTypeCacheTTLMappings) will be used
        *     2. read TTL is used to compare cache entry is too old. If it is too old, then it will be considered as not found
        *        cacheEntry could be added by IO or cache entry might be already in cache for some reason. like cache deletion/update fail. So readTTL can be used to consider those already existing cache entry as stale entry.
        *        if read TTL is not specified and writeTTL is specified, then writeTTL will be used as readTTL
        *     3. AddNotFound is used when source client returns NotFound and we want to add it to cache with this TTL.
        *        if addNotFound TTL is not specified and writeTTL is specified, then writeTTL will be used as addNotFound TTL
        #     4. addNotFound is supported in OutputSourceOfTruth but cache write is not supported in OutputSourceoftruth. OutputSourceOfTruth content cache writing is done inside IOService
        *   
        */

        internal ReadOnlyDictionary<string, ClientProviderConfigList> ParseAllowedTypeConfigValue(
            string? value, 
            HashSet<ClientProviderType> allowedProviderTypes)            
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new ReadOnlyDictionary<string, ClientProviderConfigList>(new Dictionary<string, ClientProviderConfigList>(0));
            }

            var rows = value.ConvertToList();
            if (rows.Count == 0)
            {
                return new ReadOnlyDictionary<string, ClientProviderConfigList>(new Dictionary<string, ClientProviderConfigList>(0));
            }

            var resourceProvidersMap = new Dictionary<string, ClientProviderConfigList>(rows.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                bool hasSourceOfTruthProvider = row.LastIndexOf(ClientProviderType.OutputSourceoftruth.FastEnumToString(), StringComparison.OrdinalIgnoreCase) > 0;

                // TypeAndProvidersDelimiter could be used inside option. So we need to find first one instead of Split
                var typeAndProvidersIndex = row.IndexOf(TypeAndProvidersDelimiter);
                GuardHelper.ArgumentConstraintCheck(typeAndProvidersIndex > 0, InvalidFormatMessage);
                
                var allowedType = row.Substring(0, typeAndProvidersIndex);
                var providerInfos = row.Substring(typeAndProvidersIndex+1).Split(ProvidersDelimiter, _splitOptions);

                GuardHelper.ArgumentConstraintCheck(providerInfos.Length > 0, InvalidFormatMessage);

                ClientProviderConfigList providerConfigList = new(allowedType, hasSourceOfTruthProvider);
                resourceProvidersMap.Add(allowedType, providerConfigList);

                foreach (var providerInfo in providerInfos)
                {
                    // providerName|info
                    var columns = providerInfo.Split(ProviderInfoDelimiter, _splitOptions);

                    GuardHelper.ArgumentConstraintCheck(columns.Length > 0, InvalidFormatMessage);

                    // Find provider
                    var providerName = columns[0];

                    // Chechk if provider is allowed provider type
                    var providerType = StringEnumCache.GetEnumIgnoreCase<ClientProviderType>(providerName);
                    if (!allowedProviderTypes.Contains(providerType))
                    {
                        throw new NotSupportedException("Not supported Provider Name: " + providerName + ", row: " + row);
                    }

                    switch (providerType)
                    {
                        case ClientProviderType.Cache:
                            {
                                // cacheOptions format:
                                // cache | readTTL/00:10:00 | writeTTL/00:30:00 | addNotFound/00:10:00

                                TimeSpan? readTTL = null;
                                bool writeEnabled = false;
                                TimeSpan? writeTTL = null;
                                bool addNotFound = false;
                                TimeSpan? addNotFoundWriteTTL = null;

                                // op=0 is providerName
                                for (int op = 1; op < columns.Length; op++)
                                {
                                    var options = columns[op].Split('/', _splitOptions);
                                    GuardHelper.ArgumentConstraintCheck(options.Length > 0, InvalidFormatMessage);
                                    var optionName = options[0];
                                    var optionValue = options.Length > 1 ? options[1] : null;

                                    if (optionName.EqualsInsensitively(CacheOption_Read))
                                    {
                                        if (readTTL != null)
                                        {
                                            throw new NotSupportedException("Duplicated optionName: " + optionName + ", row: " + row);
                                        }
                                        readTTL = !string.IsNullOrWhiteSpace(optionValue) ? TimeSpan.Parse(optionValue) : null;
                                    }
                                    else if (optionName.EqualsInsensitively(CacheOption_Write))
                                    {
                                        if (writeEnabled)
                                        {
                                            throw new NotSupportedException("Duplicated optionName: " + optionName + ", row: " + row);
                                        }
                                        writeEnabled = true;
                                        writeTTL = !string.IsNullOrWhiteSpace(optionValue) ? TimeSpan.Parse(optionValue) : null;
                                    }
                                    else if (optionName.EqualsInsensitively(CacheOption_AddNotFound))
                                    {
                                        if (addNotFound)
                                        {
                                            throw new NotSupportedException("Duplicated optionName: " + optionName + ", row: " + row);
                                        }

                                        addNotFound = true;
                                        addNotFoundWriteTTL = !string.IsNullOrWhiteSpace(optionValue) ? TimeSpan.Parse(optionValue) : null;
                                    }
                                    else
                                    {
                                        throw new NotSupportedException("Not supported optionName: " + optionName + ", row: " + row);
                                    }
                                }

                                var providerConfigInfo = new CacheClientProviderConfig(
                                    allowedTypeName: allowedType,
                                    hasSourceOfTruthProvider: hasSourceOfTruthProvider,
                                    readTTL: readTTL, 
                                    writeEnabled: writeEnabled,
                                    writeTTL: writeTTL, 
                                    addNotFound: addNotFound,
                                    addNotFoundWriteTTL: addNotFoundWriteTTL, 
                                    cacheTTLManager: _cacheTTLManager);

                                providerConfigList.Add(providerConfigInfo);
                            }
                            break;
                        case ClientProviderType.Arm:
                        case ClientProviderType.ArmAdmin:
                        case ClientProviderType.Cas:
                            {
                                // API Version is mandatory

                                // arm|2022-12-01
                                // armadminclient|2022-12-01
                                // qfdclient|2022-12-01
                                // casClient|2022-12-01
                                var apiVersion = columns.Length > 1 && !string.IsNullOrWhiteSpace(columns[1]) ? columns[1] : null;
                                GuardHelper.ArgumentNotNullOrEmpty(apiVersion, "ApiVersion is mandatory for " + providerType.FastEnumToString());

                                var providerConfigInfo = new ClientProviderConfig(allowedTypeName: allowedType, providerType: providerType, apiVersion: apiVersion);
                                providerConfigList.Add(providerConfigInfo);
                            }
                            break;
                        case ClientProviderType.Qfd:
                        case ClientProviderType.ResourceFetcher_Arm:
                        case ClientProviderType.ResourceFetcher_Qfd:
                        case ClientProviderType.ResourceFetcher_ArmAdmin:
                        case ClientProviderType.ResourceFetcher_Cas:
                            {
                                // API Version is optional
                                // qfd
                                // resourcefetcher_arm|2022-12-01
                                // resourcefetcher_qfd
                                // resourcefetcher_armadmin|2022-12-01
                                // resourcefetcher_cas|2022-12-01

                                var apiVersion = columns.Length > 1 && !string.IsNullOrWhiteSpace(columns[1]) ? columns[1] : null;
                                var providerConfigInfo = new ClientProviderConfig(allowedTypeName: allowedType, providerType: providerType, apiVersion: apiVersion);
                                providerConfigList.Add(providerConfigInfo);
                            }
                            break;

                        case ClientProviderType.OutputSourceoftruth:
                            {
                                //outputsourceoftruth
                                if (!_useSourceOfTruth)
                                {
                                    throw new NotSupportedException("Output SourceOfTruth is not enabled");
                                }
                                var providerConfigInfo = new ClientProviderConfig(allowedTypeName: allowedType, providerType: providerType, apiVersion: null);
                                providerConfigList.Add(providerConfigInfo);
                            }
                            break;
                        default:
                            throw new NotSupportedException("Not Implemented Yet. type: " + providerType.FastEnumToString() + ", row: " + row);
                    }
                }
            }

            return new ReadOnlyDictionary<string, ClientProviderConfigList>(resourceProvidersMap);
        }

        public void NotifyUpdatedConfig(ICacheTTLManager cacheTTLManager)
        {
            // CacheTTLManager is updated
            // Let's go through all config and update cacheTTLManager
            lock (_updateLock)
            {
                foreach (var resourceProxyAllowedConfigInfo in _resourceProxyAllowedConfigInfos)
                {
                    foreach (var clientProviderConfigList in resourceProxyAllowedConfigInfo.AllowedTypesMap.Values)
                    {
                        clientProviderConfigList.CacheProviderConfig?.UpdateCacheTTLManager(cacheTTLManager);
                    }
                }
            }
        }
    }

    public interface IResourceProxyAllowedTypesUpdateListener
    {
        public void NotifyUpdatedConfig(ResourceProxyAllowedConfigType configType);
    }
}