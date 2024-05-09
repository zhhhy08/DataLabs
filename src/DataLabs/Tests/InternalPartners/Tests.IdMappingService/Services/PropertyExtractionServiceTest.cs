namespace Microsoft.WindowsAzure.IdMappingService.Tests.Services
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.IdMappingService.Services;
    using Microsoft.WindowsAzure.IdMappingService.Tests.Common;
    using Newtonsoft.Json;

    [TestClass]
    public class PropertyExtractionServiceTest
    {
        [DataRow(IdMappingTestCommon.PayloadNotPopulatedEvent, DisplayName = "PayloadNotPopulatedEvent")]
        [DataRow(IdMappingTestCommon.VirtualMachineScaleSetEventWithoutInternalId, DisplayName = "VirtualMachineScaleSetEventWithoutInternalId")]
        [TestMethod]
        public void TestExtractPropertiesInvalidScenarios(string inputEventJson)
        {
            var inputEventGridNotification = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(inputEventJson);
            var resource = inputEventGridNotification?.Data?.Resources[0]?.ArmResource;
            var resourceType = GetResourceTypeFromEventType(inputEventGridNotification?.EventType);

            var configMappingSpecificationService = new ConfigMappingSpecificationService();
            var resourceTypeSpec = configMappingSpecificationService.GetInternalIdSpecification(resourceType);

            Assert.ThrowsException<ArgumentException>(() => PropertyExtractionService.ExtractProperties(resource, resourceTypeSpec, null));
        }

        [DataRow(IdMappingTestCommon.VirtualMachineScaleSetEvent, DisplayName = "VirtualMachineScaleSetEvent")]
        [DataRow(IdMappingTestCommon.InsightsComponentEvent, DisplayName = "InsightsComponentEvent - No ArmId to extract")]
        [TestMethod]
        public void TestExtractPropertiesReturnsCorrectArmId(string inputEventJson)
        {
            var inputEventGridNotification = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(inputEventJson);
            var resource = inputEventGridNotification?.Data?.Resources[0]?.ArmResource;
            var resourceType = GetResourceTypeFromEventType(inputEventGridNotification?.EventType);

            var configMappingSpecificationService = new ConfigMappingSpecificationService();
            var resourceTypeSpec = configMappingSpecificationService.GetInternalIdSpecification(resourceType);
            var identifiers = PropertyExtractionService.ExtractProperties(resource, resourceTypeSpec, null);

            Assert.IsNotNull(identifiers);
            Assert.IsTrue(identifiers.Count > 1);

            foreach (var identifier in identifiers)
            {
                // check that identifier.ArmId is either null (not present in FM) or isValidResourceId
                if (identifier != null)
                {
                    var isExtractedArmIdValid = identifier.ArmId == null || identifier.ArmId.IsValidResourceIdFormat();
                    Assert.IsTrue(isExtractedArmIdValid);
                }
            }
        }

        private string GetResourceTypeFromEventType(string? eventType)
        {
            if (eventType == null)
            {
                return String.Empty;
            }
            int idx = eventType.LastIndexOf("/") + 1;
            return eventType.Substring(0, idx - 1);
        }
    }
}
