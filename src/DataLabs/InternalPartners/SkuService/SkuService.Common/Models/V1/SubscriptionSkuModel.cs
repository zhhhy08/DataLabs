namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;
    using System.Collections.Generic;

    public class SubscriptionSkuModel
    {
        [JsonProperty("skus")]
        public ICollection<SubscriptionSkuProperties> Skus { get; set; } = default!;

        [JsonProperty("resourceType")]
        public string ResourceType { get; set; } = default!;

        [JsonIgnore]
        public string Location { get; set; } = default!;

        [JsonProperty("skuProvider")]
        public string SkuProvider { get; set; } = default!;

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
