namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SecretProviderManager
{
    using System.Diagnostics.CodeAnalysis;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    [ExcludeFromCodeCoverage]
    public class NoOpCertificateListener : ICertificateListener
    {
        public Task CertificateChangedAsync(X509Certificate2 certificate)
        {
            // TODO: switch to "throw new NotImplementedException();", this is meant to utilize SecretProviderManager without a listener
            return Task.CompletedTask;
        }
    }
}
