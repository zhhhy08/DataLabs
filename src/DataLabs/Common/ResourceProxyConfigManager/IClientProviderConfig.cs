namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager
{
    public interface IClientProviderConfig
    {
        public string AllowedTypeName { get; }
        public ClientProviderType ProviderType { get; }
        public string? ApiVersion { get; }
    }
}
