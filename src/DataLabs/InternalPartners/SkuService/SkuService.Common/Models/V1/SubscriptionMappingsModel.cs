namespace SkuService.Common.Models.V1
{
    public class SubscriptionMappingsModel
    {
        /// <summary>
        /// Gets or sets the subscription identifier.
        /// </summary>
        /// <value>
        /// The subscription identifier.
        /// </value>
        public string? SubscriptionId { get; set; }

        /// <summary>
        /// Gets or sets the managed by tenant ids.
        /// </summary>
        /// <value>
        /// The managed by tenant ids.
        /// </value>
        public string[]? ManagedByTenantIds { get; set; }

        /// <summary>
        /// Gets or sets the availability zones.
        /// SailFish team recommends storing as Dictionary instead of array of values due to increased complexity to
        /// query when it is an array.
        /// Dictionary key is Location with value being another dictionary with key as Physical Zone and value is Logical Zone.
        /// Example:
        /// {
        ///   "eastus": {
        ///     "1": "2",
        ///     "2": "1",
        ///     "3": "3"
        ///   }
        /// }
        /// </summary>
        /// <value>
        /// The availability zones.
        /// </value>
        public Dictionary<string, List<ZoneMapping>> AvailabilityZoneMappings { get; set; } = default!;
        
        /// <summary>
        /// Created Time 
        /// </summary>
        public string CreatedTime { get; set; } = default!;

        /// <summary>
        /// Changed Time
        /// </summary>
        public string ChangedTime { get; set; } = default!;
    }
}
