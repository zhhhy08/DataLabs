namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient
{
    using System.Net;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class QFDClientOptions : RestClientOptions
    {
        public required IEndPointSelector EndPointSelector { get; init; }

        public QFDClientOptions(IConfiguration configuration) : 
            base(QFDClient.UserAgent)
        {
            // Let's try to use Http2.0. Check with Store team
            HttpRequestVersion = HttpVersion.Version20;

            var clientOptionName = SolutionConstants.QfdClientOption;
            var clientOption = configuration.GetValue<string>(clientOptionName, string.Empty);
            SetRestClientOptions(clientOption.ConvertToDictionary(caseSensitive: false));
        }
    }
}