using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement;
using Moq;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ServiceBusManagement
{
    [TestClass]
    public class ServiceBusAdminManagerTests
    {
        private const string queueName = "testQueue";
        private Mock<ServiceBusClient> mockReadClient;
        private Mock<ServiceBusClient> mockWriteClient;
        private Mock<ServiceBusAdministrationClient> mockAdministrationClient;
        private ServiceBusAdminManager serviceBusAdminManager;

        [TestInitialize]
        public void Initialize()
        {
            ConfigMapUtil.Reset();
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            ConfigMapUtil.Configuration[SolutionConstants.ServiceBusDLQMaxProcessCount] = "3";
            ConfigMapUtil.Configuration[SolutionConstants.QueueOperationTimeOut] = "120";
            
            mockReadClient = new Mock<ServiceBusClient>();
            mockReadClient.Setup(c => c.FullyQualifiedNamespace).Returns("sb://fakenamespace.servicebus.windows.net/");
            mockWriteClient = new Mock<ServiceBusClient>();
            mockAdministrationClient = new Mock<ServiceBusAdministrationClient>();
            serviceBusAdminManager = new ServiceBusAdminManager(mockReadClient.Object, mockWriteClient.Object, mockAdministrationClient.Object);
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_QueueExists_DeletesSuccessfully()
        {
            var mockResponse = new Mock<Response>();

            mockAdministrationClient.Setup(x => x.QueueExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            mockAdministrationClient.Setup(x => x.DeleteQueueAsync(queueName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockResponse.Object));

            var result = await serviceBusAdminManager.DeleteIfExistsAsync(queueName, CancellationToken.None);

            Assert.IsTrue(result);
            mockAdministrationClient.Verify(x => x.DeleteQueueAsync(queueName, It.IsAny<CancellationToken>()), Times.Once());
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_QueueDoesNotExist_ReturnsFalse()
        {
            mockAdministrationClient.Setup(x => x.QueueExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

            var result = await serviceBusAdminManager.DeleteIfExistsAsync(queueName, CancellationToken.None);

            Assert.IsFalse(result);
            mockAdministrationClient.Verify(x => x.DeleteQueueAsync(queueName, It.IsAny<CancellationToken>()), Times.Never());
        }

        [TestMethod]
        public async Task CreateIfNotExistsAsync_QueueDoesNotExist_CreatesSuccessfully()
        {
            var createQueueOptions = new CreateQueueOptions(queueName);

            mockAdministrationClient.Setup(x => x.QueueExistsAsync(queueName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));
            mockAdministrationClient.Setup(x => x.QueueExistsAsync(queueName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

            var result = await serviceBusAdminManager.CreateIfNotExistsAsync(createQueueOptions, CancellationToken.None);

            Assert.IsTrue(result);
            mockAdministrationClient.Verify(x => x.CreateQueueAsync(createQueueOptions, It.IsAny<CancellationToken>()), Times.Once());
        }

        [TestMethod]
        public async Task CreateIfNotExistsAsync_QueueExists_DoesNotCreate()
        {
            var createQueueOptions = new CreateQueueOptions(queueName);

            mockAdministrationClient.Setup(x => x.QueueExistsAsync(queueName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

            var result = await serviceBusAdminManager.CreateIfNotExistsAsync(createQueueOptions, CancellationToken.None);

            Assert.IsFalse(result);
            mockAdministrationClient.Verify(x => x.CreateQueueAsync(It.IsAny<CreateQueueOptions>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [TestMethod]
        public async Task CreateIfNotExistsAsync_ExceptionThrown_ReturnsFalse()
        {
            var createQueueOptions = new CreateQueueOptions(queueName);

            mockAdministrationClient.Setup(x => x.QueueExistsAsync(queueName, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));
            mockAdministrationClient.Setup(x => x.CreateQueueAsync(createQueueOptions, It.IsAny<CancellationToken>()))
                           .ThrowsAsync(new Exception("Simulated exception"));

            await Assert.ThrowsExceptionAsync<Exception>(() => serviceBusAdminManager.CreateIfNotExistsAsync(createQueueOptions, CancellationToken.None));

            mockAdministrationClient.Verify(x => x.CreateQueueAsync(createQueueOptions, It.IsAny<CancellationToken>()), Times.Exactly(4));
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_ThrowsException_ReturnsFalse()
        {
            mockAdministrationClient.Setup(x => x.QueueExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            mockAdministrationClient.Setup(x => x.DeleteQueueAsync(queueName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated failure"));

            await Assert.ThrowsExceptionAsync<Exception>(() => serviceBusAdminManager.DeleteIfExistsAsync(queueName, CancellationToken.None));
        }

        [TestMethod]
        public async Task DeleteDeadLetterMessagesAsync_CleanUpOldMessages()
        {
            var startedSignal = new TaskCompletionSource<bool>();
            var mockReceiver = new Mock<ServiceBusReceiver>();
            mockReadClient.Setup(x => x.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
                          .Returns(mockReceiver.Object);

            // Simulating the receiver returning messages with specific enqueued times
            var messages = GenerateMessagesWithEnqueuedTimes(DateTime.UtcNow.AddHours(-5), DateTime.UtcNow.AddHours(-1));

            mockReceiver.SetupSequence(x =>
                x.ReceiveMessagesAsync(
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(messages)
                .ReturnsAsync(new List<ServiceBusReceivedMessage>());

            await serviceBusAdminManager.DeleteDeadLetterMessagesAsync("test-queue", 4, startedSignal);

            mockReceiver.Verify(x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()), Times.Once());
            mockReceiver.Verify(x => x.CloseAsync(It.IsAny<CancellationToken>()), Times.Once());
            Assert.IsTrue(startedSignal.Task.Result);
        }

        [TestMethod]
        public async Task DeleteDeadLetterMessagesAsync_NoMessagesRetrieved()
        {
            var startedSignal = new TaskCompletionSource<bool>();
            var mockReceiver = new Mock<ServiceBusReceiver>();
            mockReadClient.Setup(x => x.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
                          .Returns(mockReceiver.Object);

            // Simulating the receiver returning messages with specific enqueued times
            var messages = GenerateMessagesWithEnqueuedTimes();

            mockReceiver.SetupSequence(x =>
                x.ReceiveMessagesAsync(
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(messages)
                .ReturnsAsync(new List<ServiceBusReceivedMessage>());

            await serviceBusAdminManager.DeleteDeadLetterMessagesAsync("test-queue", 4, startedSignal);

            mockReceiver.Verify(x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()), Times.Never());
            mockReceiver.Verify(x => x.CloseAsync(It.IsAny<CancellationToken>()), Times.Once());
            Assert.IsTrue(startedSignal.Task.Result);
        }

        [TestMethod]
        public async Task ReplayDeadLetterMessagesAsync_ResubmitQualifiedMessages()
        {
            var startedSignal = new TaskCompletionSource<bool>();
            var queueRuntimeProperties = ServiceBusModelFactory.QueueRuntimeProperties(
                queueName,
                deadLetterMessageCount: 2
            );

            var mockSender = new Mock<ServiceBusSender>();
            var mockReceiver = new Mock<ServiceBusReceiver>();
            var mockQueueRuntimePropertiesResponse = new Mock<Response<QueueRuntimeProperties>>();
            mockQueueRuntimePropertiesResponse.SetupGet(r => r.Value).Returns(queueRuntimeProperties);
            mockReadClient.Setup(x => x.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
                          .Returns(mockReceiver.Object);
            mockWriteClient.Setup(x => x.CreateSender(It.IsAny<string>()))
                          .Returns(mockSender.Object);
            mockAdministrationClient.Setup(m => m.GetQueueRuntimePropertiesAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
               .ReturnsAsync(mockQueueRuntimePropertiesResponse.Object);

            // Simulating the receiver returning messages with specific enqueued times
            var messages = GenerateMessagesWithEnqueuedTimes(DateTime.UtcNow.AddHours(-5), DateTime.UtcNow.AddHours(-1));

            mockReceiver.SetupSequence(x =>
                x.ReceiveMessagesAsync(
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(messages)
                .ReturnsAsync(new List<ServiceBusReceivedMessage>());

            await serviceBusAdminManager.ReplayDeadLetterMessagesAsync("test-queue", 2, DateTime.UtcNow.ToFileTime(), startedSignal, true, 4);

            mockSender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once());
            mockReceiver.Verify(x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            mockReceiver.Verify(x => x.CloseAsync(It.IsAny<CancellationToken>()), Times.Once());
            Assert.IsTrue(startedSignal.Task.Result);
        }

        [TestMethod]
        public async Task ReplayDeadLetterMessagesAsync_NoMessagesRetrieved()
        {
            var startedSignal = new TaskCompletionSource<bool>();
            var queueRuntimeProperties = ServiceBusModelFactory.QueueRuntimeProperties(
                queueName,
                deadLetterMessageCount: 2
            );

            var mockSender = new Mock<ServiceBusSender>();
            var mockReceiver = new Mock<ServiceBusReceiver>();
            var mockQueueRuntimePropertiesResponse = new Mock<Response<QueueRuntimeProperties>>();
            mockQueueRuntimePropertiesResponse.SetupGet(r => r.Value).Returns(queueRuntimeProperties);
            mockReadClient.Setup(x => x.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
                          .Returns(mockReceiver.Object);
            mockWriteClient.Setup(x => x.CreateSender(It.IsAny<string>()))
                          .Returns(mockSender.Object);
            mockAdministrationClient.Setup(m => m.GetQueueRuntimePropertiesAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
               .ReturnsAsync(mockQueueRuntimePropertiesResponse.Object);

            // Simulating the receiver returning messages with specific enqueued times
            var messages = GenerateMessagesWithEnqueuedTimes();

            mockReceiver.SetupSequence(x =>
                x.ReceiveMessagesAsync(
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(messages)
                .ReturnsAsync(new List<ServiceBusReceivedMessage>());

            await serviceBusAdminManager.ReplayDeadLetterMessagesAsync("test-queue", 2, DateTime.UtcNow.ToFileTime(), startedSignal, true, 4);

            mockSender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never());
            mockReceiver.Verify(x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()), Times.Never());
            mockReceiver.Verify(x => x.CloseAsync(It.IsAny<CancellationToken>()), Times.Once());
            Assert.IsTrue(startedSignal.Task.Result);
        }

        private List<ServiceBusReceivedMessage> GenerateMessagesWithEnqueuedTimes(params DateTime[] enqueuedTimes)
        {
            var messages = new List<ServiceBusReceivedMessage>();
            foreach (var time in enqueuedTimes)
            {
                var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                    enqueuedTime: time,
                    properties: new Dictionary<string, object>
                    {
                        ["dlqProcessCount"] = 1
                    });
                messages.Add(message);
            }
            return messages;
        }
    }
}