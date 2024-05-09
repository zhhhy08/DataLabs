namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    public class CustomerSegmentKey
    {
        public string OfferCategory { get; }

        public string CostCategory { get; }

        public CustomerSegmentKey(string offerCategory, string costCategory)
        {
            offerCategory = CapacityStorageUtility.NormalizeOfferCategory(offerCategory!);
            costCategory = CapacityStorageUtility.NormalizeCostCategory(costCategory!);
            OfferCategory = offerCategory;
            CostCategory = costCategory;
        }

        public override string ToString()
        {
            return string.Join(",", OfferCategory, CostCategory);
        }

        public override bool Equals(object? obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (this == obj)
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((CustomerSegmentKey)obj);
        }

        public override int GetHashCode()
        {
            return (((OfferCategory != null) ? OfferCategory.GetHashCode() : 0) * 397) ^ ((CostCategory != null) ? CostCategory.GetHashCode() : 0);
        }

        protected bool Equals(CustomerSegmentKey other)
        {
            if (string.Equals(OfferCategory, other.OfferCategory, StringComparison.InvariantCultureIgnoreCase))
            {
                return string.Equals(CostCategory, other.CostCategory, StringComparison.InvariantCultureIgnoreCase);
            }

            return false;
        }
    }
}
