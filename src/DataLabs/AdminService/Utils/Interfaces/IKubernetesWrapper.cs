using k8s.Models;

namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils
{
    public interface IKubernetesWrapper
    {
        Task<V1PodList> ListPodForAllNamespacesAsync();

        Task<V1PodList> ListNamespacedPodAsync(string podNamespace);

        Task<V1ConfigMapList> ListConfigMapForAllNamespacesAsync();

        Task<Stream> ReadNamespacedPodLogAsync(string podNamespace, string podName, string? containerName);

        Task<V1ConfigMap> PatchNamespacedConfigMapAsync(V1Patch configMapOverridePatch, string configMapName, string configMapNamespace);

        Task<V1Pod> DeleteNamespacedPodAsync(string podName, string podNamespace);

        Task<V1Deployment> ReadNamespacedDeploymentAsync(string deploymentName, string deploymentNamespace);

        Task<V1Deployment> ReplaceNamespacedDeploymentAsync(V1Deployment deployment, string deploymentName, string deploymentNamespace);

        Task<V1DaemonSet> ReadNamespacedDaemonSetAsync(string daemonSetName, string daemonSetNamespace);

        Task<V1DaemonSet> ReplaceNamespacedDaemonSetAsync(V1DaemonSet daemonSet, string daemonSetName, string daemonSetNamespace);

        Task<V1EndpointSliceList> ListNamespacedEndpointSliceAsync(string podNamespace);
    }
}
