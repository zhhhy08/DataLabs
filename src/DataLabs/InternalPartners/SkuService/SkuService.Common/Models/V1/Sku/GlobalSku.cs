namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;

    public class GlobalSku
    {
        /// <summary>
        /// Gets or sets the resource type.
        /// </summary>
        [JsonProperty("resourceType", Required = Required.Always)]
        public required string ResourceType { get; set; }

        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        [JsonProperty("location", Required = Required.Always)]
        public required string Location { get; set; }

        /// <summary>
        /// Gets or sets the SkuProvider.
        /// </summary>
        [JsonProperty("skuProvider", Required = Required.Always)]
        public required string SkuProvider { get; set; }

        /// <summary>
        /// Gets or sets SkuSettings.
        /// </summary>
        [JsonProperty("skus")]
        public required SkuSetting[] Skus { get; set; }
    }
}
