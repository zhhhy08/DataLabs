namespace Tests.ResourceAliasService
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using global::ResourceAliasService;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Moq;

    [TestClass]
    public class IdMappingServiceAgentTests
    {
        private readonly string aliasResourceId1 = "/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/f3739e32-d126-44e2-aa64-29b2424a89f7/providers/microsoft.maintenance/scheduledevents/a4a962bb-bb84-4538-8c54-b6749acb12ab";
        private readonly string aliasResourceId2 = "/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/f3739e32-d126-44e2-aa64-29b2424a89f7/providers/microsoft.maintenance/scheduledevents/6d5b671b-9b7f-4e27-92b2-9feca040449c";
        private readonly string resolvedArmId1 = "/subscriptions/0a93027e-d914-4d56-90ff-22b8a5ea5688/resourceGroups/wilful_turquoise_magpie/providers/Microsoft.Compute/virtualMachines/only_purple_moose";
        private readonly string resolvedArmId2 = "/subscriptions/ece49473-f326-458c-80f6-ca724b874651/resourceGroups/wilful_turquoise_magpie/providers/Microsoft.Compute/virtualMachines/only_purple_moose";

        [TestMethod]
        public async Task GetArmIdsFromIdMapping_AllMappingSuccess()
        {
            var mockResourceProxyClient = new Mock<IResourceProxyClient>();

            var mockIdMapping1 = new IdMapping(aliasResourceId1, new List<string> { resolvedArmId1 }, ActivityStatusCode.Ok.ToString(), null);
            var mockIdMapping2 = new IdMapping(aliasResourceId2, new List<string> { resolvedArmId2 }, ActivityStatusCode.Ok.ToString(), null);
            var mockIdMappingSuccessResponse = new List<IdMapping> { mockIdMapping1, mockIdMapping2 };
            var mockDatalabsIdMappingResponse = new DataLabsIdMappingResponse(DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), mockIdMappingSuccessResponse, null, null, DataLabsDataSource.QFD);

            mockResourceProxyClient.Setup(x => x.GetIdMappingsAsync(It.IsAny<DataLabsIdMappingRequest>(), It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(mockDatalabsIdMappingResponse));
            var idMappingServiceAgent = new IdMappingServiceAgent(mockResourceProxyClient.Object);

            var resourceAliases = new List<string> { aliasResourceId1, aliasResourceId2 };
            var activity = new BasicActivity("GetArmIdsFromIdMapping_AllMappingSuccess");
            activity[SolutionConstants.PartnerTraceId] = Guid.NewGuid().ToString();

            var response = await idMappingServiceAgent.GetArmIdsFromIdMapping(resourceAliases, "VirtualMachine", Guid.NewGuid().ToString(), 0, activity, true, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.AreEqual(ActivityStatusCode.Ok.ToString(), response.StatusCode);
            Assert.AreEqual(2, response.IdMappings.Count());
            Assert.IsNull(response.ErrorMessage);

            var idMapping1 = response.IdMappings.First();
            Assert.AreEqual(ActivityStatusCode.Ok.ToString(), idMapping1.StatusCode);
            Assert.AreEqual(aliasResourceId1, idMapping1.AliasResourceId);
            Assert.AreEqual(resolvedArmId1, idMapping1.ArmIds!.First());
            Assert.IsNull(idMapping1.ErrorMessage);

            var idMapping2 = response.IdMappings.Last();
            Assert.AreEqual(ActivityStatusCode.Ok.ToString(), idMapping2.StatusCode);
            Assert.AreEqual(aliasResourceId2, idMapping2.AliasResourceId);
            Assert.AreEqual(resolvedArmId2, idMapping2.ArmIds!.First());
            Assert.IsNull(idMapping2.ErrorMessage);
        }

        [TestMethod]
        public async Task GetArmIdsFromIdMapping_PartialMappingSuccess()
        {
            var mockResourceProxyClient = new Mock<IResourceProxyClient>();

            var mockErrorMessage = "Mapping not found";
            var mockIdMapping1 = new IdMapping(aliasResourceId1, new List<string> { resolvedArmId1 }, ActivityStatusCode.Ok.ToString(), null);
            var mockIdMapping2 = new IdMapping(aliasResourceId2, null, ActivityStatusCode.Error.ToString(), mockErrorMessage);
            var mockIdMappingSuccessResponse = new List<IdMapping> { mockIdMapping1, mockIdMapping2 };
            var mockDatalabsIdMappingResponse = new DataLabsIdMappingResponse(DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), mockIdMappingSuccessResponse, null, null, DataLabsDataSource.QFD);

            mockResourceProxyClient.Setup(x => x.GetIdMappingsAsync(It.IsAny<DataLabsIdMappingRequest>(), It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(mockDatalabsIdMappingResponse));
            var idMappingServiceAgent = new IdMappingServiceAgent(mockResourceProxyClient.Object);

            var resourceAliases = new List<string> { aliasResourceId1, aliasResourceId2 };
            var activity = new BasicActivity("GetArmIdsFromIdMapping_PartialMappingSuccess");
            activity[SolutionConstants.PartnerTraceId] = Guid.NewGuid().ToString();

            var response = await idMappingServiceAgent.GetArmIdsFromIdMapping(resourceAliases, "VirtualMachine", Guid.NewGuid().ToString(), 0, activity, true, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.AreEqual(ActivityStatusCode.Ok.ToString(), response.StatusCode);
            Assert.AreEqual(2, response.IdMappings.Count());
            Assert.IsNull(response.ErrorMessage);

            var idMapping1 = response.IdMappings.First();
            Assert.AreEqual(ActivityStatusCode.Ok.ToString(), idMapping1.StatusCode);
            Assert.AreEqual(aliasResourceId1, idMapping1.AliasResourceId);
            Assert.AreEqual(resolvedArmId1, idMapping1.ArmIds!.First());
            Assert.IsNull(idMapping1.ErrorMessage);

            var idMapping2 = response.IdMappings.Last();
            Assert.AreEqual(ActivityStatusCode.Error.ToString(), idMapping2.StatusCode);
            Assert.AreEqual(aliasResourceId2, idMapping2.AliasResourceId);
            Assert.AreEqual(mockErrorMessage, idMapping2.ErrorMessage);
        }

        [TestMethod]
        public async Task GetArmIdsFromIdMapping_ErrorResponse()
        {
            var mockResourceProxyClient = new Mock<IResourceProxyClient>();

            var mockErrorMessage = "Failed to connect";
            var mockFailedComponent = "ResourceProxyClient";
            var mockIdMappingErrorResponse = new DataLabsProxyErrorResponse(DataLabsErrorType.RETRY, 1000, 500, mockErrorMessage, mockFailedComponent);
            var mockDatalabsIdMappingResponse = new DataLabsIdMappingResponse(DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), null, mockIdMappingErrorResponse, null, DataLabsDataSource.QFD);

            mockResourceProxyClient.Setup(x => x.GetIdMappingsAsync(It.IsAny<DataLabsIdMappingRequest>(), It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(mockDatalabsIdMappingResponse));
            var idMappingServiceAgent = new IdMappingServiceAgent(mockResourceProxyClient.Object);

            var resourceAliases = new List<string> { aliasResourceId1, aliasResourceId2 };
            var activity = new BasicActivity("GetArmIdsFromIdMapping_ErrorResponse");
            activity[SolutionConstants.PartnerTraceId] = Guid.NewGuid().ToString();

            var response = await idMappingServiceAgent.GetArmIdsFromIdMapping(resourceAliases, "VirtualMachine", Guid.NewGuid().ToString(), 0, activity, true, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.AreEqual(ActivityStatusCode.Error.ToString(), response.StatusCode);
            Assert.AreEqual(0, response.IdMappings.Count());
            Assert.AreEqual(mockErrorMessage, response.ErrorMessage);
        }
    }
}
