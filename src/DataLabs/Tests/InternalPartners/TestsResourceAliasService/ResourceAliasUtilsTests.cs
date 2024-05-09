namespace Tests.ResourceAliasService
{
    using global::ResourceAliasService;
    using global::Tests.ResourceAliasService.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Newtonsoft.Json;
    using static global::ResourceAliasService.ResourceAliasSolutionService;

    [TestClass]
    public class ResourceAliasUtilsTests
    {
        [TestMethod]
        public void CloneNotificationDataV3WithNewResourcesTest()
        {
            var eventGridNotificationWithAliasToResolve = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithSubjectAsAliasSuccessMapping);
            var originalNotificationDataV3 = eventGridNotificationWithAliasToResolve!.Data;
            var eventGridNotificationWithAliasResolved = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithAliasAlreadyResolved);
            var resolvedResources = eventGridNotificationWithAliasResolved!.Data.Resources;

            var updatedEventGridNotification = ResourceAliasUtils.CloneNotificationDataV3WithNewResources(originalNotificationDataV3, resolvedResources);

            Assert.IsNotNull(updatedEventGridNotification);
            Assert.IsNull(updatedEventGridNotification.HomeTenantId);
            Assert.IsNull(updatedEventGridNotification.ResourceHomeTenantId);
            Assert.AreEqual(originalNotificationDataV3.AdditionalBatchProperties, updatedEventGridNotification.AdditionalBatchProperties);
            Assert.AreEqual(originalNotificationDataV3.ApiVersion, updatedEventGridNotification.ApiVersion);
            Assert.AreEqual(originalNotificationDataV3.DataBoundary, updatedEventGridNotification.DataBoundary);
            Assert.AreEqual(originalNotificationDataV3.FrontdoorLocation, updatedEventGridNotification.FrontdoorLocation);
            Assert.AreEqual(originalNotificationDataV3.MinApiVersion, updatedEventGridNotification.MinApiVersion);
            Assert.AreEqual(originalNotificationDataV3.PublisherInfo, updatedEventGridNotification.PublisherInfo);
            Assert.AreEqual(originalNotificationDataV3.ResourceLocation, updatedEventGridNotification.ResourceLocation);
            Assert.AreEqual(originalNotificationDataV3.ResourcesContainer, updatedEventGridNotification.ResourcesContainer);
            Assert.AreEqual(originalNotificationDataV3.SchemaVersion, updatedEventGridNotification.SchemaVersion);
            Assert.AreEqual(originalNotificationDataV3.Sign, updatedEventGridNotification.Sign);

            Assert.AreEqual(resolvedResources.First().ResourceId, updatedEventGridNotification.Resources.First().ResourceId);
            Assert.AreEqual(resolvedResources.First().ArmResource.Id, updatedEventGridNotification.Resources.First().ArmResource.Id);
            Assert.AreEqual(resolvedResources.First().HomeTenantId, updatedEventGridNotification.Resources.First().HomeTenantId);
            Assert.AreEqual(resolvedResources.First().ResourceHomeTenantId, updatedEventGridNotification.Resources.First().ResourceHomeTenantId);
        }

        [TestMethod]
        public void CloneNotificationResourceDataV3WithNewResourceIdTest()
        {
            var eventGridNotificationWithAliasToResolve = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithSubjectAsAliasSuccessMapping);
            var originalResourceData = eventGridNotificationWithAliasToResolve!.Data.Resources.First();
            var eventGridNotificationWithAliasResolved = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithAliasAlreadyResolved);
            var resolvedResourceData = eventGridNotificationWithAliasResolved!.Data.Resources.First();
            var systemMetadataObj = resolvedResourceData.AdditionalResourceProperties["system"];
            var systemMetadataWithResolvedState = JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(systemMetadataObj));

            var updatedResourceData = ResourceAliasUtils.CloneNotificationResourceDataV3WithNewResourceId(originalResourceData, resolvedResourceData.ResourceId, systemMetadataWithResolvedState);

            Assert.IsNotNull(updatedResourceData);
            Assert.AreEqual(resolvedResourceData.ResourceId, updatedResourceData.ResourceId);
            Assert.AreEqual(resolvedResourceData.ArmResource.Id, updatedResourceData.ArmResource.Id);
            Assert.IsNull(updatedResourceData.HomeTenantId);
            Assert.IsNull(updatedResourceData.ResourceHomeTenantId);

            Assert.AreEqual(originalResourceData.ApiVersion, updatedResourceData.ApiVersion);
            Assert.AreEqual(originalResourceData.CorrelationId, updatedResourceData.CorrelationId);
            Assert.AreEqual(originalResourceData.ResourceEventTime, updatedResourceData.ResourceEventTime);
            Assert.AreEqual(originalResourceData.SourceResourceId, updatedResourceData.SourceResourceId);
            Assert.AreEqual(originalResourceData.StatusCode, updatedResourceData.StatusCode);

            var updatedSystemMetadatObj = updatedResourceData.AdditionalResourceProperties["system"];
            Assert.IsNotNull(updatedSystemMetadatObj);
            var updatedSystemMetadata = JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(updatedSystemMetadatObj));
            Assert.AreEqual(systemMetadataWithResolvedState!.Aliases.ResourceId.State, updatedSystemMetadata!.Aliases.ResourceId.State);
            Assert.AreEqual(systemMetadataWithResolvedState!.Aliases.ResourceId.Id, updatedSystemMetadata!.Aliases.ResourceId.Id);
            Assert.AreEqual(systemMetadataWithResolvedState!.Aliases.ResourceId.ErrorMessage, updatedSystemMetadata!.Aliases.ResourceId.ErrorMessage);
        }
    }
}
