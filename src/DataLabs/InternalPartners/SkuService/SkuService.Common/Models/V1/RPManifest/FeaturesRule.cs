namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;
    using static SkuService.Common.Models.Enums;

    public class FeaturesRule
    {
        //
        // Summary:
        //     Gets or sets the required features policy.
        [JsonProperty(Required = Required.Always)]
        public FeaturesPolicy RequiredFeaturesPolicy { get; set; }
    }
}
