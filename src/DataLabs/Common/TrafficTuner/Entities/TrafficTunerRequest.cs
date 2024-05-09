namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner
{
    /// <summary>
    /// Traffic tuner request class.
    /// </summary>
    public readonly struct TrafficTunerRequest
    {
        /// <summary>
        /// Subscription Id.
        /// </summary>
        public readonly string? SubscriptionId;

        /// <summary>
        /// Resource type.
        /// </summary>
        public readonly string? ResourceType;

        /// <summary>
        /// Tenant Id.
        /// </summary>
        public readonly string? TenantId;

        /// <summary>
        /// ResourceLocation
        /// </summary>
        public readonly string? ResourceLocation;

        /// <summary>
        /// Message retry count.
        /// </summary>
        public readonly int MessageRetryCount;

        /// <summary>
        /// Resource Id.
        /// </summary>
        public readonly string? ResourceId;


        public TrafficTunerRequest(
            string? subscriptionId = null,
            string? resourceType = null,
            string? tenantId = null,
            string? resourceLocation = null,
            int messageRetryCount = 0,
            string? resourceId = null)
        {
            SubscriptionId = subscriptionId;
            ResourceType = resourceType;
            TenantId = tenantId;
            ResourceLocation = resourceLocation;
            MessageRetryCount = messageRetryCount;
            ResourceId = resourceId;
        }
    }


}
