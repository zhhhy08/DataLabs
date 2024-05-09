namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth
{
    using static Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth.PartnerAuthorizeManager;

    public interface IPartnerAuthorizeManager
    {
        public PartnerAuthorizeConfig? GetPartnerAuthorizeConfig(string partnerName);
    }
}
