namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;
    using SkuService.Common.Extensions;

    public class SkuZoneDetail
    {
        /// <summary>
        /// Gets or sets the physical zones.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string[]? Zones { get; set; }

        /// <summary>
        /// Gets or sets the capabilities.
        /// </summary>
        [JsonProperty(PropertyName = "capabilities")]
        [JsonConverter(typeof(CustomDictionaryConverter))]
        public IDictionary<string, string>? Capabilities { get; set; }
    }
}
