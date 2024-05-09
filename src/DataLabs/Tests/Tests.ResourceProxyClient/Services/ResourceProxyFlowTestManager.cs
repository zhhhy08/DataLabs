namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceProxyClient
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;

    public class ResourceProxyFlowTestManager
    {
        private static volatile ResourceProxyFlowTestManager? _instance;
        private static readonly object SyncRoot = new object();

        public static ResourceProxyFlowTestManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new ResourceProxyFlowTestManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public const string SolutionName = "testsolution";

        public ResourceProxyAllowedTypesConfigManager ResourceProxyAllowedTypesConfigManager { get; }
        public PartnerAuthorizeManager PartnerAuthorizeManager { get; }

        public ICacheTTLManager CacheTTLManager { get; }
        public TestCacheClient CacheClient { get; }
        public IResourceCacheClient ResourceCacheClient { get; }

        public TestARMClient ARMClient { get; }
        public TestARMAdminClient ARMAdminClient { get; }
        public TestQFDClient QFDClient { get; }
        public TestCasClient CasClient { get; }

        public TestResourceFetcherService? TestResourceFetcherService { get; } 
        public HttpClient? TestResourceFetcherServiceClient { get; }
        public TestResourceFetcherProxyService? TestResourceFetcherProxyService { get;  }
        public HttpClient? TestResourceFetcherProxyServiceClient { get; }

        public IResourceProxyClient ResourceProxyClient { get; }

        private ResourceProxyFlowTestManager()
        {
            // Set the environment variables
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable(SolutionConstants.SCALE_UNIT, SolutionName);
            Environment.SetEnvironmentVariable(SolutionConstants.BUILD_VERSION, "2023.11.04.04");
            Environment.SetEnvironmentVariable(SolutionConstants.REGION, "wus3");
            Environment.SetEnvironmentVariable("CACHE_SERVICE_PORT", "3278");
            Environment.SetEnvironmentVariable("CACHE_DOMAIN", "localhost");
            Environment.SetEnvironmentVariable("CACHE_SERVICE_REPLICA_COUNT", "1");
            Environment.SetEnvironmentVariable("CACHE_SERVICE_REPLICA_PREFIX", "test");
            Environment.SetEnvironmentVariable("SECRETS_STORE_DIR", "");
            Environment.SetEnvironmentVariable(SolutionConstants.IS_DEDICATED_PARTNER_AKS, "true");

            ConfigMapUtil.Reset();
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);

            // Cache Client
            ConfigMapUtil.Configuration["CachePoolDomain"] = "localhost";
            ConfigMapUtil.Configuration["CacheNumPools"] = "1";
            ConfigMapUtil.Configuration["CachePool-0"] =
                "CacheName=test;ReadEnabled=true;WriteEnabled=true;NodeCount=1;Port=3278;StartOffset=0;NodeSelectionMode=JumpHash";
            ConfigMapUtil.Configuration["CachePoolNodeReplicationMapping-0"] = "";

            // Basic necessary config
            ConfigMapUtil.Configuration[SolutionConstants.PrimaryRegionName] = "testprimary";
            ConfigMapUtil.Configuration[SolutionConstants.BackupRegionName] = "testbackup";
            ConfigMapUtil.Configuration[SolutionConstants.UseSourceOfTruth] = "false";
            ConfigMapUtil.Configuration[SolutionConstants.ResourceProxyGrpcOption] = "LBPolicy=LOCAL";
            ConfigMapUtil.Configuration[SolutionConstants.UseCacheLookupInProxyClient] = "true";

            ConfigMapUtil.Configuration[SolutionConstants.EnableGrpcTrace] = "true";
            ConfigMapUtil.Configuration[SolutionConstants.EnableHttpClientTrace] = "true";
            ConfigMapUtil.Configuration[SolutionConstants.EnableAzureSDKActivity] = "true";

            // Resource Fetcher Client
            ConfigMapUtil.Configuration[SolutionConstants.ResourceFetcherEndpoints] = "http://localhost:6072";
            ConfigMapUtil.Configuration[SolutionConstants.ResourceFetcherTokenResource] = "https://resourcefetcherservice-int.microsoft.com";
            ConfigMapUtil.Configuration[SolutionConstants.ResourceFetcherHomeTenantId] = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Allowed Partners in Resource Fetcher Service
            ConfigMapUtil.Configuration[SolutionConstants.PartnerNames] = SolutionName;
            var clientIdskey = SolutionName + SolutionConstants.PartnerClientIdsSuffix;
            ConfigMapUtil.Configuration[clientIdskey] = "testClientId";

            // Create Test shared class
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(ConfigMapUtil.Configuration);
            services.AddSingleton<IConfigurationWithCallBack>(ConfigMapUtil.Configuration);

            // Create IResourceProxyAllowedTypesConfigManager for ResourceFetcherProxyService
            services.AddSingleton<ICacheTTLManager, CacheTTLManager>();
            services.AddSingleton<IResourceProxyAllowedTypesConfigManager, ResourceProxyAllowedTypesConfigManager>();

            // Create PartnerAuthorizeManager for ResourceFetcherService
            services.AddSingleton<IPartnerAuthorizeManager, PartnerAuthorizeManager>();

            // Create TestResourceCacheClient
            services.AddSingleton<ICacheClient, TestCacheClient>();
            services.AddSingleton<IResourceCacheClient, ResourceCacheClient>();

            // Create Test Clients
            services.AddSingleton<IARMClient, TestARMClient>();
            services.AddSingleton<IARMAdminClient, TestARMAdminClient>();
            services.AddSingleton<IQFDClient, TestQFDClient>();
            services.AddSingleton<ICasClient, TestCasClient>();

            var serviceProvider = services.BuildServiceProvider();

            CacheClient = (TestCacheClient)serviceProvider.GetRequiredService<ICacheClient>();
            ResourceCacheClient = serviceProvider.GetRequiredService<IResourceCacheClient>();
            CacheTTLManager = serviceProvider.GetRequiredService<ICacheTTLManager>();
            ResourceProxyAllowedTypesConfigManager = (ResourceProxyAllowedTypesConfigManager)serviceProvider.GetRequiredService<IResourceProxyAllowedTypesConfigManager>();
            PartnerAuthorizeManager = (PartnerAuthorizeManager)serviceProvider.GetRequiredService<IPartnerAuthorizeManager>();

            ARMClient = (TestARMClient)serviceProvider.GetRequiredService<IARMClient>();
            ARMAdminClient = (TestARMAdminClient)serviceProvider.GetRequiredService<IARMAdminClient>();
            QFDClient = (TestQFDClient)serviceProvider.GetRequiredService<IQFDClient>();
            CasClient = (TestCasClient)serviceProvider.GetRequiredService<ICasClient>();

            // Create Resource Fetcher Service
            TestResourceFetcherService = new TestResourceFetcherService(this);
            TestResourceFetcherServiceClient = TestResourceFetcherService.CreateClient();

            // Create Resource Fetcher Proxy Service
            TestResourceFetcherProxyService = new TestResourceFetcherProxyService(this);
            TestResourceFetcherProxyServiceClient = TestResourceFetcherProxyService.CreateClient();

            // Create ResourceProxyClient
            ResourceProxyClient = CreateResourceProxyClient();
        }

        private ResourceProxyClient CreateResourceProxyClient()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(ConfigMapUtil.Configuration);
            services.AddSingleton<IConfigurationWithCallBack>(ConfigMapUtil.Configuration);
            services.AddSingleton<ICacheTTLManager>(CacheTTLManager);
            services.AddSingleton<IResourceProxyAllowedTypesConfigManager>(ResourceProxyAllowedTypesConfigManager!);
            services.AddSingleton<IResourceCacheClient>(ResourceCacheClient);
            services.AddSingleton<HttpClient>(TestResourceFetcherProxyServiceClient!);

            services.AddResourceProxyClientProvider();
            var serviceProvider = services.BuildServiceProvider();

            ConfigMapUtil.Configuration.AllowMultiCallBacks = true;
            return (ResourceProxyClient)serviceProvider.GetService<IResourceProxyClient>()!;
        }

        public async Task UpdateConfigAsync(ResourceProxyAllowedConfigType configType, string valueInProxy, string valueInFetcher)
        {
            switch(configType)
            {
                case ResourceProxyAllowedConfigType.GetResourceAllowedTypes:
                    {
                        // Proxy update
                        var allowedTypesConfigInfo = ResourceProxyAllowedTypesConfigManager.GetAllowedTypesConfigInfo(configType);
                        await allowedTypesConfigInfo.UpdateAllowedTypesMap(valueInProxy).ConfigureAwait(false);

                        // Fetcher update
                        var partnerAuthorizeConfig = PartnerAuthorizeManager.GetPartnerAuthorizeConfig(SolutionName);
                        await partnerAuthorizeConfig!.UpdateArmAllowedResourceTypesMap(valueInFetcher).ConfigureAwait(false);
                        await partnerAuthorizeConfig!.UpdateQfdAllowedResourceTypesMap(valueInFetcher).ConfigureAwait(false);

                    }
                    break;

                case ResourceProxyAllowedConfigType.CallARMGenericRequestAllowedTypes:
                    {
                        // Proxy update
                        var allowedTypesConfigInfo = ResourceProxyAllowedTypesConfigManager.GetAllowedTypesConfigInfo(configType);
                        await allowedTypesConfigInfo.UpdateAllowedTypesMap(valueInProxy).ConfigureAwait(false);

                        // Fetcher update
                        var partnerAuthorizeConfig = PartnerAuthorizeManager.GetPartnerAuthorizeConfig(SolutionName);
                        await partnerAuthorizeConfig!.UpdateArmAllowedGenericURIPathsMap(valueInFetcher).ConfigureAwait(false);
                    }
                    break;
                case ResourceProxyAllowedConfigType.GetCollectionAllowedTypes:
                    {
                        // Proxy update
                        var allowedTypesConfigInfo = ResourceProxyAllowedTypesConfigManager.GetAllowedTypesConfigInfo(configType);
                        await allowedTypesConfigInfo.UpdateAllowedTypesMap(valueInProxy).ConfigureAwait(false);

                        // Fetcher update
                        var partnerAuthorizeConfig = PartnerAuthorizeManager.GetPartnerAuthorizeConfig(SolutionName);
                        await partnerAuthorizeConfig!.UpdateQfdAllowedResourceTypesMap(valueInFetcher).ConfigureAwait(false);
                    }
                    break;
                case ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes:
                    {
                        // Proxy update
                        var allowedTypesConfigInfo = ResourceProxyAllowedTypesConfigManager.GetAllowedTypesConfigInfo(configType);
                        await allowedTypesConfigInfo.UpdateAllowedTypesMap(valueInProxy).ConfigureAwait(false);

                        // Fetcher update
                        var partnerAuthorizeConfig = PartnerAuthorizeManager.GetPartnerAuthorizeConfig(SolutionName);
                        await partnerAuthorizeConfig!.UpdateArmAdminAllowedCallsMap(valueInFetcher).ConfigureAwait(false);
                    }
                    break;
                case ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes:
                    {
                        // Proxy update
                        var allowedTypesConfigInfo = ResourceProxyAllowedTypesConfigManager.GetAllowedTypesConfigInfo(configType);
                        await allowedTypesConfigInfo.UpdateAllowedTypesMap(valueInProxy).ConfigureAwait(false);

                        // Fetcher update
                        var partnerAuthorizeConfig = PartnerAuthorizeManager.GetPartnerAuthorizeConfig(SolutionName);
                        await partnerAuthorizeConfig!.UpdateArmAdminAllowedCallsMap(valueInFetcher).ConfigureAwait(false);
                    }
                    break;
                case ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes:
                    {
                        // Proxy update
                        var allowedTypesConfigInfo = ResourceProxyAllowedTypesConfigManager.GetAllowedTypesConfigInfo(configType);
                        await allowedTypesConfigInfo.UpdateAllowedTypesMap(valueInProxy).ConfigureAwait(false);

                        // Fetcher update
                        var partnerAuthorizeConfig = PartnerAuthorizeManager.GetPartnerAuthorizeConfig(SolutionName);
                        await partnerAuthorizeConfig!.UpdateCasAllowedCallsMap(valueInFetcher).ConfigureAwait(false);
                    }
                    break;
                case ResourceProxyAllowedConfigType.GetIdMappingAllowedTypes:
                    {
                        // Proxy update
                        var allowedTypesConfigInfo = ResourceProxyAllowedTypesConfigManager.GetAllowedTypesConfigInfo(configType);
                        await allowedTypesConfigInfo.UpdateAllowedTypesMap(valueInProxy).ConfigureAwait(false);

                        // Fetcher update
                        var partnerAuthorizeConfig = PartnerAuthorizeManager.GetPartnerAuthorizeConfig(SolutionName);
                        await partnerAuthorizeConfig!.UpdateIdMappingAllowedCallsMap(valueInFetcher).ConfigureAwait(false);
                    }
                    break;

                default:
                    throw new NotImplementedException("this config type is not implemented");
            }
        }

        public void Clear()
        {
            CacheClient?.Clear();
            ARMClient?.Clear();
            ARMAdminClient?.Clear();
            QFDClient?.Clear();
            CasClient?.Clear();
        }
    }
}