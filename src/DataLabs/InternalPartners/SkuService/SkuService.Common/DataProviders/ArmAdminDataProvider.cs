namespace SkuService.Common.DataProviders
{
    using Microsoft.WindowsAzure.Arm.Infra.Configuration.Specs;
    using Microsoft.WindowsAzure.Arm.Infra.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using SkuService.Common.Fabricators;
    using StackExchange.Redis;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Common.Storage;
    using SkuService.Common.Models.V1.RPManifest;
    using SkuService.Common.Models.V1;
    using SkuService.Common.Utilities;
    using Azure.Deployments.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Newtonsoft.Json;
    using Microsoft.WindowsAzure.ResourceStack.Common.Core.Definitions.Resources;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using SkuService.Common.Telemetry;

    internal class ArmAdminDataProvider : IArmAdminDataProvider
    {
        private readonly IResourceProxyClient resourceProxyClient;
        private readonly string serviceName;
        const string AllowedProviderRegistrationLocationsKey = "allowedProviderRegistrationLocations";

        private static readonly ActivityMonitorFactory GetAndUpdateArmAdminConfigsAsyncMonitorFactory = new("ArmAdminDataProvider.GetAndUpdateArmAdminConfigsAsync");

        /// <summary>
        /// Microsoft.WindowsAzure.ResourceStack.Frontdoor.CombinedZoneRules.Enabled
        /// </summary>
        private bool combinedZoneRulesEnabled = true;

        /// <summary>
        /// microsoft.WindowsAzure.ResourceStack.Frontdoor.AllowedAvailabilityZones
        /// </summary>
        private Dictionary<string, InsensitiveDictionary<string>> AllowedAvailabilityZoneMappings = new();

        /// <summary>
        /// Allowed provider registration locations with feature flag
        /// </summary>
        private ProviderRegistrationLocationElement[] AllowedProviderRegistrationLocationsWithFeatureFlag = default!;

        /// <summary>
        /// Gets the ARM zones under combined zone rules in ARM configuration.
        /// </summary>
        private Dictionary<string, ARMZonesUnderCombinedRules> ARMZones = new();

        /// <summary>
        /// Allowed provider registration locations with global location.
        /// </summary>
        private string[] AllowedProviderRegistrationlocationsWithGlobalLocation = default!;

        /// <summary>
        /// Microsoft.WindowsAzure.ResourceStack.Frontdoor.AllowedProviderRegistrationLocations
        /// </summary>
        private string[] AllowedProviderRegistrationlocations = default!;

        /// <summary>
        /// Microsoft.WindowsAzure.ResourceStack.Frontdoor.FeatureFlagsForAvailabilityZones
        /// </summary>
        private Dictionary<string, string[]> FeatureFlagsToLocationMappings = new();

        public ArmAdminDataProvider(IResourceProxyClient resourceProxyClient, ISkuServiceProvider skuServiceProvider)
        {
            this.resourceProxyClient = resourceProxyClient;
            this.serviceName = skuServiceProvider.GetServiceName();
        }

        /// <summary>
        /// Gets the extended locations used in development/testing environment
        /// </summary>
        public static ExtendedLocation[] DevelopmentExtendedLocations { get; } = new ExtendedLocation[]
        {
            new() { Name = "losangeles", DisplayName = "Los Angeles", Type = ExtendedLocationType.EdgeZone, Region = "devfabric" },
            new() { Name = "losangeles2", DisplayName = "Los Angeles 2", Type = ExtendedLocationType.EdgeZone, Region = "devfabric" },
            new() { Name = "sandiego", DisplayName = "San Diego", Type = ExtendedLocationType.EdgeZone, Region = "devfabrictwo" },
            new() { Name = "restrictedzone", DisplayName = "Restricted Zone", Type = ExtendedLocationType.EdgeZone, Region = "devfabric", RequiredFeatures = new string[] { "Microsoft.Resources/RestrictedZone" } }
        };

        public IDictionary<string, InsensitiveDictionary<string>> GetAllowedAvailabilityZoneMappings => AllowedAvailabilityZoneMappings;

        public IDictionary<string, string[]> GetFeatureFlagsToLocationMappings => FeatureFlagsToLocationMappings;

        public ProviderRegistrationLocationElement[] GetAllowedProviderRegistrationLocationsWithFeatureFlag => AllowedProviderRegistrationLocationsWithFeatureFlag;

        public static readonly ExtendedLocation[] ExtendedLocations = new ExtendedLocation[]
        {
            new() { Name = "southcentralusstgmockedge", DisplayName = "South Central US STG Mock Edge", Type = ExtendedLocationType.EdgeZone, Region = "southcentralusstg", GeographyGroup = GeoGroupNamesLocalized.US, RequiredFeatures = new string[] { "Microsoft.EdgeZones/SouthCentralUSSTGMockEdge", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "onefleetedge1mockedge", DisplayName = "Onefleet Edge 1 Mock Edge", Type = ExtendedLocationType.EdgeZone, Region = "eastus2euap", GeographyGroup = GeoGroupNamesLocalized.US,  RequiredFeatures = new string[] { "Microsoft.EdgeZones/onefleetedge1mockedge", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "microsoftrrdclab3", DisplayName = "Microsoft RRDC Lab 3", Type = ExtendedLocationType.EdgeZone, Region = "eastus2euap", Latitude = "47.69106", Longitude = "-122.03197", GeographyGroup = GeoGroupNamesLocalized.US,  RequiredFeatures = new string[] { "Microsoft.EdgeZones/microsoftrrdclab3", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "microsoftrrdclab4", DisplayName = "Microsoft RRDC Lab 4", Type = ExtendedLocationType.EdgeZone, Region = "eastus2euap", Latitude = "47.69106", Longitude = "-122.03197", GeographyGroup = GeoGroupNamesLocalized.US,  RequiredFeatures = new string[] { "Microsoft.EdgeZones/microsoftrrdclab4", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "microsoftdclabs1", DisplayName = "Microsoft DC Labs 1", Type = ExtendedLocationType.EdgeZone, Region = "eastus2euap", Latitude = "47.69106", Longitude = "-122.03197", GeographyGroup = GeoGroupNamesLocalized.US,  RequiredFeatures = new string[] { "Microsoft.EdgeZones/microsoftdclabs1", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "microsoftrrezm1", DisplayName = "Microsoft RR EZM 1", Type = ExtendedLocationType.EdgeZone, Region = "eastus2euap", Latitude = "47.691143", Longitude = "-122.031919", GeographyGroup = GeoGroupNamesLocalized.US,  RequiredFeatures = new string[] { "Microsoft.EdgeZones/microsoftrrezm1", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "microsoftlosangeles1", DisplayName = "Los Angeles Test", Type = ExtendedLocationType.EdgeZone, Region = "westus", Latitude = "34.058414", Longitude = "-118.235374", GeographyGroup = GeoGroupNamesLocalized.US,  RequiredFeatures = new string[] { "Microsoft.EdgeZones/AzureEdgeZoneLosAngelesA", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "microsoftvancouver1", DisplayName = "Vancouver A", Type = ExtendedLocationType.EdgeZone, Region = "canadacentral", Latitude = "49.259589", Longitude = "-123.029662", GeographyGroup = GeoGroupNamesLocalized.Canada, RequiredFeatures = new string[] { "Microsoft.EdgeZones/AzureEdgeZoneVancouverA", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "microsoftlasvegas1", DisplayName = "Las Vegas A", Type = ExtendedLocationType.EdgeZone, Region = "westus", Latitude = "36.055332", Longitude = "-115.216172", GeographyGroup = GeoGroupNamesLocalized.US,  RequiredFeatures = new string[] { "Microsoft.EdgeZones/AzureEdgeZoneLasVegasA", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "microsoftnewyork1", DisplayName = "New York A", Type = ExtendedLocationType.EdgeZone, Region = "eastus", Latitude = "40.556953", Longitude = "-74.484724", GeographyGroup = GeoGroupNamesLocalized.US,  RequiredFeatures = new string[] { "Microsoft.EdgeZones/AzureEdgeZoneNewYorkA", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "microsoftperth1", DisplayName = "Perth A", Type = ExtendedLocationType.EdgeZone, Region = "australiasoutheast", Latitude = "-31.955941", Longitude = "115.797655", GeographyGroup = GeoGroupNamesLocalized.AsiaPacific, RequiredFeatures = new string[] { "Microsoft.EdgeZones/AzureEdgeZonePerthA", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "attdallas1", DisplayName = "ATT Dallas A", Type = ExtendedLocationType.EdgeZone, Region = "southcentralus", Latitude = "33.00933", Longitude = "-96.67669", GeographyGroup = GeoGroupNamesLocalized.US,  RequiredFeatures = new string[] { "Microsoft.EdgeZones/EdgeZoneATTDallasA", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
            new() { Name = "losangeles", DisplayName = "Los Angeles", Type = ExtendedLocationType.EdgeZone, Region = "westus", Latitude = "34.058414", Longitude = "-118.23537", GeographyGroup = GeoGroupNamesLocalized.US,  RequiredFeatures = new string[] { "Microsoft.EdgeZones/losangeles", "Microsoft.EdgeZones/InternalAzureEdgeZones" } },
        };

        /// <inheritdoc/>
        public async Task GetAndUpdateArmAdminConfigsAsync(CancellationToken cancellationToken)
        {
            var monitor = GetAndUpdateArmAdminConfigsAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            var traceId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid().ToString();
            var cloudConfigRequest = new DataLabsConfigSpecsRequest(
                traceId,
                0,
                correlationId,
                Constants.ArmAdminCloudSpecsType);

            var cloudConfigResponse = resourceProxyClient.GetConfigSpecsAsync(cloudConfigRequest, cancellationToken);

            var globalConfigRequest = new DataLabsConfigSpecsRequest(
               traceId,
               0,
               correlationId,
               Constants.ArmAdminGlobalSpecsType);
            var globalConfigResponse = resourceProxyClient.GetConfigSpecsAsync(globalConfigRequest, cancellationToken);

            var regionConfigRequest = new DataLabsConfigSpecsRequest(
               traceId,
               0,
               correlationId,
               Constants.ArmAdminRegionSpecsType);

            var regionConfigResponse = resourceProxyClient.GetConfigSpecsAsync(regionConfigRequest, cancellationToken);


            // Fetch the Compute manifest on startup and cache it to avoid excessive calls to ARM Admin. Sweden central endpoint seems to have timeout issues.
            var manifestConfigRequest = new DataLabsManifestConfigRequest(traceId, 0, correlationId, "Microsoft.Compute");
            var manifestConfigResponse = resourceProxyClient.GetManifestConfigAsync(manifestConfigRequest, cancellationToken);

            await Task.WhenAll(cloudConfigResponse, globalConfigResponse, regionConfigResponse, manifestConfigResponse);

            if (cloudConfigResponse.Result.ErrorResponse != null)
            {
                SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, Constants.ArmAdminCloudSpecsType, Constants.ArmAdminCloudSpecsType);
                var ex = new Exception($"Failed to get cloud config from ARM Admin. Error: {cloudConfigResponse.Result.ErrorResponse.ErrorDescription}");
                monitor.OnError(ex);
                throw ex;
            }

            if (globalConfigResponse.Result.ErrorResponse != null)
            {
                SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, Constants.ArmAdminGlobalSpecsType, Constants.ArmAdminGlobalSpecsType);
                var ex = new Exception($"Failed to get global config from ARM Admin. Error: {globalConfigResponse.Result.ErrorResponse.ErrorDescription}");
                monitor.OnError(ex);
                throw ex;
            }

            if (regionConfigResponse.Result.ErrorResponse != null)
            {
                SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, Constants.ArmAdminRegionSpecsType, Constants.ArmAdminGlobalSpecsType);
                var ex = new Exception($"Failed to get region config from ARM Admin. Error: {regionConfigResponse.Result.ErrorResponse.ErrorDescription}");
                monitor.OnError(ex);
                throw ex;
            }

            var cloudConfig = JsonConvert.DeserializeObject<CloudSpec>(cloudConfigResponse.Result.SuccessAdminResponse!.Resource!);
            var regionConfig = JsonConvert.DeserializeObject<IEnumerable<RegionSpec>>(regionConfigResponse.Result.SuccessAdminResponse!.Resource!);
            var globalConfig = JsonConvert.DeserializeObject<GlobalSpec>(globalConfigResponse.Result.SuccessAdminResponse!.Resource!);

            try
            {
                var configContext = new ConfigurationContext(globalConfig, cloudConfig, regionConfig, regionConfig?.FirstOrDefault(x => x.MDSRegion.Equals(Environment.GetEnvironmentVariable(SolutionConstants.REGION), StringComparison.OrdinalIgnoreCase)), string.Empty);
                var allowedRegistrations = new AllowedProviderRegistrationLocationsFabricator().GetSettingValue(configContext, AllowedProviderRegistrationLocationsKey).Result;
                combinedZoneRulesEnabled = bool.Parse(globalConfig!.Settings["microsoft.WindowsAzure.ResourceStack.Frontdoor.CombinedZoneRules.Enabled"].Value.ToString());
                UpdateProviderRegistrations(allowedRegistrations);
                UpdateAzLocationMapping(cloudConfig!.Settings["microsoft.WindowsAzure.ResourceStack.Frontdoor.AllowedAvailabilityZones"].Value.ToString());
                ProcessZonesUnderCombinedZoneRules(cloudConfig.Settings["microsoft.WindowsAzure.ResourceStack.Frontdoor.AllowedZonesUnderCombinedZoneRules"].Value.ToString()
                    .SplitRemoveEmpty(Constants.ConfigDelimeters));
                UpdateFeatureFlagsForAz(cloudConfig.Settings["microsoft.WindowsAzure.ResourceStack.Frontdoor.FeatureFlagsForAvailabilityZones"].Value.ToString());
            }
            catch (Exception e)
            {
                monitor.OnError(e);
                throw;
            }

            monitor.OnCompleted();
        }

        private void UpdateAzLocationMapping(string azlocationMappingString)
        {
            var locationToZonesMapping = GetValidatedConfigZoneDictionary(azlocationMappingString.SplitRemoveEmpty(Constants.ConfigDelimeters));

            foreach (var mapping in locationToZonesMapping)
            {
                var invalidZoneMappings = mapping.Value
                    .Where(zoneMapping => zoneMapping.SplitRemoveEmpty('@').Length != 2)
                    .ToArray();

                if (invalidZoneMappings.Any())
                {
                    throw new InvalidOperationException(string.Format(
                        "The zones '{0}' for the location '{1}' are invalid. The supported format is: 'PZ1@1'.",
                        invalidZoneMappings.ConcatStrings(","),
                        mapping.Key));
                }

                // Validate logical zone value
                var logicalZones = mapping.Value.SelectArray(zoneMapping => zoneMapping.SplitRemoveEmpty('@')[1]);

                if (!logicalZones.All(zone => int.TryParse(zone, out int parsedZone)))
                {
                    throw new InvalidOperationException(string.Format(
                        "The zones '{0}' for the location '{1}' are invalid. The logical zone value must be an positive integer. The supported format is: 'location#PZ1@1,PZ2@2'.",
                        mapping.Value.ConcatStrings(','),
                        mapping.Key));
                }

                var parsedZones = logicalZones.SelectArray(zone => int.Parse(zone));
                if (parsedZones.Duplicates().Any() || parsedZones.Min() < 1 || parsedZones.Max() > parsedZones.Length)
                {
                    throw new InvalidOperationException(string.Format(
                        "The zones '{0}' for the location '{1}' are invalid. The logical zone values must be consecutive integers starting from 1. The supported format is: 'location#PZ1@1,PZ2@2'.",
                        mapping.Value.ConcatStrings(','),
                        mapping.Key));
                }

                // Validate physical zone value.
                var physicalZones = mapping.Value.SelectArray(zoneMapping => zoneMapping.SplitRemoveEmpty('@')[0]);
                if (physicalZones.Duplicates().Any())
                {
                    throw new InvalidOperationException(string.Format(
                        "The zones '{0}' for the location '{1}' are invalid. The physical zone values must be unique.",
                        mapping.Value.ConcatStrings(','),
                        mapping.Key));
                }
            }

            var newZoneMapping = locationToZonesMapping
                .ToDictionary(
                    keySelector: kvp => kvp.Key,
                    elementSelector: kvp => kvp.Value.ToInsensitiveDictionary(keySelector: mapping => mapping.SplitRemoveEmpty('@')[1], elementSelector: mapping => mapping.SplitRemoveEmpty('@')[0]),
                    comparer: LocationStringEqualityComparer.Instance);

            var oldZoneMapping = AllowedAvailabilityZoneMappings;
            Interlocked.CompareExchange(ref AllowedAvailabilityZoneMappings, newZoneMapping, oldZoneMapping);
        }

        private void UpdateFeatureFlagsForAz(string featureFlagsToLocationMappingString)
        {
            var featureFlagsToLocationMapping = GetValidatedFeatureFlagsForLocationsDictionary(featureFlagsToLocationMappingString.SplitRemoveEmpty(Constants.ConfigDelimeters));
            foreach (var mapping in featureFlagsToLocationMapping)
            {
                var invalidLocations = mapping.Value
                    .Where(location => location.Contains('@'))
                    .ToArray();

                if (invalidLocations.Any())
                {
                    throw new InvalidOperationException(string.Format(
                        "The locations '{0}' for the feature flag '{1}' are invalid. The character '@' is a seperator and should not be used with locations.",
                        invalidLocations.ConcatStrings(","),
                        mapping.Key));
                }

                if (mapping.Value.Duplicates().Any())
                {
                    throw new InvalidOperationException(string.Format(
                        "The locations '{0}' for the feature flag '{1}' are invalid. The location values must be unique. ",
                        mapping.Value.ConcatStrings(','),
                        mapping.Key));
                }
            }

            var oldFeatureFlagsToLocationMappings = FeatureFlagsToLocationMappings;
            Interlocked.CompareExchange(ref FeatureFlagsToLocationMappings, featureFlagsToLocationMapping, oldFeatureFlagsToLocationMappings);
        }

        /// <summary>
        /// Get validated feature to location mappings from configuration.
        /// </summary>
        /// <param name="locationConfigurations">Location configuration</param>
        private Dictionary<string, string[]> GetValidatedFeatureFlagsForLocationsDictionary(string[] locationConfigurations)
        {
            if (locationConfigurations.Length == 1 && string.IsNullOrEmpty(locationConfigurations[0].Trim()))
            {
                return new Dictionary<string, string[]>();
            }

            var invalidLocationMappingStrings = locationConfigurations
                .Where(locationMapping => locationMapping.SplitRemoveEmpty('#').Length != 2)
                .ToArray();

            if (invalidLocationMappingStrings.Any())
            {
                throw new InvalidOperationException(string.Format(
                    "The location mappings '{0}' are in an invalid format - the supported format is: 'FeatureFlag1#Location1;FeatureFlag2#Location2,Location3'.",
                    invalidLocationMappingStrings.ConcatStrings(";")));
            }

            var featureFlagToLocationsMapping = locationConfigurations
                .ToDictionary(
                    keySelector: mapping => mapping.SplitRemoveEmpty('#')[0],
                    elementSelector: mapping => mapping.SplitRemoveEmpty('#')[1].SplitRemoveEmpty(',').SelectArray(location => location.Trim()),
                    comparer: StringComparer.InvariantCultureIgnoreCase);

            Dictionary<string, OrdinalInsensitiveHashSet> zonalFeatureToLocationMap = new();
            foreach (var mapping in featureFlagToLocationsMapping)
            {
                if (!mapping.Value.CoalesceEnumerable().Any())
                {
                    throw new InvalidOperationException(string.Format(
                        "The locations for the feature flag '{0}' are empty. The supported format is: 'FeatureFlag1#Location1,Location2'.",
                        mapping.Key));
                }

                var featureSegments = mapping.Key.SplitRemoveEmpty('/');
                if (featureSegments.Count() != 2)
                {
                    throw new InvalidOperationException("The feature flag is incorrect and must be of the form 'ResourceProviderNamespace/FeatureName'");
                }

                OrdinalInsensitiveHashSet locationsWithFeatureFlagOnSingleZone = new();

                // Remove the locations from featureFlagToLocationsMapping if feature flag applies to only one of the zones and not all of the zones
                foreach (var location in mapping.Value)
                {
                    if (ARMZones != null && ARMZones.TryGetValue(location, out var zonesUnderCombinedRules) && !zonesUnderCombinedRules.RequiredFeature.IsNullOrEmpty())
                    {
                        if (zonesUnderCombinedRules.ZonesWithRequiredFeature.Length != 0 && zonesUnderCombinedRules.ZonesWithoutRequiredFeature.Length != 0)
                        {
                            locationsWithFeatureFlagOnSingleZone.Add(location);
                        }
                    }
                }

                if (!locationsWithFeatureFlagOnSingleZone.IsNullOrEmpty())
                {
                    zonalFeatureToLocationMap.Add(mapping.Key, locationsWithFeatureFlagOnSingleZone);
                }
            }

            // Remove the locations for each feature-locations pair
            if (zonalFeatureToLocationMap.Count != 0)
            {
                zonalFeatureToLocationMap.ForEach(mapping => featureFlagToLocationsMapping[mapping.Key] = featureFlagToLocationsMapping[mapping.Key].Except(mapping.Value).ToArray());
            }

            return featureFlagToLocationsMapping;
        }

        /// <summary>
        /// Get validated zone mappings from configuration.
        /// </summary>
        /// <param name="zonesConfiguration">Availability zones configuration</param>
        private Dictionary<string, string[]> GetValidatedConfigZoneDictionary(string[] zonesConfiguration)
        {
            var allowedLocations = AllowedProviderRegistrationlocations;

            var invalidZoneMappingStrings = zonesConfiguration
                .Where(zoneMapping => zoneMapping.SplitRemoveEmpty('#').Length != 2)
                .ToArray();

            if (invalidZoneMappingStrings.Any())
            {
                throw new InvalidOperationException(string.Format(
                    "The zone mappings '{0}' are in an invalid format - the supported format is: 'locationA#PZ1,PZ2;locationB#PZ1,PZ2'.",
                    invalidZoneMappingStrings.ConcatStrings(";")));
            }

            var locationToZonesMapping = zonesConfiguration
                .ToDictionary(
                    keySelector: mapping => mapping.SplitRemoveEmpty('#')[0],
                    elementSelector: mapping => mapping.SplitRemoveEmpty('#')[1].SplitRemoveEmpty(',').SelectArray(zone => zone.Trim()),
                    comparer: LocationStringEqualityComparer.Instance);

            foreach (var mapping in locationToZonesMapping)
            {
                if (allowedLocations.SingleOrDefault(location => StorageUtility.LocationsEquals(location, mapping.Key)) == null)
                {
                    throw new InvalidOperationException(string.Format(
                        "The location '{0}' is not in the 'AllowedProviderRegistrationLocations' configuration.",
                        mapping.Key));
                }

                if (!mapping.Value.CoalesceEnumerable().Any())
                {
                    throw new InvalidOperationException(string.Format(
                        "The zones for the location '{0}' are empty. The supported format is: 'location#PZ1,PZ2'.",
                        mapping.Key));
                }
            }

            return locationToZonesMapping;
        }

        private void UpdateProviderRegistrations(string locations)
        {
            var allowedLocations = locations.Split(Constants.ConfigDelimeters);

            var registrationLocationsWithFeatureFlag = new List<ProviderRegistrationLocationElement>();
            var registrationLocations = new HashSet<string>();
            var registrationLocationsWithGlobalLocation = new HashSet<string>();

            foreach (var location in allowedLocations)
            {
                // the format can be "", "location", "location@ff" or "location@ff1||ff2"
                var split = location.SplitRemoveEmpty('@');
                if (split.Length == 0)
                {
                    registrationLocationsWithGlobalLocation.Add(string.Empty);
                }
                else if (split.Length == 1)
                {
                    registrationLocations.Add(location);
                    registrationLocationsWithGlobalLocation.Add(location);
                }
                else if (split.Length == 2)
                {
                    if (split[1].Contains("||"))
                    {
                        var featureFlags = split[1].SplitRemoveEmpty("||");
                        registrationLocationsWithFeatureFlag.Add(
                            new ProviderRegistrationLocationElement
                            {
                                Location = split[0],
                                FeatureFlags = featureFlags
                            });
                    }
                    else
                    {
                        registrationLocationsWithFeatureFlag.Add(
                            new ProviderRegistrationLocationElement
                            {
                                Location = split[0],
                                FeatureFlags = new[] { split[1] }
                            });
                    }

                    registrationLocations.Add(split[0]);
                    registrationLocationsWithGlobalLocation.Add(split[0]);
                }
                else
                {
                    throw new InvalidOperationException(
                       $"The AllowedProviderRegistrationLocations has unsupported location: {location}. The format is \"location\" , \"locaiton@ff1\", \"locaiton@ff1||fff2\" ");
                }
            }

            var oldAllowedProviderRegistrationLocationsWithFeatureFlag = AllowedProviderRegistrationLocationsWithFeatureFlag;
            var oldAllowedProviderRegistrationlocations = AllowedProviderRegistrationlocations;
            var oldAllowedProviderRegistrationlocationsWithGlobalLocation = AllowedProviderRegistrationlocationsWithGlobalLocation;

            Interlocked.CompareExchange(ref AllowedProviderRegistrationLocationsWithFeatureFlag, registrationLocationsWithFeatureFlag.ToArray(), oldAllowedProviderRegistrationLocationsWithFeatureFlag);
            Interlocked.CompareExchange(ref AllowedProviderRegistrationlocations, registrationLocations.ToArray(), oldAllowedProviderRegistrationlocations);
            Interlocked.CompareExchange(ref AllowedProviderRegistrationlocationsWithGlobalLocation, registrationLocationsWithGlobalLocation.ToArray(), oldAllowedProviderRegistrationlocationsWithGlobalLocation);
        }

        /// <summary>
        /// Process zones from configuration.
        /// </summary>
        private void ProcessZonesUnderCombinedZoneRules(string[] zonesConfiguration)
        {
            var armZoneDictionary = new Dictionary<string, ARMZonesUnderCombinedRules>(LocationStringEqualityComparer.Instance);

            if (!combinedZoneRulesEnabled || !AllowedAvailabilityZoneMappings.Any())
            {
                return;
            }

            var invalidZoneStrings = zonesConfiguration
                .Where(zoneStr => zoneStr.SplitRemoveEmpty('#').Length != 2)
                .ToArray();

            if (invalidZoneStrings.Any())
            {
                throw new InvalidOperationException(
                    $"The AllowedZonesUnderCombinedZoneRules has invalid element: {invalidZoneStrings.ConcatStrings(";")} - the supported format is: 'locationA#PZ1,PZ2;locationB#PZ1@ff,PZ2@ff'.");
            }

            var locationToPhysicalZones = zonesConfiguration
                .ToDictionary(
                    keySelector: mapping => mapping.SplitRemoveEmpty('#')[0],
                    elementSelector: mapping => mapping.SplitRemoveEmpty('#')[1].SplitRemoveEmpty(',').SelectArray(zone => zone.Trim()),
                    comparer: LocationStringEqualityComparer.Instance);

            var allowedLocations = AllowedAvailabilityZoneMappings.Keys;

            foreach (string location in locationToPhysicalZones.Keys)
            {
                if (!allowedLocations.Contains(location))
                {
                    throw new InvalidOperationException(
                        $"The AllowedZonesUnderCombinedZoneRules has unsupported location: {location}.");
                }

                var allowedPhysicalZones = AllowedAvailabilityZoneMappings[location].Values;

                var zonesWithFeatureFlag = new List<string>();
                var zonesWithoutFeatureFlag = new List<string>();
                var requiredFeature = new InsensitiveHashSet();

                foreach (string value in locationToPhysicalZones[location])
                {
                    if (value.ContainsOrdinally('@'))
                    {
                        var split = value.SplitRemoveEmpty('@');
                        zonesWithFeatureFlag.Add(split[0].Trim());
                        requiredFeature.Add(split[1].Trim());
                    }
                    else
                    {
                        zonesWithoutFeatureFlag.Add(value);
                    }
                }

                if (requiredFeature.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"The AllowedZonesUnderCombinedZoneRules only allow at most one required feature at location: {location}.");
                }

                var intersectZones = zonesWithoutFeatureFlag.IntersectInsensitively(zonesWithFeatureFlag).ToArray();
                if (intersectZones.Any())
                {
                    throw new InvalidOperationException(
                       $"The AllowedZonesUnderCombinedZoneRules does not allow zone: {intersectZones.ConcatStrings(',')} both with and without required feature at location: {location}.");
                }

                var concactZones = zonesWithoutFeatureFlag.ConcatArray(zonesWithFeatureFlag).ToArray();
                var unsupportedPhysicalZones = concactZones.ExceptInsensitively(allowedPhysicalZones);

                if (unsupportedPhysicalZones.Any())
                {
                    throw new InvalidOperationException(
                        $"The AllowedZonesUnderCombinedZoneRules does not allow zones: {unsupportedPhysicalZones.ConcatStrings(',')} at location: {location}.");
                }

                armZoneDictionary[location] = requiredFeature.Any()
                    ? new ARMZonesUnderCombinedRules(zonesWithoutFeatureFlag, zonesWithFeatureFlag, requiredFeature.First())
                    : new ARMZonesUnderCombinedRules(zonesWithoutFeatureFlag, zonesWithFeatureFlag, string.Empty);

                var oldZones = ARMZones;
                Interlocked.CompareExchange(ref ARMZones, armZoneDictionary, oldZones);
            }
        }
    }
}
