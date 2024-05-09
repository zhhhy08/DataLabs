namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient
{
    using System.Diagnostics.CodeAnalysis;
    using System.Net;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    [ExcludeFromCodeCoverage]
    public class ARMAdminClientOptions : RestClientOptions
    {
        public required IEndPointSelector EndPointSelector { get; init; }

        public ARMAdminClientOptions(IConfiguration configuration) : 
            base(ARMAdminClient.UserAgent)
        {
            HttpRequestVersion = HttpVersion.Version11;

            var clientOptionName = SolutionConstants.ArmAdminClientOption;
            var clientOption = configuration.GetValue<string>(clientOptionName, string.Empty);
            SetRestClientOptions(clientOption.ConvertToDictionary(caseSensitive: false));
        }
    }
}
