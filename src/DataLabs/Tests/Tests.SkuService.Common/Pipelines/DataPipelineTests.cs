namespace Tests.SkuService.Common.Pipelines
{
    using global::SkuService.Common.Extensions;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using global::SkuService.Common.Builders;
    using global::SkuService.Common.Models.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using global::SkuService.Main.Pipelines;
    using global::SkuService.Common.DataProviders;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Newtonsoft.Json;

    [TestClass]
    public class DataPipelineTests
    {
        internal const string GlobalSkuUpdateEvent = """
            {
              "id": "subjob:b8cbc4e6-3e72-4b35-a72e-6546eba5f31f",
              "topic": "System",
              "subject": "subject",
              "eventType": "Add",
              "eventTime": "2023-03-21T18:19:00.58629+00:00",
              "metadataVersion": "",
              "dataVersion": "v1",
              "data": {
                "resourcesContainer": "Inline",
                "resourceLocation": "",
                "publisherInfo": "ARM",
                "resources": [
                  {
                    "correlationId": "b8cbc4e6-3e72-4b35-a72e-6546eba5f31f",
                    "resourceId": "/providers/Microsoft.Inventory/skuProviders/Microsoft.Compute/resourceTypes/virtualMachines/locations/australiaeast/globalSkus/AZAP_Harvest_Compute_2",
                    "armResource": {
                        "id": "/providers/Microsoft.Inventory/skuProviders/Microsoft.Compute/resourceTypes/virtualMachines/locations/australiaeast/globalSkus/AZAP_Harvest_Compute_2",
                        "location": "eastus",
                        "name": "AZAP_Harvest_Compute_2",
                        "properties": {
                            "Location" : "australiaeast",
                            "ResourceType": "virtualMachines",
                            "SkuProvider": "Microsoft.Compute",
                            "Skus": [
                                {
                                    "Capabilities": "{\"MaxResourceVolumeMB\":\"204800\",\"OSVhdSizeMB\":\"1047552\",\"vCPUs\":\"2\",\"MemoryPreservingMaintenanceSupported\":\"True\",\"HyperVGenerations\":\"V1\",\"MemoryGB\":\"16\",\"MaxDataDiskCount\":\"4\",\"CpuArchitectureType\":\"x64\",\"LowPriorityCapable\":\"True\",\"PremiumIO\":\"False\",\"VMDeploymentTypes\":\"IaaS\",\"vCPUsAvailable\":\"2\",\"ACUs\":\"160\",\"vCPUsPerCore\":\"2\",\"CombinedTempDiskAndCachedIOPS\":\"3000\",\"CombinedTempDiskAndCachedReadBytesPerSecond\":\"48234496\",\"CombinedTempDiskAndCachedWriteBytesPerSecond\":\"24117248\",\"EphemeralOSDiskSupported\":\"False\",\"EncryptionAtHostSupported\":\"False\",\"CapacityReservationSupported\":\"False\"}",
                                    "Capacity": null,
                                    "Costs": null,
                                    "Family": "azapHarvestFamily",
                                    "Kind": null,
                                    "LocationInfo": [
                                        {
                                            "ExtendedLocations": null,
                                            "IsCapacityReservationRestricted": false,
                                            "IsOndemandRestricted": false,
                                            "IsSpotRestricted": false,
                                            "Location": "australiaeast",
                                            "LocationDetails": null,
                                            "Type": null,
                                            "ZoneDetails": null,
                                            "Zones": [
                                                "australiaeast-az03"
                                            ]
                                        }
                                    ],
                                    "Locations": [
                                        "australiaeast"
                                    ],
                                    "Name": "AZAP_Harvest_Compute_2",
                                    "RequiredFeatures": [
                                        "Microsoft.Compute/AZAPInternalVMSKU",
                                        "Microsoft.Compute/TestSubscription",
                                        "Microsoft.Compute/VMSKUPreview"
                                    ],
                                    "RequiredQuotaIds": null,
                                    "Size": "Harvest_Compute_2",
                                    "Tier": "Standard"
                                }
                            ]
                        },
                        "type": "Microsoft.Inventory/skuProviders/resourceTypes/locations/globalskus"
                    },
                    "apiVersion": "v1",
                    "resourceEventTime": "2023-03-21T18:19:00.5550107+00:00",
                    "statusCode": "OK"
                  }
                ],
                "routingType": "Unknown",
                "additionalBatchProperties": {
                  "system": {
                    "contract": {
                      "version": "0.0.3",
                      "correlationId": "00000000-0000-0000-0000-000000000000"
                    }
                  }
                }
              }
            }
            """;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration["CustomConfig"] = @"
                    { 
                      ""subjobBatchSize"" : ""1000"",
                      ""configFetchIntervalInHours"" : ""6"",
                      ""globalSkuBatchSize"": ""1000"",
                      ""casClientId"": ""901c622e-9663-4c65-9008-df103ed6cc5a""
                    }
            ";
            ServiceRegistrations.InitializeServiceProvider(new ServiceCollection(), "SkuService");
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            ConfigMapUtil.Reset();
        }
        public static async IAsyncEnumerable<SubscriptionSkuModel> GetSkuValues()
        {
            yield return new SubscriptionSkuModel
            {
                Location = "AustraliaCentral",
                ResourceType = "virtualMachines",
            };
            await Task.CompletedTask;

        }

        [TestMethod]
        public async Task GetSubscriptionSkusAsync_WhenCalled_ReturnsSubscriptionSkus()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var registrationProvider = new Mock<IRegistrationProvider>();
            var builder = new Mock<IDataBuilder<SubscriptionSkuModel>>();
            builder.Setup(x => x.BuildAsync("Microsoft.Compute", It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<ChangedDatasets>(), It.IsAny<CancellationToken>()))
                .Returns(GetSkuValues);

            serviceCollection.AddSingleton(subscriptionProvider.Object);
            serviceCollection.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            serviceCollection.AddSingleton(builder.Object);
            ServiceRegistrations.ServiceProvider = serviceCollection.BuildServiceProvider();
            DataLabsARNV3Request request = new(
                DateTimeOffset.UtcNow,
                string.Empty,
                0,
                Guid.NewGuid().ToString(),
                JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(GlobalSkuUpdateEvent)!,
                null,
                "p-eus");

            var pipeline = new DataPipeline(subscriptionProvider.Object, registrationProvider.Object, builder.Object);
            // Act
            await foreach (var actual in pipeline.GetResourcesForSubjobsAsync(request, CancellationToken.None))
            {
                Assert.IsNotNull(actual);
            }

            Mock.VerifyAll(subscriptionProvider, registrationProvider, builder);
        }

        [TestMethod]
        public async Task GetResourcesForSubjobsAsync_WhenCalled_ThrowsException()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var registrationProvider = new Mock<IRegistrationProvider>();
            var builder = new Mock<IDataBuilder<SubscriptionSkuModel>>();
            var sub1 = "b8cbc4e6-3e72-4b35-a72e-6546eba5f31f";

            builder.Setup(x => x.BuildAsync("Microsoft.Compute", sub1, It.IsAny<IActivity>(), It.IsAny<ChangedDatasets>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Any exception"));

            serviceCollection.AddSingleton(subscriptionProvider.Object);
            serviceCollection.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            serviceCollection.AddSingleton(builder.Object);
            ServiceRegistrations.ServiceProvider = serviceCollection.BuildServiceProvider();
            DataLabsARNV3Request request = new(
                DateTimeOffset.UtcNow,
                string.Empty,
                0,
                Guid.NewGuid().ToString(),
                JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(GlobalSkuUpdateEvent)!,
                null,
                "p-eus");

            var pipeline = new DataPipeline(subscriptionProvider.Object, registrationProvider.Object, builder.Object);

            // Act
            var result = new List<DataLabsARNV3Response>();
            await foreach (var actual in pipeline.GetResourcesForSubjobsAsync(request, CancellationToken.None))
            {
                result.Add(actual);
            }
            Assert.AreEqual(1, result.Count);
            Assert.IsNotNull(result[0].ErrorResponse);
            Mock.VerifyAll(subscriptionProvider, registrationProvider, builder);
        }

        [TestMethod]
        public async Task GetResourcesForSingleSubscriptionAsync_WhenCalled_ReturnsSubscriptionSkus()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var builder = new Mock<IDataBuilder<SubscriptionSkuModel>>();
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var registrationProvider = new Mock<IRegistrationProvider>();
            builder.Setup(x => x.BuildAsync("Microsoft.Compute", It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<ChangedDatasets>(), It.IsAny<CancellationToken>()))
                .Returns(GetSkuValues);

            serviceCollection.AddSingleton(subscriptionProvider.Object);
            serviceCollection.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            serviceCollection.AddSingleton(builder.Object);
            ServiceRegistrations.ServiceProvider = serviceCollection.BuildServiceProvider();
            DataLabsARNV3Request request = new(
                DateTimeOffset.UtcNow,
                string.Empty,
                0,
                Guid.NewGuid().ToString(),
                new EventGridNotification<NotificationDataV3<GenericResource>>(
                    "/providers/Microsoft.Inventory/skuProviders/Microsoft.Compute/resourceTypes/availabilitySets/locations/AustraliaCentral/skus/Aligned",
                    string.Empty,
                    "test",
                    "Snapshot",
                    DateTimeOffset.UtcNow,
                    new NotificationDataV3<GenericResource>(
                        "Microsoft.ResourceGraph",
                        [
                            new NotificationResourceDataV3<GenericResource>(
                                Guid.NewGuid(),
                                new GenericResource
                                {
                                    Id = "subscriptions/11777a53-26b6-4eb5-a274-4d79564ca8dd/providers/Microsoft.Resources/subscriptionZoneMappings/default",
                                    Type = "Microsoft.Resources/subscriptionZoneMappings",
                                    Name = "subscriptionZoneMappings",
                                    Properties = new SubscriptionMappingsModel(),
                                },
                                "2023-06-01",
                                DateTimeOffset.UtcNow
                            )
                        ])),
                null,
                "p-eus");
            // Act
            var pipeline = new DataPipeline(subscriptionProvider.Object, registrationProvider.Object, builder.Object);
            await foreach (var actual in pipeline.GetResourcesForSingleSubscriptionAsync(request, CancellationToken.None))
            {
                Assert.IsNotNull(actual);
            }

            Mock.VerifyAll(subscriptionProvider, registrationProvider, builder);
        }

        [TestMethod]
        public async Task GetSubTasksAsync_WhenCalled_ReturnsSubtasks()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var builder = new Mock<IDataBuilder<SubscriptionSkuModel>>();
            var registrationProvider = new Mock<IRegistrationProvider>();
            string[] subscriptions = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];
            subscriptionProvider.Setup(x => x.GetSubscriptionsByRangeAsync("subscriptions-0", 0, -1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new string[] { subscriptions[0] });
            subscriptionProvider.Setup(x => x.GetSubscriptionsByRangeAsync("subscriptions-1", 0, -1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new string[] { subscriptions[1] });
            serviceCollection.AddSingleton(subscriptionProvider.Object);
            serviceCollection.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            serviceCollection.AddSingleton(builder.Object);
            ServiceRegistrations.ServiceProvider = serviceCollection.BuildServiceProvider();
            DataLabsARNV3Request request = new(
                DateTimeOffset.UtcNow,
                string.Empty,
                0,
                Guid.NewGuid().ToString(),
                new EventGridNotification<NotificationDataV3<GenericResource>>(
                    "/providers/Microsoft.Inventory/skuProviders/Microsoft.Compute/resourceTypes/availabilitySets/locations/AustraliaCentral/skus/Aligned",
                    string.Empty,
                    "test",
                    "Snapshot",
                    DateTimeOffset.UtcNow,
                    new NotificationDataV3<GenericResource>(
                        "Microsoft.ResourceGraph",
                        [
                            new NotificationResourceDataV3<GenericResource>(
                                Guid.NewGuid(),
                                new GenericResource
                                {
                                    Id = "/providers/Microsoft.Inventory/skuProviders/Microsoft.Compute/resourceTypes/availabilitySets/locations/AustraliaCentral/skus/Aligned",
                                    Type = "Microsoft.Inventory/skuProviders/resourceTypes/locations/skus",
                                    Name = "Aligned",
                                    Properties = new SkuSetting
                                    {
                                        Capabilities = new Dictionary<string, string>
                                        {
                                            { "a", "Aligned" },
                                            { "b", "Aligned" },
                                        },
                                        Name = "aaa",
                                    },
                                },
                                "2023-06-01",
                                DateTimeOffset.UtcNow
                            )
                        ])),
                null,
                "p-eus");

            var pipeline = new DataPipeline(subscriptionProvider.Object, registrationProvider.Object, builder.Object);
            var results = new List<DataLabsARNV3Response>();

            // Act
            await foreach (var actual in pipeline.GetSubJobsAsync(request, CancellationToken.None))
            {
                results.Add(actual);
            }

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual($"subjob:{subscriptions[0]}", results[0]?.SuccessResponse?.Resource?.Id);
            Assert.AreEqual($"subjob:{subscriptions[1]}", results[1]?.SuccessResponse?.Resource?.Id);
            Mock.VerifyAll(subscriptionProvider, registrationProvider, builder);
        }

        [TestMethod]
        public async Task GetFullSyncSubTasksAsync_WhenCalled_ReturnsSubtasks()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var builder = new Mock<IDataBuilder<SubscriptionSkuModel>>();
            var registrationProvider = new Mock<IRegistrationProvider>();
            string[] subscriptions = ["cf18a41e-69d2-4b9a-ab5f-01e8246b40dc", "cee43662-4990-48eb-ba4e-02633c2e0ee6"];
            subscriptionProvider.Setup(x => x.GetSubscriptionsByRangeAsync("subscriptions-0", 0, -1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new string[] { subscriptions[0] });
            subscriptionProvider.Setup(x => x.GetSubscriptionsByRangeAsync("subscriptions-1", 0, -1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new string[] { subscriptions[1] });
            serviceCollection.AddSingleton(subscriptionProvider.Object);
            serviceCollection.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            serviceCollection.AddSingleton(builder.Object);
            ServiceRegistrations.ServiceProvider = serviceCollection.BuildServiceProvider();
            DataLabsARNV3Request request = new(
                DateTimeOffset.UtcNow,
                string.Empty,
                0,
                Guid.NewGuid().ToString(),
                new EventGridNotification<NotificationDataV3<GenericResource>>(
                    "/providers/Microsoft.ResourceGraph/skuFullSyncTriggerEvents/default",
                    string.Empty,
                    "Microsoft.Compute",
                    "Snapshot",
                    DateTimeOffset.UtcNow,
                    new NotificationDataV3<GenericResource>(
                        "Microsoft.ResourceGraph",
                        [
                            new NotificationResourceDataV3<GenericResource>(
                                Guid.NewGuid(),
                                new GenericResource
                                {
                                    Id = "/providers/Microsoft.ResourceGraph/skuFullSyncTriggerEvents/default",
                                    Type = "Microsoft.ResourceGraph/skuFullSyncTriggerEvents",
                                },
                                "2023-06-01",
                                DateTimeOffset.UtcNow
                            )
                        ])),
                null);

            registrationProvider.Setup(x => x.GetResourceProvidersAsync(CancellationToken.None))
                .ReturnsAsync(["Microsoft.Compute"]);
            var pipeline = new DataPipeline(subscriptionProvider.Object, registrationProvider.Object, builder.Object);
            var results = new List<DataLabsARNV3Response>();

            // Act
            await foreach (var actual in pipeline.GetSubJobsForFullSyncAsync(request, CancellationToken.None))
            {
                results.Add(actual);
            }

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual($"subjob:{subscriptions[0]}", results[0]?.SuccessResponse?.Resource?.Id);
            Assert.AreEqual($"subjob:{subscriptions[1]}", results[1]?.SuccessResponse?.Resource?.Id);
            Mock.VerifyAll(subscriptionProvider, registrationProvider, builder);
        }

        [TestMethod]
        public async Task GetSubJobsForFullSyncAsync_WhenSubsInConfig_ReadsFromConfig()
        {
            // Arrange
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration["CustomConfig"] = @"
                    { 
                      ""subjobBatchSize"" : ""1000"",
                      ""configFetchIntervalInHours"" : ""6"",
                      ""globalSkuBatchSize"": ""1000"",
                      ""casClientId"": ""901c622e-9663-4c65-9008-df103ed6cc5a"",
                      ""previewSubscriptions"": ""cf18a41e-69d2-4b9a-ab5f-01e8246b40dc,cee43662-4990-48eb-ba4e-02633c2e0ee6""
                    }
            ";
            
            var serviceCollection = new ServiceCollection();
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var builder = new Mock<IDataBuilder<SubscriptionSkuModel>>();
            var registrationProvider = new Mock<IRegistrationProvider>();
            string[] subscriptions = ["cf18a41e-69d2-4b9a-ab5f-01e8246b40dc", "cee43662-4990-48eb-ba4e-02633c2e0ee6"];
            serviceCollection.AddSingleton(subscriptionProvider.Object);
            serviceCollection.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            serviceCollection.AddSingleton(builder.Object);
            ServiceRegistrations.InitializeServiceProvider(serviceCollection, "SkuService");
            DataLabsARNV3Request request = new(
                DateTimeOffset.UtcNow,
                string.Empty,
                0,
                Guid.NewGuid().ToString(),
                new EventGridNotification<NotificationDataV3<GenericResource>>(
                    "/providers/Microsoft.ResourceGraph/skuFullSyncTriggerEvents/default",
                    string.Empty,
                    "Microsoft.Compute",
                    "Snapshot",
                    DateTimeOffset.UtcNow,
                    new NotificationDataV3<GenericResource>(
                        "Microsoft.ResourceGraph",
                        [
                            new NotificationResourceDataV3<GenericResource>(
                                Guid.NewGuid(),
                                new GenericResource
                                {
                                    Id = "/providers/Microsoft.ResourceGraph/skuFullSyncTriggerEvents/default",
                                    Type = "Microsoft.ResourceGraph/skuFullSyncTriggerEvents",
                                },
                                "2023-06-01",
                                DateTimeOffset.UtcNow
                            )
                        ])),
                null);

            registrationProvider.Setup(x => x.GetResourceProvidersAsync(CancellationToken.None))
                .ReturnsAsync(["Microsoft.Compute"]);
            var pipeline = new DataPipeline(subscriptionProvider.Object, registrationProvider.Object, builder.Object);
            var results = new List<DataLabsARNV3Response>();

            // Act
            await foreach (var actual in pipeline.GetSubJobsForFullSyncAsync(request, CancellationToken.None))
            {
                results.Add(actual);
            }

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual($"subjob:{subscriptions[0]}", results[0]?.SuccessResponse?.Resource?.Id);
            Assert.AreEqual($"subjob:{subscriptions[1]}", results[1]?.SuccessResponse?.Resource?.Id);
            Mock.VerifyAll(subscriptionProvider, registrationProvider, builder);
        }

        [TestMethod]
        public async Task GetFullSyncSubJobsAsync_WhenNoRPsFound_ThrowsException()
        {
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var builder = new Mock<IDataBuilder<SubscriptionSkuModel>>();
            var registrationProvider = new Mock<IRegistrationProvider>();
            registrationProvider.Setup(x => x.GetResourceProvidersAsync(CancellationToken.None))
                .ReturnsAsync([]);

            DataLabsARNV3Request request = default!;
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
                await new DataPipeline(subscriptionProvider.Object, registrationProvider.Object, builder.Object).GetSubJobsForFullSyncAsync(request, CancellationToken.None).ToListAsync());
            Mock.VerifyAll(subscriptionProvider, registrationProvider, builder);
        }

        [TestMethod]
        public async Task GetResourcesForSubjobsAsync_InvalidSubjobId_ThrowsException()
        {
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var builder = new Mock<IDataBuilder<SubscriptionSkuModel>>();
            var registrationProvider = new Mock<IRegistrationProvider>();
            DataLabsARNV3Request request = new(
                DateTimeOffset.UtcNow,
                string.Empty,
                0,
                Guid.NewGuid().ToString(),
                new EventGridNotification<NotificationDataV3<GenericResource>>(
                    "subjob:1:1",
                    string.Empty,
                    "Microsoft.Compute",
                    "Snapshot",
                    DateTimeOffset.UtcNow,
                    new NotificationDataV3<GenericResource>(
                        "Microsoft.ResourceGraph",
                        [
                            new NotificationResourceDataV3<GenericResource>(
                                Guid.NewGuid(),
                                new GenericResource
                                {
                                    Id = "/providers/Microsoft.ResourceGraph/skuFullSyncTriggerEvents/default",
                                    Type = "Microsoft.ResourceGraph/skuFullSyncTriggerEvents",
                                },
                                "2023-06-01",
                                DateTimeOffset.UtcNow
                            )
                        ])),
                null);
            _ = await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                await new DataPipeline(subscriptionProvider.Object, registrationProvider.Object, builder.Object).GetResourcesForSubjobsAsync(request, CancellationToken.None).ToListAsync());
            Mock.VerifyAll(subscriptionProvider, registrationProvider, builder);
        }

        [TestMethod]
        public async Task GetResourcesForSubjobsAsync_InvalidSubscription_ThrowsException()
        {
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var builder = new Mock<IDataBuilder<SubscriptionSkuModel>>();
            var registrationProvider = new Mock<IRegistrationProvider>();
            DataLabsARNV3Request request = new(
                DateTimeOffset.UtcNow,
                string.Empty,
                0,
                Guid.NewGuid().ToString(),
                new EventGridNotification<NotificationDataV3<GenericResource>>(
                    "subjob:1",
                    string.Empty,
                    "Microsoft.Compute",
                    "Snapshot",
                    DateTimeOffset.UtcNow,
                    new NotificationDataV3<GenericResource>(
                        "Microsoft.ResourceGraph",
                        [
                            new NotificationResourceDataV3<GenericResource>(
                                Guid.NewGuid(),
                                new GenericResource
                                {
                                    Id = "/providers/Microsoft.ResourceGraph/skuFullSyncTriggerEvents/default",
                                    Type = "Microsoft.ResourceGraph/skuFullSyncTriggerEvents",
                                },
                                "2023-06-01",
                                DateTimeOffset.UtcNow
                            )
                        ])),
                null);
            _ = await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                await new DataPipeline(subscriptionProvider.Object, registrationProvider.Object, builder.Object).GetResourcesForSubjobsAsync(request, CancellationToken.None).ToListAsync());
            Mock.VerifyAll(subscriptionProvider, registrationProvider, builder);
        }
    }
}
