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
    public class GetIdMappingFlowTests : BaseTestsInitialize
    {
        [TestMethod]
        public async Task DirectQFDIdMapping200()
        {
            // Update config
            var allowedTypesInProxy = "*:qfd";
            var allowedCallInFetcher = "GetPacificIdMappingsAsync";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetIdMappingAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedCallInFetcher).ConfigureAwait(false);

            var resourceType = "microsoft.maintenance/scheduledevents";
            var resourceAliases = new List<string> { "/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44" };

            // Set content to QFDClient IdMapping call
            _resourceProxyFlowTestManager.QFDClient.SetResource(resourceAliases.First(), ResourceProxyClientTestData.IdMappingGetArmIdsByResourceAlias);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsIdMappingRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceType: resourceType,
                idMappingRequestBody: new IdMappingRequestBody { AliasResourceIds = resourceAliases });

            var dataLabsIdMappingResponse = await _resourceProxyClient.GetIdMappingsAsync(
                request: request,
                cancellationToken: default).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.QFD, dataLabsIdMappingResponse.DataSource);
            Assert.IsTrue(dataLabsIdMappingResponse.Attributes == null || dataLabsIdMappingResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsIdMappingResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsIdMappingResponse.CorrelationId);

            Assert.IsNull(dataLabsIdMappingResponse.ErrorResponse);
            Assert.IsNotNull(dataLabsIdMappingResponse.SuccessResponse);
            Assert.IsTrue(dataLabsIdMappingResponse.SuccessResponse?.Count > 0);
        }

        [TestMethod]
        public async Task DirectQFDIdMapping500()
        {
            // Update config
            var allowedTypesInProxy = "*:qfd";
            var allowedCallInFetcher = "GetPacificIdMappingsAsync";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetIdMappingAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedCallInFetcher).ConfigureAwait(false);

            var errorHttpStatusCode = HttpStatusCode.InternalServerError;
            _resourceProxyFlowTestManager.QFDClient.ErrStatusCode = errorHttpStatusCode;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var resourceType = "microsoft.maintenance/scheduledevents";
            var resourceAliases = new List<string> { "/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44" };

            var request = new DataLabsIdMappingRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceType: resourceType,
                idMappingRequestBody: new IdMappingRequestBody { AliasResourceIds = resourceAliases });

            var dataLabsIdMappingResponse = await _resourceProxyClient.GetIdMappingsAsync(
                request: request,
                cancellationToken: default).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.QFD, dataLabsIdMappingResponse.DataSource);
            Assert.IsTrue(dataLabsIdMappingResponse.Attributes == null || dataLabsIdMappingResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsIdMappingResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsIdMappingResponse.CorrelationId);

            Assert.IsNull(dataLabsIdMappingResponse.SuccessResponse);
            Assert.IsNotNull(dataLabsIdMappingResponse.ErrorResponse);

            Assert.IsNotNull(dataLabsIdMappingResponse.ErrorResponse);
            Assert.AreEqual(DataLabsErrorType.RETRY, dataLabsIdMappingResponse.ErrorResponse.ErrorType);
            Assert.AreEqual(0, dataLabsIdMappingResponse.ErrorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual((int)errorHttpStatusCode, dataLabsIdMappingResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(errorHttpStatusCode.FastEnumToString(), dataLabsIdMappingResponse.ErrorResponse.ErrorDescription);
            Assert.AreEqual(ClientProviderType.Qfd.FastEnumToString(), dataLabsIdMappingResponse.ErrorResponse.FailedComponent);
        }
    }
}