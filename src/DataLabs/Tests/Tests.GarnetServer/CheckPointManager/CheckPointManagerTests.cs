namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.GarnetServer.CheckPointManager
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.GarnetServer;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common;

    [TestClass]
    public class CheckPointManagerTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(config, false);
        }

        [TestCleanup]
        public void TestCleanup()
        {

        }

        [TestMethod]
        public async Task TestUseBackgroundSaveCallBack()
        {
            ConfigMapUtil.Configuration[GarnetConstants.UseBackgroundSave] = "false";

            using var checkPointManager = new CheckPointManager();

            var useBackGroundSave = (bool)PrivateFunctionAccessHelper.GetPrivateField(
                      typeof(CheckPointManager),
                      "_useBackgroundSave", checkPointManager);

            Assert.IsFalse(useBackGroundSave);

            // Hotconfig

            ConfigMapUtil.Configuration[GarnetConstants.UseBackgroundSave] = "true";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(default);
            await Task.Delay(100);

            useBackGroundSave = (bool)PrivateFunctionAccessHelper.GetPrivateField(
                typeof(CheckPointManager),
                "_useBackgroundSave", checkPointManager);
            Assert.IsTrue(useBackGroundSave);
        }
    }
}

