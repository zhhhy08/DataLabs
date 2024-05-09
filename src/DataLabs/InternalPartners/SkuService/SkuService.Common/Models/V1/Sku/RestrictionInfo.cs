namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;

    public class RestrictionInfo
    {
        /// <summary>
        /// Gets or sets the restriction locations.
        /// </summary>
        [JsonProperty("locations")]
        public string[] Locations { get; set; } = default!;

        /// <summary>
        /// Gets or sets the restriction logical zones.
        /// </summary>
        [JsonProperty("zones")]
        public string[] Zones { get; set; } = default!;
    }
}
