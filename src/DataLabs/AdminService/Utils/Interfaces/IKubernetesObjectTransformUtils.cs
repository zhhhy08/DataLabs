using k8s.Models;

namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils
{
    public interface IKubernetesObjectTransformUtils
    {
        IList<object> SimplifyPodListOutput(V1PodList podList);

        IList<object> SimplifyConfigMapOutput(V1ConfigMapList configMapList);

        V1Deployment ScaleDeploymentReplicasObject(V1Deployment deployment, int newReplicaCount);

        V1Deployment UpdateDeploymentObjectRestart(V1Deployment deployment, DateTime restartTime = default);

        V1DaemonSet UpdateDaemonSetObjectRestart(V1DaemonSet daemonSet, DateTime restartTime = default);
    }
}
