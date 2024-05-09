namespace SkuService.Common.DataProviders
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Newtonsoft.Json.Linq;
    using SkuService.Common.Models.V1;
    using SkuService.Common.Telemetry;
    using SkuService.Common.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class SubscriptionDataProvider : ISubscriptionProvider
    {
        private readonly IResourceProxyClient resourceProxyClient;
        private readonly ICacheClient cacheClient;
        private readonly string serviceName;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionDataProvider" /> class.
        /// </summary>
        public SubscriptionDataProvider(IResourceProxyClient resourceProxyClient, ICacheClient cacheClient, ISkuServiceProvider skuServiceProvider)
        {
            this.cacheClient = cacheClient;
            this.resourceProxyClient = resourceProxyClient;
            this.serviceName = skuServiceProvider.GetServiceName();
            GuardHelper.ArgumentNotNull(this.cacheClient, nameof(this.cacheClient));
            GuardHelper.ArgumentNotNull(this.resourceProxyClient, nameof(this.resourceProxyClient));
            GuardHelper.ArgumentNotNull(this.serviceName, nameof(this.serviceName));
        }

        /// <inheritdoc/>
        public async Task<IList<SubscriptionFeatureRegistrationPropertiesModel>> GetSubscriptionFeatureRegistrationPropertiesAsync(string subscriptionId, string resourceProvider, IActivity activity, CancellationToken cancellationToken)
        {
            var request = new DataLabsResourceRequest(activity[SolutionConstants.PartnerTraceId]?.ToString()!, 0, activity[SolutionConstants.CorrelationId] as string, string.Format(Constants.SubscriptionFeatureRegistrationResourceId, subscriptionId, resourceProvider), string.Empty);
            var resource = await this.GetCollectionAsync(request, cancellationToken);

            List<SubscriptionFeatureRegistrationPropertiesModel> result = [];

            foreach (var featureRegistration in resource)
            {
                result.Add(((JObject)featureRegistration.Properties).ToObject<SubscriptionFeatureRegistrationPropertiesModel>()!);
            }

            if (result.Count == 0)
            {
                SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, Constants.SubscriptionFeatureRegistrationsDataset, request.ResourceId);
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<SubscriptionInternalPropertiesModel> GetSubscriptionInternalPropertiesAsync(string subscriptionId, IActivity activity, CancellationToken cancellationToken)
        {
            var request = new DataLabsResourceRequest(activity[SolutionConstants.PartnerTraceId]?.ToString()!, 0, activity[SolutionConstants.CorrelationId] as string, string.Format(Constants.SubscriptionInternalPropertiesResourceId, subscriptionId), string.Empty);
            var resource = await this.GetCollectionAsync(request, cancellationToken);
            if (resource.Count == 0)
            {
                SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, Constants.SubscriptionInternalPropertiesDataset, request.ResourceId);
                throw new Exception($"Subscription internal properties not found for subscription: {subscriptionId}");
            }
            var result = ((JObject)resource.FirstOrDefault()!.Properties).ToObject<SubscriptionInternalPropertiesModel>()!;
            if (string.IsNullOrEmpty(result.SubscriptionId))
            {
                activity.Properties["SubIdMissingInSubsInternalProperties"] = true;
                result.SubscriptionId = subscriptionId;
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<SubscriptionMappingsModel> GetSubscriptionMappingsAsync(string subscriptionId, IActivity activity, CancellationToken cancellationToken)
        {
            var request = new DataLabsResourceRequest(activity[SolutionConstants.PartnerTraceId]?.ToString()!, 0, activity[SolutionConstants.CorrelationId] as string, string.Format(Constants.SubscriptionMappingsResourceId, subscriptionId), string.Empty);
            var resource = await this.GetCollectionAsync(request, cancellationToken);
            if (resource.Count == 0)
            {
                SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, Constants.SubscriptionZoneMappingsDataset, request.ResourceId);
                throw new Exception($"Subscription zone mappings not found for subscription: {subscriptionId}");
            }

            return ((JObject)resource.FirstOrDefault()!.Properties).ToObject<SubscriptionMappingsModel>()!;
        }

        /// <inheritdoc/>
        public Task<string[]> GetSubscriptionsByRangeAsync(string key, int start, int end, CancellationToken cancellationToken)
        {
            return this.cacheClient.SortedSetRangeByRankAsync(key, start, end, cancellationToken)!;
        }

        /// <inheritdoc/>
        public async Task<SubscriptionRegistrationModel> GetSubscriptionRegistrationAsync(string subscriptionId, string resourceProvider, IActivity activity, CancellationToken cancellationToken)
        {
            var request = new DataLabsResourceRequest(
                activity[SolutionConstants.PartnerTraceId]?.ToString()!,
                0,
                activity[SolutionConstants.CorrelationId] as string,
                string.Format(Constants.SubscriptionRegistrationsResourceId,
                subscriptionId),
                string.Empty);
            var resource = await this.GetCollectionAsync(request, cancellationToken);

            if (resource.Count == 0)
            {
                SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, Constants.SubscriptionRegistrationsDataset, request.ResourceId);
                return new SubscriptionRegistrationModel
                {
                    RegistrationDate = DateTime.MaxValue.ToString(),
                };
            }

            var returnResource =  resource.FirstOrDefault(x => x.Properties != null && ((JObject)x.Properties).ToObject<SubscriptionRegistrationModel>()!
                .ResourceProviderNamespace.Equals(resourceProvider, StringComparison.OrdinalIgnoreCase));

            if(returnResource != null)
            {
                SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, Constants.SubscriptionRegistrationsDataset, request.ResourceId);
                return ((JObject)returnResource.Properties).ToObject<SubscriptionRegistrationModel>()!;
            }

            return new SubscriptionRegistrationModel
            {
                RegistrationDate = DateTime.MaxValue.ToString(),
            };
        }

        private async Task<List<GenericResource>> GetCollectionAsync(DataLabsResourceRequest request, CancellationToken cancellationtoken)
        {
            var resource = await this.resourceProxyClient.GetCollectionAsync(request, cancellationtoken, true);

            if (resource.SuccessResponse != null)
            {
                return resource.SuccessResponse.Value!;
            }

            var resourceType = ArmUtils.GetResourceTypeForCollectionCall(request.ResourceId);
            SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, resourceType!, request.ResourceId);
            throw new SkuPartnerException(resource.ErrorResponse!);
        }
    }
}
