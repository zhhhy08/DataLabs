namespace SkuService.Common.Extensions
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Common.Storage;
    using SkuService.Common.DataProviders;
    using SkuService.Common.Models.V1;

    public static class SubscriptionExtensions
    {
        public static InsensitiveHashSet ToLocationsWithNoZoneMapping(this IEnumerable<SubscriptionFeatureRegistrationPropertiesModel> featureRegistrations)
        {
            var adminProvider = ServiceRegistrations.ServiceProvider.GetService<IArmAdminDataProvider>();
            GuardHelper.ArgumentNotNull(adminProvider, nameof(adminProvider));
            InsensitiveHashSet locationsWithNoZoneMapping = new();
            var featureFlagsToLocationMappings = adminProvider.GetFeatureFlagsToLocationMappings;
            var subscriptionFeatures = featureRegistrations.ToInsensitiveHashSet(elementSelector: registration => registration.FullyQualifiedName);
            featureFlagsToLocationMappings.ForEach(feature =>
            {
                if (!subscriptionFeatures.Contains(feature.Key))
                {
                    feature.Value.ForEach(location => locationsWithNoZoneMapping.Add(StorageUtility.NormalizeLocationForStorage(location)));
                }
            });

            return locationsWithNoZoneMapping;
        }


        /// <summary>
        /// Gets a list of extended locations that are available for a subscription
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        public static ExtendedLocation[] GetExtendedLocations(this IEnumerable<SubscriptionFeatureRegistrationPropertiesModel> subscriptionFeatureRegistrations)
        {
            var extendedLocations = ArmAdminDataProvider.ExtendedLocations;
            return extendedLocations?.Where(extendedLocation => subscriptionFeatureRegistrations.HasRequiredFeatures(extendedLocation.RequiredFeatures))
                .ToArray()!;
        }

        /// <summary>
        /// Gets if the subscription has the required features.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        /// <param name="requiredFeatures">The required features.</param>
        private static bool HasRequiredFeatures(this IEnumerable<SubscriptionFeatureRegistrationPropertiesModel> subscriptionFeatureRegistrations, string[] requiredFeatures)
        {
            if (!requiredFeatures.CoalesceEnumerable().Any())
            {
                return true;
            }

            return subscriptionFeatureRegistrations
                .DistinctArray(feature => feature.FullyQualifiedName)
                .CoalesceEnumerable()
                .IntersectInsensitively(requiredFeatures.CoalesceEnumerable())
                .Any();
        }
    }
}
