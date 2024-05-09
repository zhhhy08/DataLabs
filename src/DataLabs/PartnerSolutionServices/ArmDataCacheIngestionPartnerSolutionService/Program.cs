namespace ArmDataCacheIngestionPartnerSolutionService
{
    using Microsoft.Azure.ARMDataInsights.ArmDataCacheService;
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase;

    public class Program
    {
        public static void Main(string[] args)
        {
            var partnerNuget = new ArmDataCacheIngestionService();
            PartnerSolutionServiceHelper.Startup(partnerNuget, args);
        }
    }
}