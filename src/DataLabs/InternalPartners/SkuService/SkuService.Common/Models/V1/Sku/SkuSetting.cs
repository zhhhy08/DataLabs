namespace SkuService.Common.Models.V1
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Newtonsoft.Json;
    using SkuService.Common.Extensions;

    public class SkuSetting
    {
        /// <summary>
        /// Gets or sets the SKU name.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public required string? Name { get; set; }

        /// <summary>
        /// Gets or sets the SKU tier.
        /// </summary>
        public string? Tier { get; set; }

        /// <summary>
        /// Gets or sets the SKU size.
        /// </summary>
        public string? Size { get; set; }

        /// <summary>
        /// Gets or sets the SKU family.
        /// </summary>
        public string? Family { get; set; }

        /// <summary>
        /// Gets or sets the SKU kind.
        /// </summary>
        public string? Kind { get; set; }

        /// <summary>
        /// Gets or sets the SKU supported locations.
        /// </summary>
        public string[] Locations { get; set; } = default!;

        public string? Location => Locations?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the SKU supported location info.
        /// </summary>
        public SkuLocationInfo[]? LocationInfo { get; set; }

        /// <summary>
        /// Gets or sets the quota Ids SKU supported.
        /// </summary>
        public string[]? RequiredQuotaIds { get; set; }

        /// <summary>
        /// Gets or sets the required features. 
        /// </summary>
        public string[]? RequiredFeatures { get; set; }

        /// <summary>
        /// Gets or sets the SKU capacity.
        /// </summary>
        public SkuCapacity? Capacity { get; set; }

        /// <summary>
        /// Gets or sets the SKU costs.
        /// </summary>
        public SkuCost[]? Costs { get; set; }

        /// <summary>
        /// Gets or sets the SKU capabilities.
        /// </summary>
        [JsonConverter(typeof(CustomDictionaryConverter))]
        public IDictionary<string, string>? Capabilities { get; set; }

        /// <summary>
        /// Gets the SKU location info.
        /// </summary>
        public SkuLocationInfo[]? GetLocationInfo()
        {
            return LocationInfo.CoalesceEnumerable().Any()
                ? LocationInfo
                : Locations.CoalesceEnumerable().SelectArray(location => new SkuLocationInfo { Location = location });
        }

        public bool IsAvailabilityZonesEnabled
        {
            get
            {
                return GetLocationInfo()!.Any(locationInfo => locationInfo.Zones.CoalesceEnumerable().Any());
            }
        }

        /// <summary>
        /// Sets the SKU location info.
        /// </summary>
        /// <param name="locationInfo">The location info.</param>
        public void SetLocationInfo(SkuLocationInfo[] locationInfo)
        {
            LocationInfo = locationInfo;
            Locations = locationInfo.Select(singleLocation => singleLocation.Location).DistinctArrayOrdinalInsensitively();
        }

    }
}
