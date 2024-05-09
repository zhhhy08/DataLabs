namespace Microsoft.Azure.ARMDataInsights.Tests.ArmDataCacheIngestionService
{
    using System;
    using System.Threading.Tasks;
    using global::Tests.ArmDataCacheIngestionService.Helpers;
    using Microsoft.Azure.ARMDataInsights.ArmDataCacheService;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class ArmDataCacheIngestionServiceTests
    {
        Mock<ICacheClient> cacheClient = new Mock<ICacheClient>();
        Mock<ILoggerFactory> loggerFactory  = new Mock<ILoggerFactory>();
        Mock<IResourceProxyClient> resourceProxyClient = new Mock<IResourceProxyClient>();
        Mock<ILogger> mockLogger = new Mock<ILogger>();
        Mock<ILoggerFactory> mockLoggerFactory = new Mock<ILoggerFactory>();

        ArmDataCacheIngestionService cacheIngestionService = new ArmDataCacheIngestionService();

       
        [TestInitialize]
        public void Setup()
        {
            cacheIngestionService.SetCacheClient(cacheClient.Object);
            cacheIngestionService.SetLoggerFactory(loggerFactory.Object);
            cacheIngestionService.SetResourceProxyClient(resourceProxyClient.Object);
            mockLogger.Setup(
                m => m.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<object>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<object, Exception?, string>>()));
            mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(() => mockLogger.Object);
        }

        [TestMethod]
        public async Task GetResponseAsyncSubscriptionIngestionSuccess()
        {
            EventGridNotification<NotificationDataV3<GenericResource>>? inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.subInternalPropEvent);

            cacheClient.Setup(x => x.SortedSetAddAsync(It.IsAny<string>(), inputResource!.Id.GetSubscriptionId(), It.IsAny<double>(), CancellationToken.None)).Returns(Task.FromResult(true));

            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now, 
                                                    "traceId", 
                                                    1, 
                                                    "correlationID", 
                                                    inputResource!, 
                                                    null);

            var response = await cacheIngestionService.GetResponseAsync(request, CancellationToken.None);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.IsNull(response.ErrorResponse);
        }

        [TestMethod]
        public async Task GetResponseAsyncDeletedSubscriptionRemove()
        {
            EventGridNotification<NotificationDataV3<GenericResource>>? inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.subInternalPropDeleteEvent);
            double? score = 20;
            cacheClient.Setup(x => x.SortedSetScoreAsync(It.IsAny<string>(), inputResource!.Id.GetSubscriptionId(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(score));
            cacheClient.Setup(x => x.SortedSetRemoveAsync(It.IsAny<string>(), inputResource!.Id.GetSubscriptionId(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));

            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    1,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await cacheIngestionService.GetResponseAsync(request, CancellationToken.None);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.IsNull(response.ErrorResponse);
        }

        [TestMethod]
        public async Task GetResponseAsyncSubscriptionExistsIngestionSuccess()
        {
            EventGridNotification<NotificationDataV3<GenericResource>>? inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.subInternalPropEvent);

            double? score = 20;
            cacheClient.Setup(x => x.SortedSetScoreAsync(It.IsAny<string>(), inputResource!.Id.GetSubscriptionId(), CancellationToken.None)).Returns(Task.FromResult(score));
            cacheClient.Setup(x => x.SortedSetAddAsync(It.IsAny<string>(), inputResource!.Id.GetSubscriptionId(), It.IsAny<double>(), CancellationToken.None)).Returns(Task.FromResult(true));

            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    1,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await cacheIngestionService.GetResponseAsync(request, CancellationToken.None);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.IsNull(response.ErrorResponse);
        }

        [TestMethod]
        public async Task GetResponseAsyncSubscriptionIngestionFailed()
        {
            EventGridNotification<NotificationDataV3<GenericResource>>? inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.subInternalPropEvent);

            cacheClient.Setup(x => x.SortedSetAddAsync(It.IsAny<string>(), inputResource!.Id.GetSubscriptionId(), It.IsAny<double>(), CancellationToken.None)).Returns(Task.FromResult(false));

            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    1,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await cacheIngestionService.GetResponseAsync(request, CancellationToken.None);
            Assert.IsNotNull(response);
            Assert.IsNull(response.SuccessResponse);
            Assert.IsNotNull(response.ErrorResponse);
        }

        [TestMethod]
        public async Task GetResponseAsyncGlobalSkuIngestionSuccess()
        {
            EventGridNotification<NotificationDataV3<GenericResource>>? inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.globalSku);

            cacheClient.Setup(x => x.GetValueAsync(inputResource!.Id, CancellationToken.None)).Returns(Task.FromResult<byte[]?>(null));
            cacheClient.Setup(x => x.SetValueIfGreaterThanWithExpiryAsync(inputResource!.Id, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<long>(), TimeSpan.FromHours(60), CancellationToken.None)).Returns(Task.FromResult(true));
            cacheClient.Setup(x => x.SortedSetAddAsync("Microsoft.Compute", It.IsAny<string>(), It.IsAny<double>(), CancellationToken.None)).Returns(Task.FromResult(true));

            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    1,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await cacheIngestionService.GetResponseAsync(request, CancellationToken.None);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SuccessResponse);
            Assert.IsNull(response.ErrorResponse);
        }

        [TestMethod]
        public async Task GetResponseAsyncGlobalSkuIngestionFailed()
        {
            EventGridNotification<NotificationDataV3<GenericResource>>? inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(Datasets.globalSku);
            cacheClient.Setup(x => x.GetValueAsync(inputResource!.Id, CancellationToken.None)).Returns(Task.FromResult<byte[]?>(null));
            cacheClient.Setup(x => x.SetValueIfGreaterThanWithExpiryAsync(inputResource!.Id, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<long>(), TimeSpan.FromHours(60), CancellationToken.None)).Returns(Task.FromResult(false));

            DataLabsARNV3Request request = new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    1,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);

            var response = await cacheIngestionService.GetResponseAsync(request, CancellationToken.None);
            Assert.IsNotNull(response);
            Assert.IsNull(response.SuccessResponse);
            Assert.IsNotNull(response.ErrorResponse);
        }

        [TestMethod]
        public void TestGetTraceSourceAndMeterNames()
        {
            var traceSrcList = cacheIngestionService.GetTraceSourceNames();
            Assert.AreEqual(traceSrcList.ElementAt(0), ArmDataCacheIngestionService.PartnerActivitySourceName);

            var meterNames = cacheIngestionService.GetMeterNames();
            Assert.AreEqual(meterNames.ElementAt(0), ArmDataCacheIngestionService.PartnerMeterName);

            var cusMeterNames = cacheIngestionService.GetCustomerMeterNames();
            Assert.AreEqual(cusMeterNames.ElementAt(0), ArmDataCacheIngestionService.PartnerMeterName);

            var loggerTableNames = cacheIngestionService.GetLoggerTableNames();
            Assert.AreEqual(loggerTableNames.GetValueOrDefault(ArmDataCacheIngestionService.LoggerTable), ArmDataCacheIngestionService.LoggerTable);
        }
    }
}
