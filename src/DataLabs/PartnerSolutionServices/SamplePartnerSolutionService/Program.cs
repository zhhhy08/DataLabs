namespace Microsoft.WindowsAzure.Governance.DataLabs.SamplePartnerSolutionService
{
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase;
    using SamplePartnerNuget.SolutionInterface;

    public class Program
    {
        public static void Main(string[] args)
        {
            var partnerNuget = new SamplePartnerService();
            PartnerSolutionServiceHelper.Startup(partnerNuget, args);
        }
    }
}