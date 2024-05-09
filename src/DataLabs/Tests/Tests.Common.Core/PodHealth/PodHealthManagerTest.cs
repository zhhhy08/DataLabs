namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.PodHealth
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PodHealth;

    [TestClass]
    public class PodHealthManagerTest
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
        public void DenyListInConstructorTest()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.PartnerDenyList] = "10.0.0.2";

            var serviceAddr = "TestService";
            var podHealthManager = new PodHealthManager(serviceAddr, SolutionConstants.PartnerDenyList);

            Assert.AreEqual(serviceAddr, podHealthManager.ServiceName);

            var expectedSet = new HashSet<string>() { "10.0.0.2" };
            Assert.IsTrue(expectedSet.SetEquals(podHealthManager.DenyListedNodes));

        }

        [TestMethod]
        public void HotConfigTest()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);

            var serviceAddr = "TestService";
            var podHealthManager = new PodHealthManager(serviceAddr, SolutionConstants.PartnerDenyList);

            Assert.AreEqual(serviceAddr, podHealthManager.ServiceName);
            Assert.AreEqual(0, podHealthManager.DenyListedNodes.Count);

            // Add HotConfig
            var oldDenyListNodes = podHealthManager.DenyListedNodes;
            ConfigMapUtil.Configuration[SolutionConstants.PartnerDenyList] = "10.10.10.100;10.10.10.101;10.10.10.102";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(default);
            Thread.Sleep(200);

            var expectedSet = new HashSet<string>() { "10.10.10.100", "10.10.10.101", "10.10.10.102" };
            Assert.IsTrue(expectedSet.SetEquals(podHealthManager.DenyListedNodes));
            Assert.IsFalse(ReferenceEquals(oldDenyListNodes, podHealthManager.DenyListedNodes));

            // UnDenyList 10.10.10.100
            oldDenyListNodes = podHealthManager.DenyListedNodes;
            ConfigMapUtil.Configuration[SolutionConstants.PartnerDenyList] = "10.10.10.101;10.10.10.102";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(default);
            Thread.Sleep(200);

            expectedSet = new HashSet<string>() { "10.10.10.101", "10.10.10.102" };
            Assert.IsTrue(expectedSet.SetEquals(podHealthManager.DenyListedNodes));
            Assert.IsFalse(ReferenceEquals(oldDenyListNodes, podHealthManager.DenyListedNodes));

            // UnDenyList all
            oldDenyListNodes = podHealthManager.DenyListedNodes;
            ConfigMapUtil.Configuration[SolutionConstants.PartnerDenyList] = SolutionConstants.NoneValue;
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(default);
            Thread.Sleep(200);
            Assert.IsTrue(podHealthManager.DenyListedNodes.Count == 0);
            Assert.IsFalse(ReferenceEquals(oldDenyListNodes, podHealthManager.DenyListedNodes));
        }
    }
}

