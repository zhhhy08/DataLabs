namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;

    /// <summary>
    /// The SKU definition.
    /// </summary>
    public class SubscriptionSkuProperties
    {
        /// <summary>
        /// Gets or sets the SKU name.
        /// </summary>
        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; set; } = default!;

        /// <summary>
        /// Gets or sets the SKU tier.
        /// </summary>
        [JsonProperty("tier", NullValueHandling = NullValueHandling.Ignore)]
        public string Tier { get; set; } = default!;

        /// <summary>
        /// Gets or sets the SKU size.
        /// </summary>
        [JsonProperty("size", NullValueHandling = NullValueHandling.Ignore)]
        public string Size { get; set; } = default!;

        /// <summary>
        /// Gets or sets the SKU family.
        /// </summary>
        [JsonProperty("family", NullValueHandling = NullValueHandling.Ignore)]
        public string Family { get; set; } = default!;

        /// <summary>
        /// Gets or sets the SKU kind.
        /// </summary>
        [JsonProperty("kind", NullValueHandling = NullValueHandling.Ignore)]
        public string Kind { get; set; } = default!;

        /// <summary>
        /// Gets or sets the SKU supported locations.
        /// </summary>
        [JsonProperty("locations")]
        public string[] Locations { get; set; } = default!;

        /// <summary>
        /// Gets or sets the SKU supported location and logical zone info.
        /// </summary>
        [JsonProperty("locationInfo")]
        public SkuLocationAndZones[] LocationInfo { get; set; } = default!;

        /// <summary>
        /// Gets or sets the SKU capacity.
        /// </summary>
        [JsonProperty("capacity", NullValueHandling = NullValueHandling.Ignore)]
        public SkuCapacity Capacity { get; set; } = default!;

        /// <summary>
        /// Gets or sets the SKU costs.
        /// </summary>
        [JsonProperty("costs", NullValueHandling = NullValueHandling.Ignore)]
        public SkuCost[] Costs { get; set; } = default!;

        /// <summary>
        /// Gets or sets the SKU capabilities.
        /// </summary>
        [JsonProperty("capabilities")]
        public IDictionary<string, string>? Capabilities { get; set; }

        /// <summary>
        /// Gets or sets the SKU restrictions.
        /// </summary>
        [JsonProperty("restrictions")]
        public SkuRestriction[] Restrictions { get; set; } = default!;
    }
}
