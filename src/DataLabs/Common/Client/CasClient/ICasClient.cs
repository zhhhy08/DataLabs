namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;

    public interface ICasClient : IDisposable
    {
        Task<HttpResponseMessage> GetCasCapacityCheckAsync(
          CasRequestBody casRequestBody,
          string apiVersion,
          string? clientRequestId,
          CancellationToken cancellationToken);
    }
}
