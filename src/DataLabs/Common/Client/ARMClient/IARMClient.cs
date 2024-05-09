namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IARMClient : IDisposable
    {
        public Task<HttpResponseMessage> GetResourceAsync(
          string resourceId,
          string? tenantId,
          string apiVersion,
          bool useResourceGraph,
          string? clientRequestId,
          CancellationToken cancellationToken);

        public Task<HttpResponseMessage> GetGenericRestApiAsync(
            string uriPath,
            IEnumerable<KeyValuePair<string, string>>? parameters,
            string? tenantId,
            string apiVersion,
            string? clientRequestId,
            CancellationToken cancellationToken);
    }
}
