using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.Controller
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Controller;
    using Moq;
    using System.Reflection;
    using System.Threading;

    [TestClass]
    public class AdminServiceBusControllerTests
    {
        [TestInitialize]
        public void Setup()
        {
            ConfigMapUtil.Reset();
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
        }

        [TestMethod]
        public async Task TestDeleteAndRecreateServiceBusQueue_Returns_SuccessResponse()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var expectedReponse = $"Service Bus Queue {queueName} was successfully deleted and recreated.";

            var mockReadClient = new Mock<ServiceBusClient>();
            mockReadClient.Setup(c => c.FullyQualifiedNamespace).Returns("sb://fakenamespace.servicebus.windows.net/");
            var mockWriteClient = new Mock<ServiceBusClient>();
            var mockAdministrationClient = new Mock<ServiceBusAdministrationClient>();

            var mockManager = new Mock<ServiceBusAdminManager>(mockReadClient.Object, mockWriteClient.Object, mockAdministrationClient.Object);
            mockManager.Setup(x => x.DeleteIfExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.FromResult(true));
            mockManager.Setup(x => x.CreateRetryQueueWriter(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.FromResult(true));
            var retryQueueNameProperty = typeof(ServiceBusAdminManager).GetProperty("RetryQueueName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            retryQueueNameProperty.SetValue(mockManager.Object, queueName);

            var controller = new AdminServiceBusController(ConfigMapUtil.Configuration, new List<ServiceBusAdminManager> { mockManager.Object });
            var result = await controller.DeleteAndRecreateServiceBusQueue(queueName).ConfigureAwait(false);

            Assert.AreEqual(expectedReponse, result);
            mockManager.Verify(x => x.DeleteIfExistsAsync(queueName, It.IsAny<CancellationToken>()), Times.Once);
            mockManager.Verify(x => x.CreateRetryQueueWriter(queueName, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task TestDeleteAndRecreateServiceBusQueue_Request_Fails()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var expectedExceptionMessage = "DeleteIfExistsAsync got exception";

            var mockReadClient = new Mock<ServiceBusClient>();
            mockReadClient.Setup(c => c.FullyQualifiedNamespace).Returns("sb://fakenamespace.servicebus.windows.net/");
            var mockWriteClient = new Mock<ServiceBusClient>();
            var mockAdministrationClient = new Mock<ServiceBusAdministrationClient>();

            var mockManager = new Mock<ServiceBusAdminManager>(mockReadClient.Object, mockWriteClient.Object, mockAdministrationClient.Object);
            mockManager.Setup(x => x.DeleteIfExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new Exception(expectedExceptionMessage));
            var retryQueueNameProperty = typeof(ServiceBusAdminManager).GetProperty("RetryQueueName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            retryQueueNameProperty.SetValue(mockManager.Object, queueName);

            var controller = new AdminServiceBusController(ConfigMapUtil.Configuration, new List<ServiceBusAdminManager> { mockManager.Object });
            var result = await controller.DeleteAndRecreateServiceBusQueue(queueName).ConfigureAwait(false);

            Assert.AreEqual(expectedExceptionMessage, result);
            mockManager.Verify(x => x.DeleteIfExistsAsync(queueName, It.IsAny<CancellationToken>()), Times.Once);
            mockManager.Verify(x => x.CreateRetryQueueWriter(queueName, It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public void TestDeleteDeadLetterMessages_Returns_SuccessResponse()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var deleteLookBackHours = 2;
            var expectedReponse = $"Task for deleting dead letter messages older than {deleteLookBackHours} hours ago has started.";

            var mockReadClient = new Mock<ServiceBusClient>();
            mockReadClient.Setup(c => c.FullyQualifiedNamespace).Returns("sb://fakenamespace.servicebus.windows.net/");
            var mockWriteClient = new Mock<ServiceBusClient>();
            var mockAdministrationClient = new Mock<ServiceBusAdministrationClient>();

            var mockManager = new Mock<ServiceBusAdminManager>(mockReadClient.Object, mockWriteClient.Object, mockAdministrationClient.Object);
            mockManager.Setup(x => x.DeleteDeadLetterMessagesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TaskCompletionSource<bool>>()))
                       .Returns(Task.CompletedTask)
                       .Callback<string, int, TaskCompletionSource<bool>>((qName, hours, tcs) => tcs.SetResult(true));
            var retryQueueNameProperty = typeof(ServiceBusAdminManager).GetProperty("RetryQueueName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            retryQueueNameProperty.SetValue(mockManager.Object, queueName);

            var controller = new AdminServiceBusController(ConfigMapUtil.Configuration, new List<ServiceBusAdminManager> { mockManager.Object });
            var result = controller.DeleteDeadLetterMessages(queueName, deleteLookBackHours);

            Assert.AreEqual(expectedReponse, result);
            mockManager.Verify(x => x.DeleteDeadLetterMessagesAsync(queueName, deleteLookBackHours, It.IsAny<TaskCompletionSource<bool>>()), Times.Once);
        }

        [TestMethod]
        public void TestDeleteDeadLetterMessages_Request_Fails()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var deleteLookBackHours = 2;
            var expectedExceptionMessage = "Deleting DLQ messages is already in progress";

            var mockReadClient = new Mock<ServiceBusClient>();
            mockReadClient.Setup(c => c.FullyQualifiedNamespace).Returns("sb://fakenamespace.servicebus.windows.net/");
            var mockWriteClient = new Mock<ServiceBusClient>();
            var mockAdministrationClient = new Mock<ServiceBusAdministrationClient>();

            var mockManager = new Mock<ServiceBusAdminManager>(mockReadClient.Object, mockWriteClient.Object, mockAdministrationClient.Object);
            mockManager.Setup(x => x.DeleteDeadLetterMessagesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TaskCompletionSource<bool>>()))
                       .Callback<string, int, TaskCompletionSource<bool>>((qName, hours, tcs) => {
                           tcs.SetResult(true);
                           throw new Exception(expectedExceptionMessage);
                       });
            var retryQueueNameProperty = typeof(ServiceBusAdminManager).GetProperty("RetryQueueName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            retryQueueNameProperty.SetValue(mockManager.Object, queueName);

            var controller = new AdminServiceBusController(ConfigMapUtil.Configuration, new List<ServiceBusAdminManager> { mockManager.Object });
            var result = controller.DeleteDeadLetterMessages(queueName, deleteLookBackHours);

            Assert.AreEqual(expectedExceptionMessage, result);
            mockManager.Verify(x => x.DeleteDeadLetterMessagesAsync(queueName, deleteLookBackHours, It.IsAny<TaskCompletionSource<bool>>()), Times.Once);
        }

        [TestMethod]
        public void TestReplayDeadLetterMessages_Returns_SuccessResponse()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var replayLookBackHours = 2;
            var deleteLookBackHours = 2;
            var utcNowFileTime = DateTime.UtcNow.ToFileTime();
            var expectedReponse = $"Task for replaying dead letter messages in the past {replayLookBackHours} hours has started. Task for deleting dead letter messages older than {deleteLookBackHours} hours ago has started.";

            var mockReadClient = new Mock<ServiceBusClient>();
            mockReadClient.Setup(c => c.FullyQualifiedNamespace).Returns("sb://fakenamespace.servicebus.windows.net/");
            var mockWriteClient = new Mock<ServiceBusClient>();
            var mockAdministrationClient = new Mock<ServiceBusAdministrationClient>();

            var mockManager = new Mock<ServiceBusAdminManager>(mockReadClient.Object, mockWriteClient.Object, mockAdministrationClient.Object);
            mockManager.Setup(x => x.ReplayDeadLetterMessagesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<TaskCompletionSource<bool>>(), It.IsAny<bool>(), It.IsAny<int>()))
                       .Returns(Task.CompletedTask)
                       .Callback<string, int, long, TaskCompletionSource<bool>, bool, int>((qName, hours, utcNow, tcs, nDelete, dHours) => tcs.SetResult(true));
            var retryQueueNameProperty = typeof(ServiceBusAdminManager).GetProperty("RetryQueueName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            retryQueueNameProperty.SetValue(mockManager.Object, queueName);

            var controller = new AdminServiceBusController(ConfigMapUtil.Configuration, new List<ServiceBusAdminManager> { mockManager.Object });
            var result = controller.ReplayDeadLetterMessages(queueName, replayLookBackHours, utcNowFileTime, true, deleteLookBackHours);

            Assert.AreEqual(expectedReponse, result);
            mockManager.Verify(x => x.ReplayDeadLetterMessagesAsync(queueName, deleteLookBackHours, utcNowFileTime, It.IsAny<TaskCompletionSource<bool>>(), true, 2), Times.Once);
        }

        [TestMethod]
        public void TestReplayDeadLetterMessages_Request_Fails()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var replayLookBackHours = 2;
            var deleteLookBackHours = 2;
            var utcNowFileTime = DateTime.UtcNow.ToFileTime();
            var expectedExceptionMessage = "Replaying DLQ messages is already in progress";

            var mockReadClient = new Mock<ServiceBusClient>();
            mockReadClient.Setup(c => c.FullyQualifiedNamespace).Returns("sb://fakenamespace.servicebus.windows.net/");
            var mockWriteClient = new Mock<ServiceBusClient>();
            var mockAdministrationClient = new Mock<ServiceBusAdministrationClient>();

            var mockManager = new Mock<ServiceBusAdminManager>(mockReadClient.Object, mockWriteClient.Object, mockAdministrationClient.Object);
            mockManager.Setup(x => x.ReplayDeadLetterMessagesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<TaskCompletionSource<bool>>(), It.IsAny<bool>(), It.IsAny<int>()))
                       .Callback<string, int, long, TaskCompletionSource<bool>, bool, int>((qName, hours, utcNow, tcs, nDelete, dHours) => {
                           tcs.SetResult(true);
                           throw new Exception(expectedExceptionMessage);
                       });
            var retryQueueNameProperty = typeof(ServiceBusAdminManager).GetProperty("RetryQueueName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            retryQueueNameProperty.SetValue(mockManager.Object, queueName);

            var controller = new AdminServiceBusController(ConfigMapUtil.Configuration, new List<ServiceBusAdminManager> { mockManager.Object });
            var result = controller.ReplayDeadLetterMessages(queueName, replayLookBackHours, utcNowFileTime, true, deleteLookBackHours);

            Assert.AreEqual(expectedExceptionMessage, result);
            mockManager.Verify(x => x.ReplayDeadLetterMessagesAsync(queueName, deleteLookBackHours, utcNowFileTime, It.IsAny<TaskCompletionSource<bool>>(), true, 2), Times.Once);
        }
    }
}