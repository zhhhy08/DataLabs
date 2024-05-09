namespace Tests.ResourceAliasService
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using global::ResourceAliasService;
    using global::Tests.ResourceAliasService.Helpers;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Moq;
    using Newtonsoft.Json;
    using static global::ResourceAliasService.ResourceAliasSolutionService;

    [TestClass]
    public class ResourceAliasServiceTests
    {
        private readonly string aliasInSubjectWithSuccessMapping = "/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/4cabbc43-cf76-4eb7-b016-0e5a5317630f";
        private readonly string aliasInResourceIdWithSuccessMapping = "/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d";
        private readonly string aliasInSubjectWithNoMapping = "/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ea9ce855-f457-442a-9d44-0de59bc45ae3";
        private readonly string aliasInResourceIdWithNoMapping = "/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/959c5346-46a9-45b9-b300-97e85175c615";

        private readonly string resolvedArmId1 = "/subscriptions/0a93027e-d914-4d56-90ff-22b8a5ea5688/resourceGroups/wilful_turquoise_magpie/providers/Microsoft.Compute/virtualMachines/only_purple_moose";
        private readonly string resolvedArmId2 = "/subscriptions/ece49473-f326-458c-80f6-ca724b874651/resourceGroups/wilful_turquoise_magpie/providers/Microsoft.Compute/virtualMachines/only_purple_moose";
        private readonly string resolvedSubject = "/subscriptions/0a93027e-d914-4d56-90ff-22b8a5ea5688/resourceGroups/wilful_turquoise_magpie/providers/Microsoft.Compute/virtualMachines/only_purple_moose/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44";
        private readonly string resolvedResourceId = "/subscriptions/ece49473-f326-458c-80f6-ca724b874651/resourceGroups/wilful_turquoise_magpie/providers/Microsoft.Compute/virtualMachines/only_purple_moose/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf";
        private readonly string resolvedSubjectWithNoSuffix = "/subscriptions/0a93027e-d914-4d56-90ff-22b8a5ea5688/resourceGroups/wilful_turquoise_magpie/providers/Microsoft.Compute/virtualMachines/only_purple_moose";

        Mock<ICacheClient> cacheClient = new Mock<ICacheClient>();
        Mock<IResourceProxyClient> resourceProxyClient = new Mock<IResourceProxyClient>();
        Mock<ILogger> mockLogger = new Mock<ILogger>();
        Mock<ILoggerFactory> mockLoggerFactory = new Mock<ILoggerFactory>();

        ResourceAliasSolutionService resoruceAliasSolutionService = new ResourceAliasSolutionService();

        [TestInitialize]
        public void Setup()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[MaxRetryCountKey] = "3";
            var configurationWithCallBack = ConfigMapUtil.Configuration;

            mockLogger.Setup(
                m => m.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<object>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<object, Exception?, string>>()));
            mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(() => mockLogger.Object);

            resoruceAliasSolutionService.SetCacheClient(cacheClient.Object);
            resoruceAliasSolutionService.SetLoggerFactory(mockLoggerFactory.Object);
            resoruceAliasSolutionService.SetResourceProxyClient(resourceProxyClient.Object);
            resoruceAliasSolutionService.SetConfiguration(configurationWithCallBack);

            var serviceCollection = new ServiceCollection();
            var idMappingServiceAgent = new Mock<IIdMappingServiceAgent>();

            var mockIdMappingSuccessResultWithSubjectAsAlias = new IdMappingsDto
            {
                IdMappings = new List<IdMappingRecord>
                {
                    new IdMappingRecord
                    {
                        ArmIds = new List<string> () { resolvedArmId1 },
                        AliasResourceId = aliasInSubjectWithSuccessMapping.ToUpperInvariant(),
                        StatusCode = ActivityStatusCode.Ok.ToString(),
                        ErrorMessage = null
                    },
                    new IdMappingRecord
                    {
                        ArmIds = new List<string> () { resolvedArmId2 },
                        AliasResourceId = aliasInResourceIdWithSuccessMapping.ToUpperInvariant(),
                        StatusCode = ActivityStatusCode.Ok.ToString().ToUpperInvariant(),
                        ErrorMessage = null
                    },
                },
                StatusCode = ActivityStatusCode.Ok.ToString()
            };
            var mockIdMappingSuccessResultWithSubjectNotAlias = new IdMappingsDto
            {
                IdMappings = new List<IdMappingRecord>
                {
                    new IdMappingRecord
                    {
                        ArmIds = new List<string> () { resolvedArmId2 },
                        AliasResourceId = aliasInResourceIdWithSuccessMapping.ToLowerInvariant(),
                        StatusCode = ActivityStatusCode.Ok.ToString().ToUpperInvariant(),
                        ErrorMessage = null
                    },
                },
                StatusCode = ActivityStatusCode.Ok.ToString()
            };

            var mockIdMappingFailureResultWithSubjectResolutionFailure = new IdMappingsDto
            {
                IdMappings = new List<IdMappingRecord>
                {
                    new IdMappingRecord
                    {
                        ArmIds = new List<string> { },
                        AliasResourceId = aliasInSubjectWithNoMapping,
                        StatusCode = ActivityStatusCode.Error.ToString(),
                        ErrorMessage = "Mapping not found"
                    },
                    new IdMappingRecord
                    {
                        ArmIds = new List<string> { resolvedArmId2 },
                        AliasResourceId = aliasInResourceIdWithSuccessMapping,
                        StatusCode = ActivityStatusCode.Ok.ToString(),
                        ErrorMessage = null
                    },
                },
                StatusCode = ActivityStatusCode.Ok.ToString()
            };
            var mockIdMappingFailureResultWithResourceIdResolutionFailure = new IdMappingsDto
            {
                IdMappings = new List<IdMappingRecord>
                {
                    new IdMappingRecord
                    {
                        ArmIds = new List<string> { resolvedArmId1 },
                        AliasResourceId = aliasInSubjectWithSuccessMapping,
                        StatusCode = ActivityStatusCode.Ok.ToString(),
                        ErrorMessage = null
                    },
                    new IdMappingRecord
                    {
                        ArmIds = new List<string> { },
                        AliasResourceId = aliasInResourceIdWithNoMapping,
                        StatusCode = ActivityStatusCode.Error.ToString(),
                        ErrorMessage = "Mapping not found"
                    },
                },
                StatusCode = ActivityStatusCode.Ok.ToString()
            };

            var mockIdMappingEmptyResult = new IdMappingsDto
            {
                IdMappings = new List<IdMappingRecord>(),
                StatusCode = ActivityStatusCode.Ok.ToString()
            };

            idMappingServiceAgent.Setup(x => x.GetArmIdsFromIdMapping(new List<string> { aliasInSubjectWithSuccessMapping, aliasInResourceIdWithSuccessMapping }, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockIdMappingSuccessResultWithSubjectAsAlias));
            idMappingServiceAgent.Setup(x => x.GetArmIdsFromIdMapping(new List<string> { aliasInResourceIdWithSuccessMapping }, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockIdMappingSuccessResultWithSubjectNotAlias));
            idMappingServiceAgent.Setup(x => x.GetArmIdsFromIdMapping(new List<string> { aliasInSubjectWithNoMapping, aliasInResourceIdWithSuccessMapping }, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockIdMappingFailureResultWithSubjectResolutionFailure));
            idMappingServiceAgent.Setup(x => x.GetArmIdsFromIdMapping(new List<string> { aliasInSubjectWithSuccessMapping, aliasInResourceIdWithNoMapping }, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockIdMappingFailureResultWithResourceIdResolutionFailure));
            idMappingServiceAgent.Setup(x => x.GetArmIdsFromIdMapping(new List<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockIdMappingEmptyResult));

            serviceCollection.AddSingleton(idMappingServiceAgent.Object);
            resoruceAliasSolutionService.InitializeServiceProvider(serviceCollection);
        }

        [TestMethod]
        public async Task GetResponseAsyncResourceAliasResolutionSuccessWithSubjectResolved()
        {
            var inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithSubjectAsAliasSuccessMapping);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    0,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await resoruceAliasSolutionService.GetResponseAsync(request, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.AreEqual(resolvedSubject, response.SuccessResponse.Resource!.Subject);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ResourceId);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ArmResource.Id);
            Assert.IsNull(response.ErrorResponse);

            var systemMetadataObj = response.SuccessResponse.Resource!.Data.Resources.First().AdditionalResourceProperties["system"];
            var systemMetadata = systemMetadataObj != null ? JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(systemMetadataObj)) : null;
            Assert.IsNotNull(systemMetadata);
            Assert.AreEqual(ResourceAliasResolutionState.Resolved, systemMetadata.Aliases.ResourceId.State);
            Assert.AreEqual(inputResource!.Data.Resources.First().ResourceId, systemMetadata.Aliases.ResourceId.Id);
        }

        [TestMethod]
        public async Task GetResponseAsyncResourceAliasResolutionSuccessWithSubjectNotAlias()
        {
            var inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithSubjectNotAliasSuccessMapping);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    0,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await resoruceAliasSolutionService.GetResponseAsync(request, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.AreEqual(inputResource!.Subject, response.SuccessResponse.Resource!.Subject);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ResourceId);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ArmResource.Id);
            Assert.IsNull(response.ErrorResponse);

            var systemMetadataObj = response.SuccessResponse.Resource!.Data.Resources.First().AdditionalResourceProperties["system"];
            var systemMetadata = systemMetadataObj != null ? JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(systemMetadataObj)) : null;
            Assert.IsNotNull(systemMetadata);
            Assert.AreEqual(ResourceAliasResolutionState.Resolved, systemMetadata.Aliases.ResourceId.State);
            Assert.AreEqual(inputResource!.Data.Resources.First().ResourceId, systemMetadata.Aliases.ResourceId.Id);
        }

        [TestMethod]
        public async Task GetResponseAsyncResourceAliasResolutionSuccessWithSameSubjectAndResourceId()
        {
            var inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithSameSubjectAndResourceIdSuccessMapping);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    0,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await resoruceAliasSolutionService.GetResponseAsync(request, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Subject);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ResourceId);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ArmResource.Id);
            Assert.IsNull(response.ErrorResponse);

            var systemMetadataObj = response.SuccessResponse.Resource!.Data.Resources.First().AdditionalResourceProperties["system"];
            var systemMetadata = systemMetadataObj != null ? JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(systemMetadataObj)) : null;
            Assert.IsNotNull(systemMetadata);
            Assert.AreEqual(ResourceAliasResolutionState.Resolved, systemMetadata.Aliases.ResourceId.State);
            Assert.AreEqual(inputResource!.Data.Resources.First().ResourceId, systemMetadata.Aliases.ResourceId.Id);
        }

        [TestMethod]
        public async Task GetResponseAsyncSuccessWithResolutionStateMetadataNotInPayload()
        {
            var inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithSuccessMappingAndResolutionStateNotInPayload);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    0,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await resoruceAliasSolutionService.GetResponseAsync(request, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.AreEqual(resolvedSubject, response.SuccessResponse.Resource!.Subject);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ResourceId);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ArmResource.Id);
            Assert.IsNull(response.ErrorResponse);

            var systemMetadataObj = response.SuccessResponse.Resource!.Data.Resources.First().AdditionalResourceProperties["system"];
            var systemMetadata = systemMetadataObj != null ? JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(systemMetadataObj)) : null;
            Assert.IsNotNull(systemMetadata);
            Assert.AreEqual(ResourceAliasResolutionState.Resolved, systemMetadata.Aliases.ResourceId.State);
            Assert.AreEqual(inputResource!.Data.Resources.First().ResourceId, systemMetadata.Aliases.ResourceId.Id);
        }

        [TestMethod]
        public async Task GetResponseAsyncSuccessWithAliasAlreadyResolvedInPayload()
        {
            var inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithAliasAlreadyResolved);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    0,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await resoruceAliasSolutionService.GetResponseAsync(request, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.AreEqual(inputResource!.Subject, response.SuccessResponse.Resource!.Subject);
            Assert.AreEqual(inputResource.Data.Resources.First().ResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ResourceId);
            Assert.AreEqual(inputResource.Data.Resources.First().ArmResource.Id, response.SuccessResponse.Resource!.Data.Resources.First().ArmResource.Id);
            Assert.IsNull(response.ErrorResponse);

            var systemMetadataObj = response.SuccessResponse.Resource!.Data.Resources.First().AdditionalResourceProperties["system"];
            var systemMetadata = systemMetadataObj != null ? JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(systemMetadataObj)) : null;
            Assert.IsNotNull(systemMetadata);
            Assert.AreEqual(ResourceAliasResolutionState.Resolved, systemMetadata.Aliases.ResourceId.State);
        }

        [TestMethod]
        public async Task GetResponseAsyncResourceAliasResolutionSuccessWithSubjectAsAliasButNoSuffix()
        {
            var inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithSubjectAsAliasButNoSuffixSuccessMapping);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    0,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await resoruceAliasSolutionService.GetResponseAsync(request, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.AreEqual(resolvedSubjectWithNoSuffix, response.SuccessResponse.Resource!.Subject);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ResourceId);
            Assert.AreEqual(resolvedResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ArmResource.Id);
            Assert.IsNull(response.ErrorResponse);

            var systemMetadataObj = response.SuccessResponse.Resource!.Data.Resources.First().AdditionalResourceProperties["system"];
            var systemMetadata = systemMetadataObj != null ? JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(systemMetadataObj)) : null;
            Assert.IsNotNull(systemMetadata);
            Assert.AreEqual(ResourceAliasResolutionState.Resolved, systemMetadata.Aliases.ResourceId.State);
            Assert.AreEqual(inputResource!.Data.Resources.First().ResourceId, systemMetadata.Aliases.ResourceId.Id);
        }

        [TestMethod]
        public async Task GetResponseAsyncResourceAliasResolutionFailedWithRetryResponse()
        {
            var inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithFailureMappingForResourceId);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    0,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await resoruceAliasSolutionService.GetResponseAsync(request, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.IsNull(response.SuccessResponse);
            Assert.IsNotNull(response.ErrorResponse);
            Assert.AreEqual(DataLabsErrorType.RETRY, response.ErrorResponse.ErrorType);
        }

        [TestMethod]
        public async Task GetResponseAsyncSubjectResolutionFailedWithUnresolvedStateNotification()
        {
            var inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithFailureMappingForSubject);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    3,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await resoruceAliasSolutionService.GetResponseAsync(request, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.AreEqual(inputResource!.Subject, response.SuccessResponse.Resource!.Subject);
            Assert.AreEqual(inputResource.Data.Resources.First().ResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ResourceId);
            Assert.AreEqual(inputResource.Data.Resources.First().ArmResource.Id, response.SuccessResponse.Resource!.Data.Resources.First().ArmResource.Id);
            Assert.IsNull(response.ErrorResponse);

            var systemMetadataObj = response.SuccessResponse.Resource!.Data.Resources.First().AdditionalResourceProperties["system"];
            var systemMetadata = systemMetadataObj != null ? JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(systemMetadataObj)) : null;
            Assert.IsNotNull(systemMetadata);
            Assert.AreEqual(ResourceAliasResolutionState.Unresolved, systemMetadata.Aliases.ResourceId.State);
            Assert.AreEqual("SubjectResolutionError: Mapping not found", systemMetadata.Aliases.ResourceId.ErrorMessage);
        }

        [TestMethod]
        public async Task GetResponseAsyncResourceIdResolutionFailedWithUnresolvedStateNotification()
        {
            var inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithFailureMappingForResourceId);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    3,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await resoruceAliasSolutionService.GetResponseAsync(request, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.AreEqual(inputResource!.Subject, response.SuccessResponse.Resource!.Subject);
            Assert.AreEqual(inputResource.Data.Resources.First().ResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ResourceId);
            Assert.AreEqual(inputResource.Data.Resources.First().ArmResource.Id, response.SuccessResponse.Resource!.Data.Resources.First().ArmResource.Id);
            Assert.IsNull(response.ErrorResponse);

            var systemMetadataObj = response.SuccessResponse.Resource!.Data.Resources.First().AdditionalResourceProperties["system"];
            var systemMetadata = systemMetadataObj != null ? JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(systemMetadataObj)) : null;
            Assert.IsNotNull(systemMetadata);
            Assert.AreEqual(ResourceAliasResolutionState.Unresolved, systemMetadata.Aliases.ResourceId.State);
            Assert.AreEqual("Mapping not found", systemMetadata.Aliases.ResourceId.ErrorMessage);
        }

        [TestMethod]
        public async Task GetResponseAsyncAliasNotCorrectlyFormattedIdFailureWithUnresolvedStateNotification()
        {
            var inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.notificationWithAliasNotCorrectlyFormatted);
            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    3,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await resoruceAliasSolutionService.GetResponseAsync(request, CancellationToken.None);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.AreEqual(inputResource!.Subject, response.SuccessResponse.Resource!.Subject);
            Assert.AreEqual(inputResource.Data.Resources.First().ResourceId, response.SuccessResponse.Resource!.Data.Resources.First().ResourceId);
            Assert.AreEqual(inputResource.Data.Resources.First().ArmResource.Id, response.SuccessResponse.Resource!.Data.Resources.First().ArmResource.Id);
            Assert.IsNull(response.ErrorResponse);

            var systemMetadataObj = response.SuccessResponse.Resource!.Data.Resources.First().AdditionalResourceProperties["system"];
            var systemMetadata = systemMetadataObj != null ? JsonConvert.DeserializeObject<SystemMetadata>(JsonConvert.SerializeObject(systemMetadataObj)) : null;
            Assert.IsNotNull(systemMetadata);
            Assert.AreEqual(ResourceAliasResolutionState.Unresolved, systemMetadata.Aliases.ResourceId.State);
        }
    }
}
