namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Threading;

    internal interface IRFProxyGetResourceClient
    {
        public Task<HttpResponseMessage> GetRFProxyResourceAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            string? regionName,
            int retryFlowCount,
            CancellationToken cancellationToken);
    }
}
