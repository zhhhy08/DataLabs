namespace Microsoft.WindowsAzure.IdMappingService.Tests.Services
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.IdMappingService.Services;
    using Microsoft.WindowsAzure.IdMappingService.Tests.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Linq;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.IdMappingService.Utilities;
    using System.Threading.Tasks;

    [TestClass]
    public class IdMappingSolutionServiceTest
    {

        IdMappingSolutionService _idMappingSolutionService = new IdMappingSolutionService();

        [TestInitialize]
        public void Initialize()
        {
            _idMappingSolutionService = new IdMappingSolutionService();
        }

        [TestMethod]
        [DataRow(IdMappingTestCommon.VirtualMachineScaleSetEvent, DisplayName = "VirtualMachineScaleSetEvent")]
        [DataRow(IdMappingTestCommon.InsightsComponentEvent, DisplayName = "InsightsComponentEvent")]
        [DataRow(IdMappingTestCommon.VirtualMachineDeleteEvent, DisplayName = "VirtualMachineDeleteEvent")]
        public async Task TestGetResponseAsyncSuccessAsync(string inputEventJson)
        {
            var inputEventGridNotification = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(inputEventJson);
           
            if (inputEventGridNotification != null)
            {
                var inputRequest = new DataLabsARNV3Request(
                    DateTimeOffset.Now,
                    "traceId",
                    1,
                    "correlationID",
                    inputResource: inputEventGridNotification,
                    null);

                var response = await _idMappingSolutionService.GetResponseAsync(inputRequest, CancellationToken.None);
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.SuccessResponse);
                Assert.IsNull(response.ErrorResponse);
            }
            else
            {
                throw new ArgumentException("Test case input did not deserialize properly");
            }
            
        }

        [TestMethod]
        [DataRow(IdMappingTestCommon.VirtualMachineScaleSetEvent, "6315cfd5-911f-4097-b652-a0c04c81565d", DisplayName = "VirtualMachineScaleSetEvent")]
        [DataRow(IdMappingTestCommon.InsightsComponentEvent, "509417b5-e854-4323-a9d0-8917f1b6c716", DisplayName = "InsightsComponentEvent")]
        [DataRow(IdMappingTestCommon.VirtualMachineDeleteEvent, DisplayName = "VirtualMachineDeleteEvent")]
        public void TestCreateIdentifierResources(string inputEventJson, string identifierValue = "")
        {
            var inputEventGridNotification = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(inputEventJson);
            var inputNotificationData = inputEventGridNotification?.Data;
            var inputResourceData = inputNotificationData?.Resources[0];
            var inputArmResource = inputResourceData?.ArmResource;

            var identifierResourceEventData = _idMappingSolutionService.CreateIdentifierResources(inputNotificationData, inputEventGridNotification?.EventType, null);
            Assert.AreEqual(identifierResourceEventData.Resources.Count, 1);
            Assert.AreEqual(inputNotificationData?.HomeTenantId, identifierResourceEventData.HomeTenantId);
            Assert.AreEqual(inputNotificationData?.ResourceHomeTenantId, identifierResourceEventData.ResourceHomeTenantId);

            var resourceData = identifierResourceEventData.Resources.First();
            var resource = resourceData.ArmResource;

            if(inputResourceData?.ResourceEventTime.HasValue ?? false)
            {
                // if input eventTime is null, we use current time, so only assert equals if input is not null
                Assert.AreEqual(resourceData.ResourceEventTime, inputResourceData.ResourceEventTime);
            }
            
            Assert.AreEqual(resource.Id, inputArmResource?.Id + "/providers/Microsoft.Idmapping/Identifiers/default");
            Assert.AreEqual(resource.Type, "Microsoft.Idmapping/Identifiers");
            
            // skip validating properties for delete events
            if (!inputEventGridNotification?.EventType.EndsWith("/delete") ?? false)
            {
                var properties = JToken.FromObject(resource.Properties);

                //TODO update test to support checking more than one identifier if applicable for resourceType
                var outputIdentifierValue = properties?.SelectToken("resourceIdentifiers")?.First().SelectToken("Value");
                Assert.AreEqual(inputArmResource?.Type, properties?.SelectToken("resourceType"));
                Assert.AreEqual(outputIdentifierValue, identifierValue);
            }
            
        }

        [TestMethod]
        [DataRow(IdMappingTestCommon.MissingResourcesEvent, DisplayName = "MissingResourcesEventThrowsException")]
        [DataRow(IdMappingTestCommon.PayloadNotPopulatedEvent, DisplayName = "PayloadNotPopulatedEventThrowsException")]
        public void TestCreateIdentifierResourcesThrowsException(string inputEventJson)
        {
            var inputEventGridNotification = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(inputEventJson);
            var inputNotificationData = inputEventGridNotification?.Data;

            Assert.ThrowsException<ArgumentException>(() =>_idMappingSolutionService.CreateIdentifierResources(inputNotificationData, inputEventGridNotification?.EventType, null));
        }

        [TestMethod]
        [DataRow(IdMappingTestCommon.VirtualMachineDeleteEvent, DisplayName = "VirtualMachineDeleteEvent")]
        public void TestBuildIdMappingErrorResponse(string inputEventJson)
        {
            var inputEventGridNotification = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(inputEventJson);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    1,
                                                    "correlationID",
                                                    inputEventGridNotification!,
                                                    null);
            var response = IdMappingUtils.BuildIdMappingErrorResponse(request, new ArgumentException());
            Assert.IsNotNull(response.ErrorResponse);
            Assert.AreEqual(response.ErrorResponse!.ErrorType, DataLabsErrorType.DROP);
        }
    }
}
