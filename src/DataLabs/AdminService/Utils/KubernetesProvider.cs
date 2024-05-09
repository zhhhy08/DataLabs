namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils
{
    using k8s;

    public class KubernetesProvider : IKubernetesProvider
    {
        public IKubernetesWrapper GetKubernetesClient()
        {
            var config = KubernetesClientConfiguration.IsInCluster()
                ? KubernetesClientConfiguration.InClusterConfig()
                : KubernetesClientConfiguration.BuildConfigFromConfigFile();
            var client = new Kubernetes(config);
            return new KubernetesWrapper(client);
        }
    }
}
