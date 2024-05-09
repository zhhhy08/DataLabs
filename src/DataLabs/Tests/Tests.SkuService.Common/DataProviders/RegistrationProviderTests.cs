namespace Tests.SkuService.Common.DataProviders
{
    using global::SkuService.Common.DataProviders;
    using global::SkuService.Common.Models.V1;
    using global::SkuService.Common.Models.V1.RPManifest;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Registration provider tests.
    /// </summary>
    [TestClass]
    public class RegistrationProviderTests
    {
        internal const string GlobalSkuUpdateEvent = """
            {
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
                                            "LocationDetails": [
                                                {
                                                    "Capabilities" : 
                                                  "{\"ultraSSDAvailable\":\"True\"}",
                                                    "ExtendedLocations": ["test"],
                                                }
                                            ],
                                            "IsCapacityReservationRestricted": false,
                                            "IsOndemandRestricted": false,
                                            "IsSpotRestricted": false,
                                            "Location": "australiaeast",
                                            "Type": null,
                                            "ZoneDetails": [ 
                                             {
                                                  "Zones": null,
                                                  "Capabilities" : 
                                                  "{\"ultraSSDAvailable\":\"True\"}"

                                            }],
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
                    }
            """;

        private static readonly Mock<ICacheClient> mockCacheClient = new();
        private static readonly Mock<IResourceProxyClient> proxyClient = new();
        private static readonly Mock<IArmAdminDataProvider> armDataProvider = new();
        private static readonly Mock<ISkuServiceProvider> skuServiceProvider = new();

        [TestCleanup]
        public void Cleanup()
        {
            mockCacheClient.Reset();
            proxyClient.Reset();
            armDataProvider.Reset();
        }

        [TestInitialize]
        public void Initialize()
        {
            skuServiceProvider.Setup(x => x.GetServiceName()).Returns("SkuService");
        }

        [TestMethod]
        public async Task FindRegistrationsForFeatureSet_IsSuccessful()
        {
            IActivity activity = new BasicActivity("FindRegistrationsForFeatureSet_IsSuccessful");
            activity[SolutionConstants.PartnerTraceId] = Guid.NewGuid().ToString();
            activity[SolutionConstants.CorrelationId] = Guid.NewGuid().ToString();
            var response = new DataLabsARMAdminResponse(DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), new DataLabsARMAdminSuccessResponse(await File.ReadAllTextAsync("CRP Manifest.json"), DateTimeOffset.UtcNow), null, null, DataLabsDataSource.ARMADMIN);
            proxyClient.Setup(client => client.GetManifestConfigAsync(It.IsAny<DataLabsManifestConfigRequest>(), CancellationToken.None, false, false, null, null))
                .ReturnsAsync(response);
            armDataProvider.Setup(x => x.GetAllowedProviderRegistrationLocationsWithFeatureFlag).Returns(Array.Empty<ProviderRegistrationLocationElement>());
            var afecList = new List<SubscriptionFeatureRegistrationPropertiesModel>
            {
                new() {
                    ProviderNamespace = "Microsoft.Resources",
                    FeatureName = "FranceSouth",
                    State = "Registered",
                    SubscriptionId = Guid.NewGuid().ToString(),
                }
            };
            var actual = await new RegistrationProvider(proxyClient.Object, mockCacheClient.Object, armDataProvider.Object, skuServiceProvider.Object).FindRegistrationsForFeatureSetAsync(
                "Microsoft.Compute",
                afecList,
                activity,
                CancellationToken.None);

            Assert.IsNotNull(actual);
        }

        [TestMethod]
        public async Task GetGlobalSkuAsync_ForComputeRP_IsSuccessful()
        {
            var afec = new List<SubscriptionFeatureRegistrationPropertiesModel>
            {
                new()
                {
                    ProviderNamespace = "Microsoft.Compute",
                    FeatureName = "AZAPInternalVMSKU",
                    State = "Registered",
                    SubscriptionId = Guid.NewGuid().ToString(),
                },
                new()
                {
                    ProviderNamespace = "Microsoft.Compute",
                    FeatureName = "TestSubscription",
                    State = "Registered",
                    SubscriptionId = Guid.NewGuid().ToString(),
                },
                new()
                {
                    ProviderNamespace = "Microsoft.Compute",
                    FeatureName = "VMSKUPreview",
                    State = "Registered",
                    SubscriptionId = Guid.NewGuid().ToString(),
                }
            };

            mockCacheClient.Setup(client => client.GetCollectionValuesAsync("Microsoft.Compute",
                0, 999, 8, false, CancellationToken.None))
                .ReturnsAsync([GlobalSkuUpdateEvent]);

            var actual = await new RegistrationProvider(proxyClient.Object, mockCacheClient.Object, armDataProvider.Object, skuServiceProvider.Object).GetGlobalSkuAsync(
                               "Microsoft.Compute",
                               afec,
                               null,
                               CancellationToken.None);
            Assert.IsNotNull(actual);
            Assert.IsTrue(actual.Any());
            Assert.AreEqual("AZAP_Harvest_Compute_2", actual.First().Skus.First().Name);
        }

        [TestMethod]
        public async Task GetGlobalSkuAsync_WithChangedSkus_IsSuccessful()
        {
            var afec = new List<SubscriptionFeatureRegistrationPropertiesModel>();

            var changedSkus = new GlobalSku
            {
                Skus = new SkuSetting[1] {
                    new()
                    {
                        Name = "AZAP_Harvest_Compute_3",
                        Family = "azapHarvestFamily",
                        Tier = "Standard",
                        Locations = new string[] { "australiaeast" },
                    }
                },
                Location = "australiaeast",
                ResourceType = "virtualMachines",
                SkuProvider = "Microsoft.Compute",
            };
            var actual = await new RegistrationProvider(proxyClient.Object, mockCacheClient.Object, armDataProvider.Object, skuServiceProvider.Object).GetGlobalSkuAsync(
                               "Microsoft.Compute",
                               afec,
                               changedSkus,
                               CancellationToken.None);
            Assert.IsNotNull(actual);
            Assert.IsTrue(actual.Any());
            Assert.AreEqual("AZAP_Harvest_Compute_3", actual.First().Skus.First().Name);
        }

        [TestMethod]
        public async Task GetGlobalSkuAsync_WitCacheErrror_ThrowsException()
        {
            var afec = new List<SubscriptionFeatureRegistrationPropertiesModel>
            {
                new()
                {
                    ProviderNamespace = "Microsoft.Compute",
                    FeatureName = "AZAPInternalVMSKU",
                    State = "Registered",
                    SubscriptionId = Guid.NewGuid().ToString(),
                },
                new()
                {
                    ProviderNamespace = "Microsoft.Compute",
                    FeatureName = "TestSubscription",
                    State = "Registered",
                    SubscriptionId = Guid.NewGuid().ToString(),
                },
                new()
                {
                    ProviderNamespace = "Microsoft.Compute",
                    FeatureName = "VMSKUPreview",
                    State = "Registered",
                    SubscriptionId = Guid.NewGuid().ToString(),
                }
            };

            mockCacheClient.Setup(client => client.GetCollectionValuesAsync("Microsoft.Compute",
                0, 999, 8, false, CancellationToken.None))
                .ThrowsAsync(new OperationCanceledException("Cache error"));

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(async() => await new RegistrationProvider(proxyClient.Object, mockCacheClient.Object, armDataProvider.Object, skuServiceProvider.Object).GetGlobalSkuAsync(
                               "Microsoft.Compute",
                               afec,
                               null,
                               CancellationToken.None));
            mockCacheClient.VerifyAll();
        }

    }
}
