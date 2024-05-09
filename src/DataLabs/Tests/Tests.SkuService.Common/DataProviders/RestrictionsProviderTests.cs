namespace Tests.SkuService.Common.DataProviders
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Moq;
    using global::SkuService.Common.DataProviders;
    using global::SkuService.Common.Models.V1;
    using System.Text.Json.Nodes;
    using BillingProperties = global::SkuService.Common.Models.V1.BillingProperties;
    using BillingAccount = global::SkuService.Common.Models.V1.BillingAccount;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using global::SkuService.Common.Extensions;
    using Microsoft.Extensions.DependencyInjection;

    [TestClass]
    public class RestrictionsProviderTests
    {
        readonly Mock<IResourceProxyClient> resourceProxyClient = new();
        readonly IActivityMonitor monitor = new ActivityMonitorFactory("Test").ToMonitor();

        public RestrictionsProviderTests()
        {
            monitor.Activity[SolutionConstants.PartnerTraceId] = Guid.NewGuid().ToString();
            monitor.Activity.CorrelationId = Guid.NewGuid().ToString();
            var configs = @"
                    { 
                      ""subjobBatchSize"" : ""1000"",
                      ""configFetchIntervalInHours"" : ""6"",
                      ""globalSkuBatchSize"": ""1000"",
                      ""casClientId"": ""901c622e-9663-4c65-9008-df103ed6cc5a""
                    }
            ";

            var appSettingsStub = new Dictionary<string, string>
            {
                { "CustomConfig", configs }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub!);

            ConfigMapUtil.Initialize(config, false);
            ServiceRegistrations.InitializeServiceProvider(new ServiceCollection(), "SkuService");
        }

        [TestCleanup]
        public void Cleanup()
        {
            resourceProxyClient.Reset();
        }

        readonly string exampleResponse = @"{
	        ""responseCode"": ""OK"",
	        ""restrictionsByOfferFamily"": {
		        ""STANDARDD15V2"": [{
				        ""offerTerms"": [
					        ""Ondemand""
				        ],
				""location"": ""CANADACENTRAL"",
				""physicalAvailabilityZones"": []
			},
			{
				""offerTerms"": [
					""Ondemand""
				],
				""location"": ""EASTUS"",
				""physicalAvailabilityZones"": []
			},
			{
				""offerTerms"": [
					""Ondemand""
				],
				""location"": ""SOUTHINDIA"",
				""physicalAvailabilityZones"": []
			},
			{
				""offerTerms"": [
					""Ondemand""
				],
				""location"": ""WESTUS2"",
				""physicalAvailabilityZones"": []
			}
		]
	},
	        ""restrictedSkuNamesToOfferFamily"": {
		        ""STANDARDD15V2"": ""STANDARDD15V2""
	        }
    }";

        readonly SubscriptionMappingsModel subMapping = new SubscriptionMappingsModel
        {
            SubscriptionId = "subscriptionId",
            AvailabilityZoneMappings = new Dictionary<string, List<ZoneMapping>>
                {
                    {"eastus", new List<ZoneMapping>
                        { new ZoneMapping
                            {
                                PhysicalZone = "physicalZone",
                                LogicalZone = "logicalZone"
                            }
                        }
                    }
                }
        };

        readonly SubscriptionInternalPropertiesModel subscriptionInternalProperties = new SubscriptionInternalPropertiesModel
        {
            SubscriptionId = "subscriptionId",
            OfferCategory = "offerCategory",
            EntitlementStartDate = "entitlemenetStartDate",
            BillingProperties = new BillingProperties
            {
                ChannelType = "channelType",
                PaymentType = "paymentType",
                WorkloadType = "workloadType",
                BillingType = "billingType",
                Tier = "tier",
                BillingAccount = new BillingAccount
                {
                    Id = "id"
                }
            }
        };

        [TestMethod]
        public async Task TestGetSkuCapacityRestrictionsAsync()
        {
            var obj = JsonObject.Parse(exampleResponse)!.AsObject();
            var successResponse = new DataLabsCasSuccessResponse(
              resource: obj.ToString(),
              DateTimeOffset.MinValue
            );

            var response = new DataLabsCasResponse(DateTimeOffset.MinValue, string.Empty, successResponse, null, null, DataLabsDataSource.CAS);

            resourceProxyClient.Setup(x =>
             x.GetCasResponseAsync(
               It.IsAny<DataLabsCasRequest>(),
               It.IsAny<CancellationToken>(),
               false,
               false,
               null,
               null)
               )
              .Returns(Task.FromResult(response));
            var result = await new RestrictionsProvider(resourceProxyClient.Object).GetSkuCapacityRestrictionsAsync(
              "resourceProvider",
              DateTime.UtcNow.ToString(),
              subscriptionInternalProperties,
              subMapping,
              monitor.Activity,
              false,
              default
              );

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Keys.Count > 0);
            resourceProxyClient.VerifyAll();
        }

        [TestMethod]
        public async Task TestGetSkuCapacityRestrictions_ThrowsException()
        {
            var obj = JsonObject.Parse(exampleResponse)!.AsObject();

            var successResponse = new DataLabsCasSuccessResponse(
              resource: obj.ToString(),
              DateTimeOffset.MinValue
            );

            var response = new DataLabsCasResponse(DateTimeOffset.MinValue, string.Empty, successResponse, null, null, DataLabsDataSource.CAS);

            resourceProxyClient.Setup(x =>
             x.GetCasResponseAsync(
               It.IsAny<DataLabsCasRequest>(),
               It.IsAny<CancellationToken>(),
               true,
               false,
               null,
               null)
               )
              .Throws(new Exception());


            await Assert.ThrowsExceptionAsync<Exception>(async () => await new RestrictionsProvider(resourceProxyClient.Object).GetSkuCapacityRestrictionsAsync(
              "resourceProvider",
              DateTime.UtcNow.ToString(),
              subscriptionInternalProperties,
              subMapping,
              monitor.Activity,
              true,
              default
              ));

            resourceProxyClient.VerifyAll();
        }
    }
}
