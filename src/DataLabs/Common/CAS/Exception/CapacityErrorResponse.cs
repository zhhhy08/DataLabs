namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS.Exception
{
    using Newtonsoft.Json;

    public sealed class CapacityErrorResponse
    {
        [JsonProperty]
        public CapacityErrorDetails? Error { get; set; }
    }
}
