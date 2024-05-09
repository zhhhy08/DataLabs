namespace SkuService.Common.Models.V1
{
    /// <summary>
    /// The feature registration properties.
    /// </summary>
    public class SubscriptionFeatureRegistrationPropertiesModel
    {
        /// <summary>
        /// Gets or sets the subscription Id.
        /// </summary>       
        public string? SubscriptionId { get; set; }

        /// <summary>
        /// Gets or sets the feature provider namespace.
        /// </summary>
        public string? ProviderNamespace { get; set; }

        /// <summary>
        /// Gets or sets the feature name.
        /// </summary>
        public string? FeatureName { get; set; }

        /// <summary>
        /// Gets or sets the registration state.
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Gets or sets the Metadata.
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Gets or sets the doc created date.
        /// </summary>
        public string? CreatedTime { get; set; }

        /// <summary>
        /// Gets or sets the doc changed date.
        /// </summary>
        public string? ChangedTime { get; set; }

        public string FullyQualifiedName => ProviderNamespace + "/" + FeatureName;
    }
}
