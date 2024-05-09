namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils
{
    using k8s.Models;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;

    public class KubernetesObjectTransformUtils : IKubernetesObjectTransformUtils
    {
        public IList<object> SimplifyPodListOutput(V1PodList podList)
        {
            if (podList.Items.IsNullOrEmpty())
            {
                return new List<object> { "emptyPodList" };
            }
            return podList.Items.Select(SimplifyPodOutput).ToList();
        }

        private object SimplifyPodOutput(V1Pod pod)
        {
            return new
            {
                podName = pod.Name(),
                podNamespace = pod.Namespace(),
                podStatus = pod.Status.Phase,
                podCreationTime = pod.Metadata.CreationTimestamp,
                podNode = pod.Spec.NodeName,
                podContainerInfo = pod.Status.ContainerStatuses.Select(container => new
                {
                    containerName = container.Name,
                    restartCount = container.RestartCount,
                    state = container.State
                }).ToArray()
            };
        }

        public IList<object> SimplifyConfigMapOutput(V1ConfigMapList configMapList)
        {
            if (configMapList.Items.IsNullOrEmpty())
            {
                return new List<object> { "emptyConfigMapList" };
            }
            return configMapList.Items.Select(SimplifyConfigMapOutput).ToList();
        }

        private object SimplifyConfigMapOutput(V1ConfigMap configMap)
        {
            return new
            {
                configMapName = configMap.Name(),
                configMapNamespace = configMap.Namespace(),
                configMapData = configMap.Data
            };
        }

        public V1Deployment ScaleDeploymentReplicasObject(V1Deployment deployment, int newReplicaCount)
        {
            if (IActivityMonitor.CurrentActivity != null)
            {
                IActivityMonitor.CurrentActivity.Properties["oldReplicaCount"] = deployment.Spec.Replicas;
                IActivityMonitor.CurrentActivity.Properties["newReplicaCount"] = newReplicaCount;
            }
            deployment.Spec.Replicas = newReplicaCount;
            return deployment;
        }

        public V1Deployment UpdateDeploymentObjectRestart(V1Deployment deployment, DateTime restartTime = default)
        {
            if (restartTime == default)
            {
                restartTime = DateTime.UtcNow;
            }
            var utcTimeNow = restartTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            if (IActivityMonitor.CurrentActivity != null)
            {
                IActivityMonitor.CurrentActivity.Properties["expectedRestartTime"] = utcTimeNow;
            }

            deployment.Spec.Template.Metadata.Annotations = new Dictionary<string, string>
            {
                {
                    "kubectl.kubernetes.io/restartedAt", utcTimeNow
                }
            };
            return deployment;
        }

        public V1DaemonSet UpdateDaemonSetObjectRestart(V1DaemonSet daemonSet, DateTime restartTime = default)
        {
            if (restartTime == default)
            {
                restartTime = DateTime.UtcNow;
            }
            var utcTimeNow = restartTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            if (IActivityMonitor.CurrentActivity != null)
            {
                IActivityMonitor.CurrentActivity.Properties["expectedRestartTime"] = utcTimeNow;
            }
            
            daemonSet.Spec.Template.Metadata.Annotations = new Dictionary<string, string>
            {
                {
                    "kubectl.kubernetes.io/restartedAt", utcTimeNow
                }
            };
            return daemonSet;
        }
    }
}
