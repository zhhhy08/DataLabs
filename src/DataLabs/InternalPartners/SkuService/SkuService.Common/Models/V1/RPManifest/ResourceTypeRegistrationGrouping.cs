namespace SkuService.Common.Models.V1.RPManifest
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using SkuService.Common.Extensions;
    using System.Linq;

    public class ResourceTypeRegistrationGrouping
    {
        /// <summary>
        /// Initializes a new instance of the ResourceTypeRegistrationGrouping class.
        /// </summary>
        /// <param name="resourceTypeRegistrations">Array of resource type registrations to be stored.</param>
        public ResourceTypeRegistrationGrouping(ResourceTypeRegistration[] resourceTypeRegistrations)
        {
            this.All = resourceTypeRegistrations;
            this.WithoutFeatures = this.All.Where(resourceType => !resourceType.AreAnyFeaturesRequired).ToArray();
            this.WithFeatures = this.All.Except(this.WithoutFeatures).ToArray();

            this.RequiredFeatures = this.WithFeatures
                .SelectMany(registration => registration.GetAllProviderAndRegionRequiredFeatures())
                .ToOrdinalInsensitiveHashSet();
        }

        /// <summary>
        /// Gets the full list of resource type registrations 
        /// </summary>
        public ResourceTypeRegistration[] All { get; private set; }

        /// <summary>
        /// Gets or sets the set of unique required features in all registrations.
        /// </summary>
        public OrdinalInsensitiveHashSet RequiredFeatures { get; set; }

        /// <summary>
        /// Gets or sets all of the actions that have throttling rules specified
        /// </summary>
        public OrdinalInsensitiveHashSet ThrottlingRulesActions { get; set; } = default!;

        /// <summary>
        /// Gets or sets the set of unique required features in all registrations.
        /// </summary>
        public OrdinalInsensitiveHashSet ThrottlingRulesRequiredFeatures { get; set; } = default!;

        /// <summary>
        /// Gets only those resource type registrations with feature flags.
        /// </summary>
        public ResourceTypeRegistration[] WithFeatures { get; private set; }

        /// <summary>
        /// Gets only those resource type registrations without feature flags.
        /// </summary>
        public ResourceTypeRegistration[] WithoutFeatures { get; private set; }
    }
}
