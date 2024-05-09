namespace Microsoft.WindowsAzure.Governance.DataLabs.CapabilitiesPartnerSolutionService
{
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase;
    using System.Net.Http;
    using Microsoft.WindowsAzure.Governance.ResourceCapabilities.Services;
    using Microsoft.WindowsAzure.Governance.ResourceCapabilities.Logging;

    public class Program
    {
        public static void Main(string[] args)
        {
            HttpClientHandler handler = new HttpClientHandler();
            var partnerNuget = new PartnerSolutionService(new CapabilitiesLoggerFactory(), new PolicyServiceExecutableClient(handler));
            PartnerSolutionServiceHelper.Startup(partnerNuget, args);
        }
    }
}
