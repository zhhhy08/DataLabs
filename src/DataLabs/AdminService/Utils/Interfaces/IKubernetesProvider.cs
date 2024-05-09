namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils
{
    using k8s;

    public interface IKubernetesProvider
    {
        IKubernetesWrapper GetKubernetesClient();
    }
}
