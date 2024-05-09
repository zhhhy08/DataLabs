namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SecretProviderManager
{
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    public interface ICertificateListener
    {
        public Task CertificateChangedAsync(X509Certificate2 certificate);
    }
}
