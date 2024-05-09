namespace Microsoft.WindowsAzure.Governance.DataLabs.SamplePartnerSolutionService
{
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase;
    using Microsoft.Azores.PartnerSolution;

    public class Program
    {
        public static void Main(string[] args)
        {
            var partnerNuget = new AzoresPartnerSolution();
            PartnerSolutionServiceHelper.Startup(partnerNuget, args);
        }
    }
}