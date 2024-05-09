namespace Tests.IdMappingService.CustomResourceTypeHandling.Handlers
{
    using global::IdMappingService.CustomResourceTypeHandling.Handlers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.IdMappingService.Services;
    using Microsoft.WindowsAzure.IdMappingService.Tests.Common;
    using Newtonsoft.Json;

    [TestClass]
    public class TrimUrisHandlerTest
    {
        [DataRow(IdMappingTestCommon.VirtualMachineScaleSetEvent, DisplayName = "VirtualMachineScaleSetEvent")]
        [DataRow(IdMappingTestCommon.InsightsComponentEvent, DisplayName = "InsightsComponentEvent")]
        [TestMethod]
        public void TestNoOpForNonUris(string inputEventJson)
        {
            var handler = new TrimUrisHandler();
            var inputEventGridNotification = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(inputEventJson);
            var resourceData = inputEventGridNotification?.Data?.Resources[0];
            var resource = resourceData?.ArmResource;
            var resourceType = GetResourceTypeFromEventType(inputEventGridNotification?.EventType);

            var configMappingSpecificationService = new ConfigMappingSpecificationService();
            var resourceTypeSpec = configMappingSpecificationService.GetInternalIdSpecification(resourceType);
            var expectedIdentifiers = PropertyExtractionService.ExtractProperties(resource, resourceTypeSpec, null);

            var handlerIdentifiers = handler.CreateIdentifiers(resourceData, resourceTypeSpec, null);

            Assert.AreEqual(expectedIdentifiers.Count, handlerIdentifiers.Count);

            for (int i =0; i< expectedIdentifiers.Count; i++)
            {
                var expected = expectedIdentifiers[i];
                var actual = handlerIdentifiers[i];

                Assert.AreEqual(expected.Value, actual.Value);
                Assert.AreEqual(expected.Name, actual.Name);
                Assert.AreEqual(expected.ArmId, actual.ArmId);
            }
        }

        [DataRow(IdMappingTestCommon.ServiceBusNamespaceEventWithUrlMetricId, "test-sb.servicebus.windows.net", DisplayName = "TestUrlWithPortNumber")]
        [DataRow(IdMappingTestCommon.ServiceBusNamespaceEventWithMetricIdUrlQueryParam, "test-sb.servicebus.windows.net", DisplayName = "TestUrlWithQueryParam")]
        [TestMethod]
        public void TestTrimUrisHandler(string inputEventJson, string expectedTrimmedInternalId)
        {
            var handler = new TrimUrisHandler();
            var inputEventGridNotification = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(inputEventJson);
            var resourceData = inputEventGridNotification?.Data?.Resources[0];
            var resource = resourceData?.ArmResource;
            var resourceType = GetResourceTypeFromEventType(inputEventGridNotification?.EventType);

            var configMappingSpecificationService = new ConfigMappingSpecificationService();
            var resourceTypeSpec = configMappingSpecificationService.GetInternalIdSpecification(resourceType);
            var expectedIdentifiers = PropertyExtractionService.ExtractProperties(resource, resourceTypeSpec, null);

            var handlerIdentifiers = handler.CreateIdentifiers(resourceData, resourceTypeSpec, null);

            Assert.AreEqual(expectedIdentifiers.Count, handlerIdentifiers.Count);

            for (int i = 0; i < expectedIdentifiers.Count; i++)
            {
                var expected = expectedIdentifiers[i];
                var actual = handlerIdentifiers[i];

                Assert.AreEqual(expectedTrimmedInternalId, actual.Value);
                Assert.AreEqual(expected.Name, actual.Name);
                Assert.AreEqual(expected.ArmId, actual.ArmId);
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
