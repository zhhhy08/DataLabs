namespace SkuService.Common.DataProviders
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Newtonsoft.Json;
    using SkuService.Common.Models.V1;
    using System.Threading.Tasks;
    using BillingAccount = Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface.BillingAccount;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using SkuService.Common.Utilities;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using SkuService.Common.Extensions;
    using SkuService.Common.Telemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;

    internal class RestrictionsProvider : IRestrictionsProvider
    {
        private readonly IResourceProxyClient resourceProxyClient;
        private readonly string casClientId = "SkuService";
        private readonly string serviceName = "serviceName";
        private static readonly ActivityMonitorFactory GetSkuCapacityRestrictionsAsyncMonitorFactory = new("RestrictionsProvider.GetSkuCapacityRestrictionsAsync");
        /// <summary>
        /// Initializes a new instance of the <see cref="RestrictionsProvider" /> class.
        /// </summary>
        public RestrictionsProvider(IResourceProxyClient resourceProxyClient)
        {
            this.resourceProxyClient = resourceProxyClient;
            if (ServiceRegistrations.GetCustomConfigDictionary.ContainsKey(Constants.CasClientId))
            {
                ServiceRegistrations.GetCustomConfigDictionary.TryGetValue(Constants.CasClientId, out casClientId!);
            }
            if (ServiceRegistrations.GetCustomConfigDictionary.ContainsKey(Constants.ServiceName))
            {
                ServiceRegistrations.GetCustomConfigDictionary.TryGetValue(Constants.ServiceName, out serviceName!);
            }
        }

        /// <inheritdoc/>
        public async Task<InsensitiveDictionary<SkuLocationInfo[]>> GetSkuCapacityRestrictionsAsync(
            string resourceProvider, string registrationDate, SubscriptionInternalPropertiesModel subscriptionInternalProperties, SubscriptionMappingsModel subscriptionMappings, 
            IActivity parentActivity, bool skipCacheRead, CancellationToken cancellationToken)
        {
            using var monitor = GetSkuCapacityRestrictionsAsyncMonitorFactory.ToMonitor(parentActivity);
            var casRequest = new DataLabsCasRequest(
              traceId: parentActivity[SolutionConstants.PartnerTraceId]!.ToString()!,
              retryCount: 0,
              correlationId: parentActivity.CorrelationId,
              casRequestBody: new CasRequestBody
              {
                  SubscriptionId = subscriptionInternalProperties.SubscriptionId!,
                  Provider = resourceProvider,
                  OfferCategory = subscriptionInternalProperties.OfferCategory!,
                  ClientAppId = casClientId,
                  SubscriptionRegistrationDate = registrationDate,
                  EntitlementStartDate = subscriptionInternalProperties.EntitlementStartDate,
                  SubscriptionLocationsAndZones = ConvertSubscriptionMappingsToCasSubscriptionLocationsAndZones(subscriptionMappings),
                  BillingProperties = new Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface.BillingProperties
                  {
                      ChannelType = subscriptionInternalProperties.BillingProperties!.ChannelType,
                      PaymentType = subscriptionInternalProperties.BillingProperties.PaymentType,
                      WorkloadType = subscriptionInternalProperties.BillingProperties.WorkloadType,
                      BillingType = subscriptionInternalProperties.BillingProperties.BillingType,
                      Tier = subscriptionInternalProperties.BillingProperties.Tier,
                      BillingAccount = new BillingAccount
                      {
                          Id = subscriptionInternalProperties!.BillingProperties!.BillingAccount!.Id
                      }
                  },
                  InternalSubscriptionPolicies = new Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface.InternalSubscriptionPolicies
                  {
                      SubscriptionCostCategory = subscriptionInternalProperties.InternalSubscriptionPolicies?.CostCategory ?? string.Empty,
                      SubscriptionPcCode = subscriptionInternalProperties.InternalSubscriptionPolicies?.PcCode ?? string.Empty,
                      SubscriptionEnvironment = (subscriptionInternalProperties.InternalSubscriptionPolicies?.Environment) == null ?
                        string.Empty : ((bool)subscriptionInternalProperties.InternalSubscriptionPolicies.Environment ? "1" : "0")
                  }
              }
            );


            var response = await this.resourceProxyClient.GetCasResponseAsync(casRequest, cancellationToken, skipCacheRead);
            if (response == null || response.SuccessCasResponse == null)
            {
                SkuSolutionMetricProvider.ResourceProxyResourceMissedMetricReport(this.serviceName, Constants.CapacityRestrictionsDataset, $"/{casRequest.casRequestBody.SubscriptionId}/{casRequest.casRequestBody.Provider}");
                return [];
            }

            var restrictions = JsonConvert.DeserializeObject<CapacityRestrictionsResponse>(response.SuccessCasResponse.Resource!);
            GuardHelper.ArgumentNotNull(restrictions, nameof(restrictions));

            var convertedResponse = restrictions.GetRestrictionsBySkuName().ToInsensitiveDictionary(
                        keySelector: skuNameRestrictionPair => skuNameRestrictionPair.Key,
                        elementSelector: skuNameRestrictionPair =>
                            skuNameRestrictionPair.Value
                                .CoalesceEnumerable()
                                .Where(restriction => restriction != null)
                                .SelectArray(restriction => GetCapacityRestrictionLocationInfo(restriction)));

            monitor.OnCompleted();
            return convertedResponse;
        }

        private static SkuLocationInfo GetCapacityRestrictionLocationInfo(RestrictionsInfo restrictionsInfo)
        {
            return new SkuLocationInfo
            {
                Location = restrictionsInfo.Location!,
                Zones = restrictionsInfo.PhysicalAvailabilityZones,
                IsSpotRestricted = restrictionsInfo.IsSpot,
                IsOndemandRestricted = restrictionsInfo.IsOndemand,
                IsCapacityReservationRestricted = restrictionsInfo.IsCapacityReservation,
            };
        }

        private static List<SubscriptionLocationsAndZones> ConvertSubscriptionMappingsToCasSubscriptionLocationsAndZones(SubscriptionMappingsModel model)
        {
            var result = new List<SubscriptionLocationsAndZones>();
            var mappings = model?.AvailabilityZoneMappings;

            mappings?.ForEach(mapping =>
            {
                var convertedZoneMappings = mapping.Value.ConvertAll(zones =>
                {
                    return new Zones
                    {
                        PhysicalZone = zones.PhysicalZone,
                        LogicalZone = zones.LogicalZone
                    };
                });

                result.Add(new SubscriptionLocationsAndZones
                {
                    Location = mapping.Key,
                    Zones = convertedZoneMappings
                });
            });

            return result;
        }
    }
}
