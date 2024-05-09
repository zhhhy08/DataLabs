namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceFetcherService.Utils
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientTimeOutManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    [TestClass]
    public class ArmClientTimeOutManagerTests
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
        public void TestArmClientTimeOutManager()
        {
            var appSettingsStub = new Dictionary<string, string>
            {
                { SolutionConstants.DefaultArmClientGetResourceTimeOutInSec, "10/20" },
                { SolutionConstants.DefaultArmClientGenericApiTimeOutInSec, "15/25" }
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub);

            ConfigMapUtil.Initialize(config, false);

            ArmClientTimeOutManager.Reset();
            var timeoutManager = ArmClientTimeOutManager.Create(ConfigMapUtil.Configuration);

            Assert.AreEqual(TimeSpan.FromSeconds(10), timeoutManager.GetResourceTypeTimeOut("testType1", 0));
            Assert.AreEqual(TimeSpan.FromSeconds(20), timeoutManager.GetResourceTypeTimeOut("testType1", 1));

            Assert.AreEqual(TimeSpan.FromSeconds(15), timeoutManager.GetGenericApiTimeOut("urlPath1", 0));
            Assert.AreEqual(TimeSpan.FromSeconds(25), timeoutManager.GetGenericApiTimeOut("urlPath1", 1));

            // HotConfig to add resourceType specific timeout
            ConfigMapUtil.Configuration[SolutionConstants.ArmClientResourceTimeOutMappings] = "testType1|4/14;testType2|24/34";
            ConfigMapUtil.Configuration[SolutionConstants.ArmClientGenericApiTimeOutMappings] = "urlPath1|4/14;urlPath2|24/34";

            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            Assert.AreEqual(TimeSpan.FromSeconds(4), timeoutManager.GetResourceTypeTimeOut("testType1", 0));
            Assert.AreEqual(TimeSpan.FromSeconds(14), timeoutManager.GetResourceTypeTimeOut("testType1", 1));
            Assert.AreEqual(TimeSpan.FromSeconds(24), timeoutManager.GetResourceTypeTimeOut("testType2", 0));
            Assert.AreEqual(TimeSpan.FromSeconds(34), timeoutManager.GetResourceTypeTimeOut("testType2", 1));

            Assert.AreEqual(TimeSpan.FromSeconds(4), timeoutManager.GetGenericApiTimeOut("urlPath1", 0));
            Assert.AreEqual(TimeSpan.FromSeconds(14), timeoutManager.GetGenericApiTimeOut("urlPath1", 1));
            Assert.AreEqual(TimeSpan.FromSeconds(24), timeoutManager.GetGenericApiTimeOut("urlPath2", 0));
            Assert.AreEqual(TimeSpan.FromSeconds(34), timeoutManager.GetGenericApiTimeOut("urlPath2", 1));

            Assert.AreEqual(TimeSpan.FromSeconds(10), timeoutManager.GetResourceTypeTimeOut("testType3", 0));
            Assert.AreEqual(TimeSpan.FromSeconds(20), timeoutManager.GetResourceTypeTimeOut("testType3", 1));
            Assert.AreEqual(TimeSpan.FromSeconds(15), timeoutManager.GetGenericApiTimeOut("urlPath3", 0));
            Assert.AreEqual(TimeSpan.FromSeconds(25), timeoutManager.GetGenericApiTimeOut("urlPath3", 1));

            ArmClientTimeOutManager.Reset();
        }
    }
}
