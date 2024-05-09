namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;

    public class NoOpCasClient : ICasClient
    {
        public static readonly NoOpCasClient Instance = new();

        private NoOpCasClient()
        {
        }

        public Task<HttpResponseMessage> GetCasCapacityCheckAsync(CasRequestBody casRequestBody, string apiVersion, string? clientRequestId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}