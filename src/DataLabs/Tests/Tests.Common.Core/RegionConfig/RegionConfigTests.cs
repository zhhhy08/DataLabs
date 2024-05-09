

namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.RegionConfig
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;

    [TestClass]
    public class RegionConfigTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.UseSourceOfTruth] = "false";
            ConfigMapUtil.Configuration[SolutionConstants.PrimaryRegionName] = "p-eus";
            ConfigMapUtil.Configuration[SolutionConstants.BackupRegionName] = "b-eus";

        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public void TestRegionConfigExists_SourceOfTruthNotUsed()
        {
            RegionConfigManager.Initialize(ConfigMapUtil.Configuration, CancellationToken.None);
            string primaryRegionName = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.PrimaryRegionName);
            string backupRegionName = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.BackupRegionName);


            var primary = RegionConfigManager.GetRegionConfig(primaryRegionName);
            Assert.IsNotNull(primary);
            Assert.IsNull(primary.outputBlobClient);
            Assert.AreEqual(string.Empty, primary.sourceOfTruthStorageAccountNames);

            var backup = RegionConfigManager.GetRegionConfig(backupRegionName);
            Assert.IsNotNull(backup);
            Assert.IsNull(backup.outputBlobClient);
            Assert.AreEqual(string.Empty, backup.sourceOfTruthStorageAccountNames);
        }

        [TestMethod]
        public void TestRegionConfigDoesNotExist_ThrowsException()
        {
            RegionConfigManager.Initialize(ConfigMapUtil.Configuration, CancellationToken.None);
            Assert.ThrowsException<NotSupportedException>(() => RegionConfigManager.GetRegionConfig("RandomRegion"));
        }

        [DataTestMethod]
        [DataRow(null, DisplayName = "TestGetRegionConfig_AcceptedInput_null")]
        [DataRow("", DisplayName = "TestGetRegionConfig_AcceptedInput_emptystring")]
        public void TestGetRegionConfig_AcceptedInput(string regionPairName)
        {
            RegionConfigManager.Initialize(ConfigMapUtil.Configuration, CancellationToken.None);
            
            try
            {
                var result = RegionConfigManager.GetRegionConfig(regionPairName);
                Assert.IsNotNull(result);
            } catch (NotSupportedException)
            {
                Assert.Fail("Test is not supposed to throw exception");
            }
        }

        [TestMethod]
        public void TestRegionConfig_NotInitialized()
        {
            Assert.ThrowsException<NotSupportedException>(() => RegionConfigManager.GetRegionConfig("RandomRegion"));
        }
    }
}
