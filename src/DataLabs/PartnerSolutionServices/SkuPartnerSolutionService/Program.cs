namespace Microsoft.WindowsAzure.Governance.DataLabs.SkuPartnerSolutionService
{
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase;
    using SkuService.Main.Services;

    public class Program
    {
        public static void Main(string[] args)
        {
            var partnerNuget = new SkuSolutionService();
            PartnerSolutionServiceHelper.Startup(partnerNuget, args);
        }
    }
}