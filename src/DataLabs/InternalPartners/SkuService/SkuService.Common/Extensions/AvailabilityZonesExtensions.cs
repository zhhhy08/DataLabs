namespace SkuService.Common.Extensions
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Common.Storage;
    using SkuService.Common.DataProviders;
    using SkuService.Common.Models.V1;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The availability zones extension helper.
    /// </summary>
    public static class AvailabilityZonesExtensions
    {
        /// <summary>
        /// Convert the physical zones based on physical zones mapping.
        /// </summary>
        /// <param name="availabilityZone">The availability zone for one location.</param>
        public static Dictionary<string, InsensitiveDictionary<string>> ConvertPhysicalZones(
            this Dictionary<string, List<ZoneMapping>> availabilityZones)
        {
            var adminProvider = ServiceRegistrations.ServiceProvider.GetService<IArmAdminDataProvider>();
            GuardHelper.ArgumentNotNull(adminProvider, nameof(adminProvider));
            var allowedAvailabilityZoneMappings = adminProvider.GetAllowedAvailabilityZoneMappings;
            availabilityZones.ForEach(availabilityZone => availabilityZone.Value
                   .Where(zoneMapping => allowedAvailabilityZoneMappings.ContainsKey(availabilityZone.Key)
                       && allowedAvailabilityZoneMappings[availabilityZone.Key].ContainsKey(zoneMapping!.PhysicalZone!))
                   .SelectList(zoneMapping => new ZoneMapping
                   {
                       LogicalZone = zoneMapping.LogicalZone,
                       PhysicalZone = allowedAvailabilityZoneMappings[availabilityZone.Key][zoneMapping!.PhysicalZone!]
                   }));

            var subscriptionAvailabilityZoneLookup = availabilityZones
                .ToDictionary(
                    keySelector: allowedAvailabilityZone => allowedAvailabilityZone.Key,
                    elementSelector: allowedAvailabilityZone => allowedAvailabilityZone.Value.ToInsensitiveDictionary(
                        keySelector: zoneMapping => zoneMapping.PhysicalZone,
                        elementSelector: zoneMapping => zoneMapping.LogicalZone),
                    comparer: LocationStringEqualityComparer.Instance);
            return subscriptionAvailabilityZoneLookup!;
        }
    }
}
