namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.DstsClient
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class DstsConfigValues
    {
        // Refer to below document about regions
        // https://review.learn.microsoft.com/en-us/identity/dsts/overview?branch=main#supported-environments-and-sovereign-clouds

        public required string ClientId { get; init; }

        // Application Id of the service being called
        public required string ServerId { get; init; }

        // string clientHomeDsts = "https://co2agg04-passive-dsts.dsts.core.azure-test.net/dstsv2/7a433bfc-2514-4697-b467-e0933190487f";
        public required string ClientHome { get; init; }

        // string serverHomeDsts...
        public required string? ServerHome { get; init; }

        // This is only necessary when serverHome exist and is different with ClientHome (corss region authentication)
        // string serverRealm = "realm://dsts.core.azure-test.net/7a433bfc-2514-4697-b467-e0933190487f/";
        public required string? ServerRealm { get; init; } 

        // Certificate Name
        public required string CertificateName { get; init; }
        
        // For Test EndPoint, we might have to skip server certificate Validation
        public bool SkipCertificateValidation { get; init; }

        public static DstsConfigValues CreateDstsConfigValues(DstsConfigNames configNames, IConfiguration configuration)
        {
            var clientId = configuration.GetValue<string>(configNames.ConfigNameForDstsClientId);
            var serverId = configuration.GetValue<string>(configNames.ConfigNameForDstsServerId);
            var clientHome = configuration.GetValue<string>(configNames.ConfigNameForClientHome);
            var serverHome = configuration.GetValue<string>(configNames.ConfigNameForServerHome);
            var serverRealm = configuration.GetValue<string>(configNames.ConfigNameForServerRealm);
            var certificateName = configuration.GetValue<string>(configNames.ConfigNameForCertificateName);
            var skipCertificateValidation = configuration.GetValue<bool>(configNames.ConfigNameForSkipServerCertificateValidation, false);

            GuardHelper.ArgumentNotNullOrEmpty(clientId);
            GuardHelper.ArgumentNotNullOrEmpty(serverId);
            GuardHelper.ArgumentNotNullOrEmpty(clientHome);
            GuardHelper.ArgumentNotNullOrEmpty(certificateName);

            return new DstsConfigValues()
            {
                ClientId = clientId,
                ServerId = serverId,
                ClientHome = clientHome,
                ServerHome = serverHome,
                ServerRealm = serverRealm,
                CertificateName = certificateName,
                SkipCertificateValidation = skipCertificateValidation
            };
        }
    }
}
