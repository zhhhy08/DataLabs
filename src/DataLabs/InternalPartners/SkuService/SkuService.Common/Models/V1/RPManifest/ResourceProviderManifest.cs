namespace SkuService.Common.Models.V1
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Common.Storage;
    using Microsoft.WindowsAzure.ResourceStack.Common.Utilities;
    using Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration;
    using SkuService.Common.Models.V1.RPManifest;

    public class ResourceProviderManifest
    {
        public FeaturesRule FeaturesRule { get; set; } = default!;

        public string[] RequiredFeatures { get; set; } = default!;

        public string Namespace { get; set; } = default!;

        /// <summary>
        /// Gets or sets the resource types.
        /// </summary>
        public ResourceType[] ResourceTypes { get; set; } = default!;

        /// <summary>
        /// Gets the resource type registrations.
        /// </summary>
        public ResourceTypeRegistration[] ToResourceTypeRegistrations(ProviderRegistrationLocationElement[] getAllowedProviderRegistrationLocationsWithFeatureFlag) => this.ResourceTypes
                .CoalesceEnumerable()
                .SelectManyArray(resourceType => resourceType.Endpoints
                    .CoalesceEnumerable()
                    .Where(endpoint => endpoint.Enabled ?? true)
                    .SelectMany(endpoint => endpoint.GetApiVersions(resourceType.CommonApiVersions, resourceType.ResourceTypeCommonAttributeManagement?.CommonApiVersionsMergeMode ?? CommonApiVersionsMergeMode.Merge)
                        .SelectMany(apiVersion => endpoint.Locations.CoalesceEnumerable().Select(location => new ResourceTypeRegistration
                        {
                            Manifest = this,
                            ResourceTypeDefinition = resourceType,
                            Endpoint = endpoint,
                            ProviderRequiredFeatures = this.GetProviderRequiredFeatures(endpoint: endpoint, resourceType: resourceType),
                            RegionRequiredFeatures = GetRequiredFeaturesForLocation(location, getAllowedProviderRegistrationLocationsWithFeatureFlag)!,
                            Location = location,

                            // Intern the location string since they are heavily duplicated and the total unique values is tiny
                            NormalizedLocation = string.Intern(NormalizationUtility.NormalizeLocationForStorage(location)),
                            ApiVersion = apiVersion,
                        }))));

        private string[] GetProviderRequiredFeatures(ResourceProviderEndpoint endpoint, ResourceType resourceType)
        {
            return this.RequiredFeatures
                .CoalesceEnumerable()
                .Concat(resourceType.GetAllProviderRequiredFeatures(endpoint))
                .DistinctArrayOrdinalInsensitively(feature => feature);
        }

        /// <summary>
        /// Get region wide required features for a specific location.
        /// </summary>
        /// <param name="location">The location.</param>
        private static string[]? GetRequiredFeaturesForLocation(string location, ProviderRegistrationLocationElement[] getAllowedProviderRegistrationLocationsWithFeatureFlag)
        {
            return getAllowedProviderRegistrationLocationsWithFeatureFlag
                .CoalesceEnumerable()
                .FirstOrDefault(registration => StorageUtility.LocationsEquals(registration?.Location, location))?
                .FeatureFlags;
        }
    }
}
