namespace SkuService.Common.Models.V1.RPManifest
{
    using Newtonsoft.Json;
    using SkuService.Common.Extensions;
    using System;

    public class AsyncTimeoutRule
    {
        //
        // Summary:
        //     Gets or sets the action name.
        [JsonProperty(Required = Required.Always)]
        public string ActionName { get; set; } = default!;

        //
        // Summary:
        //     Gets or sets the timeout value.
        [JsonProperty(Required = Required.Always)]
        [JsonConverter(typeof(TimeSpanConverter))]
        public TimeSpan Timeout { get; set; }
    }
}
