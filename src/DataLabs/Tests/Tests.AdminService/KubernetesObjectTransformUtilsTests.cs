namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.AdminService
{
    using k8s.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class KubernetesObjectTransformUtilsTests
    {
        [TestMethod]
        public void SimplifyPodListOutput_Test()
        {
            // Setup
            DateTime time = DateTime.Now;

            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Name = "podName",
                    NamespaceProperty = "podNamespace",
                    CreationTimestamp = time
                },
                Spec = new V1PodSpec
                {
                    NodeName = "nodeName"
                },
                Status = new V1PodStatus
                {
                    Phase = "podPhase",
                    ContainerStatuses = new[]
                    {
                        new V1ContainerStatus
                        {
                            Name = "containerName",
                            RestartCount = 0,
                            State = new V1ContainerState(new V1ContainerStateRunning())
                        }
                    }
                }
            };

            var podList = new V1PodList(new List<V1Pod> { pod });

            // Expected Result
            var expectedResult = new[]
            {
                new
                {
                    podName = "podName",
                    podNamespace = "podNamespace",
                    podStatus = "podPhase",
                    podCreationTime = time,
                    podNode = "nodeName",
                    podContainerInfo = new []
                    {
                        new
                        {
                            containerName = "containerName",
                            restartCount = 0,
                            state = new V1ContainerState(new V1ContainerStateRunning())
                        }
                    }
                }
            };
            JToken expectedResultJToken = JToken.FromObject(expectedResult);
            //Console.WriteLine(expectedResultJToken.ToString());

            var simplifiedPodList = new KubernetesObjectTransformUtils().SimplifyPodListOutput(podList);
            Assert.AreEqual(1, expectedResult.Length);

            JToken simplifiedPodJToken = JToken.FromObject(simplifiedPodList);
            //Console.WriteLine(simplifiedPodJToken.ToString());

            var result = JToken.DeepEquals(simplifiedPodJToken, expectedResultJToken);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void UpdateDeploymentObjectRestart_Test()
        {
            // setup
            var deployment = new V1Deployment
            {
                Spec = new V1DeploymentSpec
                {
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {

                        }
                    }
                }
            };
            var time = DateTime.Now;

            // action
            var result = new KubernetesObjectTransformUtils().UpdateDeploymentObjectRestart(deployment, time);

            var annotations = result.Spec.Template.Metadata.Annotations;
            Assert.IsNotNull(annotations);
            Assert.IsInstanceOfType(annotations, typeof(Dictionary<string, string>));

            annotations.TryGetValue("kubectl.kubernetes.io/restartedAt", out var actualTime);

            Assert.AreEqual(time.ToString("yyyy-MM-ddTHH:mm:ssZ"), actualTime);
        }

        [TestMethod]
        public void UpdateDaemonSetObjectRestart_Test()
        {
            // setup
            var daemonSet = new V1DaemonSet
            {
                Spec = new V1DaemonSetSpec
                {
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {

                        }
                    }
                }
            };
            var time = DateTime.Now;

            // action
            var result = new KubernetesObjectTransformUtils().UpdateDaemonSetObjectRestart(daemonSet, time);

            var annotations = result.Spec.Template.Metadata.Annotations;
            Assert.IsNotNull(annotations);
            Assert.IsInstanceOfType(annotations, typeof(Dictionary<string, string>));

            annotations.TryGetValue("kubectl.kubernetes.io/restartedAt", out var actualTime);

            Assert.AreEqual(time.ToString("yyyy-MM-ddTHH:mm:ssZ"), actualTime);
        }
    }
}
