namespace SkuService.Common.Models.V1
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Core.Definitions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class SubscriptionRegistrationModel
    {
        /// <summary>
        /// Gets the registration date.
        /// </summary>
        [JsonProperty("registrationDate", Required = Required.Always)]
        public string RegistrationDate { get; set; } = default!;

        /// <summary>
        /// Gets the RP Namespace.
        /// </summary>
        [JsonProperty("resourceProviderNamespace")]
        public string ResourceProviderNamespace { get; set; } = default!;

        /// <summary>
        /// Gets the registration state.
        /// </summary>
        [JsonProperty("registrationState")]
        [JsonConverter(typeof(StringEnumConverter))]
        public SubscriptionRegistrationState RegistrationState { get; set; } = default!;
    }
}
