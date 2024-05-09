namespace SkuService.Common.Models.V1
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Core.Definitions.Resources;

    /// <summary>
    /// Extended location
    /// </summary>
    public class ExtendedLocation
    {
        /// <summary>
        /// Gets or sets the name
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// Gets or sets the display name
        /// </summary>
        public string DisplayName { get; set; } = default!;

        /// <summary>
        /// Gets or sets the type of extended location
        /// </summary>
        public ExtendedLocationType Type { get; set; }

        /// <summary>
        /// Gets or sets the region
        /// </summary>
        public string Region { get; set; } = default!;

        /// <summary>
        /// Gets or sets the GPS Longitude
        /// </summary>
        public string Longitude { get; set; } = default!;

        /// <summary>
        /// Gets or sets the GPS Latitdue
        /// </summary>
        public string Latitude { get; set; } = default!;

        /// <summary>
        /// Gets or sets the geography group
        /// </summary>
        public string GeographyGroup { get; set; } = default!;

        /// <summary>
        /// Gets or sets the physical location
        /// </summary>
        public string PhysicalLocation { get; set; } = default!;

        /// <summary>
        /// Gets or sets the required features
        /// </summary>
        public string[] RequiredFeatures { get; set; } = default!;
    }
}
