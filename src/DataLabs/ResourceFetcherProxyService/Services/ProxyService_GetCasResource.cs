namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Monitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using SubscriptionLocationsAndZones = Common.Partner.DataLabsInterface.SubscriptionLocationsAndZones;
    using Zones = Common.Partner.DataLabsInterface.Zones;
    using BillingProperties = Common.Partner.DataLabsInterface.BillingProperties;
    using BillingAccount = Common.Partner.DataLabsInterface.BillingAccount;
    using InternalSubscriptionPolicies = Common.Partner.DataLabsInterface.InternalSubscriptionPolicies;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient;

    internal partial class ProxyService : ResourceProxyService.ResourceProxyServiceBase
    {
        private static readonly ActivityMonitorFactory ProxyServiceGetCasResource = new("ProxyService.GetCasResource");

        public override async Task<ResourceResponse> GetCas(
            CasRequest request,
            ServerCallContext context)
        {
            var callMethod = nameof(GetCas);
            var activityMonitorFactory = ProxyServiceGetCasResource;
            var activityName = ResourceFetcherProxyConstants.ActivityName_GetCas;

            using var activity = new OpenTelemetryActivityWrapper(
                source: ResourceFetcherProxyMetricProvider.ResourceFetcherProxyActivitySource,
                name: activityName,
                kind: ActivityKind.Server,
                parentId: request.TraceId);

            var scenario = ParseScenario(request.ReqAttributes);
            using var monitor = activityMonitorFactory.ToMonitor(scenario: scenario, component: SolutionConstants.ResourceFetcherProxyService);

            int retryCount = request.RetryCount;
            string? correlationId = request.CorrelationId;
            string? typeDimensionValue = request.CasRequestBody.Provider;

            try
            {
                ResourceFetcherProxyMetricProvider.AddRFProxyClientToRecvMetric(callMethod, request.RequestEpochTime, context);

                monitor.OnStart(false);

                activity.SetTag(SolutionConstants.SubscriptionId, request.CasRequestBody.SubscriptionId);
                activity.SetTag(SolutionConstants.Provider, request.CasRequestBody.Provider);

                string? resourceIdColumnValue = request.CasRequestBody.SubscriptionId;
                string allowedType = ClientProviderConfigList.AllAllowedSymbol; // For now, for CAS, we only support allow or not allow scenario
                string? subscriptionId = null;
                string? tenantId = null;
                string? resourceType = null;
                string? cacheKey = ResourceProxyClient.GetCacheKeyForCas(request.CasRequestBody.SubscriptionId, request.CasRequestBody.Provider);
                var defaultDataFormat = ResourceCacheDataFormat.CAS;

                return await ExecuteAsync(
                    allowedTypesMap: _clientProvidersManager.GetCasResponseAllowedTypesMap,
                    clientDelegateFunc: ExecuteGetCasCapacityCheckAsync,
                    deletegateFuncArg: request,
                    clientTimeOutDelegate: GetCasCapacityCheckTimeOut,
                    callMethod: callMethod,
                    correlationId: correlationId,
                    resourceIdColumnValue: resourceIdColumnValue,
                    retryFlowCount: retryCount,
                    allowedType: allowedType,
                    defaultDataFormat: defaultDataFormat,
                    subscriptionId: subscriptionId,
                    tenantId: tenantId,
                    resourceType: resourceType,
                    typeDimensionValue: typeDimensionValue,
                    cacheKey: cacheKey,
                    skipCacheRead: request.SkipCacheRead,
                    skipCacheWrite: request.SkipCacheWrite,
                    otelActivity: activity,
                    monitor: monitor,
                    parentCancellationToken: context.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(
                    correlationId: correlationId,
                    callMethod: callMethod,
                    retryFlowCount: retryCount,
                    typeDimensionValue: typeDimensionValue,
                    httpStatusCode: 0,
                    providerType: ClientProviderType.None,
                    proxyError: ResourceFetcherProxyError.INTERNAL_EXCEPTION,
                    errorType: ProxyErrorType.Retry,
                    errorMessage: ex.Message,
                    retryAfter: 0,
                    exception: ex,
                    otelActivity: activity,
                    monitor: monitor);
            }
        }

        private static CasRequestBody ConvertToCasRequestBody(CasRequest request)
        {
            var subscriptionLocationsAndZones = request.CasRequestBody.SubscriptionLocationsAndZones;
            var subscriptionLocationsAndZonesList = new List<SubscriptionLocationsAndZones>(subscriptionLocationsAndZones.Count);
            foreach (var subscriptionLocationAndZone in subscriptionLocationsAndZones)
            {
                var zones = new List<Zones>();
                foreach (var zone in subscriptionLocationAndZone.Zones)
                {
                    zones.Add(new Zones { LogicalZone = zone.LogicalZone, PhysicalZone = zone.PhysicalZone });
                }

                subscriptionLocationsAndZonesList.Add(new SubscriptionLocationsAndZones { Location = subscriptionLocationAndZone.Location, Zones = zones });
            }

            return new CasRequestBody
            {
                ClientAppId = request.CasRequestBody.ClientAppId,
                OfferCategory = request.CasRequestBody.OfferCategory,
                Provider = request.CasRequestBody.Provider,
                SubscriptionId = request.CasRequestBody.SubscriptionId,
                SubscriptionRegistrationDate = request.CasRequestBody.SubscriptionRegistrationDate,
                EntitlementStartDate = request.CasRequestBody.EntitlementStartDate,
                SubscriptionLocationsAndZones = subscriptionLocationsAndZonesList,
                BillingProperties = new BillingProperties
                {
                    BillingAccount = new BillingAccount
                    {
                        Id = request.CasRequestBody.BillingProperties.BillingAccount.Id
                    },
                    BillingType = request.CasRequestBody.BillingProperties.BillingType,
                    ChannelType = request.CasRequestBody.BillingProperties.ChannelType,
                    PaymentType = request.CasRequestBody.BillingProperties.PaymentType,
                    Tier = request.CasRequestBody.BillingProperties.Tier,
                    WorkloadType = request.CasRequestBody.BillingProperties.WorkloadType
                },
                InternalSubscriptionPolicies = new InternalSubscriptionPolicies
                {
                    SubscriptionCostCategory = request.CasRequestBody.InternalSubscriptionPolicies.SubscriptionCostCategory,
                    SubscriptionPcCode = request.CasRequestBody.InternalSubscriptionPolicies.SubscriptionPcCode,
                    SubscriptionEnvironment = request.CasRequestBody.InternalSubscriptionPolicies.SubscriptionEnvironment,
                }
            };
        }

        private static Task<HttpResponseMessage> ExecuteGetCasCapacityCheckAsync(
            ICasClient client,
            object arg,
            string? apiVersion,
            CancellationToken cancellationToken)
        {
            var casRequestBoby = ConvertToCasRequestBody((CasRequest)arg);
            return client.GetCasCapacityCheckAsync(
                casRequestBody: casRequestBoby,
                apiVersion: apiVersion ?? string.Empty,
                clientRequestId: null,
                cancellationToken: cancellationToken);
        }

        private static TimeSpan? GetCasCapacityCheckTimeOut(ClientProviderType providerType, string callMethod, string allowedType, int retryFlowCount)
        {
            if (providerType == ClientProviderType.Cas)
            {
                return _casClientTimeOutManager.GetCasCallTimeOut(callMethod: callMethod, retryFlowCount: retryFlowCount);
            }
            return null;
        }
    }
}