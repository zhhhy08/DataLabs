namespace SkuService.Common.Builders
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Common.Storage;
    using SkuService.Common.DataProviders;
    using SkuService.Common.Extensions;
    using SkuService.Common.Models.V1;
    using SkuService.Common.Utilities;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using static SkuService.Common.Models.Enums;

    public class SubscriptionSkuBuilder : IDataBuilder<SubscriptionSkuModel>
    {
        private readonly IRegistrationProvider registrationProvider;
        private readonly ISubscriptionProvider subscriptionProvider;
        private readonly IRestrictionsProvider restrictionsProvider;
        private static readonly ActivityMonitorFactory BuildAsyncMonitorFactory = new("SubscriptionSkuBuilder.BuildAsync");
        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionSkuBuilder"/> class.
        /// </summary>
        /// <param name="registrationProvider"></param>
        /// <param name="subscriptionProvider"></param>
        /// <param name="restrictionsProvider"></param>
        public SubscriptionSkuBuilder(IRegistrationProvider registrationProvider, ISubscriptionProvider subscriptionProvider, IRestrictionsProvider restrictionsProvider)
        {
            this.registrationProvider = registrationProvider;
            this.subscriptionProvider = subscriptionProvider;
            this.restrictionsProvider = restrictionsProvider;
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<SubscriptionSkuModel> BuildAsync(string resourceProvider, string subscriptionId, IActivity parentActivity, ChangedDatasets changedDatasets, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var monitor = BuildAsyncMonitorFactory.ToMonitor(parentActivity);
            Task<IList<SubscriptionFeatureRegistrationPropertiesModel>> subscriptionFeatureRegistrationPropertiesTask;
            Task<SubscriptionMappingsModel> subscriptionMappings;
            Task<SubscriptionInternalPropertiesModel> subscriptionInternalProperties;
            IEnumerable<GlobalSku> globalSkus;
            ResourceTypeRegistration[] resourceTypeRegistrations;
            InsensitiveDictionary<SkuLocationInfo[]> capacityRestrictions = [];
            InsensitiveHashSet locationsWithNoZoneMapping;
            InsensitiveHashSet? capacityEnabledResourceTypes;
            ExtendedLocation[] extendedLocations;

            try
            {
                monitor.OnStart();
                if (changedDatasets.SubscriptionFeatureRegistrationProperties != null)
                {
                    IList<SubscriptionFeatureRegistrationPropertiesModel> afecList = [changedDatasets.SubscriptionFeatureRegistrationProperties];
                    subscriptionFeatureRegistrationPropertiesTask = Task.FromResult(afecList);
                }
                else
                {
                    subscriptionFeatureRegistrationPropertiesTask = this.subscriptionProvider.GetSubscriptionFeatureRegistrationPropertiesAsync(subscriptionId, resourceProvider, parentActivity, cancellationToken);
                }

                subscriptionMappings = changedDatasets.SubscriptionMappings == null ? this.subscriptionProvider.GetSubscriptionMappingsAsync(subscriptionId, parentActivity, cancellationToken) : Task.FromResult(changedDatasets.SubscriptionMappings);
                subscriptionInternalProperties = changedDatasets.SubscriptionInternalProperties == null ? this.subscriptionProvider.GetSubscriptionInternalPropertiesAsync(subscriptionId, parentActivity, cancellationToken) : Task.FromResult(changedDatasets.SubscriptionInternalProperties);
                await Task.WhenAll(subscriptionMappings, subscriptionInternalProperties, subscriptionFeatureRegistrationPropertiesTask);
                var subscriptionFeatureRegistrationProperties = subscriptionFeatureRegistrationPropertiesTask.Result;

                // When partial sync for global data is received, only use those records for updates.
                globalSkus = await this.registrationProvider.GetGlobalSkuAsync(resourceProvider, subscriptionFeatureRegistrationProperties, changedDatasets.SkuSettings, cancellationToken);
                monitor.Activity.Properties[$"FilteredGlobalSkusCount for {resourceProvider}"] = globalSkus.Count();
                monitor.Activity.Properties[$"AfecCount for {resourceProvider} and {subscriptionId}"] = subscriptionFeatureRegistrationProperties.Count;
                resourceTypeRegistrations = await this.registrationProvider.FindRegistrationsForFeatureSetAsync(resourceProvider, subscriptionFeatureRegistrationProperties, parentActivity, cancellationToken);
                monitor.Activity.Properties[$"ResourceTypeRegistrations count for {resourceProvider}"] = resourceTypeRegistrations.Length;
                extendedLocations = subscriptionFeatureRegistrationProperties.GetExtendedLocations();

                // List of resource types that can be restricted by CAS such as VMs
                capacityEnabledResourceTypes = resourceTypeRegistrations
                    .Where(registration => registration.IsCapacityRuleEnabled)
                    .ToInsensitiveHashSet(registration => registration.ResourceType);

                if (capacityEnabledResourceTypes.Count > 0)
                {
                    var subscriptionRegistrations = await this.subscriptionProvider.GetSubscriptionRegistrationAsync(subscriptionId, resourceProvider, parentActivity, cancellationToken);
                    if (subscriptionRegistrations.RegistrationDate == DateTime.MaxValue.ToString())
                    {
                        monitor.Activity.Properties["SubscriptionRegistrations"] = "NotFound";
                    }

                    capacityRestrictions = await this.restrictionsProvider.GetSkuCapacityRestrictionsAsync(
                       resourceProvider,
                       subscriptionRegistrations.RegistrationDate,
                       subscriptionInternalProperties.Result,
                       subscriptionMappings.Result,
                       parentActivity,
                       changedDatasets!.CapacityRestrictionsInputModel != null,
                       cancellationToken);

                    if (capacityRestrictions.Count == 0) 
                    {
                        monitor.Activity.Properties["CapacityRestrictions"] = "NotFound";
                    }
                }
                else
                {
                    capacityRestrictions = [];
                    monitor.Activity.Properties["NoCapacityResources"] = resourceProvider;
                }

                locationsWithNoZoneMapping = subscriptionFeatureRegistrationProperties.ToLocationsWithNoZoneMapping();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }

            // Our output is a grouping of Resource Type and Location. / VirtualMachines / default / locations / eastus
            // Combine all Skus within this group into 1 record

            foreach (var group in globalSkus
                .GroupBy(sku => new { sku.ResourceType, sku.Location }))
            {
                var subscriptionSku = new SubscriptionSkuModel
                {
                    Skus = [],
                    Location = group.Key.Location!,
                    ResourceType = group.Key.ResourceType!,
                    SkuProvider = resourceProvider
                };

                var skuSettings = group.SelectMany(x => x.Skus);
                var isAvailabilityZonesEnabled = skuSettings.Any(sku => sku.IsAvailabilityZonesEnabled)
                                && resourceTypeRegistrations.Any(resourceTypeRegistration => resourceTypeRegistration.IsAvailabilityZonesEnabled);

                var subscriptionAllowedAvailabilityZones = isAvailabilityZonesEnabled ?
                    subscriptionMappings.Result.AvailabilityZoneMappings.ConvertPhysicalZones()
                    : [];

                foreach (var skuSetting in skuSettings)
                {
                    try
                    {
                        subscriptionSku.Skus.Add(
                            CreateSubscriptionSku(
                                group.Key.ResourceType,
                                skuSetting,
                                subscriptionInternalProperties.Result,
                                capacityRestrictions,
                                subscriptionAllowedAvailabilityZones,
                                locationsWithNoZoneMapping,
                                capacityEnabledResourceTypes,
                                extendedLocations));
                    }
                    catch (Exception ex)
                    {
                        monitor.OnError(ex);
                        continue;
                    }
                }

                yield return subscriptionSku;
            }

            monitor.OnCompleted();
        }

        private static SubscriptionSkuProperties CreateSubscriptionSku(
            string resourceType,
            SkuSetting skuSetting,
            SubscriptionInternalPropertiesModel subscriptionInternalProperties,
            InsensitiveDictionary<SkuLocationInfo[]> capacityRestrictions,
            Dictionary<string, InsensitiveDictionary<string>> subscriptionAllowedAvailabilityZones,
            InsensitiveHashSet locationsWithNoZoneMapping,
            InsensitiveHashSet capacityEnabledResourceTypes,
            ExtendedLocation[] extendedLocations)
        {
            var capacityRestrictedLocationInfo = RestrictionsHelper.GetCapacityRestrictionsForSku(
                resourceType,
                skuSetting,
                capacityEnabledResourceTypes,
                capacityRestrictions);

            var skuLocations = skuSetting
                .GetLocationInfo()!
                .CoalesceEnumerable()
                .Select(info => info.Location)
                .DistinctArray(comparer: LocationStringEqualityComparer.Instance);

            var restrictions = new List<SkuRestriction>();
            if (skuSetting.RequiredQuotaIds.CoalesceEnumerable().Any() &&
                (subscriptionInternalProperties.SubscriptionPolicies == null ||
                !skuSetting.RequiredQuotaIds.ContainsInsensitively(subscriptionInternalProperties.SubscriptionPolicies.QuotaId)))
            {
                restrictions.Add(RestrictionsHelper.GetSkuLocationRestriction(locations: skuLocations, code: SkuRestrictionReasonCode.QuotaId, isZoneSupported: true));
            }

            restrictions.AddRange(RestrictionsHelper.GetSkuCapacityRestrictions(
                skuLocations: skuLocations,
                capacityRestrictedLocationInfo: capacityRestrictedLocationInfo,
                subscriptionAvailabilityZoneLookup: subscriptionAllowedAvailabilityZones,
                isZoneSupported: true));

            var capabilitiesWithRestrictions = RestrictionsHelper.GetCapabilitiesWithApplicableRestrictions(
                capabilities: skuSetting.Capabilities!,
                locations: skuLocations,
                capacityRestrictedLocationInfo: capacityRestrictedLocationInfo);

            return new SubscriptionSkuProperties
            {
                Name = skuSetting.Name!,
                Tier = skuSetting.Tier!,
                Size = skuSetting.Size!,
                Family = skuSetting.Family!,
                Kind = skuSetting.Kind!,
                Locations = skuLocations,
                LocationInfo = SkuExtensions.GetSkuLocationInfo(
                        skuSetting,
                        subscriptionAllowedAvailabilityZones,
                        locationsWithNoZoneMapping,
                        extendedLocations),
                Capacity = skuSetting.Capacity!,
                Capabilities = capabilitiesWithRestrictions,
                Restrictions = restrictions.ToArray()
            };
        }
    }
}
