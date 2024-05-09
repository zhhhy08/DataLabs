namespace SkuService.Common.Fabricators
{
    using Microsoft.WindowsAzure.Arm.Infra.Configuration;
    using Microsoft.WindowsAzure.Arm.Infra.Configuration.Specs;
    using Microsoft.WindowsAzure.Arm.Infra.Configuration.Standards;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class AllowedProviderRegistrationLocationsFabricator : IDefinedSettingFabricator
    {
        public ConfigurationResult<string> GetSettingValue(ConfigurationContext context, string settingName)
        {
            var allowedRegionStates = new[]
            {
                RegionState.PreBuildout,
                RegionState.Buildout,
                RegionState.PostBuildout,
                RegionState.Live
            };

            var allowedRegions = context.Regions.Where(r => allowedRegionStates.Contains(r.State));
            var regionalValues = allowedRegions
                .Select(ProviderRegistrationLocations.GetValueIfAllowed)
                .Where(r => r != null)
                .Concat(GetVirtualRegionLocations(context))
                .Concat(context.Cloud.AdditionalProviderRegistrationLocations)
                .OrderBy(x => x);

            // Special cases - Add empty string and global for endpoints in manifests which need to be registered globally
            return $";{string.Join(";", regionalValues)};global";
        }

        private static IEnumerable<string> GetVirtualRegionLocations(ConfigurationContext context)
        {
            return context.Cloud.VirtualRegions.Select(virtualRegion =>
            {
                var policyOperator = (virtualRegion.Value.Customizations?.RequiredFeaturesPolicy ?? RequiredFeaturesPolicy.Any) == RequiredFeaturesPolicy.All ? "&&" : "||";
                var requiredFeatures = string.Join(policyOperator, virtualRegion.Value.Customizations?.RequiredFeatures ?? Enumerable.Empty<string>());

                if (string.IsNullOrEmpty(requiredFeatures))
                {
                    return virtualRegion.Key;
                }

                return $"{virtualRegion.Key}@{requiredFeatures}";
            });
        }
    }
}
