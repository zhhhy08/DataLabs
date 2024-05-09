namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Client
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AccessTokenProvider;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client;
    using Moq;
    using System.Reflection;
    using System.Net;

    [TestClass]
    public class ArmClientTests
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
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public async Task TestArmClientOk()
        {
            var mockRestClient = new Mock<IRestClient>();
            mockRestClient.Setup(x => x.CallRestApiAsync(
                It.IsAny<IEndPointSelector>(), 
                It.IsAny<string>(),
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<KeyValuePair<string, string>>?>(),
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("test") });

            var endPointSelector = new EndPointSelector(new string[] { "http://127.0.0.1" } );
            var armClientOptions = new ARMClientOptions(ConfigMapUtil.Configuration)
            {
                EndPointSelector = endPointSelector,
                ARMTokenResource = "https://management.azure.com/",
                AADAuthority = "https://login.microsoftonline.com/",
                DefaultTenantId = "02d59989-f8a9-4b69-9919-1ef51df4eff6",
                FirstPartyAppId = null
            };

            var armClient = new ARMClient(new TestAccessTokenProvider(), armClientOptions);

            // Set RestClient with Mock RestClient
            FieldInfo fieldInfo = typeof(ARMClient).GetField("_restClient", BindingFlags.Instance | BindingFlags.NonPublic);
            fieldInfo.SetValue(armClient, mockRestClient.Object);

            var resourceId = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var apiVersion = "2022-11-01";

            var useResourceGraph = false;
            var response = await armClient.GetResourceAsync(
                resourceId: resourceId, 
                tenantId: null, 
                apiVersion: apiVersion, 
                useResourceGraph: useResourceGraph, 
                clientRequestId: null, 
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public async Task TestArmClientWithARGOk()
        {
            var mockRestClient = new Mock<IRestClient>();
            mockRestClient.Setup(x => x.CallRestApiAsync(
                It.IsAny<IEndPointSelector>(),
                It.IsAny<string>(),
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<KeyValuePair<string, string>>?>(),
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("test") });

            var endPointSelector = new EndPointSelector(new string[] { "http://127.0.0.1" });
            var armClientOptions = new ARMClientOptions(ConfigMapUtil.Configuration)
            {
                EndPointSelector = endPointSelector,
                ARMTokenResource = "https://management.azure.com/",
                AADAuthority = "https://login.microsoftonline.com/",
                DefaultTenantId = "02d59989-f8a9-4b69-9919-1ef51df4eff6",
                FirstPartyAppId = null
            };

            var armClient = new ARMClient(new TestAccessTokenProvider(), armClientOptions);

            // Set RestClient with Mock RestClient
            FieldInfo fieldInfo = typeof(ARMClient).GetField("_restClient", BindingFlags.Instance | BindingFlags.NonPublic);
            fieldInfo.SetValue(armClient, mockRestClient.Object);

            var resourceId = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var apiVersion = "2022-11-01";

            var useResourceGraph = true;
            var response = await armClient.GetResourceAsync(
                resourceId: resourceId,
                tenantId: null,
                apiVersion: apiVersion,
                useResourceGraph: useResourceGraph,
                clientRequestId: null,
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public async Task TestArmClientWithARG422()
        {
            var mockRestClient = new Mock<IRestClient>();
            mockRestClient.SetupSequence(x => x.CallRestApiAsync(
                It.IsAny<IEndPointSelector>(),
                It.IsAny<string>(),
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<KeyValuePair<string, string>>?>(),
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("test") });

            var endPointSelector = new EndPointSelector(new string[] { "http://127.0.0.1" });
            var armClientOptions = new ARMClientOptions(ConfigMapUtil.Configuration)
            {
                EndPointSelector = endPointSelector,
                ARMTokenResource = "https://management.azure.com/",
                AADAuthority = "https://login.microsoftonline.com/",
                DefaultTenantId = "02d59989-f8a9-4b69-9919-1ef51df4eff6",
                FirstPartyAppId = null
            };

            var armClient = new ARMClient(new TestAccessTokenProvider(), armClientOptions);

            // Set RestClient with Mock RestClient
            FieldInfo fieldInfo = typeof(ARMClient).GetField("_restClient", BindingFlags.Instance | BindingFlags.NonPublic);
            fieldInfo.SetValue(armClient, mockRestClient.Object);

            var resourceId = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var apiVersion = "2022-11-01";

            var useResourceGraph = true;
            var response = await armClient.GetResourceAsync(
                resourceId: resourceId,
                tenantId: null,
                apiVersion: apiVersion,
                useResourceGraph: useResourceGraph,
                clientRequestId: null,
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

