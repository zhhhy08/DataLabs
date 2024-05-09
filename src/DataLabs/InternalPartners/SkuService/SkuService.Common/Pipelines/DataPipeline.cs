namespace SkuService.Main.Pipelines
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Newtonsoft.Json.Linq;
    using SkuService.Common.Builders;
    using SkuService.Common.DataProviders;
    using SkuService.Common.Extensions;
    using SkuService.Common.Models.V1;
    using SkuService.Common.Models.V1.CAS;
    using SkuService.Common.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Data pipeline class for generating notifications.
    /// </summary>
    public class DataPipeline : IDataPipeline<DataLabsARNV3Request, DataLabsARNV3Response>
    {
        private readonly ISubscriptionProvider subscriptionProvider;
        private readonly IDataBuilder<SubscriptionSkuModel> subscriptionSkuBuilder;
        private readonly IRegistrationProvider registrationProvider;
        private static readonly ActivityMonitorFactory GetResourceAsyncMonitorFactory = new("DataPipeline.GetResourcesForSingleSubscriptionAsync");
        private static readonly ActivityMonitorFactory GetResourcesAsyncMonitorFactory = new("DataPipeline.GetResourcesForMultiSubscriptionsAsync");
        private static readonly ActivityMonitorFactory GetSubJobsAsyncMonitorFactory = new("DataPipeline.GetSubJobsAsync");
        private static readonly ActivityMonitorFactory GetSubJobsForFullSyncAsyncMonitorFactory = new("DataPipeline.GetSubJobsForFullSyncAsync");
        private static readonly ActivityMonitorFactory GenerateSkusAsyncMonitorFactory = new("DataPipeline.GenerateSkusAsync");
        private int subscriptionsKeyCount = 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataPipeline"/> class.
        /// </summary>
        /// <param name="subscriptionProvider"></param>
        /// <param name="registrationProvider"></param>
        /// <param name="dataBuilder"></param>
        public DataPipeline(ISubscriptionProvider subscriptionProvider, IRegistrationProvider registrationProvider, IDataBuilder<SubscriptionSkuModel> dataBuilder)
        {
            this.subscriptionProvider = subscriptionProvider;
            this.subscriptionSkuBuilder = dataBuilder;
            this.registrationProvider = registrationProvider;
            if (ServiceRegistrations.GetCustomConfigDictionary.ContainsKey(Constants.SubscriptionsKeyCount))
            {
                ServiceRegistrations.GetCustomConfigDictionary.TryGetValue(Constants.SubscriptionsKeyCount, out string? size);
                _ = int.TryParse(size!, out subscriptionsKeyCount);
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<DataLabsARNV3Response> GetSubJobsAsync(DataLabsARNV3Request request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var monitor = GetSubJobsAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            var subscriptions = await GetSubscriptionsAsync(monitor, cancellationToken);

            int subjobsCount = 0;
            foreach (var subscription in subscriptions)
            {
                subjobsCount++;
                yield return GenerateSubjobNotification(request, subscription, string.Empty);
            }

            monitor.Activity.Properties[Constants.SubJobsCount] = subjobsCount;
            monitor.Activity.Properties[Constants.SubscriptionsCount] = subscriptions.Count;
            monitor.OnCompleted();
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<DataLabsARNV3Response> GetResourcesForSubjobsAsync(DataLabsARNV3Request request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var monitor = GetResourcesAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            var subJobId = request.InputResource.Id.Split(':');
            if (subJobId.Length != 2)
            {
                //subjob:subscription
                throw new ArgumentException($"Invalid subjob id {request.InputResource.Id}");
            }

            GuardHelper.IsArgumentValidGuid(subJobId[1], nameof(subJobId));
            var subscription = subJobId[1];

            var globalResource = request.InputResource?.Data?.Resources?.First().ArmResource;
            GuardHelper.ArgumentNotNull(globalResource, nameof(globalResource));

            var resourceProvider = globalResource.Id.GetResourceNamespace(true);
            var isFullSync = false;

            // Full sync subjobs have Microsoft.ResourceGraph as the RP. Extract the correct RP.
            if (string.IsNullOrEmpty(resourceProvider) || resourceProvider.Equals(Constants.ResourceGraphProvider, StringComparison.OrdinalIgnoreCase))
            {
                resourceProvider = request.InputResource?.Subject;
                isFullSync = true;
            }

            GuardHelper.ArgumentNotNullOrEmpty(resourceProvider, nameof(resourceProvider));
            monitor.Activity.Properties[Constants.ResourceProvider] = resourceProvider;
            monitor.Activity.Properties["SubjobId"] = request.InputResource!.Id;
            monitor.Activity[SolutionConstants.PartnerTraceId] = request.TraceId;
            monitor.Activity[SolutionConstants.CorrelationId] = request.CorrelationId;
            var changedGlobalDatasets = new ChangedDatasets();
            if (globalResource.Type.Equals(Constants.GlobalSkuResourceType, StringComparison.OrdinalIgnoreCase))
            {
                var obj = JObject.FromObject(globalResource.Properties);
                changedGlobalDatasets.SkuSettings = obj.ToObject<GlobalSku>()!;
            }

            IAsyncEnumerator<List<NotificationResourceDataV3<GenericResource>>> enumerator = GenerateSkusAsync(resourceProvider, subscription, request.CorrelationId!, changedGlobalDatasets, monitor.Activity, cancellationToken).GetAsyncEnumerator(cancellationToken);
            await foreach (var op in NotificationUtils.GenerateArnOutputResponseAsync(request, enumerator, isFullSync ? "snapshot" : "write", monitor, cancellationToken))
            {
                yield return op;
            }

            monitor.OnCompleted();
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<DataLabsARNV3Response> GetResourcesForSingleSubscriptionAsync(DataLabsARNV3Request request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var monitor = GetResourceAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            var resource = request.InputResource.Data.Resources?.First().ArmResource;
            GuardHelper.ArgumentNotNull(resource, nameof(resource));
            var subscriptionId = resource.Id.GetSubscriptionId();
            GuardHelper.ArgumentNotNullOrEmpty(subscriptionId, nameof(subscriptionId));
            var resourceProvider = string.Empty;

            if (Constants.ComputeResourceTypes.Contains(resource.Type))
            {
                resourceProvider = "Microsoft.Compute";
            }

            // For Feature registrations
            if (string.IsNullOrEmpty(resourceProvider))
            {
                resourceProvider = resource.Id.GetResourceNamespace(true);
            }

            GuardHelper.ArgumentNotNullOrEmpty(resourceProvider, nameof(resourceProvider));
            monitor.Activity.Properties[Constants.ResourceProvider] = resourceProvider;
            monitor.Activity[SolutionConstants.PartnerTraceId] = request.TraceId;
            monitor.Activity[SolutionConstants.CorrelationId] = request.CorrelationId;
            var changedDatasets = new ChangedDatasets();
            var obj = JObject.FromObject(resource.Properties);
            switch (resource.Type.ToLower())
            {
                case Constants.SubscriptionInternalPropertiesResourceType:
                    changedDatasets.SubscriptionInternalProperties = obj.ToObject<SubscriptionInternalPropertiesModel>()!;
                    break;

                case Constants.SubscriptionMappingResourceType:
                    changedDatasets.SubscriptionMappings = obj.ToObject<SubscriptionMappingsModel>()!;
                    break;

                case Constants.CapacityRestrictionsResourceType:
                    changedDatasets.CapacityRestrictionsInputModel = obj.ToObject<CapacityRestrictionsInputModel>()!;
                    break;

                case Constants.SubscriptionFeatureRegistrationType:
                    changedDatasets.SubscriptionFeatureRegistrationProperties = obj.ToObject<SubscriptionFeatureRegistrationPropertiesModel>()!;
                    break;

                default:
                    var ex = new ArgumentException($"Invalid resource type {resource.Type}");
                    monitor.OnError(ex);
                    throw ex;

            }

            IAsyncEnumerator<List<NotificationResourceDataV3<GenericResource>>> enumerator = GenerateSkusAsync(resourceProvider, subscriptionId, request.CorrelationId!, changedDatasets, monitor.Activity, cancellationToken).GetAsyncEnumerator(cancellationToken);
            await foreach (var op in NotificationUtils.GenerateArnOutputResponseAsync(request, enumerator, "write", monitor, cancellationToken))
            {
                yield return op;
            }

            monitor.OnCompleted();
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<DataLabsARNV3Response> GetSubJobsForFullSyncAsync(DataLabsARNV3Request request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var monitor = GetSubJobsForFullSyncAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            var resourceProviders = await this.registrationProvider.GetResourceProvidersAsync(cancellationToken);

            if (resourceProviders == null || resourceProviders.Length == 0)
            {
                throw new InvalidOperationException("No resource providers found");
            }

            var subscriptions = await GetSubscriptionsAsync(monitor, cancellationToken);

            int subjobsCount = 0;
            int rpCount = 0;
            foreach (var resourceProvider in resourceProviders)
            {
                rpCount++;
                foreach (var subscription in subscriptions)
                {
                    subjobsCount++;
                    yield return GenerateSubjobNotification(request, subscription, resourceProvider!);
                }
            }

            monitor.Activity.Properties[Constants.SubJobsCount] = subjobsCount;
            monitor.Activity.Properties[Constants.ResourceProvidersCount] = rpCount;
            monitor.Activity.Properties[Constants.SubscriptionsCount] = subscriptions.Count;
            monitor.OnCompleted();
        }

        private static DataLabsARNV3Response GenerateSubjobNotification(DataLabsARNV3Request request, string subscription, string resourceProvider)
        {
            var inputResource = request.InputResource?.Data?.Resources?.First().ArmResource;
            var notificationResourceData = new List<NotificationResourceDataV3<GenericResource>>
                            {
                                new(Guid.NewGuid(),
                                inputResource!,
                                Constants.SkuApiVersion,
                                DateTimeOffset.UtcNow)
                            };
            var notificationData = new NotificationDataV3<GenericResource>(
                publisherInfo: Constants.PublisherInfo,
                resources: notificationResourceData,
                resourceLocation: MonitoringConstants.REGION);
            var egNotification = new EventGridNotification<NotificationDataV3<GenericResource>>(
                id: $"{Constants.Subjob}:{subscription}",
                topic: request.InputResource?.Topic,
                subject: string.IsNullOrEmpty(resourceProvider) ? request.InputResource?.Subject : resourceProvider,
                eventType: request.InputResource?.EventType,
                eventTime: request.InputResource!.EventTime,
                data: notificationData);
            var successResponse = new DataLabsARNV3SuccessResponse(egNotification, DateTimeOffset.UtcNow, null);
            var dlResponse = new DataLabsARNV3Response(
                DateTimeOffset.UtcNow,
                Guid.NewGuid().ToString(),
                successResponse,
                null,
                new Dictionary<string, string> { { "SUBJOB", "True" } });

            return dlResponse;
        }

        private async IAsyncEnumerable<List<NotificationResourceDataV3<GenericResource>>> GenerateSkusAsync(string resourceProvider, string subscription, string correlationId, ChangedDatasets changedGlobalDatasets, IActivity activity, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var monitor = GenerateSkusAsyncMonitorFactory.ToMonitor(activity);
            var notificationResourceData = new List<NotificationResourceDataV3<GenericResource>>();
            await foreach (var sku in this.subscriptionSkuBuilder.BuildAsync(resourceProvider, subscription, activity, changedGlobalDatasets, cancellationToken))
            {
                var skuData = new GenericResource
                {
                    ApiVersion = Constants.SkuApiVersion,
                    Id = string.Format(Constants.SubscriptionSkuResourceId, subscription, resourceProvider, sku.ResourceType.Replace(@"/", "-"), sku.Location),
                    Location = sku.Location,
                    Type = Constants.SubscriptionSkuResourceType,
                    Name = "default",
                    Properties = sku
                };
                notificationResourceData.Add(new NotificationResourceDataV3<GenericResource>(
                Guid.Parse(correlationId),
                skuData,
                Constants.SkuApiVersion,
                DateTimeOffset.UtcNow));

                yield return notificationResourceData;
                notificationResourceData.Clear();
            }

            monitor.OnCompleted();
        }

        private async Task<List<string>> GetSubscriptionsAsync(IActivityMonitor monitor, CancellationToken cancellationToken)
        {
            var subscriptions = new List<string>();
            if (ServiceRegistrations.GetCustomConfigDictionary.ContainsKey(Constants.PreviewSubscriptions))
            {
                ServiceRegistrations.GetCustomConfigDictionary.TryGetValue(Constants.PreviewSubscriptions, out string? subscriptionIds);
                if (!string.IsNullOrEmpty(subscriptionIds))
                {
                    subscriptions.AddRange(subscriptionIds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                }
            }

            if (subscriptions.Count > 0)
            {
                monitor.Activity.Properties["SubscriptionsFromConfig"] = true;
            }
            else
            {
                for (int idx = 0; idx < subscriptionsKeyCount; idx++)
                {
                    // Range end -1 means all elements.
                    // TODO: Paginate after private preview
                    subscriptions.AddRange(await this.subscriptionProvider.GetSubscriptionsByRangeAsync($"{Constants.SubscriptionsCacheKeyPrefix}{idx}", 0, -1, cancellationToken));
                }

                monitor.Activity.Properties["SubscriptionsFromCache"] = true;
            }

            return subscriptions;
        }

    }
}
