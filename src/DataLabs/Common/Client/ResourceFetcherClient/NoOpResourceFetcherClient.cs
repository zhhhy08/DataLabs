namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;

    public class NoOpResourceFetcherClient : IResourceFetcherClient
    {
        public static readonly NoOpResourceFetcherClient Instance = new();

        private NoOpResourceFetcherClient()
        {
        }

        public void Dispose()
        {
        }

        public Task<HttpResponseMessage> GetCasCapacityCheckAsync(CasRequestBody casRequestBody, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<HttpResponseMessage> GetConfigSpecsAsync(string apiExtension, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<HttpResponseMessage> GetGenericRestApiAsync(string uriPath, IEnumerable<KeyValuePair<string, string>>? parameters, string? tenantId, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<HttpResponseMessage> GetManifestConfigAsync(string manifestProvider, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<HttpResponseMessage> GetPacificCollectionAsync(string resourceId, string? tenantId, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<HttpResponseMessage> GetPacificResourceAsync(string resourceId, string? tenantId, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<HttpResponseMessage> GetResourceAsync(string resourceId, string? tenantId, string apiVersion, bool useResourceGraph, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<HttpResponseMessage> GetPacificIdMappingsAsync(IdMappingRequestBody idMappingRequestBody, string? correlationId, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}