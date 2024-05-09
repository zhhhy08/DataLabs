namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils
{
    using k8s;
    using k8s.Models;
 
    public class KubernetesWrapper : IKubernetesWrapper
    {
        private Kubernetes _kubernetesClient;

        public KubernetesWrapper(Kubernetes kubernetesClient)
        {
            _kubernetesClient = kubernetesClient;
        }

        public async Task<V1PodList> ListPodForAllNamespacesAsync()
        {
            return await _kubernetesClient.ListPodForAllNamespacesAsync();
        }

        public async Task<V1ConfigMapList> ListConfigMapForAllNamespacesAsync()
        {
            return await _kubernetesClient.ListConfigMapForAllNamespacesAsync();
        }

        public async Task<V1PodList> ListNamespacedPodAsync(string podNamespace)
        {
            return await _kubernetesClient.ListNamespacedPodAsync(podNamespace);
        }

        public async Task<Stream> ReadNamespacedPodLogAsync(string podNamespace, string podName, string? containerName = null)
        {
            return await _kubernetesClient.ReadNamespacedPodLogAsync(podNamespace, podName, containerName);
        }

        public async Task<V1ConfigMap> PatchNamespacedConfigMapAsync(V1Patch configMapOverridePatch, string configMapName, string configMapNamespace)
        {
            return await _kubernetesClient.PatchNamespacedConfigMapAsync(configMapOverridePatch, configMapName, configMapNamespace);
        }

        public async Task<V1Pod> DeleteNamespacedPodAsync(string podName, string podNamespace)
        {
            return await _kubernetesClient.DeleteNamespacedPodAsync(podName, podNamespace);
        }

        public async Task<V1Deployment> ReadNamespacedDeploymentAsync(string deploymentName, string deploymentNamespace)
        {
            return await _kubernetesClient.ReadNamespacedDeploymentAsync(deploymentName, deploymentNamespace);
        }

        public async Task<V1Deployment> ReplaceNamespacedDeploymentAsync(V1Deployment deployment, string deploymentName, string deploymentNamespace)
        {
            return await _kubernetesClient.ReplaceNamespacedDeploymentAsync(deployment, deploymentName, deploymentNamespace);
        }

        public async Task<V1DaemonSet> ReadNamespacedDaemonSetAsync(string daemonSetName, string daemonSetNamespace)
        {
            return await _kubernetesClient.ReadNamespacedDaemonSetAsync(daemonSetName, daemonSetNamespace);
        }

        public async Task<V1DaemonSet> ReplaceNamespacedDaemonSetAsync(V1DaemonSet daemonSet, string daemonSetName, string daemonSetNamespace)
        {
            return await _kubernetesClient.ReplaceNamespacedDaemonSetAsync(daemonSet, daemonSetName, daemonSetNamespace);
        }

        public async Task<V1EndpointSliceList> ListNamespacedEndpointSliceAsync(string podNamespace)
        {
            return await _kubernetesClient.ListNamespacedEndpointSliceAsync(podNamespace);
        }
    }
}
