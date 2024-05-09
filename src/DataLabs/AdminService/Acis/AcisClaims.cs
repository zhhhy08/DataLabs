namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Acis
{
    /// <summary>
    /// Defines which claims are required from admin user to be able to run it.
    /// Important: Each controller can have more than one claim.
    /// </summary>
    public class AcisClaims
    {
        // TODO: how to add Jit to prod only
        public const string ReadOperationClaim = "Acis::Claim::AzureResourceBuilderDataLabs-PlatformServiceViewer";
        public const string WriteOperationClaim = "Acis::Claim::AzureResourceBuilderDataLabs-PlatformServiceAdministrator";
    }
}