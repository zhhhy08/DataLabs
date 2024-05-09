
namespace SkuService.Common.Utilities
{
    public class Constants
    {
        #region Resource Types
        public const string SubscriptionMappingsResourceId = "/subscriptions/{0}/providers/Microsoft.Resources/subscriptionZoneMappings";
        public const string SubscriptionInternalPropertiesResourceId = "/subscriptions/{0}/providers/Microsoft.Inventory/subscriptionInternalProperties";
        public const string SubscriptionFeatureRegistrationResourceId = "/subscriptions/{0}/providers/Microsoft.Features/featureProviders/{1}/subscriptionFeatureRegistrations";
        public const string SubscriptionRegistrationsResourceId = "/subscriptions/{0}/providers/Microsoft.Resources/subscriptionRegistrations";
        public const string GlobalSkuResourceId = "/providers/Microsoft.Inventory/skuProviders/{0}";
        public const string SubscriptionSkuResourceId = "/subscriptions/{0}/providers/Microsoft.ResourceGraph/skuProviders/{1}/resourceTypes/{2}/locations/{3}/skus/default";
        public const string GlobalSkuResourceType = "Microsoft.Inventory/skuProviders/resourceTypes/locations/globalSkus";
        public const string SubscriptionMappingResourceType = "microsoft.resources/subscriptionzonemappings";
        public const string SubscriptionInternalPropertiesResourceType = "microsoft.inventory/subscriptioninternalproperties";
        public const string CapacityRestrictionsResourceType = "microsoft.capacityallocation/capacityrestrictions";
        public const string SubscriptionFeatureRegistrationType = "microsoft.features/featureproviders/subscriptionfeatureregistrations";
        public const string ManifestProviderResourceType = "microsoft.inventory/manifestprovider";
        public const string ArmAdminResourceType = "microsoft.inventory/configspecs";
        public const string ArmAdminCloudSpecsType = "clouds/public";
        public const string ArmAdminGlobalSpecsType = "global";
        public const string ArmAdminRegionSpecsType = "clouds/public/regions";
        public const string SubscriptionSkuResourceType = "Microsoft.ResourceGraph/skuProviders/resourceTypes/locations/skus";
        public static readonly HashSet<string> ComputeResourceTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            SubscriptionMappingResourceType,
            SubscriptionInternalPropertiesResourceType,
            CapacityRestrictionsResourceType,
        };

        public static readonly HashSet<string> SubscriptionResources = new(ComputeResourceTypes, StringComparer.OrdinalIgnoreCase)
        {
            "subscriptionFeatureRegistrations",
        };

        #endregion

        /// <summary>
        /// Low Priority Capable
        /// </summary>
        public const string LowPriorityCapable = "LowPriorityCapable";

        /// <summary>
        /// Capacity Reservation Supported
        /// </summary>
        public const string CapacityReservationSupported = "CapacityReservationSupported";

        public static readonly char[] ConfigDelimeters = new char[] { ':', ';' };

        public const string SkuApiVersion = "2023-06-01";

        public const string PublisherInfo = "Microsoft.DataLabs";
        public const string ResourceGraphProvider = "Microsoft.ResourceGraph";

        public const string CustomConfig = "CustomConfig";
        public const string ResourceProvider = "ResourceProvider";

        public const string SubJobsCount = "SubJobsCount";
        public const string OutputCount = "OutputCount";
        public const string SubscriptionsCount = "SubscriptionsCount";
        public const string ResourceProvidersCount = "ResourceProvidersCount";
        public const string SubscriptionsCacheKeyPrefix = "subscriptions-";

        public const string CasClientId = "casClientId";
        public const string Topic = "SubscriptionSku";
        public const string InvalidSubscriptionId = "InvalidSubscriptionId";
        #region Custom Config settings
        public const string ConfigFetchIntervalInHours = "configFetchIntervalInHours";
        public const string SubscriptionsKeyCount = "subscriptionsKeyCount";
        public const string GlobalSkuBatchSize = "globalSkuBatchSize";
        public const string ServiceName = "serviceName";
        public const string PreviewSubscriptions = "previewSubscriptions";
        public const string UseMget = "useMget";
        #endregion

        public const int EventTimeBytes = 8;

        public const string State = "state";
        public const string Subjob = "subjob";
        //registration states
        public const string Deleted = "deleted";
        public const string Pending = "pending";

        // Datasets
        public const string SubscriptionRegistrationsDataset = "SubscriptionRegistrations";
        public const string SubscriptionZoneMappingsDataset = "SubscriptionZoneMappings";
        public const string SubscriptionInternalPropertiesDataset = "SubscriptionInternalProperties";
        public const string SubscriptionFeatureRegistrationsDataset = "SubscriptionFeatureRegistrations";
        public const string CapacityRestrictionsDataset = "CapacityRestrictions";
        public const string RPManifestDataset = "RPManifest";
    }
}
