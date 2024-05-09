namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IQFDClient : IDisposable
    {
        Task<HttpResponseMessage> GetPacificResourceAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken);

        Task<HttpResponseMessage> GetPacificCollectionAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken);

        Task<HttpResponseMessage> GetPacificIdMappingsAsync(
            IdMappingRequestBody idMappingRequestBody,
            string? correlationId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken);
    }
}