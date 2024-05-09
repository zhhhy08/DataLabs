namespace SkuService.Common.DataProviders
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using SkuService.Common.Extensions;
    using SkuService.Common.Models.V1;
    using SkuService.Common.Models.V1.RPManifest;
    using SkuService.Common.Telemetry;
    using SkuService.Common.Utilities;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The Registration provider class.
    /// </summary>
    internal class RegistrationProvider : IRegistrationProvider
    {
        private readonly ICacheClient cacheClient;

        private readonly IResourceProxyClient resourceProxyClient;

        private readonly IArmAdminDataProvider adminDataProvider;

        private readonly string serviceName;

        private readonly int batchSize = 1000;

        private readonly bool useMget = false;

        private static readonly ActivityMonitorFactory BuildAsyncMonitorFactory = new("RegistrationProvider.GetGlobalSkuAsync");

        private const string SkusChanged = "SkusChanged";

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistrationProvider"/> class.
        /// </summary>
        public RegistrationProvider(IResourceProxyClient resourceProxyClient, ICacheClient cacheClient, IArmAdminDataProvider armAdminDataProvider, ISkuServiceProvider skuServiceProvider)
        {
            this.cacheClient = cacheClient;
            this.resourceProxyClient = resourceProxyClient;
            this.adminDataProvider = armAdminDataProvider;
            this.serviceName = skuServiceProvider.GetServiceName();
            GuardHelper.ArgumentNotNull(this.cacheClient, nameof(this.cacheClient));
            GuardHelper.ArgumentNotNull(this.resourceProxyClient, nameof(this.resourceProxyClient));
            GuardHelper.ArgumentNotNull(this.adminDataProvider, nameof(this.adminDataProvider));
            GuardHelper.ArgumentNotNull(this.serviceName, nameof(this.serviceName));
            if (ServiceRegistrations.GetCustomConfigDictionary.ContainsKey(Constants.GlobalSkuBatchSize))
            {
                ServiceRegistrations.GetCustomConfigDictionary.TryGetValue(Constants.GlobalSkuBatchSize, out string? size);
                _ = int.TryParse(size!, out batchSize);
            }

            if (ServiceRegistrations.GetCustomConfigDictionary.ContainsKey(Constants.UseMget))
            {
                ServiceRegistrations.GetCustomConfigDictionary.TryGetValue(Constants.UseMget, out string? mGet);
                _ = bool.TryParse(mGet, out useMget);
            }
        }

        /// <inheritdoc/>
        public async Task<ResourceTypeRegistration[]> FindRegistrationsForFeatureSetAsync(string resourceProvider, IEnumerable<SubscriptionFeatureRegistrationPropertiesModel> subscriptionFeatureRegistrationProperties, IActivity activity, CancellationToken cancellationToken)
        {
            var response = await this.GetManifestDataAsync(resourceProvider, activity, cancellationToken);
            var manifest = JsonConvert.DeserializeObject<ResourceProviderManifest>(response);
            var resourceTypeGrouping = new ResourceTypeRegistrationGrouping(manifest?.ToResourceTypeRegistrations(this.adminDataProvider.GetAllowedProviderRegistrationLocationsWithFeatureFlag)!);
            return GetFilteredRegistrationsByFeatures(resourceTypeGrouping, subscriptionFeatureRegistrationProperties);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<GlobalSku>> GetGlobalSkuAsync(string resourceProvider, IEnumerable<SubscriptionFeatureRegistrationPropertiesModel> subscriptionFeatureRegistrationProperties, GlobalSku? changedSkus, CancellationToken cancellationToken)
        {
            var monitor = BuildAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            GuardHelper.ArgumentNotNull(resourceProvider, nameof(resourceProvider));
            GuardHelper.ArgumentNotNull(subscriptionFeatureRegistrationProperties, nameof(subscriptionFeatureRegistrationProperties));
            var skuRegistrations = new List<GlobalSku>();
            var registeredFeaturesLookup = subscriptionFeatureRegistrationProperties
                .ToOrdinalInsensitiveHashSet(feature => feature.FullyQualifiedName);

            if (changedSkus != null && changedSkus.Skus.Length > 0)
            {
                monitor.Activity.Properties[SkusChanged] = true;
                skuRegistrations.Add(changedSkus);
            }
            else
            {
                monitor.Activity.Properties[SkusChanged] = false;
                for (int startIdx = 0; ; startIdx += batchSize)
                {
                    try
                    {
                        var resources = await this.cacheClient.GetCollectionValuesAsync(resourceProvider, startIdx, startIdx + batchSize - 1, Constants.EventTimeBytes, useMget, cancellationToken);
                        if (resources == null || resources.Length == 0)
                        {
                            SkuSolutionMetricProvider.PartnerCacheMissedResponseMetricReport(this.serviceName, resourceProvider);
                            break;
                        }
                        GlobalSku[] skus = new GlobalSku[resources.Length];
                        Parallel.For(0, resources.Length, i =>
                        {
                            try
                            {
                                if (resources[i] == null)
                                {
                                    return;
                                }

                                var genericResource = JsonConvert.DeserializeObject<GenericResource>(resources[i]!)
                                    ?? throw new NullReferenceException("GenericResource is null");
                                skus[i] = ((JObject)genericResource.Properties).ToObject<GlobalSku>()!;
                            }
                            catch (Exception ex)
                            {
                                monitor.OnError(ex);
                            }
                        });
                        
                        skuRegistrations.AddRange(skus.Where(x => x != null));
                        if (resources.Length < batchSize)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        monitor.OnError(ex);
                        throw;
                    }
                }
            }

            if (skuRegistrations.Count == 0)
            {
                var ex = new Exception("No skus found in cache.");
                monitor.OnError(ex);
                throw ex;
            }

            monitor.Activity.Properties[$"GlobalSkusCount for {resourceProvider}"] = skuRegistrations.Count;
            monitor.OnCompleted();

            return skuRegistrations
                .Where(globalSku => globalSku.Skus
                .Any(sku => !sku.RequiredFeatures.CoalesceEnumerable().Any()
                               || sku.RequiredFeatures!.Any(feature => registeredFeaturesLookup.Contains(feature))));
        }

        /// <inheritdoc/>
        public Task<string?[]> GetResourceProvidersAsync(CancellationToken cancellationToken)
        {
            // Get all RPs in 1 call. As of private preview the count is 27.
            return this.cacheClient.SortedSetRangeByRankAsync(SolutionConstants.ResourceProvidersKey, 0, -1, cancellationToken);
        }

        private static ResourceTypeRegistration[] GetFilteredRegistrationsByFeatures(ResourceTypeRegistrationGrouping resourceTypeRegistrations, IEnumerable<SubscriptionFeatureRegistrationPropertiesModel> subscriptionFeatureRegistrations)
        {
            var registeredFeaturesLookup = subscriptionFeatureRegistrations
                .ToOrdinalInsensitiveHashSet(feature => feature.FullyQualifiedName);

            var filteredRegistrations = resourceTypeRegistrations.WithFeatures
                .Where(registration => AreFeaturesRegistered(registeredFeaturesLookup, registration))
                .GroupBy(filteredRegistration => filteredRegistration, ResourceTypeRegistrationComparer.Instance)
                .ToHashSet(
                    elementSelector: group =>
                    {
                        if (group.Any(registration => registration.AreAllProviderFeaturesRequired))
                        {
                            return group
                                .Where(registration => registration.AreAllProviderFeaturesRequired)
                                .OrderByDescending(registration => registration.ProviderRequiredFeatures.Length)
                                .ThenBy(registration => registration.GetUniqueIdentity().ToUpperInvariant())
                                .First();
                        }
                        else
                        {
                            return group.OrderByAscendingInsensitively(registration => registration.GetUniqueIdentity()).First();
                        }
                    },
                    comparer: ResourceTypeRegistrationComparer.Instance);

            return resourceTypeRegistrations.WithoutFeatures
                .Where(resourceType => !filteredRegistrations.Contains(resourceType))
                .ConcatArray(filteredRegistrations);
        }

        private static bool AreFeaturesRegistered(OrdinalInsensitiveHashSet registeredFeaturesLookup, ResourceTypeRegistration registration)
        {
            // First check for overall region access
            if (registration.RegionRequiredFeatures.CoalesceEnumerable().Any())
            {
                var isRegionAccessAllowed = registration.RegionRequiredFeatures.Any(registeredFeaturesLookup.Contains);

                // If access to the overall region is not allowed stop and report no access
                if (!isRegionAccessAllowed)
                {
                    return false;
                }
            }

            // If no provider specific feature we are done
            if (!registration.ProviderRequiredFeatures.CoalesceEnumerable().Any())
            {
                return true;
            }

            // Check the provider specific features to decide access
            return registration.AreAllProviderFeaturesRequired
                ? registration.ProviderRequiredFeatures.All(registeredFeaturesLookup.Contains)
                : registration.ProviderRequiredFeatures.Any(registeredFeaturesLookup.Contains);
        }

        private async Task<string> GetManifestDataAsync(string resourceProvider, IActivity activity, CancellationToken cancellationToken)
        {
            var request = new DataLabsManifestConfigRequest(
                activity[SolutionConstants.PartnerTraceId]!.ToString()!,
                0,
                activity[SolutionConstants.CorrelationId]?.ToString(),
                resourceProvider);
            var response = await this.resourceProxyClient.GetManifestConfigAsync(request, cancellationToken);
            if (response == null || response.ErrorResponse != null)
            {
                SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, Constants.RPManifestDataset, resourceProvider);
                throw new Exception($"{response?.ErrorResponse?.ErrorDescription}");
            }

            return response.SuccessAdminResponse?.Resource!;
        }
    }
}
