namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;

    public class SkuRegistrationsModel
    {
        /// <summary>
        /// Gets or sets the resource type name.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public required string ResourceType { get; set; }

        /// <summary>
        /// Gets or sets the SKU settings.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public required SkuSetting[] Skus { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Location { get; set; } = default!;
    }
}
