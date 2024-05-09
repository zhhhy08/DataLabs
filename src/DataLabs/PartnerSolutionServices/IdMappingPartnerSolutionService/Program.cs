namespace Microsoft.WindowsAzure.Governance.DataLabs.IdMappingSolutionService
{
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase;
    using Microsoft.WindowsAzure.IdMappingService.Services;

    public class Program
    {
        public static void Main(string[] args)
        {
            var partnerNuget = new IdMappingSolutionService();
            PartnerSolutionServiceHelper.Startup(partnerNuget, args);
        }
    }
}