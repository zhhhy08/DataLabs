namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using static SkuService.Common.Models.Enums;

    public class SkuCapacity
    {
        /// <summary>
        /// Gets or sets the minimum.
        /// </summary>
        [JsonProperty("minimum", Required = Required.Always)]
        public int Minimum { get; set; }

        /// <summary>
        /// Gets or sets the maximum.
        /// </summary>
        [JsonProperty("maximum")]
        public int Maximum { get; set; }

        /// <summary>
        /// Gets or sets the default.
        /// </summary>.
        [JsonProperty("default")]
        public int Default { get; set; }

        /// <summary>
        /// Gets or sets the type of the scale.
        /// </summary>
        [JsonProperty("scaleType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public SkuScaleType ScaleType { get; set; }
    }
}
