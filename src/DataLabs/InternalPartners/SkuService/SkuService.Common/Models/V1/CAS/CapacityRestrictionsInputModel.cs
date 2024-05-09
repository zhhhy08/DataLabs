namespace SkuService.Common.Models.V1.CAS
{
    using Newtonsoft.Json;

    public class CapacityRestrictionsInputModel
    {
        /// <summary>
        /// The provider namespace.
        /// </summary>
        [JsonProperty("providerNamespace", Required = Required.Always)]
        public string ProviderNamespace { get; set; } = string.Empty;

        /// <summary>
        /// The subscription id.
        /// </summary>
        [JsonProperty("subscriptionId", Required = Required.Always)]
        public string SubscriptionId { get; set; } = string.Empty;
    }
}
