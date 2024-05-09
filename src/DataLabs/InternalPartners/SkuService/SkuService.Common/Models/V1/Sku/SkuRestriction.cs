namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using static SkuService.Common.Models.Enums;

    public class SkuRestriction
    {
        /// <summary>
        /// Gets or sets the type of restriction.
        /// </summary>
        [JsonProperty("type", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public SkuRestrictionType Type { get; set; }

        /// <summary>
        /// Gets or sets the restriction values.
        /// </summary>
        [JsonProperty("values")]
        public string[] Values { get; set; } = default!;

        /// <summary>
        /// Gets or sets the restriction information.
        /// After API version 2017-09-01 this is the field with zone restrictions. Values field only contains location restrictions. 
        /// </summary>
        [JsonProperty("restrictionInfo")]
        public RestrictionInfo RestrictionInfo { get; set; } = default!;

        /// <summary>
        /// Gets or sets the restriction reason code.
        /// </summary>
        [JsonProperty("reasonCode", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public SkuRestrictionReasonCode ReasonCode { get; set; }
    }
}