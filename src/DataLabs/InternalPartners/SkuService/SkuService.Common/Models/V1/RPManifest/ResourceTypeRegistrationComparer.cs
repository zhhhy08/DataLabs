namespace SkuService.Common.Models.V1.RPManifest
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Storage;
    using System;
    using System.Collections.Generic;

    public class ResourceTypeRegistrationComparer : IEqualityComparer<ResourceTypeRegistration>
    {
        /// <summary>
        /// The singleton instance of registration equality comparer.
        /// </summary>
        public static readonly ResourceTypeRegistrationComparer Instance = new ResourceTypeRegistrationComparer();

        /// <summary>
        /// Determines whether the specified objects are equal.
        /// </summary>
        /// <param name="first">The first registration.</param>
        /// <param name="second">The second registration.</param>
        public bool Equals(ResourceTypeRegistration? first, ResourceTypeRegistration? second)
            => StringComparer.OrdinalIgnoreCase.Equals(first!.ApiVersion, second!.ApiVersion) &&
            StringComparer.OrdinalIgnoreCase.Equals(first.ResourceProviderNamespace, second.ResourceProviderNamespace) &&
            StringComparer.OrdinalIgnoreCase.Equals(first.ResourceType, second.ResourceType) &&
            LocationStringEqualityComparer.Instance.Equals(first.Location, second.Location);

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <param name="obj">The request registration.</param>
        public int GetHashCode(ResourceTypeRegistration obj)
            => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ApiVersion) ^
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ResourceProviderNamespace) ^
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ResourceType) ^
            LocationStringEqualityComparer.Instance.GetHashCode(obj.Location);
    }
}
