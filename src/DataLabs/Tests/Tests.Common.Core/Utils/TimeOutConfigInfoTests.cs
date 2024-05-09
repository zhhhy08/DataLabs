namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Utils
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    [TestClass]
    public class TimeOutConfigInfoTests
    {
        [TestMethod]
        public void TestParseDefaultTimeOut()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Reset();
            ConfigMapUtil.Initialize(configBuilder, false);

            var configKey = "TestTimeOut";
            var defaultTimeOutString = "35/60";

            var timeOutConfigInfo = new TimeOutConfigInfo(configKey, defaultTimeOutString, ConfigMapUtil.Configuration);

            Assert.AreEqual(TimeSpan.Parse("00:00:35"), timeOutConfigInfo.NonRetryFlowTimeOut);
            Assert.AreEqual(TimeSpan.Parse("00:00:35"), timeOutConfigInfo.GetTimeOut(0));
            Assert.AreEqual(TimeSpan.Parse("00:01:00"), timeOutConfigInfo.RetryFlowTimeOut);
            Assert.AreEqual(TimeSpan.Parse("00:01:00"), timeOutConfigInfo.GetTimeOut(1));

            // HotConfig
            ConfigMapUtil.Configuration[configKey] = "15/45";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            Assert.AreEqual(TimeSpan.Parse("00:00:15"), timeOutConfigInfo.NonRetryFlowTimeOut);
            Assert.AreEqual(TimeSpan.Parse("00:00:15"), timeOutConfigInfo.GetTimeOut(0));
            Assert.AreEqual(TimeSpan.Parse("00:00:45"), timeOutConfigInfo.RetryFlowTimeOut);
            Assert.AreEqual(TimeSpan.Parse("00:00:45"), timeOutConfigInfo.GetTimeOut(1));
        }
    }
}
