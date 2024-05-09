namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class PartnerBlobClientOptions : RestClientOptions
    {
        public PartnerBlobClientOptions(IConfiguration configuration) :
            base(PartnerBlobClient.UserAgent)
        {
            var clientOptionName = SolutionConstants.PartnerBlobClientOption;
            var clientOption = configuration.GetValue<string>(clientOptionName, string.Empty);
            SetRestClientOptions(clientOption.ConvertToDictionary(caseSensitive: false));
        }
    }
}