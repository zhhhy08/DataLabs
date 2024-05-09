namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ResourceProxyConfigManager
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;

    [TestClass]
    public class ResourceProxyAllowedTypesConfigManagerTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            ConfigMapUtil.Reset();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void CacheWriteTestWithSourceOfTruthTest()
        {
            var allowedTypes ="microsoft.partner/type1:cache|read/00:05:00|write/00:18:00|addNotFound,outputsourceoftruth";
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.GetResourceAllowedTypes] = allowedTypes;

            var configuration = ConfigMapUtil.Configuration;

            var cacheTTLManager = new CacheTTLManager(configuration);
            var resourceProxyAllowedTypesConfigManager = new ResourceProxyAllowedTypesConfigManager(cacheTTLManager, configuration);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApiVersionTestWithClient()
        {
            var allowedTypes = "microsoft.partner/type1:cache|read/00:05:00|write/00:18:00|addNotFound,arm";
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.GetResourceAllowedTypes] = allowedTypes;

            var configuration = ConfigMapUtil.Configuration;

            var cacheTTLManager = new CacheTTLManager(configuration);
            var resourceProxyAllowedTypesConfigManager = new ResourceProxyAllowedTypesConfigManager(cacheTTLManager, configuration);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void InvalidTypeTest()
        {
            var allowedTypes = "microsoft.partner/type1:cache|read/00:05:00|write/00:18:00|addNotFound,abcdef";
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.GetResourceAllowedTypes] = allowedTypes;

            var configuration = ConfigMapUtil.Configuration;

            var cacheTTLManager = new CacheTTLManager(configuration);
            var resourceProxyAllowedTypesConfigManager = new ResourceProxyAllowedTypesConfigManager(cacheTTLManager, configuration);
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void UnsupportedProviderTypeTest()
        {
            var allowedTypes = "microsoft.partner/type1:cache|read/00:05:00|write/00:18:00|addNotFound,resourcefetcher_armadmin";
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.GetResourceAllowedTypes] = allowedTypes;

            var configuration = ConfigMapUtil.Configuration;

            var cacheTTLManager = new CacheTTLManager(configuration);
            var resourceProxyAllowedTypesConfigManager = new ResourceProxyAllowedTypesConfigManager(cacheTTLManager, configuration);
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void DuplicatedOptionTest()
        {
            var allowedTypes = "microsoft.partner/type1:cache|read/00:05:00|read/00:18:00|addNotFound,resourcefetcher_arm";
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.GetResourceAllowedTypes] = allowedTypes;

            var configuration = ConfigMapUtil.Configuration;

            var cacheTTLManager = new CacheTTLManager(configuration);
            var resourceProxyAllowedTypesConfigManager = new ResourceProxyAllowedTypesConfigManager(cacheTTLManager, configuration);
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void SourceOfTruthNotEnabledTest()
        {
            var allowedTypes = "microsoft.partner/type1:cache|read/00:05:00|read/00:18:00|addNotFound,outputsourceoftruth";
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.GetResourceAllowedTypes] = allowedTypes;

            var configuration = ConfigMapUtil.Configuration;

            var cacheTTLManager = new CacheTTLManager(configuration);
            var resourceProxyAllowedTypesConfigManager = new ResourceProxyAllowedTypesConfigManager(cacheTTLManager, configuration);
        }

        [TestMethod]
        public void GetResourceAllowedTypesTest()
        {
            var allowedTypes =
                // No Cache, ResourceFetcher
                "microsoft.partner/type1:resourcefetcher_arm"
                // Cache Read, Write, AddNotFound to Cache, ResourceFetcher
                + ";microsoft.partner/type2:cache|write/00:18:00|addNotFound,resourcefetcher_arm|2022-12-01"
                // Cache Read, Write, Directly ARM
                + ";microsoft.partner/type3:cache|read/00:05:00|write/00:18:00,arm|2022-12-01"
                // Cache Read, directly QFD
                + ";microsoft.partner/type4:cache|read/00:18:00,qfd|2022-12-01"
                // no explicit write TTL
                + ";microsoft.partner/type5:cache|read/00:05:00|write|addNotFound,resourcefetcher_arm"
                // no explicit read/write TTL
                + ";microsoft.partner/type6:cache|read|write|addNotFound,resourcefetcher_arm";

            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);

            ConfigMapUtil.Configuration[SolutionConstants.DefaultInputCacheTTL] = "00:10:00";
            ConfigMapUtil.Configuration[SolutionConstants.DefaultNotFoundEntryCacheTTL] = "00:13:00";
            ConfigMapUtil.Configuration[SolutionConstants.ResourceTypeCacheTTLMappings] = "microsoft.partner/type6|01:00:00";
            ConfigMapUtil.Configuration[SolutionConstants.GetResourceAllowedTypes] = allowedTypes;
            

            var configuration = ConfigMapUtil.Configuration;

            var cacheTTLManager = new CacheTTLManager(configuration);
            var resourceProxyAllowedTypesConfigManager = new ResourceProxyAllowedTypesConfigManager(cacheTTLManager, configuration);

            Assert.IsNotNull(resourceProxyAllowedTypesConfigManager.CacheTTLManager);

            var allowedMap = resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetResourceAllowedTypes);
            Assert.IsTrue(allowedMap.Count == 6);

            Assert.IsTrue(allowedMap.TryGetValue("microsoft.partner/type1", out var allowedType1));
            Assert.IsTrue(allowedMap.TryGetValue("microsoft.partner/type2", out var allowedType2));
            Assert.IsTrue(allowedMap.TryGetValue("microsoft.partner/type3", out var allowedType3));
            Assert.IsTrue(allowedMap.TryGetValue("microsoft.partner/type4", out var allowedType4));
            Assert.IsTrue(allowedMap.TryGetValue("microsoft.partner/type5", out var allowedType5));
            Assert.IsTrue(allowedMap.TryGetValue("microsoft.partner/type6", out var allowedType6));

            // Type1
            //"microsoft.partner/type1:resourcefetcher_arm"
            var allowedType = allowedType1;
            Assert.IsNull(allowedType.CacheProviderConfig);
            Assert.AreEqual(1, allowedType.ClientProviderConfigs.Count);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm, allowedType.ClientProviderConfigs[0].ProviderType);
            Assert.IsNull(allowedType.ClientProviderConfigs[0].ApiVersion);

            // Type2
            //+ ";microsoft.partner/type2:cache|write/00:18:00|addNotFound,resourcefetcher_arm|2022-12-01"+";microsoft.partner/type2:cache|write/00:18:00|addNotFound,resourcefetcher_arm|2022-12-01"
            allowedType = allowedType2;
            Assert.IsNotNull(allowedType.CacheProviderConfig);
            Assert.AreEqual(2, allowedType.ClientProviderConfigs.Count);
            Assert.AreEqual(ClientProviderType.Cache, allowedType.ClientProviderConfigs[0].ProviderType);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm, allowedType.ClientProviderConfigs[1].ProviderType);
            Assert.AreEqual("2022-12-01", allowedType.ClientProviderConfigs[1].ApiVersion);

            Assert.AreEqual(ClientProviderType.Cache, allowedType.CacheProviderConfig.ProviderType);
            Assert.AreEqual(TimeSpan.FromMinutes(18), allowedType.CacheProviderConfig.ReadTTL); // Read TTL from cache write
            Assert.IsFalse(allowedType.CacheProviderConfig.ReadTTLFromTTLManager);
            Assert.IsTrue(allowedType.CacheProviderConfig.WriteEnabled);
            Assert.AreEqual(TimeSpan.FromMinutes(18), allowedType.CacheProviderConfig.WriteTTL); // Read TTL from cache write
            Assert.IsFalse(allowedType.CacheProviderConfig.WriteTTLFromTTLManager);

            Assert.IsTrue(allowedType.CacheProviderConfig.AddNotFound);
            Assert.AreEqual(TimeSpan.FromMinutes(18), allowedType.CacheProviderConfig.AddNotFoundWriteTTL); // Read TTL from cache write;
            Assert.IsFalse(allowedType.CacheProviderConfig.AddNotFoundTTLFromTTLManager);
            Assert.IsFalse(allowedType.CacheProviderConfig.HasSourceOfTruthProvider);

            // Type3
            //+";microsoft.partner/type3:cache|read/00:05:00|write/00:18:00,arm|2022-12-01"
            allowedType = allowedType3;
            Assert.IsNotNull(allowedType.CacheProviderConfig);
            Assert.AreEqual(2, allowedType.ClientProviderConfigs.Count);
            Assert.AreEqual(ClientProviderType.Cache, allowedType.ClientProviderConfigs[0].ProviderType);
            Assert.AreEqual(ClientProviderType.Arm, allowedType.ClientProviderConfigs[1].ProviderType);
            Assert.AreEqual("2022-12-01", allowedType.ClientProviderConfigs[1].ApiVersion);

            Assert.AreEqual(ClientProviderType.Cache, allowedType.CacheProviderConfig.ProviderType);
            Assert.AreEqual(TimeSpan.FromMinutes(5), allowedType.CacheProviderConfig.ReadTTL);
            Assert.IsFalse(allowedType.CacheProviderConfig.ReadTTLFromTTLManager);
            Assert.IsTrue(allowedType.CacheProviderConfig.WriteEnabled);
            Assert.AreEqual(TimeSpan.FromMinutes(18), allowedType.CacheProviderConfig.WriteTTL);
            Assert.IsFalse(allowedType.CacheProviderConfig.WriteTTLFromTTLManager);

            Assert.IsFalse(allowedType.CacheProviderConfig.AddNotFound);
            Assert.IsNull(allowedType.CacheProviderConfig.AddNotFoundWriteTTL);
            Assert.IsFalse(allowedType.CacheProviderConfig.AddNotFoundTTLFromTTLManager);
            Assert.IsFalse(allowedType.CacheProviderConfig.HasSourceOfTruthProvider);

            // Type4
            //+";microsoft.partner/type4:cache|read/00:18:00,qfd|2022-12-01"
            allowedType = allowedType4;
            Assert.IsNotNull(allowedType.CacheProviderConfig);
            Assert.AreEqual(2, allowedType.ClientProviderConfigs.Count);
            Assert.AreEqual(ClientProviderType.Cache, allowedType.ClientProviderConfigs[0].ProviderType);
            Assert.AreEqual(ClientProviderType.Qfd, allowedType.ClientProviderConfigs[1].ProviderType);
            Assert.AreEqual("2022-12-01", allowedType.ClientProviderConfigs[1].ApiVersion);

            Assert.AreEqual(ClientProviderType.Cache, allowedType.CacheProviderConfig.ProviderType);
            Assert.AreEqual(TimeSpan.FromMinutes(18), allowedType.CacheProviderConfig.ReadTTL);
            Assert.IsFalse(allowedType.CacheProviderConfig.ReadTTLFromTTLManager);
            Assert.IsFalse(allowedType.CacheProviderConfig.WriteEnabled);
            Assert.IsNull(allowedType.CacheProviderConfig.WriteTTL);
            Assert.IsFalse(allowedType.CacheProviderConfig.WriteTTLFromTTLManager);

            Assert.IsFalse(allowedType.CacheProviderConfig.AddNotFound);
            Assert.IsNull(allowedType.CacheProviderConfig.AddNotFoundWriteTTL);
            Assert.IsFalse(allowedType.CacheProviderConfig.AddNotFoundTTLFromTTLManager);
            Assert.IsFalse(allowedType.CacheProviderConfig.HasSourceOfTruthProvider);

            // Type5
            //+";microsoft.partner/type5:cache|read/00:05:00|write|addNotFound,resourcefetcher_arm";
            allowedType = allowedType5;
            Assert.IsNotNull(allowedType.CacheProviderConfig);
            Assert.AreEqual(2, allowedType.ClientProviderConfigs.Count);
            Assert.AreEqual(ClientProviderType.Cache, allowedType.ClientProviderConfigs[0].ProviderType);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm, allowedType.ClientProviderConfigs[1].ProviderType);
            Assert.IsNull(allowedType.ClientProviderConfigs[1].ApiVersion);

            Assert.AreEqual(ClientProviderType.Cache, allowedType.CacheProviderConfig.ProviderType);
            Assert.AreEqual(TimeSpan.FromMinutes(5), allowedType.CacheProviderConfig.ReadTTL);
            Assert.IsFalse(allowedType.CacheProviderConfig.ReadTTLFromTTLManager);
            Assert.IsTrue(allowedType.CacheProviderConfig.WriteEnabled);
            Assert.AreEqual(TimeSpan.FromMinutes(10), allowedType.CacheProviderConfig.WriteTTL);
            Assert.IsTrue(allowedType.CacheProviderConfig.WriteTTLFromTTLManager); // from TTLManager

            Assert.IsTrue(allowedType.CacheProviderConfig.AddNotFound);
            Assert.AreEqual(TimeSpan.FromMinutes(13), allowedType.CacheProviderConfig.AddNotFoundWriteTTL);
            Assert.IsTrue(allowedType.CacheProviderConfig.AddNotFoundTTLFromTTLManager); // from TTLManager
            Assert.IsFalse(allowedType.CacheProviderConfig.HasSourceOfTruthProvider);

            // Type6
            //+";microsoft.partner/type6:cache|read|write|addNotFound,resourcefetcher_arm";
            //ConfigMapUtil.Configuration[SolutionConstants.ResourceTypeCacheTTLMappings] = "microsoft.partner/type6|01:00:00";
            allowedType = allowedType6;
            Assert.IsNotNull(allowedType.CacheProviderConfig);
            Assert.AreEqual(2, allowedType.ClientProviderConfigs.Count);
            Assert.AreEqual(ClientProviderType.Cache, allowedType.ClientProviderConfigs[0].ProviderType);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm, allowedType.ClientProviderConfigs[1].ProviderType);
            Assert.IsNull(allowedType.ClientProviderConfigs[1].ApiVersion);

            Assert.AreEqual(ClientProviderType.Cache, allowedType.CacheProviderConfig.ProviderType);
            Assert.AreEqual(TimeSpan.FromHours(1), allowedType.CacheProviderConfig.ReadTTL);
            Assert.IsTrue(allowedType.CacheProviderConfig.ReadTTLFromTTLManager);
            Assert.IsTrue(allowedType.CacheProviderConfig.WriteEnabled);
            Assert.AreEqual(TimeSpan.FromHours(1), allowedType.CacheProviderConfig.WriteTTL);
            Assert.IsTrue(allowedType.CacheProviderConfig.WriteTTLFromTTLManager); // from TTLManager

            Assert.IsTrue(allowedType.CacheProviderConfig.AddNotFound);
            Assert.AreEqual(TimeSpan.FromHours(1), allowedType.CacheProviderConfig.AddNotFoundWriteTTL); // from TTYManager type
            Assert.IsTrue(allowedType.CacheProviderConfig.AddNotFoundTTLFromTTLManager); // from TTLManager
            Assert.IsFalse(allowedType.CacheProviderConfig.HasSourceOfTruthProvider);

            // HotConfig change
            // Let's change TTLManager 
            ConfigMapUtil.Configuration[SolutionConstants.ResourceTypeCacheTTLMappings] = "microsoft.partner/type6|03:00:00";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            // Compare type6 again
            // Type6
            //+";microsoft.partner/type6:cache|read|write|addNotFound,resourcefetcher_arm";
            //ConfigMapUtil.Configuration[SolutionConstants.ResourceTypeCacheTTLMappings] = "microsoft.partner/type6|01:00:00";
            allowedType = allowedType6;
            Assert.IsNotNull(allowedType.CacheProviderConfig);
            Assert.AreEqual(2, allowedType.ClientProviderConfigs.Count);
            Assert.AreEqual(ClientProviderType.Cache, allowedType.ClientProviderConfigs[0].ProviderType);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm, allowedType.ClientProviderConfigs[1].ProviderType);
            Assert.IsNull(allowedType.ClientProviderConfigs[1].ApiVersion);

            Assert.AreEqual(ClientProviderType.Cache, allowedType.CacheProviderConfig.ProviderType);
            Assert.AreEqual(TimeSpan.FromHours(3), allowedType.CacheProviderConfig.ReadTTL);
            Assert.IsTrue(allowedType.CacheProviderConfig.ReadTTLFromTTLManager);
            Assert.IsTrue(allowedType.CacheProviderConfig.WriteEnabled);
            Assert.AreEqual(TimeSpan.FromHours(3), allowedType.CacheProviderConfig.WriteTTL);
            Assert.IsTrue(allowedType.CacheProviderConfig.WriteTTLFromTTLManager); // from TTLManager

            Assert.IsTrue(allowedType.CacheProviderConfig.AddNotFound);
            Assert.AreEqual(TimeSpan.FromHours(3), allowedType.CacheProviderConfig.AddNotFoundWriteTTL); // from TTYManager type
            Assert.IsTrue(allowedType.CacheProviderConfig.AddNotFoundTTLFromTTLManager); // from TTLManager
            Assert.IsFalse(allowedType.CacheProviderConfig.HasSourceOfTruthProvider);

            allowedMap = resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetResourceAllowedTypes);
            Assert.IsTrue(allowedMap.Count == 6);

            // HotConfig change for other types
            allowedTypes = "microsoft.partner/type6:cache|read|write|addNotFound,resourcefetcher_arm";
            ConfigMapUtil.Configuration[SolutionConstants.GetResourceAllowedTypes] = allowedTypes;
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            allowedMap = resourceProxyAllowedTypesConfigManager.GetAllowedTypesMap(ResourceProxyAllowedConfigType.GetResourceAllowedTypes);
            Assert.IsTrue(allowedMap.Count == 1);

            Assert.IsTrue(allowedMap.TryGetValue("microsoft.partner/type6", out allowedType6));

            allowedType = allowedType6;
            Assert.IsNotNull(allowedType.CacheProviderConfig);
            Assert.AreEqual(2, allowedType.ClientProviderConfigs.Count);
            Assert.AreEqual(ClientProviderType.Cache, allowedType.ClientProviderConfigs[0].ProviderType);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm, allowedType.ClientProviderConfigs[1].ProviderType);
            Assert.IsNull(allowedType.ClientProviderConfigs[1].ApiVersion);

            Assert.AreEqual(ClientProviderType.Cache, allowedType.CacheProviderConfig.ProviderType);
            Assert.AreEqual(TimeSpan.FromHours(3), allowedType.CacheProviderConfig.ReadTTL);
            Assert.IsTrue(allowedType.CacheProviderConfig.ReadTTLFromTTLManager);
            Assert.IsTrue(allowedType.CacheProviderConfig.WriteEnabled);
            Assert.AreEqual(TimeSpan.FromHours(3), allowedType.CacheProviderConfig.WriteTTL);
            Assert.IsTrue(allowedType.CacheProviderConfig.WriteTTLFromTTLManager); // from TTLManager

            Assert.IsTrue(allowedType.CacheProviderConfig.AddNotFound);
            Assert.AreEqual(TimeSpan.FromHours(3), allowedType.CacheProviderConfig.AddNotFoundWriteTTL); // from TTYManager type
            Assert.IsTrue(allowedType.CacheProviderConfig.AddNotFoundTTLFromTTLManager); // from TTLManager
            Assert.IsFalse(allowedType.CacheProviderConfig.HasSourceOfTruthProvider);
        }
    }
}

