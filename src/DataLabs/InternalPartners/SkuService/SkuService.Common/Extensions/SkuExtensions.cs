namespace SkuService.Common.Extensions
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Microsoft.WindowsAzure.ResourceStack.Common.Core.Definitions.Resources;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Common.Storage;
    using SkuService.Common.Models.V1;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static SkuService.Common.Models.Enums;

    internal static class SkuExtensions
    {
        /// <summary>
        /// Gets the SKU location info.
        /// </summary>
        /// <param name="subscriptionAvailabilityZoneLookup">The lookup from location to physical zone and to logical zone.</param>
        /// <param name="locationsWithNoZoneMapping">The locations eligible for zone mapping removal</param>
        /// <param name="subscriptionExtendedLocations">The extended locations visible to the subscription</param>
        /// <param name="eventSource">The event source.</param>
        public static SkuLocationAndZones[] GetSkuLocationInfo(
            this SkuSetting skuSetting,
            Dictionary<string, InsensitiveDictionary<string>> subscriptionAvailabilityZoneLookup,
            InsensitiveHashSet locationsWithNoZoneMapping,
            ExtendedLocation[] subscriptionExtendedLocations)
        {
            return skuSetting
                .GetLocationInfo()!
                .Select(locationInfo => new SkuLocationAndZones
                {
                    Location = locationInfo.Location,
                    Zones = GetEffectiveZonesMappings(locationInfo, subscriptionAvailabilityZoneLookup, locationsWithNoZoneMapping),
                    ZoneDetails = GetEffectiveZonesDetails(locationInfo, subscriptionAvailabilityZoneLookup, locationsWithNoZoneMapping),
                    Type = locationInfo.Type.HasValue ? locationInfo.Type.Value.ToLocationType() : (LocationType?)null,
                    ExtendedLocations = GetEffectiveExtendedLocations(locationInfo, subscriptionExtendedLocations)
                })
                .Where(locationAndZone => !locationAndZone.Type.HasValue || locationAndZone.ExtendedLocations.CoalesceEnumerable().Any())
                .ToArray();
        }

        /// <summary>
        /// Returns list of extended locations from Sku that are available to subscription
        /// </summary>
        /// <param name="locationInfo">the sku location information</param>
        /// <param name="subscriptionExtendedLocations">the extended locations available to the subscription</param>
        public static string[] GetEffectiveExtendedLocations(SkuLocationInfo locationInfo, ExtendedLocation[] subscriptionExtendedLocations)
        {
            if (locationInfo.ExtendedLocations == null)
            {
                return null!;
            }

            return locationInfo.ExtendedLocations
                .Where(extendedLocationName => subscriptionExtendedLocations
                    .Any(extendedLocation => ExtendedLocationEquals(
                        extendedLocation1Type: extendedLocation.Type,
                        extendedLocation1Name: extendedLocation.Name,
                        extendedLocation2Type: locationInfo.Type!.Value,
                        extendedLocation2Name: extendedLocationName)))
                .ToArray();
        }

        /// <summary>
        /// Determines whether two extended locations are equal.
        /// </summary>
        /// <param name="extendedLocation1Type">The first extended location type.</param>
        /// <param name="extendedLocation1Name">The first extended location name.</param>
        /// <param name="extendedLocation2Type">The second extended location type.</param>
        /// <param name="extendedLocation2Name">The second extended location name.</param>
        public static bool ExtendedLocationEquals(
            ExtendedLocationType extendedLocation1Type,
            string extendedLocation1Name,
            ExtendedLocationType extendedLocation2Type,
            string extendedLocation2Name)
        {
            if (extendedLocation1Type != extendedLocation2Type)
            {
                return false;
            }

            switch (extendedLocation1Type)
            {
                case ExtendedLocationType.EdgeZone:
                    return EdgeZoneEquals(extendedLocation1Name, extendedLocation2Name);
                case ExtendedLocationType.CustomLocation:
                    return CustomLocationEquals(extendedLocation1Name, extendedLocation2Name);
                default:
                    return extendedLocation1Name.EqualsOrdinalInsensitively(extendedLocation2Name);
            }
        }

        /// <summary>
        /// Determines whether two edge zone names are equal.
        /// </summary>
        /// <param name="name1">The first edge zone name.</param>
        /// <param name="name2">The second edge zone name.</param>
        public static bool EdgeZoneEquals(string name1, string name2)
        {
            return StorageUtility.LocationsEquals(name1, name2);
        }

        /// <summary>
        /// Determines whether two custom location names are equal.
        /// </summary>
        /// <param name="customlocationId1">The first custom location name.</param>
        /// <param name="customlocationId2">The second custom location name.</param>
        public static bool CustomLocationEquals(string customlocationId1, string customlocationId2)
        {
            return customlocationId1.EqualsOrdinalInsensitively(customlocationId2);
        }

        /// <summary>
        /// Returns effective zone mappings based FD configuration (feature flags for locations)
        /// </summary>
        /// <param name="locationInfo">Location info with zone mappings to filter</param>
        /// <param name="subscriptionAvailabilityZoneLookup">The lookup from location to physical zone and to logical zone.</param>
        /// <param name="locationsWithNoZoneMapping">The locations eligible for zone mapping removal</param>
        private static string[] GetEffectiveZonesMappings(
            SkuLocationInfo locationInfo,
            Dictionary<string, InsensitiveDictionary<string>> subscriptionAvailabilityZoneLookup,
            InsensitiveHashSet locationsWithNoZoneMapping)
        {
            if (locationsWithNoZoneMapping.Contains(StorageUtility.NormalizeLocationForStorage(locationInfo.Location)))
            {
                return EmptyArray<string>.Instance;
            }
            else
            {
                return locationInfo.Zones.CoalesceEnumerable()
                    .Where(physicalZone => subscriptionAvailabilityZoneLookup.ContainsKey(locationInfo.Location)
                    && subscriptionAvailabilityZoneLookup[locationInfo.Location].ContainsKey(physicalZone))
                    .SelectArray(physicalZone => subscriptionAvailabilityZoneLookup[locationInfo.Location][physicalZone]);
            }
        }

        /// <summary>
        /// Returns valid zones details for SKU.
        /// </summary>
        /// <param name="locationInfo">Location info with zone mappings to filter</param>
        /// <param name="subscriptionAvailabilityZoneLookup">The lookup from location to physical zone to logical zone.</param>
        /// <param name="locationsWithNoZoneMapping">The locations eligible for zone mapping removal</param>
        /// <param name="eventSource">The event source.</param>
        private static SkuZoneDetail[] GetEffectiveZonesDetails(
            SkuLocationInfo locationInfo,
            Dictionary<string, InsensitiveDictionary<string>> subscriptionAvailabilityZoneLookup,
            InsensitiveHashSet locationsWithNoZoneMapping)
        {
            if (locationsWithNoZoneMapping.Contains(StorageUtility.NormalizeLocationForStorage(locationInfo.Location)))
            {
                return EmptyArray<SkuZoneDetail>.Instance;
            }

            // Check if the zone details has valid zone present
            if (locationInfo.ZoneDetails.IsNullOrEmpty() || !AreZoneDetailsValid(locationInfo, subscriptionAvailabilityZoneLookup))
            {
                return EmptyArray<SkuZoneDetail>.Instance;
            }

            return locationInfo.ZoneDetails
                .SelectArray(zoneDetail => new SkuZoneDetail
                {
                    Zones = GetLogicalZonesMappings(zoneDetail.Zones!, subscriptionAvailabilityZoneLookup[locationInfo.Location]),
                    Capabilities = zoneDetail.Capabilities
                });
        }

        /// <summary>
        /// check if the zones in zone details are valid
        /// </summary>
        /// <param name="locationInfo">Location info with zone mappings to filter</param>
        /// <param name="subscriptionAvailabilityZoneLookup">The lookup from location to physical zone and to logical zone.</param>
        /// <param name="eventSource">The event source.</param>
        private static bool AreZoneDetailsValid(
            SkuLocationInfo locationInfo,
            Dictionary<string, InsensitiveDictionary<string>> subscriptionAvailabilityZoneLookup)
        {
            var invalidPhysicalZones = locationInfo.ZoneDetails!.SelectMany(ZoneDetail => ZoneDetail.Zones!)
                .Where(physicalZone => !subscriptionAvailabilityZoneLookup.ContainsKey(locationInfo.Location)
                || !subscriptionAvailabilityZoneLookup[locationInfo.Location].ContainsKey(physicalZone));

            if (invalidPhysicalZones.Any())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns logical zone mappings for zone details
        /// </summary>
        /// <param name="physicalZones">Physical zones </param>
        /// <param name="physicalAvailabilityZoneLookup">The lookup from physical zone to logical zone.</param>
        private static string[] GetLogicalZonesMappings(
            string[] physicalZones,
            InsensitiveDictionary<string> physicalAvailabilityZoneLookup)
        {
            return physicalZones.CoalesceEnumerable()
                .SelectArray(physicalZone => physicalAvailabilityZoneLookup[physicalZone]);
        }

        /// <summary>
        /// Converts a <see cref="ExtendedLocationType"/> to a <see cref="LocationType"/>
        /// </summary>
        /// <param name="extendedLocationType">the extended location type</param>
        private static LocationType ToLocationType(this ExtendedLocationType extendedLocationType)
        {
            switch (extendedLocationType)
            {
                case ExtendedLocationType.EdgeZone:
                    return LocationType.EdgeZone;
                default:
                    throw new ArgumentException($"{typeof(ExtendedLocationType).Name} value [{extendedLocationType}] does not have a mapping to {typeof(LocationType)}.");
            }
        }
    }
}
