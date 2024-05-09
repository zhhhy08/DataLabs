namespace SkuService.Common.Models.V1
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration;
    using System.Linq;
    using System.Diagnostics.CodeAnalysis;
    using SkuService.Common.Models.V1.RPManifest;

    /// <summary>
    /// TODO: Remove attribute ExcludeFromCodeCoverage after adding unit tests
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ResourceType
    {
        //
        // Summary:
        //     Gets or sets the resource type name.
        public string Name { get; set; } = default!;

        //
        // Summary:
        //     Gets or sets the api versions supported by the resource type.
        public string[] CommonApiVersions { get; set; } = default!;

        /// <summary>
        /// Gets or sets the resource type management properties.
        /// </summary>
        public ResourceTypeCommonAttributeManagement ResourceTypeCommonAttributeManagement { get; set; } = default!;

        //
        // Summary:
        //     Gets or sets the capacity rule.
        public CapacityRule CapacityRule { get; set; } = default!;

        //
        // Summary:
        //     Gets or sets the availability zone rule.
        public AvailabilityZoneRule AvailabilityZoneRule { get; set; } = default!;
      
        //
        // Summary:
        //     Gets or sets the required features.
        public string[] RequiredFeatures { get; set; } = default!;

        //
        // Summary:
        //     Gets or sets the features rule.
        public FeaturesRule FeaturesRule { get; set; } = default!;      

        public ResourceProviderEndpoint[] Endpoints { get; set; } = default!;

        //
        // Summary:
        //     Gets the api versions for this resource type
        public string[] GetApiVersions()
        {
            return CommonApiVersions.CoalesceEnumerable().ToArray();
        }

        //
        // Summary:
        //     Gets all the required features.
        //
        // Parameters:
        //   endpoint:
        //     The resource provider endpoint from manifest.
        public string[] GetAllProviderRequiredFeatures(ResourceProviderEndpoint endpoint)
        {
            return RequiredFeatures.CoalesceEnumerable().Concat(endpoint.RequiredFeatures.CoalesceEnumerable()).DistinctArrayInsensitively((string feature) => feature, (string feature) => feature);
        }
    }
}
