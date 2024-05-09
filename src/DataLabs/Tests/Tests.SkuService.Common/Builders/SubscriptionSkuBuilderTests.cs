namespace Tests.SkuService.Common.Builders
{
    using global::SkuService.Common.Builders;
    using global::SkuService.Common.DataProviders;
    using global::SkuService.Common.Extensions;
    using global::SkuService.Common.Models.V1;
    using global::SkuService.Common.Models.V1.RPManifest;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Microsoft.WindowsAzure.ResourceStack.Common.Core.Definitions;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using static global::SkuService.Common.Models.Enums;

    [TestClass]
    public class SubscriptionSkuBuilderTests
    {
        [TestMethod]
        public async Task BuildAsync_WhenCalled_ReturnsSubscriptionSkuModelAsync()
        {
            // Arrange
            var registrationProvider = new Mock<IRegistrationProvider>();
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var restrictionsProvider = new Mock<IRestrictionsProvider>();
            var adminProvider = new Mock<IArmAdminDataProvider>();
            var loggerFactory = new Mock<ILoggerFactory>();
            var subscriptionId = Guid.NewGuid().ToString();
            var resourceProvider = "Microsoft.Compute";
            var subscriptionFeatureRegistrationProperties = new List<SubscriptionFeatureRegistrationPropertiesModel>
            {
                new() {
                    ProviderNamespace = resourceProvider,
                    SubscriptionId = subscriptionId,
                    State = "Registered",
                    FeatureName = "AllowVM",
                    CreatedTime = DateTime.UtcNow.ToString(),
                },
            };

            var subscriptionMapping = new SubscriptionMappingsModel
            {
                SubscriptionId = subscriptionId,
                AvailabilityZoneMappings =

                    new Dictionary<string, List<ZoneMapping>>
                    {
                        {
                            "eastus", new List<ZoneMapping>
                            {
                                new() {
                                    LogicalZone = "1",
                                    PhysicalZone = "2",
                                }
                            }
                        }
                    }
            };

            var subscriptionInternalProperties = new SubscriptionInternalPropertiesModel
            {
                SubscriptionId = subscriptionId,
                BillingProperties = new BillingProperties
                {
                    BillingType = "Usage",
                },
                EntitlementStartDate = DateTime.UtcNow.ToString(),
                OfferType = SubscriptionOfferType.Trial,
                SubscriptionPolicies = new SubscriptionPolicies
                {
                    QuotaId = "123",
                },
            };

            var skuSettings2 = new List<SkuSetting>
            {
                new() {
                    Capabilities = new Dictionary<string, string>
                    {
                        { "vCPUs", "4" },
                        { "Memory", "8" },
                    },
                    Capacity = new SkuCapacity
                    {
                        Default = 1,
                        Maximum = 1,
                        Minimum = 1,
                    },
                    Costs =
                    [
                        new() {
                            MeterId = "123",
                            Quantity = 1,
                        },
                    ],
                    Family = "Standard",
                    Kind = "Basic",
                    Locations =
                    [
                        "eastus",
                    ],
                    Name = "D2sV2",
                    Tier = "Advanced",
                    Size = "10",
                    RequiredFeatures =
                    [
                        "Microsoft.Compute/AllowVM",
                    ],
                    RequiredQuotaIds =
                    [
                        "123",
                    ],
                },
            };


            var skuSettings = new List<SkuSetting>
            {
                new() {
                    Capabilities = new Dictionary<string, string>
                    {
                        { "vCPUs", "4" },
                        { "Memory", "8" },
                    },
                    Capacity = new SkuCapacity
                    {
                        Default = 1,
                        Maximum = 1,
                        Minimum = 1,
                    },
                    Costs =
                    [
                        new() {
                            MeterId = "123",
                            Quantity = 1,
                        },
                    ],
                    Family = "Standard",
                    Kind = "Basic",
                    Locations =
                    [
                        "eastus",
                    ],
                    Name = "D2sV1",
                    Tier = "Basic",
                    Size = "10",
                    RequiredFeatures =
                    [
                        "Microsoft.Compute/AllowVM",
                    ],
                    RequiredQuotaIds =
                    [
                        "123",
                    ],
                }
            };

            var globalSku1 = new GlobalSku
            {
                Skus = [.. skuSettings],
                Location = "eastus",
                ResourceType = "virtualMachines",
                SkuProvider = "Microsoft.Compute",
            };

            var globalSku2 = new GlobalSku
            {
                Skus = [.. skuSettings2],
                Location = "eastus",
                ResourceType = "virtualMachines",
                SkuProvider = "Microsoft.Compute",
            };

            var globalSkus = new List<GlobalSku> { globalSku1, globalSku2 };
            var manifest = new ResourceProviderManifest
            {
                ResourceTypes =
                [
                    new() {
                        CapacityRule = new Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration.CapacityRule
                        {
                            CapacityPolicy = Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration.CapacityPolicy.Restricted,
                        },
                        Name = "virtualMachines",
                        AvailabilityZoneRule = new Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration.AvailabilityZoneRule
                        {
                            AvailabilityZonePolicy = Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration.AvailabilityZonePolicy.SingleZoned
                        }
                    }
                ]
            };

            var subRegistrations = new SubscriptionRegistrationModel
            { RegistrationDate = DateTime.UtcNow.ToString(), RegistrationState = SubscriptionRegistrationState.Registered, ResourceProviderNamespace = resourceProvider };
            var restrictions = new InsensitiveDictionary<SkuLocationInfo[]>
            {
                { "D2sV1", new SkuLocationInfo[] { new() { Location = "eastus", Zones = ["1"] } } }
            };
            subscriptionProvider.Setup(x => x.GetSubscriptionFeatureRegistrationPropertiesAsync(subscriptionId, resourceProvider, It.IsAny<IActivity>(), CancellationToken.None)).ReturnsAsync(subscriptionFeatureRegistrationProperties);
            subscriptionProvider.Setup(x => x.GetSubscriptionMappingsAsync(subscriptionId, It.IsAny<IActivity>(), CancellationToken.None)).ReturnsAsync(subscriptionMapping);
            subscriptionProvider.Setup(x => x.GetSubscriptionInternalPropertiesAsync(subscriptionId, It.IsAny<IActivity>(), CancellationToken.None)).ReturnsAsync(subscriptionInternalProperties);
            subscriptionProvider.Setup(x => x.GetSubscriptionRegistrationAsync(subscriptionId, resourceProvider, It.IsAny<IActivity>(), CancellationToken.None)).ReturnsAsync(subRegistrations);
            registrationProvider.Setup(x => x.GetGlobalSkuAsync(resourceProvider, subscriptionFeatureRegistrationProperties, null, CancellationToken.None)).ReturnsAsync(globalSkus);
            registrationProvider.Setup(x => x.FindRegistrationsForFeatureSetAsync(resourceProvider, subscriptionFeatureRegistrationProperties, It.IsAny<IActivity>(), CancellationToken.None)).ReturnsAsync(manifest.ToResourceTypeRegistrations(Array.Empty<ProviderRegistrationLocationElement>()));
            adminProvider.Setup(x => x.GetFeatureFlagsToLocationMappings).Returns(new Dictionary<string, string[]> { { "Microsoft.Compute/Az_UsEast", new string[] { "US EAST" } } });
            restrictionsProvider.Setup(restrictionsProvider => restrictionsProvider.GetSkuCapacityRestrictionsAsync(resourceProvider, subRegistrations.RegistrationDate, subscriptionInternalProperties, subscriptionMapping, It.IsAny<IActivity>(), false, CancellationToken.None)).ReturnsAsync(restrictions);
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(adminProvider.Object);
            ServiceRegistrations.ServiceProvider = serviceCollection.BuildServiceProvider();
            var subscriptionSkuBuilder = new SubscriptionSkuBuilder(registrationProvider.Object, subscriptionProvider.Object, restrictionsProvider.Object);

            // Act
            await foreach (var sku in subscriptionSkuBuilder.BuildAsync(resourceProvider, subscriptionId, new Mock<IActivity>().Object, new ChangedDatasets(), CancellationToken.None))
            {
                // Assert
                Assert.IsNotNull(sku);
            }

            Mock.Verify(subscriptionProvider, registrationProvider, restrictionsProvider);
        }

        [TestMethod]
        public async Task BuildAsync_WithChangedDatasets_ReturnsSubscriptionSkuModelAsync()
        {
            // Arrange
            var registrationProvider = new Mock<IRegistrationProvider>();
            var subscriptionProvider = new Mock<ISubscriptionProvider>();
            var restrictionsProvider = new Mock<IRestrictionsProvider>();
            var adminProvider = new Mock<IArmAdminDataProvider>();
            var loggerFactory = new Mock<ILoggerFactory>();
            var subscriptionId = Guid.NewGuid().ToString();
            var resourceProvider = "Microsoft.Compute";
            var subscriptionFeatureRegistrationProperties = new List<SubscriptionFeatureRegistrationPropertiesModel>
            {
                new() {
                    ProviderNamespace = resourceProvider,
                    SubscriptionId = subscriptionId,
                    State = "Registered",
                    FeatureName = "AllowVM",
                    CreatedTime = DateTime.UtcNow.ToString(),
                },
            };

            var subscriptionMapping = new SubscriptionMappingsModel
            {
                SubscriptionId = subscriptionId,
                AvailabilityZoneMappings =

                    new Dictionary<string, List<ZoneMapping>>
                    {
                        {
                            "eastus", new List<ZoneMapping>
                            {
                                new() {
                                    LogicalZone = "1",
                                    PhysicalZone = "2",
                                }
                            }
                        }
                    }
            };

            var subscriptionInternalProperties = new SubscriptionInternalPropertiesModel
            {
                SubscriptionId = subscriptionId,
                BillingProperties = new BillingProperties
                {
                    BillingType = "Usage",
                },
                EntitlementStartDate = DateTime.UtcNow.ToString(),
                OfferType = SubscriptionOfferType.Trial,
                SubscriptionPolicies = new SubscriptionPolicies
                {
                    QuotaId = "123",
                },
            };


            var skuSettings = new List<SkuSetting>
            {
                new() {
                    Capabilities = new Dictionary<string, string>
                    {
                        { "vCPUs", "4" },
                        { "Memory", "8" },
                    },
                    Capacity = new SkuCapacity
                    {
                        Default = 1,
                        Maximum = 1,
                        Minimum = 1,
                    },
                    Costs =
                    [
                        new() {
                            MeterId = "123",
                            Quantity = 1,
                        },
                    ],
                    Family = "Standard",
                    Kind = "Basic",
                    Locations =
                    [
                        "eastus",
                    ],
                    Name = "D2sV1",
                    Tier = "Basic",
                    Size = "10",
                    RequiredFeatures =
                    [
                        "Microsoft.Compute/AllowVM",
                    ],
                    RequiredQuotaIds =
                    [
                        "123",
                    ],
                },
            };

            var globalSku = new GlobalSku
            {
                Skus = skuSettings.ToArray(),
                Location = "eastus",
                ResourceType = "virtualMachines",
                SkuProvider = "Microsoft.Compute",
            };
            var manifest = new ResourceProviderManifest
            {
                ResourceTypes =
                [
                    new() {
                        CapacityRule = new Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration.CapacityRule
                        {
                            CapacityPolicy = Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration.CapacityPolicy.Restricted,
                        },
                        Name = "virtualMachines",
                        AvailabilityZoneRule = new Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration.AvailabilityZoneRule
                        {
                            AvailabilityZonePolicy = Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration.AvailabilityZonePolicy.SingleZoned
                        }
                    }
                ]
            };

            var changedDatasets = new ChangedDatasets()
            {
                SubscriptionInternalProperties = subscriptionInternalProperties,
            };

            var subRegistrations = new SubscriptionRegistrationModel
            { RegistrationDate = DateTime.UtcNow.ToString(), RegistrationState = SubscriptionRegistrationState.Registered, ResourceProviderNamespace = resourceProvider };
            var restrictions = new InsensitiveDictionary<SkuLocationInfo[]>
            {
                { "D2sV1", new SkuLocationInfo[] { new() { Location = "eastus", Zones = ["1"] } } }
            };
            subscriptionProvider.Setup(x => x.GetSubscriptionFeatureRegistrationPropertiesAsync(subscriptionId, resourceProvider, It.IsAny<IActivity>(), CancellationToken.None)).ReturnsAsync(subscriptionFeatureRegistrationProperties);
            subscriptionProvider.Setup(x => x.GetSubscriptionMappingsAsync(subscriptionId, It.IsAny<IActivity>(), CancellationToken.None)).ReturnsAsync(subscriptionMapping);
            subscriptionProvider.Setup(x => x.GetSubscriptionRegistrationAsync(subscriptionId, resourceProvider, It.IsAny<IActivity>(), CancellationToken.None)).ReturnsAsync(subRegistrations);
            registrationProvider.Setup(x => x.GetGlobalSkuAsync(resourceProvider, subscriptionFeatureRegistrationProperties, null, CancellationToken.None)).ReturnsAsync(new List<GlobalSku> { globalSku });
            registrationProvider.Setup(x => x.FindRegistrationsForFeatureSetAsync(resourceProvider, subscriptionFeatureRegistrationProperties, It.IsAny<IActivity>(), CancellationToken.None)).ReturnsAsync(manifest.ToResourceTypeRegistrations(Array.Empty<ProviderRegistrationLocationElement>()));
            adminProvider.Setup(x => x.GetFeatureFlagsToLocationMappings).Returns(new Dictionary<string, string[]> { { "Microsoft.Compute/Az_UsEast", new string[] { "US EAST" } } });
            restrictionsProvider.Setup(restrictionsProvider => restrictionsProvider.GetSkuCapacityRestrictionsAsync(resourceProvider, subRegistrations.RegistrationDate, subscriptionInternalProperties, subscriptionMapping, It.IsAny<IActivity>(), false, CancellationToken.None)).ReturnsAsync(restrictions);
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(adminProvider.Object);
            ServiceRegistrations.ServiceProvider = serviceCollection.BuildServiceProvider();
            var subscriptionSkuBuilder = new SubscriptionSkuBuilder(registrationProvider.Object, subscriptionProvider.Object, restrictionsProvider.Object);

            // Act
            await foreach (var sku in subscriptionSkuBuilder.BuildAsync(resourceProvider, subscriptionId, new Mock<IActivity>().Object, changedDatasets, CancellationToken.None))
            {
                // Assert
                Assert.IsNotNull(sku);
            }

            subscriptionProvider.Verify(x => x.GetSubscriptionInternalPropertiesAsync(subscriptionId, It.IsAny<IActivity>(), CancellationToken.None), Times.Never());
            Mock.Verify(subscriptionProvider, registrationProvider, restrictionsProvider);
        }
    }
}
