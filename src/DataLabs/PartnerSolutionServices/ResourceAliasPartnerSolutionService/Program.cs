namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceAliasPartnerSolutionService
{
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase;
    using ResourceAliasService;

    public class Program
    {
        public static void Main(string[] args)
        {
            var partnerNuget = new ResourceAliasSolutionService();
            PartnerSolutionServiceHelper.Startup(partnerNuget, args);
        }
    }
}