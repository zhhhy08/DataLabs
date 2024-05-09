namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.AdminService
{
    using k8s.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Controllers;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils;
    using Moq;
    using System.IO;

    [TestClass]
    public class AdminKubernetesControllerTests
    {
        #region test class

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
                return Task.FromResult(new V1EndpointSliceList());
            }
        }

        public const string exceptionString = "exception here!";

        public class ExceptionKubernetesWrapper : IKubernetesWrapper
        {
            public Task<V1Pod> DeleteNamespacedPodAsync(string podName, string podNamespace)
            {
                throw new Exception(exceptionString);
            }

            public Task<V1ConfigMapList> ListConfigMapForAllNamespacesAsync()
            {
                throw new Exception(exceptionString);
            }

            public Task<V1PodList> ListNamespacedPodAsync(string podNamespace)
            {
                throw new Exception(exceptionString);
            }

            public Task<V1PodList> ListPodForAllNamespacesAsync()
            {
                throw new Exception(exceptionString);
            }

            public Task<V1ConfigMap> PatchNamespacedConfigMapAsync(V1Patch configMapOverridePatch, string configMapName, string configMapNamespace)
            {
                throw new Exception(exceptionString);
            }

            public Task<V1DaemonSet> ReadNamespacedDaemonSetAsync(string daemonSetName, string daemonSetNamespace)
            {
                throw new Exception(exceptionString);
            }

            public Task<V1Deployment> ReadNamespacedDeploymentAsync(string deploymentName, string deploymentNamespace)
            {
                throw new Exception(exceptionString);
            }

            public Task<Stream> ReadNamespacedPodLogAsync(string podNamespace, string podName, string containerName)
            {
                throw new Exception(exceptionString);
            }

            public Task<V1DaemonSet> ReplaceNamespacedDaemonSetAsync(V1DaemonSet daemonSet, string daemonSetName, string daemonSetNamespace)
            {
                throw new Exception(exceptionString);
            }

            public Task<V1Deployment> ReplaceNamespacedDeploymentAsync(V1Deployment deployment, string deploymentName, string deploymentNamespace)
            {
                throw new Exception(exceptionString);
            }

            public Task<V1EndpointSliceList> ListNamespacedEndpointSliceAsync(string podNamespace)
            {
                throw new Exception(exceptionString);
            }
        }

        #endregion

        [TestMethod]
        public async Task GetAllPodsTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetAllPods();

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
            var parsedRes = (IList<object>) ((OkObjectResult)res.Result).Value;
            
            Assert.AreEqual(parsedRes.Count(), 1);
            Assert.AreEqual(parsedRes.First(), "emptyPodList");
        }

        [TestMethod]
        public async Task GetAllPodsExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetAllPods();

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        [DataRow("solution-namespace")]
        [DataRow("partner-namespace")]
        [DataRow("resource-fetcher-namespace")]
        public async Task GetPodsTest(string podNamespace)
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetPods(podNamespace);

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
            var parsedRes = (IList<object>)((OkObjectResult)res.Result).Value;

            Assert.AreEqual(parsedRes.Count(), 1);
            Assert.AreEqual(parsedRes.First(), "emptyPodList");
        }

        [TestMethod]
        public async Task GetPodsExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetPods("solution-namespace");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        public async Task GetAllConfigMapsTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetAllConfigMaps();

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
            var parsedRes = (IList<object>)((OkObjectResult)res.Result).Value;

            Assert.AreEqual(parsedRes.Count(), 1);
            Assert.AreEqual(parsedRes.First(), "emptyConfigMapList");
        }

        [TestMethod]
        public async Task GetAllConfigMapsExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetAllConfigMaps();

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        public async Task GetDeploymentTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetDeployment("deploymentName", "deploymentNamespace");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
            Assert.IsInstanceOfType(((OkObjectResult)res.Result).Value, typeof(V1Deployment));
        }

        [TestMethod]
        public async Task GetDeploymentExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetDeployment("deploymentName", "deploymentNamespace");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        public async Task GetDaemonSetTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetDaemonSet("daemonSetName", "daemonSetNamespace");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
            Assert.IsInstanceOfType(((OkObjectResult)res.Result).Value, typeof(V1DaemonSet));
        }

        [TestMethod]
        public async Task GetDaemonSetExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetDaemonSet("daemonSetName", "daemonSetNamespace");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        public async Task GetPodLogsTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetPodLogs("podNamespace", "podName");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
            var parsedRes = (FileStreamResult)((OkObjectResult)res.Result).Value;

            Assert.AreEqual(parsedRes.ContentType, "application/octet-stream");
            Assert.AreEqual(parsedRes.FileStream, Stream.Null);
        }

        [TestMethod]
        public async Task GetPodLogsExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.GetPodLogs("podNamespace", "podName");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        public async Task UpdateConfigMapKeyTest()
        {
            var utilsClass = new Mock<IKubernetesObjectTransformUtils>();
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, utilsClass.Object);
            var res = await controller.UpdateConfigMapKey("configmap", "configmap-namespace", "key", "value");

            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
        }

        [TestMethod]
        public async Task UpdateConfigMapKeyExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.UpdateConfigMapKey("configMapNamespace", "configMapName", "configKey", "configValue");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        public async Task UpdateMultipleConfigMapKeysTest()
        {
            var utilsClass = new Mock<IKubernetesObjectTransformUtils>();
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, utilsClass.Object);
            var res = await controller.UpdateMultipleConfigMapKeys("configmap", "configmap-namespace", new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            });

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
        }

        [TestMethod]
        public async Task UpdateMultipleConfigMapKeysExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.UpdateMultipleConfigMapKeys("configMapNamespace", "configMapName", new Dictionary<string, string>());

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        public async Task DeletePodTest()
        {
            var utilsClass = new Mock<IKubernetesObjectTransformUtils>();
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, utilsClass.Object);
            var res = await controller.DeletePod("podNamespace", "podName");

            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
            var parsedRes = (string)((OkObjectResult)res.Result).Value;

            Assert.AreEqual(" () is deleted", parsedRes); // empty results since BasicResultsKubernetesWrapper returns a shell of the object.
        }

        [TestMethod]
        public async Task DeletePodExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.DeletePod("podNamespace", "podName");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        public async Task ScaleDeploymentReplicaCountTest()
        {
            var utilsClass = new Mock<IKubernetesObjectTransformUtils>();
            utilsClass.Setup(x => x.ScaleDeploymentReplicasObject(It.IsAny<V1Deployment>(), It.IsAny<int>())).Returns(new V1Deployment());
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, utilsClass.Object);
            var res = await controller.ScaleDeploymentReplicaCount("deploymentNamespace", "deploymentName", 10);

            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
            var parsedRes = (string)((OkObjectResult)res.Result).Value;

            Assert.AreEqual("NewReplicaCount: 10", parsedRes);
        }

        [TestMethod]
        public async Task ScaleDeploymentReplicaCountLssThanZeroTest()
        {
            var utilsClass = new Mock<IKubernetesObjectTransformUtils>();
            utilsClass.Setup(x => x.ScaleDeploymentReplicasObject(It.IsAny<V1Deployment>(), It.IsAny<int>())).Returns(new V1Deployment());
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, utilsClass.Object);
            var res = await controller.ScaleDeploymentReplicaCount("deploymentNamespace", "deploymentName", -1);

            Assert.IsInstanceOfType(res.Result, typeof(BadRequestObjectResult));
            var parsedRes = (string)((BadRequestObjectResult)res.Result).Value;

            Assert.AreEqual("replicaCount cannot be < 0", parsedRes);
        }

        [TestMethod]
        public async Task ScaleDeploymentReplicaCountExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.ScaleDeploymentReplicaCount("deploymentNamespace", "deploymentName", 0);

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        public async Task RestartDeploymentTest()
        {
            var utilsClass = new Mock<IKubernetesObjectTransformUtils>();
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, utilsClass.Object);
            var res = await controller.RestartDeployment("deploymentNamespace", "deploymentName");

            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
            var parsedRes = (string)((OkObjectResult)res.Result).Value;

            Assert.AreEqual("Restarted Deployment", parsedRes);
        }

        [TestMethod]
        public async Task RestartDeploymentTestExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.RestartDeployment("deploymentNamespace", "deploymentName");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }

        [TestMethod]
        public async Task RestartDaemonsetTest()
        {
            var utilsClass = new Mock<IKubernetesObjectTransformUtils>();
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new BasicResultsKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, utilsClass.Object);
            var res = await controller.RestartDaemonset("daemonSetNamespace", "daemonSetName");

            Assert.IsInstanceOfType(res.Result, typeof(OkObjectResult));
            var parsedRes = (string)((OkObjectResult)res.Result).Value;

            Assert.AreEqual("Restarted DaemonSet", parsedRes);
        }

        [TestMethod]
        public async Task RestartDaemonSetTestExceptionTest()
        {
            var providerClass = new Mock<IKubernetesProvider>();
            providerClass.Setup(x => x.GetKubernetesClient()).Returns(new ExceptionKubernetesWrapper());

            var controller = new AdminKubernetesController(providerClass.Object, new KubernetesObjectTransformUtils());
            var res = await controller.RestartDaemonset("daemonSetNamespace", "daemonSetName");

            Assert.IsNotNull(res);
            Assert.IsInstanceOfType(res.Result, typeof(ObjectResult));
            var parsedRes = (Exception)((ObjectResult)res.Result).Value;

            Assert.AreEqual(exceptionString, parsedRes.Message);
        }
    }
}
