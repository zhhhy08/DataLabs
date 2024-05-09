namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceProxyClient
{
    using global::Tests.ResourceProxyClient;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Data;
    using System.Net;

    [TestClass]
    public class GetCollectionFlowTests: BaseTestsInitialize
    {
        [TestMethod]
        public async Task ResourceFetcherQFDCollection200()
        {
            // Update config
            var allowedTypesInProxy = "microsoft.features/featureproviders/subscriptionfeatureregistrations:cache|write/00:10:00,resourcefetcher_qfd";
            var allowedTypesInFetcher = "microsoft.features/featureproviders/subscriptionfeatureregistrations|2021-07-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetCollectionAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var resourceId = "/subscriptions/30033233-7071-43b7-abd4-dee283630133/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations";
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set content to QFDClient collection call
            _resourceProxyFlowTestManager.QFDClient.SetResource(resourceId, ResourceProxyClientTestData.AfecCollectionGetArmResource);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();
            
            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceCollectionResponse = await _resourceProxyClient.GetCollectionAsync(
                request: request,
                cancellationToken: default).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.QFD, dataLabsResourceCollectionResponse.DataSource);
            Assert.IsTrue(dataLabsResourceCollectionResponse.Attributes == null || dataLabsResourceCollectionResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceCollectionResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceCollectionResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceCollectionResponse.ErrorResponse);
            Assert.IsNotNull(dataLabsResourceCollectionResponse.SuccessResponse);
            Assert.IsTrue(dataLabsResourceCollectionResponse.SuccessResponse?.Value?.Count > 0);
        }

        [TestMethod]
        public async Task DirectQFDCollection200()
        {
            // Update config
            var allowedTypesInProxy = "microsoft.features/featureproviders/subscriptionfeatureregistrations:cache|write/00:10:00,qfd";
            var allowedTypesInFetcher = "microsoft.features/featureproviders/subscriptionfeatureregistrations|2021-07-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetCollectionAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var resourceId = "/subscriptions/30033233-7071-43b7-abd4-dee283630133/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations";
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set content to QFDClient collection call
            _resourceProxyFlowTestManager.QFDClient.SetResource(resourceId, ResourceProxyClientTestData.AfecCollectionGetArmResource);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceCollectionResponse = await _resourceProxyClient.GetCollectionAsync(
                request: request,
                cancellationToken: default).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.QFD, dataLabsResourceCollectionResponse.DataSource);
            Assert.IsTrue(dataLabsResourceCollectionResponse.Attributes == null || dataLabsResourceCollectionResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceCollectionResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceCollectionResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceCollectionResponse.ErrorResponse);
            Assert.IsNotNull(dataLabsResourceCollectionResponse.SuccessResponse);
            Assert.IsTrue(dataLabsResourceCollectionResponse.SuccessResponse?.Value?.Count > 0);
        }

        [TestMethod]
        public async Task ResourceFetcherQFDCollection200AndCacheHit()
        {
            // Update config
            var allowedTypesInProxy = "microsoft.features/featureproviders/subscriptionfeatureregistrations:cache|write/00:10:00,resourcefetcher_qfd";
            var allowedTypesInFetcher = "microsoft.features/featureproviders/subscriptionfeatureregistrations|2021-07-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetCollectionAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var resourceId = "/subscriptions/30033233-7071-43b7-abd4-dee283630133/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations";
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set content to QFDClient collection call
            _resourceProxyFlowTestManager.QFDClient.SetResource(resourceId, ResourceProxyClientTestData.AfecCollectionGetArmResource);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceCollectionResponse = await _resourceProxyClient.GetCollectionAsync(
                request: request,
                cancellationToken: default).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.QFD, dataLabsResourceCollectionResponse.DataSource);
            Assert.IsTrue(dataLabsResourceCollectionResponse.Attributes == null || dataLabsResourceCollectionResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceCollectionResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceCollectionResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceCollectionResponse.ErrorResponse);
            Assert.IsNotNull(dataLabsResourceCollectionResponse.SuccessResponse);
            Assert.IsTrue(dataLabsResourceCollectionResponse.SuccessResponse?.Value?.Count > 0);

            // Now above entry is saved in cache
            // Let's call one more to get it from cache

            request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            dataLabsResourceCollectionResponse = await _resourceProxyClient.GetCollectionAsync(
                request: request,
                cancellationToken: default).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.CACHE, dataLabsResourceCollectionResponse.DataSource);
            Assert.IsTrue(dataLabsResourceCollectionResponse.Attributes == null || dataLabsResourceCollectionResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceCollectionResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceCollectionResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceCollectionResponse.ErrorResponse);
            Assert.IsNotNull(dataLabsResourceCollectionResponse.SuccessResponse);
            Assert.IsTrue(dataLabsResourceCollectionResponse.SuccessResponse?.Value?.Count > 0);
        }


        [TestMethod]
        public async Task ResourceFetcherQFDCollection500()
        {
            // Update config
            var allowedTypesInProxy = "microsoft.features/featureproviders/subscriptionFeatureRegistrations:resourcefetcher_qfd";
            var allowedTypesInFetcher = "microsoft.features/featureproviders/subscriptionFeatureRegistrations|2021-07-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetCollectionAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            // Set HttpErrorStatus to QFDClient collection call
            var errorHttpStatusCode = HttpStatusCode.InternalServerError;
            _resourceProxyFlowTestManager.QFDClient.ErrStatusCode = errorHttpStatusCode;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var resourceId = "/subscriptions/30033233-7071-43b7-abd4-dee283630133/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations";
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceCollectionResponse = await _resourceProxyClient.GetCollectionAsync(
                request: request,
                cancellationToken: default).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.QFD, dataLabsResourceCollectionResponse.DataSource);
            Assert.IsTrue(dataLabsResourceCollectionResponse.Attributes == null || dataLabsResourceCollectionResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceCollectionResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceCollectionResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceCollectionResponse.SuccessResponse);
            Assert.IsNotNull(dataLabsResourceCollectionResponse.ErrorResponse);

            Assert.IsNotNull(dataLabsResourceCollectionResponse.ErrorResponse);
            Assert.AreEqual(DataLabsErrorType.RETRY, dataLabsResourceCollectionResponse.ErrorResponse.ErrorType);
            Assert.AreEqual(0, dataLabsResourceCollectionResponse.ErrorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual((int)errorHttpStatusCode, dataLabsResourceCollectionResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(errorHttpStatusCode.FastEnumToString(), dataLabsResourceCollectionResponse.ErrorResponse.ErrorDescription);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Qfd.FastEnumToString(), dataLabsResourceCollectionResponse.ErrorResponse.FailedComponent);
        }
    }
}