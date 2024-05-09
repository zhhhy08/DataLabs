namespace Tests.SkuService.Common.Extensions
{
    using global::SkuService.Common.Models.V1;
    using global::SkuService.Common.Utilities;
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using static global::SkuService.Common.Models.Enums;

    [TestClass]
    public class RestrictionsHelperTests
    {
        public static IEnumerable<object[]> SkuCapacityRestrictionsData
        {
            get
            {
                return new[]
                {
                    new object[] { new Dictionary<string, string> { { "vCPUs", "1" }, { "ACUs", "100" } }, new Dictionary<string, string> { { "vCPUs", "1" }, { "ACUs", "100" } } },
                    new object[] { new Dictionary<string, string> { { "LowPriorityCapable", "True" }, { "CapacityReservationSupported", "True" } }, new Dictionary<string, string> { { "LowPriorityCapable", "True" }, { "CapacityReservationSupported", "True" } } },
                };
            }
        }

        [TestMethod]
        public void GetCapacityRestrictionsForSku_WithValidInputs_IsSuccessful()
        {
            var skuSetting = new SkuSetting
            {
                Name = "standardDS1v2",
            };

            var capacityEnabledResources = new InsensitiveHashSet { "virtualMachines" };

            var skuLocationInfo = new SkuLocationInfo[]
            {
                new SkuLocationInfo
                {
                    Location = "eastus",
                    Zones = new string[] { "1", "2", "3" },
                },
            };

            var capacityRestrictions = new InsensitiveDictionary<SkuLocationInfo[]>
            {
                { "standardDS1v2", skuLocationInfo },
            };

            var actual = RestrictionsHelper.GetCapacityRestrictionsForSku("virtualMachines", skuSetting, capacityEnabledResources, capacityRestrictions);
            Assert.IsNotNull(actual);
            Assert.AreEqual(skuLocationInfo, actual);
        }

        [TestMethod]
        public void GetCapacityRestrictionsForSku_WithInvalidInputs_ReturnsEmpty()
        {
            var skuSetting = new SkuSetting
            {
                Name = "standardDS1v2",
            };

            var capacityEnabledResources = new InsensitiveHashSet { "virtualMachines" };

            var skuLocationInfo = new SkuLocationInfo[]
            {
                new SkuLocationInfo
                {
                    Location = "eastus",
                    Zones = new string[] { "1", "2", "3" },
                },
            };

            var capacityRestrictions = new InsensitiveDictionary<SkuLocationInfo[]>
            {
                { "standardDS2v2", skuLocationInfo },
            };

            var actual = RestrictionsHelper.GetCapacityRestrictionsForSku("virtualMachines", skuSetting, capacityEnabledResources, capacityRestrictions);
            Assert.IsNotNull(actual);
            Assert.AreEqual(0, actual.Length);
        }

        [TestMethod]

        public void GetSkuLocationRestriction_WithValidInputs_IsSuccessful()
        {
            var locations = new string[] { "eastus" };
            var restrictionInfo = new RestrictionInfo { Locations = locations };
            var actual = RestrictionsHelper.GetSkuLocationRestriction(locations, SkuRestrictionReasonCode.NotSpecified, true);
            Assert.IsNotNull(actual);
            Assert.AreEqual(restrictionInfo.Locations[0], actual.RestrictionInfo.Locations![0]);
            Assert.AreEqual(locations, actual.Values);
        }

        [TestMethod]
        public void GetSkuCapacityRestrictions_WithValidInputs_IsSuccessful()
        {
            var locations = new string[] { "eastus" };

            var skuLocationInfo = new SkuLocationInfo[]
            {
                new SkuLocationInfo
                {
                    Location = "eastus",
                    IsOndemandRestricted = true,
                },
            };
            var subscriptionAvailabilityZoneLookup = new Dictionary<string, InsensitiveDictionary<string>>
            {
                { "eastus", new InsensitiveDictionary<string> { { "1", "useast-AZ01" }, { "2", "useast-AZ02" }, { "3", "useast-AZ03" } } },
            };

            var actual = RestrictionsHelper.GetSkuCapacityRestrictions(locations, skuLocationInfo, subscriptionAvailabilityZoneLookup, true);
            Assert.IsNotNull(actual);
            Assert.AreEqual(SkuRestrictionReasonCode.NotAvailableForSubscription, actual.First().ReasonCode);
            Assert.AreEqual(locations[0], actual.First().RestrictionInfo.Locations![0]);
        }
        
        [TestMethod]
        [DynamicData(nameof(SkuCapacityRestrictionsData))]
        public void GetCapabilitiesWithApplicableRestrictions_WithInputs_ReturnsCorrectly(Dictionary<string, string> capabilities, Dictionary<string, string> expected)
        {
            var locations = new string[] { "eastus" };
            var skuLocationInfo = new SkuLocationInfo[]
            {
                new SkuLocationInfo
                {
                    Location = "eastus",
                    IsOndemandRestricted = false,
                },
            };

            var actual = RestrictionsHelper.GetCapabilitiesWithApplicableRestrictions(capabilities, locations, skuLocationInfo);
            Assert.IsNotNull(actual);
            
        }
    }
}
