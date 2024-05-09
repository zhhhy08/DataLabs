namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceFetcherService.Auth
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;

    [TestClass]
    public class PartnerAuthorizeConfigTests
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
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);

            var partnerName = "testsolution";
            var clientKey = partnerName + SolutionConstants.PartnerClientIdsSuffix;
            var configKey1 = partnerName + SolutionConstants.ArmAllowedResourceTypesSuffix;
            var configKey2 = partnerName + SolutionConstants.ArmAllowedGenericURIPathsSuffix;

            var testClientKey = "0b18b978-4ca2-3d9a-1de5-012e2c34cbb0";
            ConfigMapUtil.Configuration[SolutionConstants.PartnerNames] = partnerName;
            ConfigMapUtil.Configuration[clientKey] = testClientKey;
            ConfigMapUtil.Configuration[configKey1] = "type1|2022-11-01;type2|2023-12-01,useResourceGraph";
            ConfigMapUtil.Configuration[configKey2] = "urlPath1|2022-10-10";

            var partnerAuthorizeManager = new PartnerAuthorizeManager(ConfigMapUtil.Configuration);
            var partnerAuthConfig = partnerAuthorizeManager.GetPartnerAuthorizeConfig(partnerName);
            Assert.IsNotNull(partnerAuthConfig);

            Assert.AreEqual(partnerName, partnerAuthConfig.PartnerName);
            Assert.AreEqual(1, partnerAuthConfig.ClientIds.Count);
            Assert.IsTrue(partnerAuthConfig.ClientIds.Contains(testClientKey));
            Assert.IsTrue(partnerAuthConfig.ArmAllowedResourceTypeApiVersionMap.TryGetValue("type1", out var apiVersionAndOption));
            Assert.AreEqual("2022-11-01", apiVersionAndOption.ApiVersion);
            Assert.IsFalse(apiVersionAndOption.UseResourceGraph);
            Assert.IsTrue(partnerAuthConfig.ArmAllowedResourceTypeApiVersionMap.TryGetValue("type2", out apiVersionAndOption));
            Assert.AreEqual("2023-12-01", apiVersionAndOption.ApiVersion);
            Assert.IsTrue(apiVersionAndOption.UseResourceGraph);
            Assert.IsFalse(partnerAuthConfig.ArmAllowedResourceTypeApiVersionMap.TryGetValue("type3", out apiVersionAndOption));
            Assert.IsTrue(partnerAuthConfig.ArmAllowedGenericURIPathApiVersionMap.TryGetValue("urlPath1", out var apiVersion));
            Assert.AreEqual("2022-10-10", apiVersion);
            Assert.IsFalse(partnerAuthConfig.ArmAllowedGenericURIPathApiVersionMap.TryGetValue("urlPath2", out apiVersion));

            // HotConfig
            ConfigMapUtil.Configuration[configKey1] = "type1|2022-11-02;type2|2023-11-02";
            ConfigMapUtil.Configuration[configKey2] = "urlPath1|2022-10-11;urlPath2|2023-10-11";

            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            Thread.Sleep(50);

            partnerAuthConfig = partnerAuthorizeManager.GetPartnerAuthorizeConfig(partnerName);
            Assert.IsNotNull(partnerAuthConfig);

            Assert.AreEqual(partnerName, partnerAuthConfig.PartnerName);
            Assert.AreEqual(1, partnerAuthConfig.ClientIds.Count);
            Assert.IsTrue(partnerAuthConfig.ClientIds.Contains(testClientKey));
            Assert.IsTrue(partnerAuthConfig.ArmAllowedResourceTypeApiVersionMap.TryGetValue("type1", out apiVersionAndOption));
            Assert.AreEqual("2022-11-02", apiVersionAndOption.ApiVersion);
            Assert.IsFalse(apiVersionAndOption.UseResourceGraph);
            Assert.IsTrue(partnerAuthConfig.ArmAllowedResourceTypeApiVersionMap.TryGetValue("type2", out apiVersionAndOption));
            Assert.AreEqual("2023-11-02", apiVersionAndOption.ApiVersion);
            Assert.IsFalse(apiVersionAndOption.UseResourceGraph);
            Assert.IsTrue(partnerAuthConfig.ArmAllowedGenericURIPathApiVersionMap.TryGetValue("urlPath1", out apiVersion));
            Assert.AreEqual("2022-10-11", apiVersion);
            Assert.IsTrue(partnerAuthConfig.ArmAllowedGenericURIPathApiVersionMap.TryGetValue("urlPath2", out apiVersion));
            Assert.AreEqual("2023-10-11", apiVersion);
        }
    }
}
