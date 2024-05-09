namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient
{
    using System.Net;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class CasClientOptions : RestClientOptions
    {
        public required IEndPointSelector EndPointSelector { get; init; }

        public CasClientOptions(IConfiguration configuration) :
            base(CasClient.UserAgent)
        {
            HttpRequestVersion = HttpVersion.Version11;
            var clientOptionName = SolutionConstants.CasClientOption;
            var clientOption = configuration.GetValue<string>(clientOptionName, string.Empty);
            SetRestClientOptions(clientOption.ConvertToDictionary(caseSensitive: false));
        }
    }
}