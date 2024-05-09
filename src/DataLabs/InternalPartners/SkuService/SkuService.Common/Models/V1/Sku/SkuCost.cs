namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;

    /// <summary>
    /// The SKU cost
    /// </summary>
    public class SkuCost
    {
        /// <summary>
        /// Gets or sets the meter Id.
        /// </summary>
        [JsonProperty("meterId", Required = Required.Always)]
        public required string MeterId { get; set; }

        /// <summary>
        /// Gets or sets the quantity.
        /// </summary>
        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        /// <summary>
        /// Gets or sets the extended unit.
        /// </summary>
        [JsonProperty("extendedUnit")]
        public string? ExtendedUnit { get; set; }
    }
}
