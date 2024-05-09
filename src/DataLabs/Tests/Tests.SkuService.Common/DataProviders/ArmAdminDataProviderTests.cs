namespace Tests.SkuService.Common.DataProviders
{
    using global::SkuService.Common.DataProviders;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Moq;
    using System;
    using System.Threading.Tasks;

    [TestClass]
    public class ArmAdminDataProviderTests
    {
        [TestMethod]
        public async Task GetAndUpdateArmAdminConfigs_IsSuccessful()
        {
            var cloudSpecsString = await File.ReadAllTextAsync("CloudSpecs.json");
            var globalSpecsString = await File.ReadAllTextAsync("GlobalSpecs.json");
            var regionSpecsString = await File.ReadAllTextAsync("RegionSpecs.json");
            var manifestString = await File.ReadAllTextAsync("CRP Manifest.json");
            var proxyClient = new Mock<IResourceProxyClient>();
            var skuProvider = new Mock<ISkuServiceProvider>();
            skuProvider.Setup(x => x.GetServiceName()).Returns("SkuService");
            Environment.SetEnvironmentVariable(SolutionConstants.REGION, "swedencentral");
            var cloudSpecResponse = new DataLabsARMAdminResponse(DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), new DataLabsARMAdminSuccessResponse(cloudSpecsString, DateTimeOffset.UtcNow), null, null, DataLabsDataSource.ARMADMIN);
            var globalSpecResponse = new DataLabsARMAdminResponse(DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), new DataLabsARMAdminSuccessResponse(globalSpecsString, DateTimeOffset.UtcNow), null, null, DataLabsDataSource.ARMADMIN);
            var regionSpecResponse = new DataLabsARMAdminResponse(DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), new DataLabsARMAdminSuccessResponse(regionSpecsString, DateTimeOffset.UtcNow), null, null, DataLabsDataSource.ARMADMIN);
            var manifestResponse = new DataLabsARMAdminResponse(DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), new DataLabsARMAdminSuccessResponse(manifestString, DateTimeOffset.UtcNow), null, null, DataLabsDataSource.ARMADMIN);
            proxyClient.SetupSequence(x => x.GetConfigSpecsAsync(It.IsAny<DataLabsConfigSpecsRequest>(), CancellationToken.None, false, false, null, null))
                .ReturnsAsync(cloudSpecResponse)
                .ReturnsAsync(globalSpecResponse)
                .ReturnsAsync(regionSpecResponse);
            proxyClient.Setup(x => x.GetManifestConfigAsync(It.IsAny<DataLabsManifestConfigRequest>(), CancellationToken.None, false, false, null, null))
                .ReturnsAsync(manifestResponse);
            var provider = new ArmAdminDataProvider(proxyClient.Object, skuProvider.Object);
            await provider.GetAndUpdateArmAdminConfigsAsync(CancellationToken.None);
            Assert.IsNotNull(provider.GetAllowedAvailabilityZoneMappings);
            Assert.IsNotNull(provider.GetFeatureFlagsToLocationMappings);
            Assert.IsNotNull(provider.GetAllowedProviderRegistrationLocationsWithFeatureFlag);
        }

        [TestMethod]
        public async Task GetAndUpdateArmAdminConfigs_ThrowsException()
        {
            var proxyClient = new Mock<IResourceProxyClient>();
            var skuProvider = new Mock<ISkuServiceProvider>();
            skuProvider.Setup(x => x.GetServiceName()).Returns("SkuService");
            proxyClient.Setup(x => x.GetConfigSpecsAsync(It.IsAny<DataLabsConfigSpecsRequest>(), CancellationToken.None, false, false, null, null))
                .ThrowsAsync(new Exception());
            var provider = new ArmAdminDataProvider(proxyClient.Object, skuProvider.Object);
            await Assert.ThrowsExceptionAsync<Exception>(() => provider.GetAndUpdateArmAdminConfigsAsync(CancellationToken.None));
        }
    }
}
