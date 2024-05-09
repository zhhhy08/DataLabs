namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using SkuService.Common.Models.V1.SubscriptionInternalProperties;
    using static SkuService.Common.Models.Enums;

    public class SubscriptionInternalPropertiesModel
    {
        /// <summary>
        /// Subscription Id
        /// </summary>
        public string? SubscriptionId { get; set; }

        /// <summary>
        /// Display Name
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Entitlement Start Date
        /// </summary>
        public string? EntitlementStartDate { get; set; }

        /// <summary>
        /// Internal SubscriptionPolicies
        /// </summary>
        public InternalSubscriptionPolicies? InternalSubscriptionPolicies { get; set; }

        /// <summary>
        /// Offer Category
        /// </summary>
        public string? OfferCategory { get; set; }

        /// <summary>
        /// Offer Type
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public SubscriptionOfferType? OfferType { get; set; }

        /// <summary>
        /// Promotions
        /// </summary>
        public Promotions[]? Promotions { get; set; }

        /// <summary>
        /// Subscription State
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Subscription Policies
        /// </summary>
        public SubscriptionPolicies? SubscriptionPolicies { get; set; }

        /// <summary>
        /// Tenant Id
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Billing properties model for ARN
        /// </summary>
        public BillingProperties? BillingProperties { get; set; }
    }
}
