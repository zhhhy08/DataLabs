namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public class CapacityConfigurationSettings
    {
        public bool? EnableCostCategoryForCustomerSegment { get; }

        public bool? EnableCapacityCheckDeploymentPath { get; }

        public bool? EnableCapacityCheckDiscoveryPath { get; }

        public bool? EnableReservationDeploymentPath { get; }

        public bool? EnableReservationDiscoveryPath { get; }

        public bool? EnableEntitlementStartDate { get; }

        public string? ReservationDiscoveryClientAppIds { get; }

        public bool? EnableAZDeploymentPath { get; }

        public bool? EnableAZDiscoveryPath { get; }

        public bool? EnableSpotDeploymentPath { get; }

        public bool? EnableSpotDiscoveryPath { get; }

        public string? SpotOfferCategories { get; }

        public bool? SpotOfferCategoryEnforcementEnabled { get; }

        public bool EnableModernCommerce { get; }

        public bool? EnableCASv2Evaluator { get; }

        public bool? EnableBlocklistEvaluation { get; }

        public bool? EnableCustomerLevelAccesslist { get; }

        public bool? EnableUsageBasedOR { get; }

        public bool? UsageBasedORsBlockByDefault { get; }

        public bool EnableDedicatedHost { get; }

        public bool? EnableCloudServicesMultiSkuEvaluation { get; }

        public bool? EnablePostRequestEvaluation { get; }

        public bool? EnableSpotPredictionEvaluation { get; }

        public bool? EnableSpotPredictionAllocableVMsEvaluation { get; }

        public int? MinimumSpotPredictionSurvivabilityRate { get; }

        public int? NumberOfHoursSpotPredictionSurvivabilityRateExpires { get; }

        public bool? EnableCapacityReservationDeploymentPath { get; }

        public bool? EnableCapacityReservationDiscoveryPath { get; }

        public bool? EnableVmssPostRequestEvaluation { get; }

        public bool? EnableOptimizedDiscoveryPath { get; }

        public string? DisableArmCapacityCheckForVMInRegions { get; }

        public string? DisableArmCapacityCheckForVmssInRegions { get; }

        public string? DisableArmCapacityCheckForDHInRegions { get; }

        public string? DisableArmCapacityCheckForCRInRegions { get; }

        public string? DisableArmCapacityCheckForCSInRegions { get; }

        public string? DisableArmCapacityCheckForPreflightInRegions { get; set; }

        public bool? DisableArmCapacityCheckForVM { get; }

        public bool? DisableArmCapacityCheckForVmss { get; }

        public bool? DisableArmCapacityCheckForDH { get; }

        public bool? DisableArmCapacityCheckForCR { get; }

        public bool? DisableArmCapacityCheckForCS { get; }

        public bool? DisableArmCapacityCheckForPreflight { get; }

        public bool? EnableSpotPredictionEvaluationForDiscoveryPath { get; }

        public bool? EnableSpotAccessListEvaluation { get; }

        public bool? EnableServiceTreeAccessListEvaluation { get; }

        public bool? EnableRiskReputationScoreOR { get; }

        public int? MinimumRiskReputationScore { get; }

        public bool? EnableRiskReputationScoreThresholdEvaluation { get; }

        public bool? EnableTieredSpotAdmissionControl { get; }

        public bool? EnableTieredSpotAdmissionControlForDiscoveryPath { get; }

        public bool? EnableBlockNewResourceCreationEvaluation { get; }

        public CapacityConfigurationSettings(bool? enableCostCategoryForCustomerSegment, bool? enableCapacityCheckDeploymentPath, bool? enableCapacityCheckDiscoveryPath, bool? enableReservationDeploymentPath, bool? enableReservationDiscoveryPath, bool? enableEntitlementStartDate, string reservationDiscoveryClientAppIds, bool? enableAzDeploymentPath, bool? enableAzDiscoveryPath, bool? enableSpotDeploymentPath = false, bool? enableSpotDiscoveryPath = false, string? spotOfferCategories = null, bool? spotOfferCategoryEnforcementEnabled = false, bool? enableModernCommerce = false, bool? enableCASv2Evaluator = false, bool? enableBlocklistEvaluation = false, bool? enableCustomerLevelAccesslist = false, bool? enableUsageBasedOR = false, bool? usageBasedORsBlockByDefault = false, bool? enableDedicatedHost = false, bool? enableCloudServicesMultiSkuEvaluation = false, bool? enablePostRequestEvaluation = false, bool? enableSpotPredictionEvaluation = false, bool? enableSpotPredictionAllocableVMsEvaluation = false, int? minimumSpotPredictionSurvivabilityRate = 10, int? numberOfHoursSpotPredictionSurvivabilityRateExpires = 24, bool? enableCapacityReservationDeploymentPath = false, bool? enableCapacityReservationDiscoveryPath = false, bool? enableVmssPostRequestEvaluation = false, bool? enableOptimizedDiscoveryPath = false, string? disableArmCapacityCheckForVMInRegions = null, string? disableArmCapacityCheckForVmssInRegions = null, string? disableArmCapacityCheckForDHInRegions = null, string? disableArmCapacityCheckForCRInRegions = null, string? disableArmCapacityCheckForCSInRegions = null, string? disableArmCapacityCheckForPreflightInRegions = null, bool? disableArmCapacityCheckForVM = false, bool? disableArmCapacityCheckForVmss = false, bool? disableArmCapacityCheckForDH = false, bool? disableArmCapacityCheckForCR = false, bool? disableArmCapacityCheckForCS = false, bool? disableArmCapacityCheckForPreflight = false, bool? enableSpotPredictionEvaluationForDiscoveryPath = false, bool? enableSpotAccessListEvaluation = false, bool? enableServiceTreeAccessListEvaluation = false, bool? enableRiskReputationScoreOR = false, int? minimumRiskReputationScore = null, bool? enableRiskReputationScoreThresholdEvaluation = false, bool? enableTieredSpotAdmissionControl = false, bool? enableTieredSpotAdmissionControlForDiscoveryPath = false, bool? enableBlockNewResourceCreationEvaluation = false)
        {
            EnableCostCategoryForCustomerSegment = enableCostCategoryForCustomerSegment ?? false;
            EnableCapacityCheckDeploymentPath = enableCapacityCheckDeploymentPath ?? false;
            EnableCapacityCheckDiscoveryPath = enableCapacityCheckDiscoveryPath ?? false;
            EnableReservationDeploymentPath = enableReservationDeploymentPath ?? false;
            EnableReservationDiscoveryPath = enableReservationDiscoveryPath ?? false;
            EnableEntitlementStartDate = enableEntitlementStartDate ?? false;
            ReservationDiscoveryClientAppIds = reservationDiscoveryClientAppIds;
            EnableAZDeploymentPath = enableAzDeploymentPath ?? false;
            EnableAZDiscoveryPath = enableAzDiscoveryPath ?? false;
            EnableSpotDeploymentPath = enableSpotDeploymentPath ?? false;
            EnableSpotDiscoveryPath = enableSpotDiscoveryPath ?? false;
            SpotOfferCategories = spotOfferCategories;
            SpotOfferCategoryEnforcementEnabled = spotOfferCategoryEnforcementEnabled ?? false;
            EnableModernCommerce = enableModernCommerce ?? false;
            EnableCASv2Evaluator = enableCASv2Evaluator ?? false;
            EnableBlocklistEvaluation = enableBlocklistEvaluation ?? false;
            EnableCustomerLevelAccesslist = enableCustomerLevelAccesslist ?? false;
            EnableUsageBasedOR = enableUsageBasedOR ?? false;
            UsageBasedORsBlockByDefault = usageBasedORsBlockByDefault ?? false;
            EnableDedicatedHost = enableDedicatedHost ?? false;
            EnableCloudServicesMultiSkuEvaluation = enableCloudServicesMultiSkuEvaluation ?? false;
            EnablePostRequestEvaluation = enablePostRequestEvaluation ?? false;
            EnableSpotPredictionEvaluation = enableSpotPredictionEvaluation ?? false;
            EnableSpotPredictionAllocableVMsEvaluation = enableSpotPredictionAllocableVMsEvaluation ?? false;
            MinimumSpotPredictionSurvivabilityRate = minimumSpotPredictionSurvivabilityRate ?? 10;
            NumberOfHoursSpotPredictionSurvivabilityRateExpires = numberOfHoursSpotPredictionSurvivabilityRateExpires ?? 24;
            EnableCapacityReservationDeploymentPath = enableCapacityReservationDeploymentPath ?? false;
            EnableCapacityReservationDiscoveryPath = enableCapacityReservationDiscoveryPath ?? false;
            EnableVmssPostRequestEvaluation = enableVmssPostRequestEvaluation ?? false;
            EnableOptimizedDiscoveryPath = enableOptimizedDiscoveryPath ?? false;
            DisableArmCapacityCheckForVMInRegions = disableArmCapacityCheckForVMInRegions;
            DisableArmCapacityCheckForVmssInRegions = disableArmCapacityCheckForVmssInRegions;
            DisableArmCapacityCheckForDHInRegions = disableArmCapacityCheckForDHInRegions;
            DisableArmCapacityCheckForCRInRegions = disableArmCapacityCheckForCRInRegions;
            DisableArmCapacityCheckForCSInRegions = disableArmCapacityCheckForCSInRegions;
            DisableArmCapacityCheckForPreflightInRegions = disableArmCapacityCheckForPreflightInRegions;
            DisableArmCapacityCheckForVM = disableArmCapacityCheckForVM ?? false;
            DisableArmCapacityCheckForVmss = disableArmCapacityCheckForVmss ?? false;
            DisableArmCapacityCheckForDH = disableArmCapacityCheckForDH ?? false;
            DisableArmCapacityCheckForCR = disableArmCapacityCheckForCR ?? false;
            DisableArmCapacityCheckForCS = disableArmCapacityCheckForCS ?? false;
            DisableArmCapacityCheckForPreflight = disableArmCapacityCheckForPreflight ?? false;
            EnableSpotPredictionEvaluationForDiscoveryPath = enableSpotPredictionEvaluationForDiscoveryPath ?? false;
            EnableSpotAccessListEvaluation = enableSpotAccessListEvaluation ?? false;
            EnableServiceTreeAccessListEvaluation = enableServiceTreeAccessListEvaluation ?? false;
            EnableRiskReputationScoreOR = enableRiskReputationScoreOR ?? false;
            MinimumRiskReputationScore = minimumRiskReputationScore;
            EnableRiskReputationScoreThresholdEvaluation = enableRiskReputationScoreThresholdEvaluation ?? false;
            EnableTieredSpotAdmissionControl = enableTieredSpotAdmissionControl ?? false;
            EnableTieredSpotAdmissionControlForDiscoveryPath = enableTieredSpotAdmissionControlForDiscoveryPath ?? false;
            EnableBlockNewResourceCreationEvaluation = enableBlockNewResourceCreationEvaluation ?? false;
        }

        public static IEnumerable<PropertyInfo> GetPublicProperties()
        {
            return typeof(CapacityConfigurationSettings)!.GetProperties().ToList();
        }

        public override string ToString()
        {
            return string.Join(",", $"EnableCostCategoryForCustomerSegment: {EnableCostCategoryForCustomerSegment}", $"EnableCapacityCheckDeploymentPath: {EnableCapacityCheckDeploymentPath}", $"EnableCapacityCheckDiscoveryPath: {EnableCapacityCheckDiscoveryPath}", $"EnableReservationDeploymentPath: {EnableReservationDeploymentPath}", $"EnableReservationDiscoveryPath: {EnableReservationDiscoveryPath}", $"EnableEntitlementStartDate: {EnableEntitlementStartDate}", "ReservationDiscoveryClientAppIds: " + ReservationDiscoveryClientAppIds, $"EnableAZDeploymentPath: {EnableAZDeploymentPath}", $"EnableAZDiscoveryPath: {EnableAZDiscoveryPath}", $"EnableSpotDeploymentPath: {EnableSpotDeploymentPath}", $"EnableSpotDiscoveryPath: {EnableSpotDiscoveryPath}", "SpotOfferCategories: " + SpotOfferCategories, $"SpotOfferCategoryEnforcementEnabled: {SpotOfferCategoryEnforcementEnabled}", $"EnableModernCommerce: {EnableModernCommerce}", $"EnableCASv2Evaluator: {EnableCASv2Evaluator}", $"EnableBlocklistEvaluation: {EnableBlocklistEvaluation}", $"EnableCustomerLevelAccesslist: {EnableCustomerLevelAccesslist}", $"EnableUsageBasedOR: {EnableUsageBasedOR}", $"EnableDedicatedHost: {EnableDedicatedHost}", $"EnableCloudServicesMultiSkuEvaluation: {EnableCloudServicesMultiSkuEvaluation}", $"EnablePostRequestEvaluation: {EnablePostRequestEvaluation}", $"EnableSpotPredictionEvaluation: {EnableSpotPredictionEvaluation}", $"EnableSpotPredictionAllocableVMsEvaluation: {EnableSpotPredictionAllocableVMsEvaluation}", $"MinimumSpotPredictionSurvivabilityRate: {MinimumSpotPredictionSurvivabilityRate}", $"NumberOfHoursSpotPredictionSurvivabilityRateExpires: {NumberOfHoursSpotPredictionSurvivabilityRateExpires}", $"EnableCapacityReservationDeploymentPath: {EnableCapacityReservationDeploymentPath}", $"EnableCapacityReservationDiscoveryPath: {EnableCapacityReservationDiscoveryPath}", $"EnableVmssPostRequestEvaluation: {EnableVmssPostRequestEvaluation}", $"EnableOptimizedDiscoveryPath: {EnableOptimizedDiscoveryPath}", "DisableArmCapacityCheckForVMInRegions: " + DisableArmCapacityCheckForVMInRegions, "DisableArmCapacityCheckForVmssInRegions: " + DisableArmCapacityCheckForVmssInRegions, "DisableArmCapacityCheckForDHInRegions: " + DisableArmCapacityCheckForDHInRegions, "DisableArmCapacityCheckForCRInRegions: " + DisableArmCapacityCheckForCRInRegions, "DisableArmCapacityCheckForCSInRegions: " + DisableArmCapacityCheckForCSInRegions, "DisableArmCapacityCheckForPreflightInRegions: " + DisableArmCapacityCheckForPreflightInRegions, $"DisableArmCapacityCheckForVM: {DisableArmCapacityCheckForVM}", $"DisableArmCapacityCheckForVmss: {DisableArmCapacityCheckForVmss}", $"DisableArmCapacityCheckForDH: {DisableArmCapacityCheckForDH}", $"DisableArmCapacityCheckForCR: {DisableArmCapacityCheckForCR}", $"DisableArmCapacityCheckForCS: {DisableArmCapacityCheckForCS}", $"DisableArmCapacityCheckForPreflight: {DisableArmCapacityCheckForPreflight}", $"EnableSpotPredictionEvaluationForDiscoveryPath: {EnableSpotPredictionEvaluationForDiscoveryPath}", $"EnableSpotAccessListEvaluation: {EnableSpotAccessListEvaluation}", $"EnableServiceTreeAccessListEvaluation: {EnableServiceTreeAccessListEvaluation}", $"EnableRiskReputationScoreOR: {EnableRiskReputationScoreOR}", $"MinimumRiskReputationScore: {MinimumRiskReputationScore}", $"EnableRiskReputationScoreThresholdEvaluation: {EnableRiskReputationScoreThresholdEvaluation}", $"EnableTieredSpotAdmissionControl: {EnableTieredSpotAdmissionControl}", $"EnableTieredSpotAdmissionControlForDiscoveryPath: {EnableTieredSpotAdmissionControlForDiscoveryPath}", $"EnableBlockNewResourceCreationEvaluation: {EnableBlockNewResourceCreationEvaluation}");
        }
    }
}
