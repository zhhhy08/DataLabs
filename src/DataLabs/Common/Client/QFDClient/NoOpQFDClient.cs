namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class NoOpQFDClient : IQFDClient
    {
        public static readonly NoOpQFDClient Instance = new();

        private NoOpQFDClient()
        {
        }

        public Task<HttpResponseMessage> GetPacificCollectionAsync(
            string resourceId,
            string? tenantId,
            string apiVersion, 
            string? clientRequestId, 
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<HttpResponseMessage> GetPacificResourceAsync(
            string resourceId,
            string? tenantId,
            string apiVersion, 
            string? clientRequestId, 
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<HttpResponseMessage> GetResourceAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken)
        {
            return GetPacificResourceAsync(
                resourceId: resourceId,
                tenantId: tenantId,
                apiVersion: apiVersion,
                clientRequestId: clientRequestId,
                cancellationToken: cancellationToken);
        }

        public Task<HttpResponseMessage> GetPacificIdMappingsAsync(
            IdMappingRequestBody idMappingRequestBody,
            string? correlationId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken) 
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}
