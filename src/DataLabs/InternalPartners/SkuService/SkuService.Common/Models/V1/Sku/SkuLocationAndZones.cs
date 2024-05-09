namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using static SkuService.Common.Models.Enums;

    public class SkuLocationAndZones
    {
        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        [JsonProperty("location", Required = Required.Always)]
        public string Location { get; set; } = default!;

        /// <summary>
        /// Gets or sets the logical availability zones.
        /// </summary>
        [JsonProperty("zones")]
        public string[] Zones { get; set; } = default!;

        /// <summary>
        /// Gets or sets the zone details.
        /// </summary>
        [JsonProperty("zoneDetails")]
        public SkuZoneDetail[] ZoneDetails { get; set; } = default!;

        /// <summary>
        /// Gets or sets the extended locations.
        /// </summary>
        [JsonProperty("extendedLocations", NullValueHandling = NullValueHandling.Ignore)]
        public string[] ExtendedLocations { get; set; } = default!;

        /// <summary>
        /// Gets or sets the type of extended locations
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public LocationType? Type { get; set; }
    }
}
