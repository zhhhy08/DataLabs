namespace Tests.ResourceProxyClient
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Data;
    using System.Net;

    [TestClass]
    public class GetManifestConfigFlowTests : BaseTestsInitialize
    {
        [TestMethod]
        public async Task ResourceFetcherGetManifestConfigResponse200()
        {
            // Arrange
            var allowedTypesInProxy = "*:cache|write/01:00:00,resourcefetcher_armadmin|2022-12-01";
            var allowedTypesInFetcher = "GetManifestConfigAsync|2016-12-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var manifestProvider = "microsoft.compute";
            var manifestRequest = new DataLabsManifestConfigRequest(traceId, 0, correlationId, manifestProvider);

            var testResponse = @"{""key"": ""value""}";
            
            // Set content to TestARMAdminClient
            _resourceProxyFlowTestManager.ARMAdminClient.SetResource(manifestProvider, testResponse);

            // Act
            var dataLabsARMAdminResponse = await _resourceProxyClient.GetManifestConfigAsync(
                request: manifestRequest,
                cancellationToken: default)
                .ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(dataLabsARMAdminResponse);
            Assert.IsNotNull(dataLabsARMAdminResponse.SuccessAdminResponse);
            Assert.IsNull(dataLabsARMAdminResponse.ErrorResponse);
            Assert.AreEqual(DataLabsDataSource.ARMADMIN, dataLabsARMAdminResponse.DataSource);
            Assert.AreEqual(testResponse, dataLabsARMAdminResponse.SuccessAdminResponse.Resource);
        }

        [TestMethod]
        public async Task ResourceFetcherConfigSpecsResponse200AndCacheHit()
        {
            // Arrange
            var allowedTypesInProxy = "*:cache|write/20:00:00,resourcefetcher_armadmin|2022-12-01";
            var allowedTypesInFetcher = "GetManifestConfigAsync|2016-12-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var resourceProxyAllowedTypesConfigManager = _resourceProxyFlowTestManager.ResourceProxyAllowedTypesConfigManager;
            var allowedTypesConfigInfo = resourceProxyAllowedTypesConfigManager.GetAllowedTypesConfigInfo(ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes);
            var CacheProviderConfig = allowedTypesConfigInfo.AllowedTypesMap["*"].CacheProviderConfig;
            Assert.IsNotNull(CacheProviderConfig);
            Assert.AreEqual(TimeSpan.Parse("20:00:00"), CacheProviderConfig.WriteTTL);
            Assert.AreEqual(TimeSpan.Parse("20:00:00"), CacheProviderConfig.ReadTTL);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var manifestProvider = "microsoft.compute";
            var manifestRequest = new DataLabsManifestConfigRequest(traceId, 0, correlationId, manifestProvider);

            var testResponse = @"{""key"": ""value""}";

            // Set content to TestARMAdminClient
            _resourceProxyFlowTestManager.ARMAdminClient.SetResource(manifestProvider, testResponse);

            // Act
            var dataLabsARMAdminResponse = await _resourceProxyClient.GetManifestConfigAsync(
                request: manifestRequest,
                cancellationToken: default)
                .ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(dataLabsARMAdminResponse);
            Assert.IsNotNull(dataLabsARMAdminResponse.SuccessAdminResponse);
            Assert.IsNull(dataLabsARMAdminResponse.ErrorResponse);
            Assert.AreEqual(DataLabsDataSource.ARMADMIN, dataLabsARMAdminResponse.DataSource);
            Assert.AreEqual(testResponse, dataLabsARMAdminResponse.SuccessAdminResponse.Resource);

            correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            traceId = ResourceProxyClientTestData.CreateNewActivityId();

            // Act
            manifestRequest = new DataLabsManifestConfigRequest(traceId, 0, correlationId, manifestProvider);

            dataLabsARMAdminResponse = await _resourceProxyClient.GetManifestConfigAsync(
                request: manifestRequest,
                cancellationToken: default)
                .ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(dataLabsARMAdminResponse);
            Assert.IsNotNull(dataLabsARMAdminResponse.SuccessAdminResponse);
            Assert.IsNull(dataLabsARMAdminResponse.ErrorResponse);
            Assert.AreEqual(DataLabsDataSource.CACHE, dataLabsARMAdminResponse.DataSource);
            Assert.AreEqual(testResponse, dataLabsARMAdminResponse.SuccessAdminResponse.Resource);
        }

        [TestMethod]
        public async Task ResourceFetcherConfigSpecsResponse404()
        {
            // Arrange
            var allowedTypesInProxy = "*:cache|write/01:00:00,resourcefetcher_armadmin|2022-12-01";
            var allowedTypesInFetcher = "GetManifestConfigAsync|2016-12-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var manifestProvider = "microsoft.compute";
            var manifestRequest = new DataLabsManifestConfigRequest(traceId, 0, correlationId, manifestProvider);

            // No Set content to TestARMAdminClient

            // Act
            var dataLabsARMAdminResponse = await _resourceProxyClient.GetManifestConfigAsync(
                request: manifestRequest,
                cancellationToken: default)
                .ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(dataLabsARMAdminResponse);
            Assert.IsNull(dataLabsARMAdminResponse.SuccessAdminResponse);
            Assert.IsNotNull(dataLabsARMAdminResponse.ErrorResponse);
            Assert.AreEqual(404, dataLabsARMAdminResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(DataLabsDataSource.ARMADMIN, dataLabsARMAdminResponse.DataSource);
        }

        [TestMethod]
        public async Task ResourceFetcherConfigSpecResponse500()
        {
            // Arrange
            var allowedTypesInProxy = "*:cache|write/01:00:00,resourcefetcher_armadmin|2022-12-01";
            var allowedTypesInFetcher = "GetManifestConfigAsync|2016-12-01";
            
            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetManifestConfigAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var httpStatusCode = HttpStatusCode.InternalServerError;
            _resourceProxyFlowTestManager.ARMAdminClient.ErrStatusCode = httpStatusCode;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();
            var manifestProvider = "microsoft.compute";
            var manifestRequest = new DataLabsManifestConfigRequest(traceId, 0, correlationId, manifestProvider);

            // Act
            var dataLabsARMAdminResponse = await _resourceProxyClient.GetManifestConfigAsync(
                request: manifestRequest,
                cancellationToken: default)
                .ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(dataLabsARMAdminResponse);
            Assert.IsNull(dataLabsARMAdminResponse.SuccessAdminResponse);
            Assert.IsNotNull(dataLabsARMAdminResponse.ErrorResponse);
            Assert.AreEqual((int)httpStatusCode, dataLabsARMAdminResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(DataLabsDataSource.ARMADMIN, dataLabsARMAdminResponse.DataSource);
        }
    }
}
