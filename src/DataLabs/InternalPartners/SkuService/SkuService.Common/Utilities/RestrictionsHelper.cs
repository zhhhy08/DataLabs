namespace SkuService.Common.Utilities
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Common.Storage;
    using SkuService.Common.Models.V1;
    using System.Collections.Generic;
    using System.Linq;
    using static SkuService.Common.Models.Enums;

    internal static class RestrictionsHelper
    {
        public static SkuLocationInfo[] GetCapacityRestrictionsForSku(
            string resourceType,
            SkuSetting skuRegistration,
            InsensitiveHashSet capacityEnabledResourceTypes,
            InsensitiveDictionary<SkuLocationInfo[]> capacityRestrictionsByName)
        {
            return capacityEnabledResourceTypes.Contains(resourceType)
                ? capacityRestrictionsByName.GetValueOrDefault(StorageUtility.NormalizeSkuName(skuRegistration.Name)).CoalesceEnumerable().ToArray()
                : Array.Empty<SkuLocationInfo>();
        }

        public static SkuRestriction GetSkuLocationRestriction(string[] locations, SkuRestrictionReasonCode code, bool isZoneSupported)
        {
            return new SkuRestriction
            {
                Type = SkuRestrictionType.Location,
                Values = locations,
                RestrictionInfo = isZoneSupported ? new RestrictionInfo { Locations = locations } : null!,
                ReasonCode = code
            };
        }

        public static SkuRestriction[] GetSkuCapacityRestrictions(
            string[] skuLocations,
            SkuLocationInfo[] capacityRestrictedLocationInfo,
            Dictionary<string, InsensitiveDictionary<string>> subscriptionAvailabilityZoneLookup,
            bool isZoneSupported)
        {
            var skuLocationLookup = skuLocations
                .ToDictionary(
                    keySelector: location => location,
                    elementSelector: location => location,
                    comparer: LocationStringEqualityComparer.Instance);

            var locationRestrictions = GetCapacityLocationsRestriction(
                skuLocationLookup: skuLocationLookup,
                restrictedLocationInfo: capacityRestrictedLocationInfo,
                isZoneSupported: isZoneSupported);

            var zoneRestrictions = isZoneSupported
                ? capacityRestrictedLocationInfo
                    .Where(restriction =>
                        restriction.IsOndemandRestricted
                        && skuLocationLookup.ContainsKey(restriction.Location)
                        && restriction.Zones.CoalesceEnumerable().Any())
                    .GroupBy(
                        keySelector: restriction => restriction.Location,
                        elementSelector: restriction => restriction.Zones,
                        comparer: LocationStringEqualityComparer.Instance)
                    .Select(restrictionGroup => GetCapacityZoneRestriction(
                        location: skuLocationLookup[restrictionGroup.Key],
                        zones: restrictionGroup.SelectManyArray(zone => zone),
                        subscriptionAvailabilityZoneLookup: subscriptionAvailabilityZoneLookup))
                    .Where(skuRestriction => skuRestriction != null)
                    .ToArray()
                : new SkuRestriction[0];

            return locationRestrictions.CoalesceAsArray().ConcatArray(zoneRestrictions);
        }

        /// <summary>
        /// Iterate over the currently supported restriction types (offer terms) to apply those restrictions on the SKU capabilities.
        /// This method will return the updated capabilities with restrictions applied.
        /// </summary>
        /// <param name="capabilities">The original SKU capabilities.</param>
        /// <param name="locations">The locations.</param>
        /// <param name="capacityRestrictedLocationInfo">The capacity restricitions information.</param>
        /// <returns>The updated capabilities with the restrictions applied.</returns>
        public static IDictionary<string, string> GetCapabilitiesWithApplicableRestrictions(
            IDictionary<string, string> capabilities,
            string[] locations,
            SkuLocationInfo[] capacityRestrictedLocationInfo)
        {
            if(capabilities == null)
            {
                return capabilities!;
            }

            // The currently supported restrictions that affect capabilities values.
            var offerTerms = new OfferTermType[] { OfferTermType.Spot, OfferTermType.CapacityReservation };
            foreach (var offerTerm in offerTerms)
            {
                capabilities = GetCapabilitiesWithApplicableRestriction(
                    offerTerm: offerTerm,
                    capabilities: capabilities,
                    locations: locations,
                    capacityRestrictedLocationInfo: capacityRestrictedLocationInfo);
            }

            return capabilities;
        }

        /// <summary>
        /// Restricts the SKU by changing the applicable capability value to "False", if:
        /// The SKU contains such capability, and it is set to "True",
        /// and restrictions contain any restriction for the offer term.
        /// </summary>
        /// <param name="offerTerm">The offer term to look restrictions for.</param>
        /// <param name="capabilities">Capabilities array to enforce spot restrictions on</param>
        /// <param name="locations">The SKU locations.</param>
        /// <param name="capacityRestrictedLocationInfo">The capacity restricted location info.</param>
        private static IDictionary<string, string> GetCapabilitiesWithApplicableRestriction(
            OfferTermType offerTerm,
            IDictionary<string, string> capabilities,
            string[] locations,
            SkuLocationInfo[] capacityRestrictedLocationInfo)
        {
            string capabilityName;
            switch (offerTerm)
            {
                case OfferTermType.Spot:
                    capabilityName = Constants.LowPriorityCapable;
                    break;
                case OfferTermType.CapacityReservation:
                    capabilityName = Constants.CapacityReservationSupported;
                    break;
                default:
                    // Unsupported offer term
                    return capabilities;
            }

            if (!capabilities.ContainsKey(capabilityName))
            {
                // Capability is not found.
                return capabilities;
            }

            var capabilityValue = capabilities[capabilityName];
            if (!capabilityValue.EqualsInsensitively(true.ToString()))
            {
                // Capability is already "False".
                return capabilities;
            }

            if (!HasRestriction(
                offerTerm: offerTerm,
                locations: locations,
                capacityRestrictedLocationInfo: capacityRestrictedLocationInfo))
            {
                // No Restrictions for offer term found.
                return capabilities;
            }

            // We need to set the capability value to false, we clone the capabilities array and then duplicate that capability
            // to not modify the source data
            var clonedCapabilities = capabilities.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            clonedCapabilities[capabilityName] = false.ToString();

            return clonedCapabilities;
        }

        /// <summary>
        /// Gets the SKU locations restriction.
        /// </summary>
        /// <param name="skuLocationLookup">The SKU location lookup with both location as both key and value.</param>
        /// <param name="restrictedLocationInfo">The restricted location info.</param>
        /// <param name="isZoneSupported">Whether zone is supported for the SKU restriction.</param>
        private static SkuRestriction GetCapacityLocationsRestriction(Dictionary<string, string> skuLocationLookup, SkuLocationInfo[] restrictedLocationInfo, bool isZoneSupported)
        {
            var skuRestrictedLocations = restrictedLocationInfo
                .Where(restriction =>
                    restriction.IsOndemandRestricted
                    && skuLocationLookup.ContainsKey(restriction.Location)
                    && !restriction.Zones.CoalesceEnumerable().Any())
                .SelectArray(locationInfo => skuLocationLookup[locationInfo.Location]);

            if (skuRestrictedLocations.Any())
            {
                return GetSkuLocationRestriction(locations: skuRestrictedLocations, code: SkuRestrictionReasonCode.NotAvailableForSubscription, isZoneSupported: isZoneSupported);
            }

            return null!;
        }

        /// <summary>
        /// Gets the SKU zone restriction for a location.
        /// </summary>
        /// <param name="location">The SKU location.</param>
        /// <param name="zones">The restricted physical zones.</param>
        /// <param name="subscriptionAvailabilityZoneLookup">The lookup from location to physical zone and to logical zone.</param>
        private static SkuRestriction GetCapacityZoneRestriction(string location, string[] zones, Dictionary<string, InsensitiveDictionary<string>> subscriptionAvailabilityZoneLookup)
        {
            var restrictedLogicalZones = zones
                .Where(physicalZone => subscriptionAvailabilityZoneLookup.ContainsKey(location) && subscriptionAvailabilityZoneLookup[location].ContainsKey(physicalZone))
                .SelectArray(physicalZone => subscriptionAvailabilityZoneLookup[location][physicalZone]);

            return restrictedLogicalZones.Any()
                ? new SkuRestriction
                {
                    Type = SkuRestrictionType.Zone,
                    Values = location.AsArray(),
                    RestrictionInfo = new RestrictionInfo
                    {
                        Locations = location.AsArray(),
                        Zones = restrictedLogicalZones,
                    },
                    ReasonCode = SkuRestrictionReasonCode.NotAvailableForSubscription
                }
                : null!;
        }

        /// <summary>
        /// Checks wheter the restrictions contain any restriction for the specified offer term.
        /// </summary>
        /// <param name="offerTerm">The offer term to evaluate against.</param>
        /// <param name="locations">The SKU locations.</param>
        /// <param name="capacityRestrictedLocationInfo">The capacity restricted location info.</param>
        /// <returns>True if a restriction for the locations is present, false otherwise.</returns>
        private static bool HasRestriction(OfferTermType offerTerm, string[] locations, SkuLocationInfo[] capacityRestrictedLocationInfo)
        {
            var skuLocationLookup = locations.ToDictionary(
                keySelector: location => location,
                elementSelector: location => location,
                comparer: LocationStringEqualityComparer.Instance);

            var restrictionsForLocation = capacityRestrictedLocationInfo
                .Where(restriction => skuLocationLookup.ContainsKey(restriction.Location));

            switch (offerTerm)
            {
                case OfferTermType.Spot:
                    restrictionsForLocation = restrictionsForLocation.Where(restriction => restriction.IsSpotRestricted);
                    break;
                case OfferTermType.CapacityReservation:
                    restrictionsForLocation = restrictionsForLocation.Where(restriction => restriction.IsCapacityReservationRestricted);
                    break;
                default:
                    // Unsupported offer term.
                    // Return no restriction.
                    return false;
            }

            return restrictionsForLocation.Any();
        }
    }
}
