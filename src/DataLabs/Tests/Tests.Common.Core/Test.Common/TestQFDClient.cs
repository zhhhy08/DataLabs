namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;

    public class TestQFDClient : IQFDClient
    {
        public Dictionary<string, string> _resourceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public HttpStatusCode? ErrStatusCode { get; set; }

        public Task<HttpResponseMessage> GetPacificCollectionAsync(string resourceId, string tenantId, string apiVersion, string clientRequestId, CancellationToken cancellationToken)
        {
            if (ErrStatusCode != null)
            {
                return Task.FromResult(new HttpResponseMessage(ErrStatusCode.Value));
            }

            if (_resourceMap.TryGetValue(resourceId, out var armResource))
            {
                var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
                httpResponseMessage.Content = new StringContent(armResource, System.Text.Encoding.UTF8, "application/json");
                return Task.FromResult(httpResponseMessage);
            }
            else
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        public Task<HttpResponseMessage> GetPacificResourceAsync(string resourceId, string tenantId, string apiVersion, string clientRequestId, CancellationToken cancellationToken)
        {
            if (ErrStatusCode != null)
            {
                return Task.FromResult(new HttpResponseMessage(ErrStatusCode.Value));
            }

            if (_resourceMap.TryGetValue(resourceId, out var armResource))
            {
                var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
                httpResponseMessage.Content = new StringContent(armResource, System.Text.Encoding.UTF8, "application/json");
                return Task.FromResult(httpResponseMessage);
            }
            else
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        public Task<HttpResponseMessage> GetPacificIdMappingsAsync(IdMappingRequestBody idMappingRequestBody, string correlationId, string apiVersion, string clientRequestId, CancellationToken cancellationToken)
        {
            if (ErrStatusCode != null)
            {
                return Task.FromResult(new HttpResponseMessage(ErrStatusCode.Value));
            }

            if (_resourceMap.TryGetValue(idMappingRequestBody.AliasResourceIds.First(), out var idMappingResponse))
            {
                var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
                httpResponseMessage.Content = new StringContent(idMappingResponse, System.Text.Encoding.UTF8, "application/json");
                return Task.FromResult(httpResponseMessage);
            }
            else
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        public void Dispose()
        {
        }

        public void SetResource(string resourceId, string armResource)
        {
            _resourceMap[resourceId] = armResource;
        }

        public void Clear()
        {
            _resourceMap.Clear();
            ErrStatusCode = null;
        }
    }
}