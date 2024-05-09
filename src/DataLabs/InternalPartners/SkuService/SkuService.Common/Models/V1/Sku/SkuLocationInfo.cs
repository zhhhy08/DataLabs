namespace SkuService.Common.Models.V1
{
    using Newtonsoft.Json;
    using Microsoft.WindowsAzure.ResourceStack.Common.Core.Definitions.Resources;

    public class SkuLocationInfo
    {
        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public required string Location { get; set; }

        /// <summary>
        /// Gets or sets the physical availability zones.
        /// </summary>
        public string[]? Zones { get; set; }

        /// <summary>
        /// Gets or sets the zone details.
        /// </summary>
        public SkuZoneDetail[]? ZoneDetails { get; set; }

        /// <summary>
        /// Gets or sets the extended locations
        /// </summary>
        public string[]? ExtendedLocations { get; set; }

        /// <summary>
        /// Gets or sets the type of extended locations
        /// </summary>
        public ExtendedLocationType? Type { get; set; }

        /// <summary>
        /// Gets or sets the location details
        /// </summary>
        public SkuExtendedLocationDetails[]? LocationDetails { get; set; }

        /// <summary>
        /// Gets or sets the Spot restricted flag.
        /// </summary>
        public bool IsSpotRestricted { get; set; }

        /// <summary>
        /// Gets or sets the Ondemand restricted flag.
        /// </summary>
        public bool IsOndemandRestricted { get; set; }

        /// <summary>
        /// Gets or sets the Capacity Reservation restricted flag.
        /// </summary>
        public bool IsCapacityReservationRestricted { get; set; }
    }
}
