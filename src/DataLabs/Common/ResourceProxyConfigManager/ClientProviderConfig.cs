namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager
{
    public class ClientProviderConfig : IClientProviderConfig
    {
        public string AllowedTypeName { get; }
        public ClientProviderType ProviderType { get; }
        public string? ApiVersion { get; }

        public ClientProviderConfig(string allowedTypeName, ClientProviderType providerType, string? apiVersion)
        {
            AllowedTypeName = allowedTypeName;
            ProviderType = providerType;
            ApiVersion = apiVersion;
        }
    }
}
