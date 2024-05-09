namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class TestARMAdminClient : IARMAdminClient
    {
        public Dictionary<string, string> _resourceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public HttpStatusCode? ErrStatusCode { get; set; }
        public bool ThrowException { get; set; }

        public Task<HttpResponseMessage> GetConfigSpecsAsync(string apiExtension, string apiVersion, string clientRequestId, CancellationToken cancellationToken)
        {
            if (ThrowException)
            {
                throw new System.Exception("Test Exception");
            }

            if (ErrStatusCode != null)
            {
                return Task.FromResult(new HttpResponseMessage(ErrStatusCode.Value));
            }

            HttpResponseMessage httpResponseMessage;
            if (_resourceMap.TryGetValue(apiExtension, out var responseResource))
            {
                httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
                httpResponseMessage.Content = new StringContent(responseResource, System.Text.Encoding.UTF8, "application/json");
            }
            else
            {
                httpResponseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return Task.FromResult(httpResponseMessage);
        }

        public Task<HttpResponseMessage> GetManifestConfigAsync(string manifestProvider, string apiVersion, string clientRequestId, CancellationToken cancellationToken)
        {
            if (ThrowException)
            {
                throw new System.Exception("Test Exception");
            }

            if (ErrStatusCode != null)
            {
                return Task.FromResult(new HttpResponseMessage(ErrStatusCode.Value));
            }

            HttpResponseMessage httpResponseMessage;
            if (_resourceMap.TryGetValue(manifestProvider, out var responseResource))
            {
                httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
                httpResponseMessage.Content = new StringContent(responseResource, System.Text.Encoding.UTF8, "application/json");
            }
            else
            {
                httpResponseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return Task.FromResult(httpResponseMessage);
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