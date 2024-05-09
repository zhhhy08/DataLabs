namespace SkuService.Common.Models.V1
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration;
    using SkuService.Common.Models.V1.RPManifest;
    using static SkuService.Common.Models.Enums;

    public class ResourceTypeRegistration
    {
        public ResourceProviderManifest Manifest { get; set; } = default!;
        public ResourceType ResourceTypeDefinition { get; set; } = default!;
        public ResourceProviderEndpoint Endpoint { get; set; } = default!;

        /// <summary>
        /// Gets or sets the required features. The property is not used in WAP.
        /// </summary>
        public string[] RegionRequiredFeatures { get; set; } = default!;

        /// <summary>
        /// Gets or sets the required features. The property is not used in WAP.
        /// </summary>
        public string[] ProviderRequiredFeatures { get; set; } = default!;

        public string Location { get; set; } = default!;
        public string NormalizedLocation { get; set; } = default!;
        public string ApiVersion { get; set; } = default!;

        /// <summary>
        /// Gets the availability zone rule.
        /// </summary>
        public AvailabilityZoneRule AvailabilityZoneRule => this.ResourceTypeDefinition.AvailabilityZoneRule;

        /// <summary>
        /// Gets the zones.
        /// </summary>
        public string[] Zones => this.Endpoint.Zones;

        public CapacityRule CapacityRule => this.ResourceTypeDefinition.CapacityRule;
        public bool IsCapacityRuleEnabled => this.CapacityRule != null &&
            this.CapacityRule.CapacityPolicy == CapacityPolicy.Restricted;
        public string ResourceType => this.ResourceTypeDefinition.Name;

        public FeaturesRule ProviderFeaturesRule => this.Endpoint.FeaturesRule ?? this.ResourceTypeDefinition.FeaturesRule ?? this.Manifest.FeaturesRule;

        public string ResourceProviderNamespace => this.Manifest.Namespace;

        /// <summary>
        /// Gets a value indicating whether all features are required together to access this resource type.
        /// </summary>
        public bool AreAllProviderFeaturesRequired => this.ProviderFeaturesRule != null &&
            this.ProviderFeaturesRule.RequiredFeaturesPolicy == FeaturesPolicy.All;

        /// <summary>
        /// Gets a value indicating whether or not there are any provider specific feature flags for this type.
        /// </summary>
        public bool AreAnyFeaturesRequired => this.ProviderRequiredFeatures.CoalesceEnumerable().Any()
            || this.RegionRequiredFeatures.CoalesceEnumerable().Any();


        public bool IsAvailabilityZonesEnabled =>  (this.AvailabilityZoneRule != null &&
                    this.AvailabilityZoneRule.AvailabilityZonePolicy != AvailabilityZonePolicy.NotSpecified &&
                    this.Zones != null);
            
    }
}