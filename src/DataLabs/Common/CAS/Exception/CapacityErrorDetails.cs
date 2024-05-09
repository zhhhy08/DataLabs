namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS.Exception
{
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;

    public sealed class CapacityErrorDetails
    {
        [JsonProperty]
        public string? Code { get; set; }

        [JsonProperty]
        public string? Message { get; set; }

        [JsonProperty]
        public JToken? ServiceDefinedValues { get; set; }
    }
}
