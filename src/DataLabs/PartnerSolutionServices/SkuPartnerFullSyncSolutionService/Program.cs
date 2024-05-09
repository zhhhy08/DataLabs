namespace SkuPartnerFullSyncSolutionService
{
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase;
    using SkuService.FullSync.Services;

    public class Program
    {
        public static void Main(string[] args)
        {
            var partnerNuget = new SkuFullSyncService();
            PartnerSolutionServiceHelper.Startup(partnerNuget, args);
        }
    }
}