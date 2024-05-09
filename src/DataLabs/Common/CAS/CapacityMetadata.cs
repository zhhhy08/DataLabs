namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS
{
    using System;
    using System.Collections.Generic;

    public class CapacityMetadata
    {
        public Dictionary<string, Dictionary<string, SkuInfo>> ResourceFamilyToOfferUnitToSkuInfoMapping { get; }

        public Dictionary<CustomerSegmentKey, string> CustomerSegmentKeyToCustomerSegmentMapping { get; }

        public Dictionary<ModernCustomerSegmentKey, string> ModernCustomerSegmentKeyToCustomerSegmentMapping { get; }

        public CapacityConfigurationSettings Settings { get; }

        public CapacityMetadata(Dictionary<string, Dictionary<string, SkuInfo>> resourceFamilyToOfferUnitToSkuInfoMapping, Dictionary<CustomerSegmentKey, string> customerSegmentKeyToCustomerSegmentMapping, CapacityConfigurationSettings settings, Dictionary<ModernCustomerSegmentKey, string>? modernCustomerSegmentKeyToCustomerSegmentMapping = null)
        {
            ResourceFamilyToOfferUnitToSkuInfoMapping = resourceFamilyToOfferUnitToSkuInfoMapping ?? throw new ArgumentNullException("resourceFamilyToOfferUnitToSkuInfoMapping");
            CustomerSegmentKeyToCustomerSegmentMapping = customerSegmentKeyToCustomerSegmentMapping ?? throw new ArgumentNullException("customerSegmentKeyToCustomerSegmentMapping");
            Settings = settings ?? throw new ArgumentNullException("settings");
            ModernCustomerSegmentKeyToCustomerSegmentMapping = (Settings.EnableModernCommerce ? (modernCustomerSegmentKeyToCustomerSegmentMapping ?? throw new ArgumentNullException("modernCustomerSegmentKeyToCustomerSegmentMapping")) : (modernCustomerSegmentKeyToCustomerSegmentMapping ?? new Dictionary<ModernCustomerSegmentKey, string>(new ModernCustomerSegmentKeyEqualityComparer())));
        }
    }
}
