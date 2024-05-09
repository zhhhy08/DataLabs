namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS.Enums;

    public class ModernCustomerSegmentKey
    {
        public WorkloadType WorkloadType { get; }

        public PaymentType PaymentType { get; }

        public ChannelType ChannelType { get; }

        public BillingType BillingType { get; }

        public Tier Tier { get; }

        public CostCategory CostCategory { get; }

        public ModernCustomerSegmentKey(WorkloadType workloadType, PaymentType paymentType, ChannelType channelType, BillingType billingType, Tier tier, CostCategory costCategory = CostCategory.None)
        {
            WorkloadType = workloadType;
            PaymentType = paymentType;
            ChannelType = channelType;
            BillingType = billingType;
            Tier = tier;
            CostCategory = costCategory;
        }

        public override bool Equals(object? obj)
        {
            ModernCustomerSegmentKey? modernCustomerSegmentKey;
            if ((modernCustomerSegmentKey = obj as ModernCustomerSegmentKey) != null && WorkloadType == modernCustomerSegmentKey.WorkloadType && PaymentType == modernCustomerSegmentKey.PaymentType && ChannelType == modernCustomerSegmentKey.ChannelType && BillingType == modernCustomerSegmentKey.BillingType && Tier == modernCustomerSegmentKey.Tier)
            {
                return CostCategory == modernCustomerSegmentKey.CostCategory;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return (((((WorkloadType.GetHashCode()) * -1521134295 + PaymentType.GetHashCode()) * -1521134295 + ChannelType.GetHashCode()) * -1521134295 + BillingType.GetHashCode()) * -1521134295 + Tier.GetHashCode()) * -1521134295 + CostCategory.GetHashCode();
        }

        public override string ToString()
        {
            return string.Join(",", WorkloadType, PaymentType, ChannelType, BillingType, Tier, CostCategory);
        }
    }
}
