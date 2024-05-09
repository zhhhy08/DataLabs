namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IARMAdminClient : IDisposable
    {
        public Task<HttpResponseMessage> GetManifestConfigAsync(
            string manifestProvider,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken);

        public Task<HttpResponseMessage> GetConfigSpecsAsync(
            string apiExtension,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken);
    }
}
