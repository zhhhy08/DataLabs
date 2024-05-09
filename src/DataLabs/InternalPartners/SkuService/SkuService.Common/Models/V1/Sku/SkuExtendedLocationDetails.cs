namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;
    using SkuService.Common.Extensions;

    public class SkuExtendedLocationDetails
    {
        /// <summary>
        /// Gets or sets the extended locations
        /// </summary>
        [JsonProperty("extendedLocations")]
        public string[]? ExtendedLocations { get; set; }

        /// <summary>
        /// Gets or sets the capabilities.
        /// </summary>
        [JsonProperty("capabilities")]
        [JsonConverter(typeof(CustomDictionaryConverter))]
        public IDictionary<string, string>? Capabilities { get; set; }
    }
}
