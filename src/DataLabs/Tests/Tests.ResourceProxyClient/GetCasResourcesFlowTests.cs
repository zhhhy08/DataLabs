namespace Tests.ResourceProxyClient
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Data;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceProxyClient;
    using Newtonsoft.Json;
    using System.Net;

    [TestClass]
    public class GetCasResourcesFlowTests: BaseTestsInitialize
    {
        [TestMethod]
        public async Task ResourceFetcherCasResponse200()
        {
            // Arrange
            var allowedTypesInProxy = "*:cache|write/01:00:00,resourcefetcher_cas|2022-12-01";
            var allowedTypesInFetcher = "GetCasCapacityCheckAsync|2016-12-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var casResourceString = ResourceProxyClientTestData.CasRestrictionsResource;
            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();
            var casRequest = new DataLabsCasRequest(
                traceId,
                0,
                correlationId,
                new CasRequestBody
                {
                    ClientAppId = "TestAppId",
                    OfferCategory = "TestOfferCategory",
                    Provider = "Microsoft.Compute",
                    SubscriptionId = Guid.NewGuid().ToString(),
                    SubscriptionRegistrationDate = DateTimeOffset.UtcNow.ToString(),
                    SubscriptionLocationsAndZones = new List<SubscriptionLocationsAndZones>
                    {
                        new() {
                            Location = "eastus",
                            Zones = new List<Zones> { new() { LogicalZone = "1", PhysicalZone = "eastus" } }
                        }
                    },
                    BillingProperties = new BillingProperties
                    {
                        BillingAccount = new BillingAccount
                        {
                            Id = "TestBillingAccountId",
                        },
                        BillingType = "TestBillingType",
                        ChannelType = "TestChannelType",
                        PaymentType = "TestPaymentType",
                        Tier = "TestTier",
                        WorkloadType = "TestWorkloadType",
                    },
                    InternalSubscriptionPolicies = new InternalSubscriptionPolicies
                    {
                        SubscriptionCostCategory = "TestSubscriptionCostCategory",
                        SubscriptionEnvironment = "TestSubscriptionEnvironment",
                        SubscriptionPcCode = "TestSubscriptionPcCode",
                    },
                    EntitlementStartDate = DateTimeOffset.UtcNow.ToString(),
                });
            // Set content to TestARMClient
            _resourceProxyFlowTestManager.CasClient.SetResource(JsonConvert.SerializeObject(casRequest.casRequestBody), casResourceString);

            // Act
            var dataLabsResourceResponse = await _resourceProxyClient.GetCasResponseAsync(
                request: casRequest,
                cancellationToken: default)
                .ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(dataLabsResourceResponse);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessCasResponse);
            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.AreEqual(DataLabsDataSource.CAS, dataLabsResourceResponse.DataSource);
            Assert.AreEqual(ResourceProxyClientTestData.CasRestrictionsResource, dataLabsResourceResponse.SuccessCasResponse.Resource);
        }

        [TestMethod]
        public async Task ResourceFetcherCasResponse200AndCacheHit()
        {
            // Arrange
            var allowedTypesInProxy = "*:cache|write/01:00:00,resourcefetcher_cas|2022-12-01";
            var allowedTypesInFetcher = "GetCasCapacityCheckAsync|2016-12-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var casResourceString = ResourceProxyClientTestData.CasRestrictionsResource;
            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();
            var subscriptionId = Guid.NewGuid().ToString();
            var casRequest = new DataLabsCasRequest(
                traceId,
                0,
                correlationId,
                new CasRequestBody
                {
                    ClientAppId = "TestAppId",
                    OfferCategory = "TestOfferCategory",
                    Provider = "Microsoft.Compute",
                    SubscriptionId = subscriptionId,
                    SubscriptionRegistrationDate = DateTimeOffset.UtcNow.ToString(),
                    SubscriptionLocationsAndZones = new List<SubscriptionLocationsAndZones>
                    {
                        new() {
                            Location = "eastus",
                            Zones = new List<Zones> { new() { LogicalZone = "1", PhysicalZone = "eastus" } }
                        }
                    },
                    BillingProperties = new BillingProperties
                    {
                        BillingAccount = new BillingAccount
                        {
                            Id = "TestBillingAccountId",
                        },
                        BillingType = "TestBillingType",
                        ChannelType = "TestChannelType",
                        PaymentType = "TestPaymentType",
                        Tier = "TestTier",
                        WorkloadType = "TestWorkloadType",
                    },
                    InternalSubscriptionPolicies = new InternalSubscriptionPolicies
                    {
                        SubscriptionCostCategory = "TestSubscriptionCostCategory",
                        SubscriptionEnvironment = "TestSubscriptionEnvironment",
                        SubscriptionPcCode = "TestSubscriptionPcCode",
                    },
                    EntitlementStartDate = DateTimeOffset.UtcNow.ToString(),
                });
            _resourceProxyFlowTestManager.CasClient.SetResource(JsonConvert.SerializeObject(casRequest.casRequestBody), casResourceString);

            // Act
            var dataLabsResourceResponse = await _resourceProxyClient.GetCasResponseAsync(
                request: casRequest,
                cancellationToken: default)
                .ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(dataLabsResourceResponse);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessCasResponse);
            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.AreEqual(DataLabsDataSource.CAS, dataLabsResourceResponse.DataSource);
            Assert.AreEqual(ResourceProxyClientTestData.CasRestrictionsResource, dataLabsResourceResponse.SuccessCasResponse.Resource);

            correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            traceId = ResourceProxyClientTestData.CreateNewActivityId();
            casRequest = new DataLabsCasRequest(
                traceId,
                0,
                correlationId,
                new CasRequestBody
                {
                    ClientAppId = "TestAppId",
                    OfferCategory = "TestOfferCategory",
                    Provider = "Microsoft.Compute",
                    SubscriptionId = subscriptionId,
                    SubscriptionRegistrationDate = DateTimeOffset.UtcNow.ToString(),
                    SubscriptionLocationsAndZones = new List<SubscriptionLocationsAndZones>
                    {
                        new() {
                            Location = "eastus",
                            Zones = new List<Zones> { new() { LogicalZone = "1", PhysicalZone = "eastus" } }
                        }
                    },
                    BillingProperties = new BillingProperties
                    {
                        BillingAccount = new BillingAccount
                        {
                            Id = "TestBillingAccountId",
                        },
                        BillingType = "TestBillingType",
                        ChannelType = "TestChannelType",
                        PaymentType = "TestPaymentType",
                        Tier = "TestTier",
                        WorkloadType = "TestWorkloadType",
                    },
                    InternalSubscriptionPolicies = new InternalSubscriptionPolicies
                    {
                        SubscriptionCostCategory = "TestSubscriptionCostCategory",
                        SubscriptionEnvironment = "TestSubscriptionEnvironment",
                        SubscriptionPcCode = "TestSubscriptionPcCode",
                    },
                    EntitlementStartDate = DateTimeOffset.UtcNow.ToString(),
                });
            _resourceProxyFlowTestManager.CasClient.SetResource(JsonConvert.SerializeObject(casRequest.casRequestBody), casResourceString);

            // Act
            dataLabsResourceResponse = await _resourceProxyClient.GetCasResponseAsync(
                request: casRequest,
                cancellationToken: default)
                .ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(dataLabsResourceResponse);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessCasResponse);
            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.AreEqual(DataLabsDataSource.CACHE, dataLabsResourceResponse.DataSource);
            Assert.AreEqual(ResourceProxyClientTestData.CasRestrictionsResource, dataLabsResourceResponse.SuccessCasResponse.Resource);
        }

        [TestMethod]
        public async Task ResourceFetcherCasResponse404()
        {
            // Arrange
            var allowedTypesInProxy = "*:cache|write/01:00:00,resourcefetcher_cas|2022-12-01";
            var allowedTypesInFetcher = "GetCasCapacityCheckAsync|2016-12-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();
            var casRequest = new DataLabsCasRequest(
                traceId,
                0,
                correlationId,
                new CasRequestBody
                {
                    ClientAppId = "TestAppId",
                    OfferCategory = "TestOfferCategory",
                    Provider = "Microsoft.Compute",
                    SubscriptionId = Guid.NewGuid().ToString(),
                    SubscriptionRegistrationDate = DateTimeOffset.UtcNow.ToString(),
                    SubscriptionLocationsAndZones = new List<SubscriptionLocationsAndZones>
                    {
                        new() {
                            Location = "eastus",
                            Zones = new List<Zones> { new() { LogicalZone = "1", PhysicalZone = "eastus" } }
                        }
                    },
                    BillingProperties = new BillingProperties
                    {
                        BillingAccount = new BillingAccount
                        {
                            Id = "TestBillingAccountId",
                        },
                        BillingType = "TestBillingType",
                        ChannelType = "TestChannelType",
                        PaymentType = "TestPaymentType",
                        Tier = "TestTier",
                        WorkloadType = "TestWorkloadType",
                    },
                    InternalSubscriptionPolicies = new InternalSubscriptionPolicies
                    {
                        SubscriptionCostCategory = "TestSubscriptionCostCategory",
                        SubscriptionEnvironment = "TestSubscriptionEnvironment",
                        SubscriptionPcCode = "TestSubscriptionPcCode",
                    },
                    EntitlementStartDate = DateTimeOffset.UtcNow.ToString(),
                });

            // Act
            var dataLabsResourceResponse = await _resourceProxyClient.GetCasResponseAsync(
                request: casRequest,
                cancellationToken: default)
                .ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(dataLabsResourceResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessCasResponse);
            Assert.IsNotNull(dataLabsResourceResponse.ErrorResponse);
            Assert.AreEqual(404, dataLabsResourceResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(DataLabsDataSource.CAS, dataLabsResourceResponse.DataSource);
        }

        [TestMethod]
        public async Task ResourceFetcherCasResponse500()
        {
            // Arrange
            var allowedTypesInProxy = "*:cache|write/01:00:00,resourcefetcher_cas|2022-12-01";
            var allowedTypesInFetcher = "GetCasCapacityCheckAsync|2016-12-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetCasResponseAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var httpStatusCode = HttpStatusCode.InternalServerError;
            _resourceProxyFlowTestManager.CasClient.ErrStatusCode = httpStatusCode;
            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();
            var casRequest = new DataLabsCasRequest(
                traceId,
                0,
                correlationId,
                new CasRequestBody
                {
                    ClientAppId = "TestAppId",
                    OfferCategory = "TestOfferCategory",
                    Provider = "Microsoft.Compute",
                    SubscriptionId = Guid.NewGuid().ToString(),
                    SubscriptionRegistrationDate = DateTimeOffset.UtcNow.ToString(),
                    SubscriptionLocationsAndZones = new List<SubscriptionLocationsAndZones>
                    {
                        new() {
                            Location = "eastus",
                            Zones = new List<Zones> { new() { LogicalZone = "1", PhysicalZone = "eastus" } }
                        }
                    },
                    BillingProperties = new BillingProperties
                    {
                        BillingAccount = new BillingAccount
                        {
                            Id = "TestBillingAccountId",
                        },
                        BillingType = "TestBillingType",
                        ChannelType = "TestChannelType",
                        PaymentType = "TestPaymentType",
                        Tier = "TestTier",
                        WorkloadType = "TestWorkloadType",
                    },
                    InternalSubscriptionPolicies = new InternalSubscriptionPolicies
                    {
                        SubscriptionCostCategory = "TestSubscriptionCostCategory",
                        SubscriptionEnvironment = "TestSubscriptionEnvironment",
                        SubscriptionPcCode = "TestSubscriptionPcCode",
                    },
                    EntitlementStartDate = DateTimeOffset.UtcNow.ToString(),
                });

            // Act
            var dataLabsResourceResponse = await _resourceProxyClient.GetCasResponseAsync(
                request: casRequest,
                cancellationToken: default)
                .ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(dataLabsResourceResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessCasResponse);
            Assert.IsNotNull(dataLabsResourceResponse.ErrorResponse);
            Assert.AreEqual((int)httpStatusCode, dataLabsResourceResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(DataLabsDataSource.CAS, dataLabsResourceResponse.DataSource);
        }
    }
}
