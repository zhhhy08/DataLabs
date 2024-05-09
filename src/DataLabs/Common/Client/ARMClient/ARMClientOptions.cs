namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient
{
    using System.Net;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class ARMClientOptions : RestClientOptions
    {
        public required IEndPointSelector EndPointSelector { get; init; }
        public required string ARMTokenResource { get; init; }
        public required string AADAuthority { get; init; }
        public required string DefaultTenantId { get; init; }
        public required string? FirstPartyAppId { get; init; }

        public ARMClientOptions(IConfiguration configuration) : 
            base(ARMClient.UserAgent)
        {
            // Let's try to use Http2.0 with ARM because ARM Supports
            HttpRequestVersion = HttpVersion.Version20;

            var clientOptionName = SolutionConstants.ARMClientOption;
            var clientOption = configuration.GetValue<string>(clientOptionName, string.Empty);
            SetRestClientOptions(clientOption.ConvertToDictionary(caseSensitive: false));
        }
    }
}
