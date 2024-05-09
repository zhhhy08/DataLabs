namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Newtonsoft.Json;

    public class TestCasClient : ICasClient
    {
        public Dictionary<string, string> _resourceMap = new Dictionary<string, string>();
        public HttpStatusCode? ErrStatusCode { get; set; }

        public Task<HttpResponseMessage> GetCasCapacityCheckAsync(CasRequestBody casRequestBody, string apiVersion, string clientRequestId, CancellationToken cancellationToken)
        {
            if (ErrStatusCode != null)
            {
                return Task.FromResult(new HttpResponseMessage(ErrStatusCode.Value));
            }

            if (_resourceMap.TryGetValue(JsonConvert.SerializeObject(casRequestBody), out var armResource))
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

        public void Dispose()
        {
        }

        public void SetResource(string resource, string armResource)
        {
            _resourceMap[resource] = armResource;
        }

        public void Clear()
        {
            _resourceMap.Clear();
            ErrStatusCode = null;
        }
    }
}