namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.AdminService
{
    using k8s.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Controllers;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Moq;
    using Moq.Protected;
    using System.ServiceModel;

    [TestClass]
    public class AdminDataLabsControllerTests
    {
        public class BasicResultsKubernetesWrapper : IKubernetesWrapper
        {
            public Task<V1Pod> DeleteNamespacedPodAsync(string podName, string podNamespace)
            {
                return Task.FromResult(new V1Pod());
            }

            public Task<V1ConfigMapList> ListConfigMapForAllNamespacesAsync()
            {
                return Task.FromResult(new V1ConfigMapList());
            }

            public Task<V1PodList> ListNamespacedPodAsync(string podNamespace)
            {
                return Task.FromResult(new V1PodList());
            }

            public Task<V1PodList> ListPodForAllNamespacesAsync()
            {
                return Task.FromResult(new V1PodList());
            }

            public Task<V1ConfigMap> PatchNamespacedConfigMapAsync(V1Patch configMapOverridePatch, string configMapName, string configMapNamespace)
            {
                return Task.FromResult(new V1ConfigMap());
            }

            public Task<V1DaemonSet> ReadNamespacedDaemonSetAsync(string daemonSetName, string daemonSetNamespace)
            {
                return Task.FromResult(new V1DaemonSet());
            }

            public Task<V1Deployment> ReadNamespacedDeploymentAsync(string deploymentName, string deploymentNamespace)
            {
                return Task.FromResult(new V1Deployment());
            }

            public Task<Stream> ReadNamespacedPodLogAsync(string podNamespace, string podName, string containerName)
            {
                return Task.FromResult(Stream.Null);
            }

            public Task<V1DaemonSet> ReplaceNamespacedDaemonSetAsync(V1DaemonSet daemonSet, string daemonSetName, string daemonSetNamespace)
            {
                return Task.FromResult(daemonSet);
            }

            public Task<V1Deployment> ReplaceNamespacedDeploymentAsync(V1Deployment deployment, string deploymentName, string deploymentNamespace)
            {
                return Task.FromResult(deployment);
            }

            public Task<V1EndpointSliceList> ListNamespacedEndpointSliceAsync(string podNamespace)
            {
                return Task.FromResult(new V1EndpointSliceList
                {
                    Items = new List<V1EndpointSlice>
                    {
                        new V1EndpointSlice
                        {
                            Metadata = new V1ObjectMeta
                            {
                                Name = "solution-io-example"
                            },
                            Endpoints = new List<V1Endpoint>
                            {
                                new V1Endpoint
                                {
                                    Addresses = new List<string> { "192.168.1.1" }, // Example IP address
                                    Conditions = new V1EndpointConditions
                                    {
                                        Ready = true
                                    },
                                    Hostname = "endpoint-hostname",
                                    TargetRef = new V1ObjectReference
                                    {
                                        Kind = "Pod",
                                        NamespaceProperty = podNamespace,
                                        Name = "pod-name",
                                        Uid = "pod-uid"
                                    }
                                }
                            }
                        }
                    }
                });
            }
        }

        public const string kubernetesExceptionString = "exception here!";

        public class ExceptionKubernetesWrapper : IKubernetesWrapper
        {
            public Task<V1Pod> DeleteNamespacedPodAsync(string podName, string podNamespace)
            {
                throw new Exception(kubernetesExceptionString);
            }

            public Task<V1ConfigMapList> ListConfigMapForAllNamespacesAsync()
            {
                throw new Exception(kubernetesExceptionString);
            }

            public Task<V1PodList> ListNamespacedPodAsync(string podNamespace)
            {
                throw new Exception(kubernetesExceptionString);
            }

            public Task<V1PodList> ListPodForAllNamespacesAsync()
            {
                throw new Exception(kubernetesExceptionString);
            }

            public Task<V1ConfigMap> PatchNamespacedConfigMapAsync(V1Patch configMapOverridePatch, string configMapName, string configMapNamespace)
            {
                throw new Exception(kubernetesExceptionString);
            }

            public Task<V1DaemonSet> ReadNamespacedDaemonSetAsync(string daemonSetName, string daemonSetNamespace)
            {
                throw new Exception(kubernetesExceptionString);
            }

            public Task<V1Deployment> ReadNamespacedDeploymentAsync(string deploymentName, string deploymentNamespace)
            {
                throw new Exception(kubernetesExceptionString);
            }

            public Task<Stream> ReadNamespacedPodLogAsync(string podNamespace, string podName, string containerName)
            {
                throw new Exception(kubernetesExceptionString);
            }

            public Task<V1DaemonSet> ReplaceNamespacedDaemonSetAsync(V1DaemonSet daemonSet, string daemonSetName, string daemonSetNamespace)
            {
                throw new Exception(kubernetesExceptionString);
            }

            public Task<V1Deployment> ReplaceNamespacedDeploymentAsync(V1Deployment deployment, string deploymentName, string deploymentNamespace)
            {
                throw new Exception(kubernetesExceptionString);
            }

            public Task<V1EndpointSliceList> ListNamespacedEndpointSliceAsync(string podNamespace)
            {
                throw new Exception(kubernetesExceptionString);
            }
        }

        [TestInitialize]
        public void Setup()
        {
            ConfigMapUtil.Reset();
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
        }

        [TestMethod]
        [DataRow(AdminDataLabsController.DLService.IOService, AdminConstants.IOServiceAdminEndpoint, "http://solution-io.solution-namespace.svc.cluster.local:7072")]
        [DataRow(AdminDataLabsController.DLService.ResourceProxy, AdminConstants.ResourceProxyAdminEndpoint, "http://resource-proxy-admin-service.solution-namespace.svc.cluster.local:7072")]
        [DataRow(AdminDataLabsController.DLService.ResourceFetcherService, AdminConstants.ResourceFetcherAdminEndpoint, "http://resource-fetcher-admin-service.resource-fetcher-namespace.svc.cluster.local:7072")]
        public async Task TestAdminDataLabsControllerGetConfiguration(AdminDataLabsController.DLService service, string endpointType, string endpointValue)
        {
            ConfigMapUtil.Configuration[endpointType] = endpointValue;

            var expectedResult = "expectedResult";
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).ReturnsAsync(new HttpResponseMessage {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(expectedResult)
                });
            var providerClass = new Mock<IKubernetesProvider>();

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(mockHttpMessageHandler.Object), providerClass.Object);

            var result = await controller.GetConfiguration(service.FastEnumToString(), "randomValue").ConfigureAwait(false);

            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
            Assert.AreEqual(expectedResult, ((OkObjectResult)result.Result).Value);
        }

        [TestMethod]
        public async Task TestAdminDataLabsControllerGetConfigurationBadHttpClient()
        {
            ConfigMapUtil.Configuration[AdminConstants.ResourceFetcherAdminEndpoint] = "resource-fetcher.endpoint";

            var expectedExceptionMessage = "expectedResult";
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).ThrowsAsync(new Exception(expectedExceptionMessage));
            var providerClass = new Mock<IKubernetesProvider>();

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(mockHttpMessageHandler.Object), providerClass.Object);

            var result = await controller.GetConfiguration(
                AdminDataLabsController.DLService.ResourceFetcherService.FastEnumToString(), 
                "randomValue").ConfigureAwait(false);

            Assert.IsInstanceOfType(result.Result, typeof(ObjectResult));
            var exception = (Exception)((ObjectResult)result.Result).Value;
            Assert.AreEqual(expectedExceptionMessage, exception.Message);
        }

        [TestMethod]
        [DataRow(AdminDataLabsController.DLService.IOService, "ResourceFetcherAKS")]
        [DataRow(AdminDataLabsController.DLService.ResourceProxy, "ResourceFetcherAKS")]
        [DataRow(AdminDataLabsController.DLService.ResourceFetcherService, "PartnerAKS")]
        public async Task TestAdminDataLabsControllerPartnerAksGetConfiguration_WrongAKSCluster(
            AdminDataLabsController.DLService service, string aksCluster)
        {
            if (aksCluster == "PartnerAKS")
            {
                ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "solution-io.endpoint";
                ConfigMapUtil.Configuration[AdminConstants.ResourceProxyAdminEndpoint] = "resource-proxy.endpoint";
            }
            else
            {
                ConfigMapUtil.Configuration[AdminConstants.ResourceFetcherAdminEndpoint] = "resource-fetcher.endpoint";
            }

            var providerClass = new Mock<IKubernetesProvider>();

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(), providerClass.Object);

            var result = await controller.GetConfiguration(service.FastEnumToString(), "randomValue").ConfigureAwait(false);

            Assert.IsInstanceOfType(result.Result, typeof(ObjectResult));
            Assert.IsInstanceOfType(((ObjectResult)result.Result).Value, typeof(EndpointNotFoundException));
        }

        [TestMethod]
        public async Task TestAdminDataControllerGetConfiguration_BadInput()
        {
            var providerClass = new Mock<IKubernetesProvider>();

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(), providerClass.Object);
            var result = await controller.GetConfiguration("randomNonExistentService", "randomValue");

            Assert.IsInstanceOfType(result.Result, typeof(ObjectResult));
            Assert.IsInstanceOfType(((ObjectResult)result.Result).Value, typeof(EndpointNotFoundException));
        }

        [TestMethod]
        public async Task TestDeleteAndRecreateServiceBusQueue_Returns_SuccessResponse()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var expectedResponse = "Queue recreated successfully";
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var response = new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(expectedResponse)
            };
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).ReturnsAsync(response);
            var providerClass = new Mock<IKubernetesProvider>();

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(mockHttpMessageHandler.Object), providerClass.Object);

            var result = await controller.DeleteAndRecreateServiceBusQueue(queueName).ConfigureAwait(false);

            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
            Assert.AreEqual(expectedResponse, ((OkObjectResult)result.Result).Value);
        }

        [TestMethod]
        public async Task TestDeleteAndRecreateServiceBusQueue_BadHttpClient()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var expectedExceptionMessage = "expectedResult";
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).ThrowsAsync(new Exception(expectedExceptionMessage));
            var providerClass = new Mock<IKubernetesProvider>();

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(mockHttpMessageHandler.Object), providerClass.Object);

            var result = await controller.DeleteAndRecreateServiceBusQueue(queueName).ConfigureAwait(false);

            Assert.IsInstanceOfType(result.Result, typeof(ObjectResult));
            var exception = (Exception)((ObjectResult)result.Result).Value;
            Assert.AreEqual(expectedExceptionMessage, exception.Message);
        }

        [TestMethod]
        public async Task TestDeleteAndRecreateServiceBusQueue_BadInput()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            var queueName = "test";

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(), providerClass.Object);
            var result = await controller.DeleteAndRecreateServiceBusQueue(queueName);

            Assert.IsInstanceOfType(result.Result, typeof(ObjectResult));
            Assert.IsInstanceOfType(((ObjectResult)result.Result).Value, typeof(EndpointNotFoundException));
        }

        [TestMethod]
        public async Task TestDeleteDeadLetterMessages_Returns_SuccessResponse()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var expectedResponse = "DLQ messages deleted successfully";
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var response = new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(expectedResponse)
            };
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).ReturnsAsync(response);
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(mockHttpMessageHandler.Object), providerClass.Object);

            var result = await controller.DeleteDeadLetterMessages(queueName, 2, 3).ConfigureAwait(false);

            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
            Assert.AreEqual(expectedResponse, ((OkObjectResult)result.Result).Value);
        }

        [TestMethod]
        public async Task TestDeleteDeadLetterMessages_GetEndpointSliceFails()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(mockHttpMessageHandler.Object), providerClass.Object);

            var result = await controller.DeleteDeadLetterMessages(queueName, 2, 3).ConfigureAwait(false);

            Assert.IsInstanceOfType(result.Result, typeof(ObjectResult));
            var exception = (Exception)((ObjectResult)result.Result).Value;
            Assert.AreEqual(kubernetesExceptionString, exception.Message);
        }

        [TestMethod]
        public async Task TestReplayDeadLetterMessages_Returns_SuccessResponse()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var expectedResponse = "DLQ messages replayed successfully";
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var response = new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(expectedResponse)
            };
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).ReturnsAsync(response);
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(mockHttpMessageHandler.Object), providerClass.Object);

            var result = await controller.ReplayDeadLetterMessages(queueName, 2, 3).ConfigureAwait(false);

            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
            Assert.AreEqual(expectedResponse, ((OkObjectResult)result.Result).Value);
        }

        [TestMethod]
        public async Task TestReplayDeadLetterMessages_GetEndpointSliceFails()
        {
            ConfigMapUtil.Configuration[AdminConstants.IOServiceAdminEndpoint] = "http://solution-io.solution-namespace.svc.cluster.local:7072";

            var queueName = "test";
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminDataLabsController(ConfigMapUtil.Configuration, new HttpClient(mockHttpMessageHandler.Object), providerClass.Object);

            var result = await controller.ReplayDeadLetterMessages(queueName, 2, 3).ConfigureAwait(false);

            Assert.IsInstanceOfType(result.Result, typeof(ObjectResult));
            var exception = (Exception)((ObjectResult)result.Result).Value;
            Assert.AreEqual(kubernetesExceptionString, exception.Message);
        }
    }
}