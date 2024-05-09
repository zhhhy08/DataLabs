namespace Tests.SkuService.Common.DataProviders
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Moq;
    using global::SkuService.Common.DataProviders;
    using global::SkuService.Common.Utilities;
    using static global::SkuService.Common.Models.Enums;
    using global::SkuService.Common.Models.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Newtonsoft.Json.Linq;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;

    [TestClass]
    public class SubscriptionDataProviderTests
    {
        readonly Mock<IResourceProxyClient> resourceProxyClient = new();
        readonly Mock<ICacheClient> cacheClient = new();
        readonly Mock<IActivity> mockActivity = new();
        readonly Mock<ISkuServiceProvider> serviceProvider = new();

        [TestCleanup]
        public void Cleanup()
        {
            resourceProxyClient.Reset();
            cacheClient.Reset();
        }

        [TestInitialize]
        public void Initialize()
        {
            serviceProvider.Setup(x => x.GetServiceName()).Returns("SkuService");
        }

        [TestMethod]
        public async Task GetSubscriptionInternalPropertiesAsync_WithValidInput_ReturnsData()
        {
            // Arrange
            var subscriptionId = Guid.NewGuid().ToString();
            var request = new DataLabsResourceRequest(string.Empty, 0, string.Empty, string.Format(Constants.SubscriptionInternalPropertiesResourceId, subscriptionId), string.Empty);
            var successResponse = new DataLabsResourceCollectionSuccessResponse([ new()
            {
                ApiVersion = "2019-10-01",
                DisplayName = "SubscriptionInternalProperties",
                Id = string.Format(Constants.SubscriptionInternalPropertiesResourceId, subscriptionId),
                Properties = JObject.FromObject(new SubscriptionInternalPropertiesModel()
                {
                    SubscriptionId = subscriptionId,
                    OfferType = SubscriptionOfferType.Buy,
                }),
            },
            ]);
            var response = new DataLabsResourceCollectionResponse(DateTimeOffset.MinValue, string.Empty, successResponse, null, null, DataLabsDataSource.ARM);
            resourceProxyClient.Setup(x => x.GetCollectionAsync(It.IsAny<DataLabsResourceRequest>(), CancellationToken.None, true, false, null, null))
                .Returns(Task.FromResult(response));

            var subscriptionDataProvider = new SubscriptionDataProvider(resourceProxyClient.Object, cacheClient.Object, serviceProvider.Object);
            // Act
            var result = await subscriptionDataProvider.GetSubscriptionInternalPropertiesAsync(subscriptionId, mockActivity.Object, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(subscriptionId, result.SubscriptionId);
            Assert.AreEqual(result.OfferType, result.OfferType);
            resourceProxyClient.VerifyAll();
        }

        [TestMethod]
        public async Task GetSubscriptionInternalPropertiesAsync_WithInvalidInput_ThrowsException()
        {
            // Arrange
            var subscriptionId = Guid.NewGuid().ToString();
            var request = new DataLabsResourceRequest(string.Empty, 0, string.Empty, string.Format(Constants.SubscriptionInternalPropertiesResourceId, subscriptionId), string.Empty);
            resourceProxyClient.Setup(x => x.GetCollectionAsync(It.IsAny<DataLabsResourceRequest>(), CancellationToken.None, true, false, null, null));

            var subscriptionDataProvider = new SubscriptionDataProvider(resourceProxyClient.Object, cacheClient.Object, serviceProvider.Object);

            // Assert
            await Assert.ThrowsExceptionAsync<NullReferenceException>(async () => await subscriptionDataProvider.GetSubscriptionInternalPropertiesAsync(subscriptionId, mockActivity.Object, CancellationToken.None));
            resourceProxyClient.VerifyAll();
        }

        [TestMethod]
        public async Task GetSubscriptionMappingsAsync_WithValidInput_ReturnsData()
        {
            // Arrange
            var subscriptionId = Guid.NewGuid().ToString();
            var request = new DataLabsResourceRequest(string.Empty, 0, string.Empty, string.Format(Constants.SubscriptionMappingsResourceId, subscriptionId), string.Empty);
            var successResponse = new DataLabsResourceCollectionSuccessResponse([ new()
            {
                ApiVersion = "2019-10-01",
                DisplayName = "SubscriptionZoneMappings",
                Id = string.Format(Constants.SubscriptionMappingsResourceId, subscriptionId),
                Properties = JObject.FromObject(new SubscriptionMappingsModel()
                {
                    SubscriptionId = subscriptionId,
                }),
            },
            ]);
            var response = new DataLabsResourceCollectionResponse(DateTimeOffset.MinValue, string.Empty, successResponse, null, null, DataLabsDataSource.ARM);
            resourceProxyClient.Setup(x => x.GetCollectionAsync(It.IsAny<DataLabsResourceRequest>(), CancellationToken.None, true, false, null, null))
                .Returns(Task.FromResult(response));

            var subscriptionDataProvider = new SubscriptionDataProvider(resourceProxyClient.Object, cacheClient.Object, serviceProvider.Object);
            // Act
            var result = await subscriptionDataProvider.GetSubscriptionMappingsAsync(subscriptionId, mockActivity.Object, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(subscriptionId, result.SubscriptionId);
            resourceProxyClient.VerifyAll();
        }

        [TestMethod]
        public async Task GetSubscriptionFeatureRegistrationPropertiesAsync_WithValidInput_ReturnsData()
        {
            // Arrange
            var subscriptionId = Guid.NewGuid().ToString();

            var request = new DataLabsResourceRequest(string.Empty, 0, string.Empty, string.Format(Constants.SubscriptionFeatureRegistrationResourceId, subscriptionId, "Microsoft.Compute"), string.Empty);

            var featureRegistrationString = JObject.FromObject(

                        new SubscriptionFeatureRegistrationPropertiesModel()
                        {
                            SubscriptionId = subscriptionId,
                        }
                    );

            var successResponse = new DataLabsResourceCollectionSuccessResponse(new List<GenericResource>
            {
                new()
                {
                    ApiVersion = "2019-10-01",
                    DisplayName = "SubscriptionFeatureRegistration",
                    Id = string.Format(Constants.SubscriptionMappingsResourceId, subscriptionId),
                    Properties = featureRegistrationString
                }
            });

            var response = new DataLabsResourceCollectionResponse(DateTimeOffset.MinValue, string.Empty, successResponse, null, null);
            resourceProxyClient.Setup(x => x.GetCollectionAsync(It.IsAny<DataLabsResourceRequest>(), CancellationToken.None, true, false, null, null))
                .Returns(Task.FromResult(response));

            var subscriptionDataProvider = new SubscriptionDataProvider(resourceProxyClient.Object, cacheClient.Object, serviceProvider.Object);

            // Act
            var result = await subscriptionDataProvider.GetSubscriptionFeatureRegistrationPropertiesAsync(subscriptionId, "Microsoft.Compute", mockActivity.Object, CancellationToken.None);


            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(subscriptionId, result.First().SubscriptionId);
            resourceProxyClient.VerifyAll();
        }

        [TestMethod]
        public async Task GetSubscriptionRegistrationsAsync_WithValidInput_ReturnsData()
        {
            // Arrange
            var subscriptionId = Guid.NewGuid().ToString();
            var registrationDate = DateTime.UtcNow.ToString();
            var resourceProvider = "Microsoft.Compute";
            var request = new DataLabsResourceRequest(string.Empty, 0, string.Empty, string.Format(Constants.SubscriptionRegistrationsResourceId, subscriptionId, resourceProvider), string.Empty);
            var successResponse = new DataLabsResourceCollectionSuccessResponse([ new GenericResource()
            {
                ApiVersion = "2019-10-01",
                DisplayName = "SubscriptionRegistrations",
                Id = string.Format(Constants.SubscriptionMappingsResourceId, subscriptionId),
                Properties = JObject.FromObject(new SubscriptionRegistrationModel()
                {
                    RegistrationDate = registrationDate,
                    ResourceProviderNamespace = resourceProvider,
                    RegistrationState = Microsoft.WindowsAzure.ResourceStack.Common.Core.Definitions.SubscriptionRegistrationState.Registered,
                }),
            },
            ]);
            var response = new DataLabsResourceCollectionResponse(DateTimeOffset.MinValue, string.Empty, successResponse, null, null, DataLabsDataSource.ARM);
            resourceProxyClient.Setup(x => x.GetCollectionAsync(It.IsAny<DataLabsResourceRequest>(), CancellationToken.None, true, false, null, null))
                .Returns(Task.FromResult(response));

            var subscriptionDataProvider = new SubscriptionDataProvider(resourceProxyClient.Object, cacheClient.Object, serviceProvider.Object);
            // Act
            var result = await subscriptionDataProvider.GetSubscriptionRegistrationAsync(subscriptionId, resourceProvider, mockActivity.Object, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(registrationDate, result.RegistrationDate);
            resourceProxyClient.VerifyAll();
        }

        [TestMethod]
        public async Task GetSubscriptionRegistrationsAsync_WithNoCRP_ReturnsMaxDate()
        {
            // Arrange
            var subscriptionId = Guid.NewGuid().ToString();
            var registrationDate = DateTime.MaxValue.ToString();
            var resourceProvider = "Microsoft.Kusto";
            var request = new DataLabsResourceRequest(string.Empty, 0, string.Empty, string.Format(Constants.SubscriptionRegistrationsResourceId, subscriptionId, resourceProvider), string.Empty);
            var successResponse = new DataLabsResourceCollectionSuccessResponse([ new GenericResource()
            {
                ApiVersion = "2019-10-01",
                DisplayName = "SubscriptionRegistrations",
                Id = string.Format(Constants.SubscriptionMappingsResourceId, subscriptionId),
                Properties = JObject.FromObject(new SubscriptionRegistrationModel()
                {
                    RegistrationDate = registrationDate,
                    ResourceProviderNamespace = resourceProvider,
                    RegistrationState = Microsoft.WindowsAzure.ResourceStack.Common.Core.Definitions.SubscriptionRegistrationState.Registered,
                }),
            },
            ]);
            var response = new DataLabsResourceCollectionResponse(DateTimeOffset.MinValue, string.Empty, successResponse, null, null, DataLabsDataSource.ARM);
            resourceProxyClient.Setup(x => x.GetCollectionAsync(It.IsAny<DataLabsResourceRequest>(), CancellationToken.None, true, false, null, null))
                .Returns(Task.FromResult(response));

            var subscriptionDataProvider = new SubscriptionDataProvider(resourceProxyClient.Object, cacheClient.Object, serviceProvider.Object);
            // Act
            var result = await subscriptionDataProvider.GetSubscriptionRegistrationAsync(subscriptionId, resourceProvider, mockActivity.Object, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(registrationDate, result.RegistrationDate);
            resourceProxyClient.VerifyAll();
        }

        [TestMethod]
        public async Task GetSubscriptionRegistrationsAsync_MissingResource_ReturnsMaxDate()
        {
            // Arrange
            var subscriptionId = Guid.NewGuid().ToString();
            var registrationDate = DateTime.MaxValue.ToString();
            var resourceProvider = "Microsoft.Compute";
            var request = new DataLabsResourceRequest(string.Empty, 0, string.Empty, string.Format(Constants.SubscriptionRegistrationsResourceId, subscriptionId, resourceProvider), string.Empty);
            var successResponse = new DataLabsResourceCollectionSuccessResponse([]);
            var response = new DataLabsResourceCollectionResponse(DateTimeOffset.MinValue, string.Empty, successResponse, null, null, DataLabsDataSource.ARM);
            resourceProxyClient.Setup(x => x.GetCollectionAsync(It.IsAny<DataLabsResourceRequest>(), CancellationToken.None, true, false, null, null))
                .Returns(Task.FromResult(response));

            var subscriptionDataProvider = new SubscriptionDataProvider(resourceProxyClient.Object, cacheClient.Object, serviceProvider.Object);
            // Act
            var result = await subscriptionDataProvider.GetSubscriptionRegistrationAsync(subscriptionId, resourceProvider, mockActivity.Object, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(registrationDate, result.RegistrationDate);
            resourceProxyClient.VerifyAll();
        }
    }
}
