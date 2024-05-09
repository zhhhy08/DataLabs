namespace SkuService.Common.Models.V1
{
    /// <summary>
    /// Internal Subscription Policies
    /// </summary>
    public class InternalSubscriptionPolicies
    {
        /// <summary>
        /// Cost Category
        /// </summary>
        public string? CostCategory { get; set; }

        /// <summary>
        /// Environment, 0 is prod 1 is non-prod
        /// </summary>
        public bool? Environment { get; set; }

        /// <summary>
        /// Profit Center Code
        /// </summary>
        public string? PcCode { get; set; }
    }
}