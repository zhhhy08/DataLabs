namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceProxyClient
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AADAuth;

    public class TestResourceFetcherService : WebApplicationFactory<Program>
    {
        private ResourceProxyFlowTestManager _testManager;

        public TestResourceFetcherService(ResourceProxyFlowTestManager testManager)
        {
            _testManager = testManager;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // AllowedTypesConfigManager
                services.RemoveAll<IPartnerAuthorizeManager>();
                services.AddSingleton<IPartnerAuthorizeManager>(_testManager.PartnerAuthorizeManager);

                // ARMClient
                services.RemoveAll<IARMClient>();
                services.AddSingleton<IARMClient>(_testManager.ARMClient);

                // ARMAdminClient
                services.RemoveAll<IARMAdminClient>();
                services.AddSingleton<IARMAdminClient>(_testManager.ARMAdminClient);

                // QFDClient
                services.RemoveAll<IQFDClient>();
                services.AddSingleton<IQFDClient>(_testManager.QFDClient);

                // CasClient
                services.RemoveAll<ICasClient>();
                services.AddSingleton<ICasClient>(_testManager.CasClient);

                //AADTokenAuthenticator
                services.RemoveAll<IAADTokenAuthenticator>();
                services.AddSingleton<IAADTokenAuthenticator, NoOpAADTokenAuthenticator>();
            });
        }
    }
}