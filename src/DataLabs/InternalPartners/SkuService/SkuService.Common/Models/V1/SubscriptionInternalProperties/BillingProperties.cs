namespace SkuService.Common.Models.V1
{
    public class BillingProperties
    {
        /// <summary>
        /// Billing Account
        /// </summary>
        public BillingAccount? BillingAccount { get; set; }

        /// <summary>
        /// Billing Type
        /// </summary>
        public string? BillingType { get; set; }

        /// <summary>
        /// Channel Type
        /// </summary>
        public string? ChannelType { get; set; }

        /// <summary>
        /// Payment Type
        /// </summary>
        public string? PaymentType { get; set; }

        /// <summary>
        /// Tier
        /// </summary>
        public string? Tier { get; set; }

        /// <summary>
        /// Workload Type
        /// </summary>
        public string? WorkloadType { get; set; }

        /// <summary>
        /// Additional State Properties
        /// </summary>
        public AdditionalStateProperties? AdditionalStateProperties { get; set; }
    }
}
