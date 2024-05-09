namespace Tests.ResourceProxyClient
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Data;
    using Newtonsoft.Json;
    using System.Net;

    [TestClass]
    public class GetConfigSpecsFlowTests : BaseTestsInitialize
    {
        [TestMethod]
        public async Task ResourceFetcherConfigSpecsResponse200()
        {
            // Arrange
            var allowedTypesInProxy = "*:cache|write/01:00:00,resourcefetcher_armadmin|2022-12-01";
            var allowedTypesInFetcher = "GetConfigSpecsAsync|2016-12-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var specType = "/clouds/public";
            var configSpecsRequest = new DataLabsConfigSpecsRequest(traceId, 0, correlationId, specType);

            var testResponse = @"{""key"": ""value""}";
            
            // Set content to TestARMAdminClient
            _resourceProxyFlowTestManager.ARMAdminClient.SetResource(specType, testResponse);

            // Act
            var dataLabsARMAdminResponse = await _resourceProxyClient.GetConfigSpecsAsync(
                request: configSpecsRequest,
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
            var allowedTypesInProxy = "*:cache|write/01:00:00,resourcefetcher_armadmin|2022-12-01";
            var allowedTypesInFetcher = "GetConfigSpecsAsync|2016-12-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var specType = "/clouds/public";
            var configSpecsRequest = new DataLabsConfigSpecsRequest(traceId, 0, correlationId, specType);

            var testResponse = @"{""key"": ""value""}";

            // Set content to TestARMAdminClient
            _resourceProxyFlowTestManager.ARMAdminClient.SetResource(specType, testResponse);

            // Act
            var dataLabsARMAdminResponse = await _resourceProxyClient.GetConfigSpecsAsync(
                request: configSpecsRequest,
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
            configSpecsRequest = new DataLabsConfigSpecsRequest(traceId, 0, correlationId, specType);

            dataLabsARMAdminResponse = await _resourceProxyClient.GetConfigSpecsAsync(
                request: configSpecsRequest,
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
            var allowedTypesInFetcher = "GetConfigSpecsAsync|2016-12-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var specType = "/global";
            var configSpecsRequest = new DataLabsConfigSpecsRequest(traceId, 0, correlationId, specType);

            // No Set content to TestARMAdminClient

            // Act
            var dataLabsARMAdminResponse = await _resourceProxyClient.GetConfigSpecsAsync(
                request: configSpecsRequest,
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
            var allowedTypesInFetcher = "GetConfigSpecsAsync|2016-12-01";
            
            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetConfigSpecsAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var httpStatusCode = HttpStatusCode.InternalServerError;
            _resourceProxyFlowTestManager.ARMAdminClient.ErrStatusCode = httpStatusCode;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();
            var specType = "/clouds/public";
            var configSpecsRequest = new DataLabsConfigSpecsRequest(traceId, 0, correlationId, specType);

            // Act
            var dataLabsARMAdminResponse = await _resourceProxyClient.GetConfigSpecsAsync(
                request: configSpecsRequest,
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
