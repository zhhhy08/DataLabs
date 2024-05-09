namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Manager
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArmThrottle;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.Cache;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.ClientProvider.ResourceFetcher;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.OutputSourceOfTruth;

    internal class ClientProvidersManager : IClientProvidersManager, IResourceProxyAllowedTypesUpdateListener
    {
        public IArmThrottleManager ArmThrottleManager => _armThrottleManager;

        public ReadOnlyDictionary<string, ClientProviderList<IRFProxyGetResourceClient>> GetResourceAllowedTypesMap => _getResourceAllowedTypesMap;
        public ReadOnlyDictionary<string, ClientProviderList<IARMClient>> CallARMGenericRequestAllowedTypesMap => _callARMGenericRequestAllowedTypesMap;
        public ReadOnlyDictionary<string, ClientProviderList<IQFDClient>> GetCollectionAllowedTypesMap => _getCollectionAllowedTypesMap;
        public ReadOnlyDictionary<string, ClientProviderList<IARMAdminClient>> GetManifestConfigAllowedTypesMap => _getManifestConfigAllowedTypesMap;
        public ReadOnlyDictionary<string, ClientProviderList<IARMAdminClient>> GetConfigSpecsAllowedTypesMap => _getConfigSpecsAllowedTypesMap;
        public ReadOnlyDictionary<string, ClientProviderList<ICasClient>> GetCasResponseAllowedTypesMap => _getCasResponseAllowedTypesMap;
        public ReadOnlyDictionary<string, ClientProviderList<IQFDClient>> GetIdMappingAllowedTypesMap => _getIdMappingAllowedTypesMap;

        private static readonly ILogger<ClientProvidersManager> Logger = DataLabLoggerFactory.CreateLogger<ClientProvidersManager>();

        private ReadOnlyDictionary<string, ClientProviderList<IRFProxyGetResourceClient>> _getResourceAllowedTypesMap;
        private ReadOnlyDictionary<string, ClientProviderList<IARMClient>> _callARMGenericRequestAllowedTypesMap;
        private ReadOnlyDictionary<string, ClientProviderList<IQFDClient>> _getCollectionAllowedTypesMap;
        private ReadOnlyDictionary<string, ClientProviderList<IARMAdminClient>> _getManifestConfigAllowedTypesMap;
        private ReadOnlyDictionary<string, ClientProviderList<IARMAdminClient>> _getConfigSpecsAllowedTypesMap;
        private ReadOnlyDictionary<string, ClientProviderList<ICasClient>> _getCasResponseAllowedTypesMap;
        private ReadOnlyDictionary<string, ClientProviderList<IQFDClient>> _getIdMappingAllowedTypesMap;

        private readonly IARMClient _armClient;
        private readonly IARMAdminClient _armAdminClient;
        private readonly IQFDClient _qfdClient;
        private readonly ICasClient _casClient;
        private readonly IResourceFetcherClient _resourceFetcherClient;
        private readonly IRFProxyCacheClient _rfProxyCacheClient;
        private readonly IRFProxyOutputSourceOfTruthClient _rfProxyOutputSourceOfTruthClient;
        private readonly IResourceProxyAllowedTypesConfigManager _resourceProxyAllowedTypesConfigManager;

        private readonly IArmThrottleManager _armThrottleManager;

        public ClientProvidersManager(
            IARMClient armClient,
            IARMAdminClient armAdminClient,
            IQFDClient qfdClient,
            ICasClient casClient,
            IResourceFetcherClient resourceFetcherClient,
            IRFProxyCacheClient rfProxyCacheClient,
            IRFProxyOutputSourceOfTruthClient rfProxyOutputSourceOfTruthClient,
            IResourceProxyAllowedTypesConfigManager resourceProxyAllowedTypesConfigManager, 
            IConfiguration configuration)
        {
            _armClient = armClient;
            _armAdminClient = armAdminClient;
            _qfdClient = qfdClient;
            _casClient = casClient;
            _resourceFetcherClient = resourceFetcherClient;
            _rfProxyCacheClient = rfProxyCacheClient;
            _rfProxyOutputSourceOfTruthClient = rfProxyOutputSourceOfTruthClient;

            _resourceProxyAllowedTypesConfigManager = resourceProxyAllowedTypesConfigManager;
            _armThrottleManager = new ARMThrottleManager(_rfProxyCacheClient.ResourceCacheClient, configuration);

            // GetResourceAllowedTypes
            _getResourceAllowedTypesMap = CreateClientProviderListMap<IRFProxyGetResourceClient>(
                _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetResourceAllowedTypes));

            // CallARMGenericRequestAllowedTypes
            _callARMGenericRequestAllowedTypesMap = CreateClientProviderListMap<IARMClient>(
                _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.CallARMGenericRequestAllowedTypes));

            // GetCollectionAllowedTypes
            _getCollectionAllowedTypesMap = CreateClientProviderListMap<IQFDClient>(
                _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetCollectionAllowedTypes));

            // GetManifestConfigAllowedTypes
            _getManifestConfigAllowedTypesMap = CreateClientProviderListMap<IARMAdminClient>(
                _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes));

            // GetConfigSpecsAllowedTypes
            _getConfigSpecsAllowedTypesMap = CreateClientProviderListMap<IARMAdminClient>(
                _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes));

            // GetCasResponseAllowedTypes
            _getCasResponseAllowedTypesMap = CreateClientProviderListMap<ICasClient>(
                _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes));

            // GetIdMappingAllowedTypes
            _getIdMappingAllowedTypesMap = CreateClientProviderListMap<IQFDClient>(
                _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetIdMappingAllowedTypes));

            // Add config value change listener
            _resourceProxyAllowedTypesConfigManager.AddUpdateListener(this);
        }

        private ReadOnlyDictionary<string, ClientProviderList<T>> CreateClientProviderListMap<T>(
            ReadOnlyDictionary<string, ClientProviderConfigList> allowedTypesConfigMap)
        {
            if (allowedTypesConfigMap.Count == 0)
            {
                return new ReadOnlyDictionary<string, ClientProviderList<T>>(
                    new Dictionary<string, ClientProviderList<T>>(0));
            }

            var resourceProvidersMap = new Dictionary<string, ClientProviderList<T>>(allowedTypesConfigMap.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in allowedTypesConfigMap)
            {
                var allowedType = kvp.Key;
                var providerConfigList = kvp.Value;

                var clientProviderList = new ClientProviderList<T>(providerConfigList);
                resourceProvidersMap.Add(allowedType, clientProviderList);

                CacheClientProvider? cacheClientProvider = null;

                foreach (var providerConfig in providerConfigList.ClientProviderConfigs)
                {
                    // providerName|info
                    switch(providerConfig.ProviderType)
                    {
                        case ClientProviderType.Cache: 
                            {
                                if (!_rfProxyCacheClient.ResourceCacheClient.CacheEnabled)
                                {
                                    throw new NotSupportedException("Cache is not enabled");
                                }

                                var cacheClientProviderConfig = (CacheClientProviderConfig)providerConfig;

                                cacheClientProvider = new CacheClientProvider(_rfProxyCacheClient, cacheClientProviderConfig: cacheClientProviderConfig);
                                
                                var clientProvider = (IClientProvider<T>)cacheClientProvider;
                                clientProviderList.Add(clientProvider);
                            }
                            break;
                        case ClientProviderType.Arm:
                            {
                                // armclient|2022-12-01
                                if (_armClient == NoOpARMClient.Instance)
                                {
                                    throw new NotSupportedException("ArmClient is not supported");
                                }

                                if (typeof(T) == typeof(IRFProxyGetResourceClient))
                                {
                                    var clientProvider = new ClientProvider<T>(
                                        providerConfig.ProviderType, 
                                        (T)ArmGetResourceClient.Create(_armClient), 
                                        providerConfig.ApiVersion, 
                                        cacheClientProvider); ;
                                    clientProviderList.Add(clientProvider);
                                }
                                else
                                {
                                    var clientProvider = new ClientProvider<T>(
                                        providerConfig.ProviderType, 
                                        (T)_armClient, 
                                        providerConfig.ApiVersion,
                                        cacheClientProvider);
                                    clientProviderList.Add(clientProvider);
                                }
                            }
                            break;
                        case ClientProviderType.ArmAdmin:
                            {
                                // armadminclient|2022-12-01
                                if (_armAdminClient == NoOpARMAdminClient.Instance)
                                {
                                    throw new NotSupportedException("ArmAdminClient is not supported");
                                }

                                var clientProvider = new ClientProvider<T>(providerConfig.ProviderType, (T)_armAdminClient, providerConfig.ApiVersion, cacheClientProvider);
                                clientProviderList.Add(clientProvider);
                            }
                            break;
                        case ClientProviderType.Qfd:
                            {
                                // qfdclient|2022-12-01
                                if (_qfdClient == NoOpQFDClient.Instance)
                                {
                                    throw new NotSupportedException("QFDClient is not supported");
                                }

                                if (typeof(T) == typeof(IRFProxyGetResourceClient))
                                {
                                    var clientProvider = new ClientProvider<T>(
                                        providerConfig.ProviderType,
                                        (T)QfdGetResourceClient.Create(_qfdClient),
                                        providerConfig.ApiVersion,
                                        cacheClientProvider); ;
                                    clientProviderList.Add(clientProvider);
                                }
                                else
                                {
                                    var clientProvider = new ClientProvider<T>(
                                        providerConfig.ProviderType, 
                                        (T)_qfdClient, 
                                        providerConfig.ApiVersion, 
                                        cacheClientProvider);
                                    clientProviderList.Add(clientProvider);
                                }
                            }
                            break;
                        case ClientProviderType.Cas:
                            {
                                // casClient|2022-12-01
                                if (_casClient == NoOpCasClient.Instance)
                                {
                                    throw new NotSupportedException("CasClient is not supported");
                                }

                                var clientProvider = new ClientProvider<T>(providerConfig.ProviderType, (T)_casClient, providerConfig.ApiVersion, cacheClientProvider);
                                clientProviderList.Add(clientProvider);
                            }
                            break;
                        case ClientProviderType.ResourceFetcher_Arm:
                            {
                                // resourcefetcher_arm|2022-12-01
                                var clientProvider = (IClientProvider<T>) new ResourceFetcherArmClientProvider(
                                    resourceFetcherClient: _resourceFetcherClient,
                                    apiVersion: providerConfig.ApiVersion,
                                    cacheClientProvider: cacheClientProvider);

                                clientProviderList.Add(clientProvider);
                            }
                            break;

                        case ClientProviderType.ResourceFetcher_Qfd:
                            {
                                // resourcefetcher_qfd|2022-12-01
                                var clientProvider = (IClientProvider<T>) new ResourceFetcherQfdClientProvider(
                                    resourceFetcherClient: _resourceFetcherClient,
                                    apiVersion: providerConfig.ApiVersion,
                                    cacheClientProvider: cacheClientProvider);

                                clientProviderList.Add(clientProvider);
                            }
                            break;

                        case ClientProviderType.ResourceFetcher_ArmAdmin:
                            {
                                // resourcefetcher_armadmin|2022-12-01
                                var clientProvider = (IClientProvider<T>) new ResourceFetcherArmAdminClientProvider(
                                    resourceFetcherClient: _resourceFetcherClient,
                                    apiVersion: providerConfig.ApiVersion,
                                    cacheClientProvider: cacheClientProvider);

                                clientProviderList.Add(clientProvider);
                            }
                            break;

                        case ClientProviderType.ResourceFetcher_Cas:
                            {
                                // resourcefetcher_cas|2022-12-01
                                var clientProvider = (IClientProvider<T>)new ResourceFetcherCasClientProvider(
                                    resourceFetcherClient: _resourceFetcherClient,
                                    apiVersion: providerConfig.ApiVersion,
                                    cacheClientProvider: cacheClientProvider);

                                clientProviderList.Add(clientProvider);
                            }
                            break;

                        case ClientProviderType.OutputSourceoftruth:
                            {
                                //outputsourceoftruth
                                var clientProvider = new ClientProvider<T>(providerConfig.ProviderType, (T)_rfProxyOutputSourceOfTruthClient, null, cacheClientProvider);
                                clientProviderList.Add(clientProvider);
                            }
                            break;


                        default:
                            throw new NotSupportedException("Not Implemented Type: " + providerConfig.ProviderType.FastEnumToString());
                    }
                }
            }

            return new ReadOnlyDictionary<string, ClientProviderList<T>>(resourceProvidersMap);
        }

        public void NotifyUpdatedConfig(ResourceProxyAllowedConfigType configType)
        {
            switch(configType)
            {
                case ResourceProxyAllowedConfigType.GetResourceAllowedTypes:
                    {
                        var oldMap = _getResourceAllowedTypesMap;
                        var newMap = CreateClientProviderListMap<IRFProxyGetResourceClient>(
                            _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetResourceAllowedTypes));
                        if (Interlocked.CompareExchange(ref _getResourceAllowedTypesMap, newMap, oldMap) == oldMap)
                        {
                            Logger.LogWarning("{config} is changed", "GetResourceAllowedTypesMap");
                        }
                    }
                    break;
                case ResourceProxyAllowedConfigType.CallARMGenericRequestAllowedTypes:
                    {
                        var oldMap = _callARMGenericRequestAllowedTypesMap;
                        var newMap = CreateClientProviderListMap<IARMClient>(
                            _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.CallARMGenericRequestAllowedTypes));
                        if (Interlocked.CompareExchange(ref _callARMGenericRequestAllowedTypesMap, newMap, oldMap) == oldMap)
                        {
                            Logger.LogWarning("{config} is changed", "CallARMGenericRequestAllowedTypesMap");
                        }
                    }
                    break;
                case ResourceProxyAllowedConfigType.GetCollectionAllowedTypes:
                    {
                        var oldMap = _getCollectionAllowedTypesMap;
                        var newMap = CreateClientProviderListMap<IQFDClient>(
                            _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetCollectionAllowedTypes));
                        if (Interlocked.CompareExchange(ref _getCollectionAllowedTypesMap, newMap, oldMap) == oldMap)
                        {
                            Logger.LogWarning("{config} is changed", "GetCollectionAllowedTypesMap");
                        }
                    }
                    break;
                case ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes:
                    {
                        var oldMap = _getManifestConfigAllowedTypesMap;
                        var newMap = CreateClientProviderListMap<IARMAdminClient>(
                            _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes));
                        if (Interlocked.CompareExchange(ref _getManifestConfigAllowedTypesMap, newMap, oldMap) == oldMap)
                        {
                            Logger.LogWarning("{config} is changed", "GetManifestConfigAllowedTypesMap");
                        }
                    }
                    break;
                case ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes:
                    {
                        var oldMap = _getConfigSpecsAllowedTypesMap;
                        var newMap = CreateClientProviderListMap<IARMAdminClient>(
                            _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes));
                        if (Interlocked.CompareExchange(ref _getConfigSpecsAllowedTypesMap, newMap, oldMap) == oldMap)
                        {
                            Logger.LogWarning("{config} is changed", "GetConfigSpecsAllowedTypesMap");
                        }
                    }
                    break;
                case ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes:
                    {
                        var oldMap = _getCasResponseAllowedTypesMap;
                        var newMap = CreateClientProviderListMap<ICasClient>(
                            _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes));
                        if (Interlocked.CompareExchange(ref _getCasResponseAllowedTypesMap, newMap, oldMap) == oldMap)
                        {
                            Logger.LogWarning("{config} is changed", "GetCasResponseAllowedTypesMap");
                        }
                    }
                    break;
                case ResourceProxyAllowedConfigType.GetIdMappingAllowedTypes:
                    {
                        var oldMap = _getIdMappingAllowedTypesMap;
                        var newMap = CreateClientProviderListMap<IQFDClient>(
                            _resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetIdMappingAllowedTypes));
                        if (Interlocked.CompareExchange(ref _getIdMappingAllowedTypesMap, newMap, oldMap) == oldMap)
                        {
                            Logger.LogWarning("{config} is changed", "GetIdMappingAllowedTypesMap");
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException("Not Implemented Type: " + configType.FastEnumToString());
            }
        }
    }

    internal class ClientProviderList<T>
    {
        public ClientProviderConfigList ProviderConfigList { get; }
        public List<IClientProvider<T>> ClientProviders => _clientProviders;

        private readonly List<IClientProvider<T>> _clientProviders;

        public ClientProviderList(ClientProviderConfigList clientProviderConfigList)
        {
            ProviderConfigList = clientProviderConfigList;
            _clientProviders = new List<IClientProvider<T>>(clientProviderConfigList.ClientProviderConfigs.Count);
        }

        public void Add(IClientProvider<T> clientProvider)
        {
            _clientProviders.Add(clientProvider);
        }
    }
}