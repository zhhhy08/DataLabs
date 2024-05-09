namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceProxyClient
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService;

    public class TestResourceFetcherProxyService : WebApplicationFactory<Program>
    {
        private ResourceProxyFlowTestManager _testManager;

        public TestResourceFetcherProxyService(ResourceProxyFlowTestManager testManager)
        {
            _testManager = testManager;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Cache Related
                services.RemoveAll<ICacheTTLManager>();
                services.AddSingleton<ICacheTTLManager>(_testManager.CacheTTLManager);
                services.RemoveAll<ICacheClient>();
                services.AddSingleton<ICacheClient>(_testManager.CacheClient);
                services.RemoveAll<IResourceCacheClient>();
                services.AddSingleton<IResourceCacheClient>(_testManager.ResourceCacheClient);

                // AllowedTypesConfigManager
                services.RemoveAll<IResourceProxyAllowedTypesConfigManager>();
                services.AddSingleton<IResourceProxyAllowedTypesConfigManager>(_testManager.ResourceProxyAllowedTypesConfigManager);

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

                // ResourceFetcherClient
                services.RemoveAll<IResourceFetcherClient>();
                services.AddSingleton<IResourceFetcherClient>(sp => 
                    new ResourceFetcherClient(testHttpClient: _testManager.TestResourceFetcherServiceClient!,
                    configuration: ConfigMapUtil.Configuration));
            });
        }
    }
    
}