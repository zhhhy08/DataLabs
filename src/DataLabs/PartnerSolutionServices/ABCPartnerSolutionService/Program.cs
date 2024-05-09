namespace Microsoft.WindowsAzure.Governance.DataLabs.ABCPartnerSolutionService
{
    using Microsoft.AzureBusinessContinuity.PartnerSolution;
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase;

    public class Program
    {
        public static void Main(string[] args)
        {
            var partnerNuget = new ABCPartnerSolution();
            PartnerSolutionServiceHelper.Startup(partnerNuget, args);
        }
    }
}