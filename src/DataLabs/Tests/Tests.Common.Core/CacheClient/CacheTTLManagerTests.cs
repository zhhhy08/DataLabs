namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.CacheClient
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    
    [TestClass]
    public class CacheTTLManagerTests
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
        public void CacheTTLManagerTest()
        {
            string resourceTypeCacheTTLMappings = "microsoft.compute/virtualmachines|01:00:00;microsoft.storage/storageaccounts|00:10:00";
            string defaultInputCacheTTLString = "2.00:00:00";
            string defaultOutputCacheTTLString = "01:00:00";
            string defaultNotFoundEntryCacheTTLString = "12:00:00";
            
            TimeSpan defaultInputCacheTTL = TimeSpan.Parse(defaultInputCacheTTLString);
            TimeSpan defaultOutputCacheTTL = TimeSpan.Parse(defaultOutputCacheTTLString);
            TimeSpan defaultNotFoundEntryCacheTTL = TimeSpan.Parse(defaultNotFoundEntryCacheTTLString);
    
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.DefaultInputCacheTTL] = defaultInputCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.DefaultOutputCacheTTL] = defaultOutputCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.DefaultNotFoundEntryCacheTTL] = defaultNotFoundEntryCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.ResourceTypeCacheTTLMappings] = resourceTypeCacheTTLMappings;

            var cacheTTLManager = new CacheTTLManager(ConfigMapUtil.Configuration);

            // Output Default
            var ttl = cacheTTLManager.GetCacheTTL("Resourcetyp1", inputType: false);
            Assert.AreEqual(defaultOutputCacheTTL, ttl);

            // Input Default
            ttl = cacheTTLManager.GetCacheTTL("Resourcetyp1", inputType: true);
            Assert.AreEqual(defaultInputCacheTTL, ttl);

            // NotFound Entry Default
            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Resourcetyp1");
            Assert.AreEqual(defaultNotFoundEntryCacheTTL, ttl);

            // ResourceType Specific
            ttl = cacheTTLManager.GetCacheTTL("microsoft.compute/Virtualmachines", inputType: true);
            Assert.AreEqual(TimeSpan.Parse("01:00:00"), ttl);

            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Microsoft.compute/virtualmachines");
            Assert.AreEqual(TimeSpan.Parse("01:00:00"), ttl);

            ttl = cacheTTLManager.GetCacheTTL("Microsoft.storage/Storageaccounts", inputType: false);
            Assert.AreEqual(TimeSpan.Parse("00:10:00"), ttl);

            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Microsoft.storage/storageaccounts");
            Assert.AreEqual(TimeSpan.Parse("00:10:00"), ttl);

            string hotConfigResourceTypeCacheTTLMappings = "microsoft.compute/virtualmachines|01:00:00";
            string hotConfigInputCacheTTLString = "1.00:00:00";
            string hotConfigOutputCacheTTLString = "02:00:00";
            string hotConfigNotFoundEntryCacheTTLString = "09:00:00";

            TimeSpan hotConfigInputCacheTTL = TimeSpan.Parse(hotConfigInputCacheTTLString);
            TimeSpan hotConfigOutputCacheTTL = TimeSpan.Parse(hotConfigOutputCacheTTLString);
            TimeSpan hotConfigNotFoundEntryCacheTTL = TimeSpan.Parse(hotConfigNotFoundEntryCacheTTLString);

            ConfigMapUtil.Configuration[SolutionConstants.DefaultInputCacheTTL] = hotConfigInputCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.DefaultOutputCacheTTL] = hotConfigOutputCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.DefaultNotFoundEntryCacheTTL] = hotConfigNotFoundEntryCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.ResourceTypeCacheTTLMappings] = hotConfigResourceTypeCacheTTLMappings;

            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            // Output Default
            ttl = cacheTTLManager.GetCacheTTL("Resourcetyp1", inputType: false);
            Assert.AreEqual(hotConfigOutputCacheTTL, ttl);

            // Input Default
            ttl = cacheTTLManager.GetCacheTTL("Resourcetyp1", inputType: true);
            Assert.AreEqual(hotConfigInputCacheTTL, ttl);

            // NotFound Entry Default
            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Resourcetyp1");
            Assert.AreEqual(hotConfigNotFoundEntryCacheTTL, ttl);

            // ResourceType Specific
            ttl = cacheTTLManager.GetCacheTTL("microsoft.compute/Virtualmachines", inputType: true);
            Assert.AreEqual(TimeSpan.Parse("01:00:00"), ttl);

            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Microsoft.compute/virtualmachines");
            Assert.AreEqual(TimeSpan.Parse("01:00:00"), ttl);

            // storage account is deleted thorugh hotconfig
            ttl = cacheTTLManager.GetCacheTTL("Microsoft.storage/Storageaccounts", inputType: false);
            Assert.AreEqual(hotConfigOutputCacheTTL, ttl);

            ttl = cacheTTLManager.GetCacheTTL("Microsoft.storage/Storageaccounts", inputType: true);
            Assert.AreEqual(hotConfigInputCacheTTL, ttl);

            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Microsoft.storage/storageaccounts");
            Assert.AreEqual(hotConfigNotFoundEntryCacheTTL, ttl);
        }

        public void CacheTTLManagerTestWithHotConfig()
        {
            string resourceTypeCacheTTLMappings = "microsoft.compute/virtualmachines|01:00:00;microsoft.storage/storageaccounts|00:10:00";
            string defaultInputCacheTTLString = "2.00:00:00";
            string defaultOutputCacheTTLString = "01:00:00";
            string defaultNotFoundEntryCacheTTLString = "12:00:00";

            

            TimeSpan defaultInputCacheTTL = TimeSpan.Parse(defaultInputCacheTTLString);
            TimeSpan defaultOutputCacheTTL = TimeSpan.Parse(defaultOutputCacheTTLString);
            TimeSpan defaultNotFoundEntryCacheTTL = TimeSpan.Parse(defaultNotFoundEntryCacheTTLString);

            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.DefaultInputCacheTTL] = defaultInputCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.DefaultOutputCacheTTL] = defaultOutputCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.DefaultNotFoundEntryCacheTTL] = defaultNotFoundEntryCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.ResourceTypeCacheTTLMappings] = resourceTypeCacheTTLMappings;

            var cacheTTLManager = new CacheTTLManager(ConfigMapUtil.Configuration);

            // HotConfig

            // Output Default
            var ttl = cacheTTLManager.GetCacheTTL("Resourcetyp1", inputType: false);
            Assert.AreEqual(defaultOutputCacheTTL, ttl);

            // Input Default
            ttl = cacheTTLManager.GetCacheTTL("Resourcetyp1", inputType: true);
            Assert.AreEqual(defaultInputCacheTTL, ttl);

            // NotFound Entry Default
            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Resourcetyp1");
            Assert.AreEqual(defaultNotFoundEntryCacheTTL, ttl);

            // ResourceType Specific
            ttl = cacheTTLManager.GetCacheTTL("microsoft.compute/Virtualmachines", inputType: true);
            Assert.AreEqual(TimeSpan.Parse("01:00:00"), ttl);

            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Microsoft.compute/virtualmachines");
            Assert.AreEqual(TimeSpan.Parse("01:00:00"), ttl);

            ttl = cacheTTLManager.GetCacheTTL("Microsoft.storage/Storageaccounts", inputType: false);
            Assert.AreEqual(TimeSpan.Parse("00:10:00"), ttl);

            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Microsoft.storage/storageaccounts");
            Assert.AreEqual(TimeSpan.Parse("00:10:00"), ttl);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void CacheTTLManagerInvalidFormatTest()
        {
            string defaultInputCacheTTLString = "2.00:00:00";
            string defaultOutputCacheTTLString = "01:00:00";

            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.DefaultInputCacheTTL] = defaultInputCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.DefaultOutputCacheTTL] = defaultOutputCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.ResourceTypeCacheTTLMappings] = "microsoft.compute/virtualmachines:01:00:00;microsoft.storage/storageaccounts:00:10:00";

            var cacheTTLManager = new CacheTTLManager(ConfigMapUtil.Configuration);
        }

        [TestMethod]
        public void CacheTTLManagerZeroTimeSpanTest()
        {
            string defaultInputCacheTTLString = "00:00:00";
            string defaultOutputCacheTTLString = "00:00:00";
            string resourceTypeCacheTTLMappings = "microsoft.compute/virtualmachines|00:00:00;microsoft.storage/storageaccounts|00:00:00";

            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.DefaultInputCacheTTL] = defaultInputCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.DefaultOutputCacheTTL] = defaultOutputCacheTTLString;
            ConfigMapUtil.Configuration[SolutionConstants.ResourceTypeCacheTTLMappings] = resourceTypeCacheTTLMappings;

            var cacheTTLManager = new CacheTTLManager(ConfigMapUtil.Configuration);

            // Output Default
            var ttl = cacheTTLManager.GetCacheTTL("Resourcetyp1", inputType: false);
            var hasTTL = ttl != TimeSpan.Zero;
            Assert.IsFalse(hasTTL);

            // Input Default
            ttl = cacheTTLManager.GetCacheTTL("Resourcetyp1", inputType: true);
            hasTTL = ttl != TimeSpan.Zero;
            Assert.IsFalse(hasTTL);
            
            // NotFound Entry Default
            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Resourcetyp1");
            hasTTL = ttl != TimeSpan.Zero;
            Assert.IsFalse(hasTTL);
            
            // ResourceType Specific
            ttl = cacheTTLManager.GetCacheTTL("microsoft.compute/Virtualmachines", inputType: true);
            hasTTL = ttl != TimeSpan.Zero;
            Assert.IsFalse(hasTTL);

            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Microsoft.compute/virtualmachines");
            hasTTL = ttl != TimeSpan.Zero;
            Assert.IsFalse(hasTTL);

            ttl = cacheTTLManager.GetCacheTTL("Microsoft.storage/Storageaccounts", inputType: false);
            hasTTL = ttl != TimeSpan.Zero;
            Assert.IsFalse(hasTTL);

            ttl = cacheTTLManager.GetCacheTTLForNotFoundEntry("Microsoft.storage/storageaccounts");
            hasTTL = ttl != TimeSpan.Zero;
            Assert.IsFalse(hasTTL);
        }
    }
}

