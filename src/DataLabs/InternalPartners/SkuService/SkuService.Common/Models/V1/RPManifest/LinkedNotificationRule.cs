namespace SkuService.Common.Models.V1.RPManifest
{
    using System;
    using Newtonsoft.Json;
    using SkuService.Common.Extensions;
    using JsonConverter = Newtonsoft.Json.JsonConverter;

    public class LinkedNotificationRule: Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration.LinkedNotificationRule
    {
        //
        // Summary:
        //     Gets or sets the linked notification time out.
        [JsonProperty(Required = Required.Default)]
        [JsonConverter(typeof(TimeSpanConverter))]
        new public TimeSpan? LinkedNotificationTimeout { get; set; }
    }
}
