namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel
{
    using Newtonsoft.Json;

    /// <summary>
    /// Partner Resources Routing Config for Incoming resources.
    /// </summary>
    public struct PartnerResourcesRoutingConfig
    {
        [JsonProperty]
        public string ResourceTypes { get;  private set; }

        [JsonProperty]
        public string EventTypes { get;  private set; }
        
        [JsonProperty]
        public string PartnerChannelName { get; private set; }

        [JsonProperty]
        public string PartnerChannelAddress { get; private set; }
    }
}
