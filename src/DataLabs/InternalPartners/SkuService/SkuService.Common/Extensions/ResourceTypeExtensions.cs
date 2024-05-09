namespace SkuService.Common.Extensions
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Common.Storage;
    using SkuService.Common.Models.V1;
    using StringExtensions = Microsoft.WindowsAzure.ResourceStack.Common.Extensions.StringExtensions;

    internal static class ResourceTypeExtensions
    {
        /// <summary>
        /// Gets the unique identity string of a resource type.
        /// </summary>
        /// <param name="registration">The resource type registration.</param>
        public static string GetUniqueIdentity(this ResourceTypeRegistration registration)
        {
            return StringExtensions.ConcatStrings(
                strings: new[]
                {
                    registration.ResourceProviderNamespace.ToUpperInvariant(),
                    registration.ResourceType.ToUpperInvariant(),
                    StorageUtility.NormalizeLocationForStorage(registration.Location),
                    registration.ApiVersion.CoalesceString().ToUpperInvariant(),
                    GetRequiredFeaturesIdentity(registration.ProviderRequiredFeatures)
                },
                separator: "-");
        }

        /// <summary>
        /// Gets the required features identity.
        /// </summary>
        /// <param name="requiredFeatures">The required features.</param>
        public static string GetRequiredFeaturesIdentity(string[] requiredFeatures)
        {
            const int RequiredFeaturesIdentityMaxLength = 100;

            var normalizedFeatures = requiredFeatures
                .CoalesceEnumerable()
                .SelectArray(feature => feature.ToUpperInvariant());

            return StorageUtility.TrimStorageKey(
                storageKey: normalizedFeatures.OrderByAscendingInsensitively(feature => feature).ConcatStrings(":"),
                limit: RequiredFeaturesIdentityMaxLength);
        }

        /// <summary>
        /// Gets the full set of AFEC flags that are used in allowing access to this type.
        /// </summary>
        /// <param name="registration">The resource type registration.</param>
        public static string[] GetAllProviderAndRegionRequiredFeatures(this ResourceTypeRegistration registration)
        {
            return registration.ProviderRequiredFeatures
                .CoalesceEnumerable()
                .Union(registration.RegionRequiredFeatures.CoalesceEnumerable(), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
