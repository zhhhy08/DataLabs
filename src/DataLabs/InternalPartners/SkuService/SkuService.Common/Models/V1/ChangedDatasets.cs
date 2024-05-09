namespace SkuService.Common.Models.V1
{
    using SkuService.Common.Models.V1.CAS;

    public class ChangedDatasets
    {
        public GlobalSku? SkuSettings { get; set; }

        public SubscriptionInternalPropertiesModel? SubscriptionInternalProperties { get; set; }

        public SubscriptionMappingsModel? SubscriptionMappings { get; set; }

        public CapacityRestrictionsInputModel? CapacityRestrictionsInputModel { get; set; }

        public SubscriptionFeatureRegistrationPropertiesModel? SubscriptionFeatureRegistrationProperties { get; set; }
    }
}
