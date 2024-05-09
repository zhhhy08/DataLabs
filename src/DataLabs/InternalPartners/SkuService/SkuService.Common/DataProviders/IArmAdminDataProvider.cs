namespace SkuService.Common.DataProviders
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using SkuService.Common.Models.V1.RPManifest;

    public interface IArmAdminDataProvider
    {
        public Task GetAndUpdateArmAdminConfigsAsync(CancellationToken cancellationToken);
        public IDictionary<string, InsensitiveDictionary<string>> GetAllowedAvailabilityZoneMappings { get; }

        public IDictionary<string, string[]> GetFeatureFlagsToLocationMappings { get; }
        
        public ProviderRegistrationLocationElement[] GetAllowedProviderRegistrationLocationsWithFeatureFlag { get; }

    }
}