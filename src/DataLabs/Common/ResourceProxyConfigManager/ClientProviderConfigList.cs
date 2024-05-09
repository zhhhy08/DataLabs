namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager
{
    using System.Collections.Generic;

    public class ClientProviderConfigList
    {
        public const string AllAllowedSymbol = "*";

        public CacheClientProviderConfig? CacheProviderConfig { get; private set; }
        public bool HasSourceOfTruthProvider { get; private set; }
        public bool HasAllAllowed { get; }
        public string AllowedType { get; }
        public List<IClientProviderConfig> ClientProviderConfigs => _clientProviderConfigs;

        private readonly List<IClientProviderConfig> _clientProviderConfigs;

        public ClientProviderConfigList(string allowedType, bool hasSourceOfTruthProvider)
        {
            AllowedType = allowedType;
            HasSourceOfTruthProvider = hasSourceOfTruthProvider;
            if (allowedType == AllAllowedSymbol)
            {
                HasAllAllowed = true;
            }
            _clientProviderConfigs = new(2);
        }

        public void Add(IClientProviderConfig clientProviderConfig)
        {
            _clientProviderConfigs.Add(clientProviderConfig);

            if (clientProviderConfig.ProviderType == ClientProviderType.Cache)
            {
                CacheProviderConfig = (CacheClientProviderConfig)clientProviderConfig;
            }
            else if (clientProviderConfig.ProviderType == ClientProviderType.OutputSourceoftruth)
            {
                HasSourceOfTruthProvider = true;
            }
        }
    }
}
