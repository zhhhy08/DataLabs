namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.DstsClient
{
    public readonly struct DstsConfigNames
    {
        public required string ConfigNameForDstsClientId { get; init; }
        public required string ConfigNameForDstsServerId { get; init; }
        public required string ConfigNameForClientHome { get; init; }
        public required string ConfigNameForServerHome { get; init; }
        public required string ConfigNameForServerRealm { get; init; }
        public required string ConfigNameForCertificateName { get; init; }
        public required string ConfigNameForSkipServerCertificateValidation { get; init; }
    }
}
