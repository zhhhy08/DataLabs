namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class NoOpARMAdminClient : IARMAdminClient
    {
        public static readonly NoOpARMAdminClient Instance = new();

        private NoOpARMAdminClient()
        {
        }

        public Task<HttpResponseMessage> GetManifestConfigAsync(string manifestProvider, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<HttpResponseMessage> GetConfigSpecsAsync(string apiExtension, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}
