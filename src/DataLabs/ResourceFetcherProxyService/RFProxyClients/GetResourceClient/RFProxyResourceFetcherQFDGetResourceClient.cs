namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient;

    internal class RFProxyResourceFetcherQfdGetResourceClient : IRFProxyGetResourceClient
    {
        private static volatile RFProxyResourceFetcherQfdGetResourceClient? _instance;
        private static readonly object SyncRoot = new();

        public static RFProxyResourceFetcherQfdGetResourceClient Create(IResourceFetcherClient resourceFetcherClient)
        {
            if (_instance == null)
            {
                lock (SyncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new RFProxyResourceFetcherQfdGetResourceClient(resourceFetcherClient);
                    }
                }
            }
            return _instance;
        }

        private readonly IResourceFetcherClient _resourceFetcherClient;

        private RFProxyResourceFetcherQfdGetResourceClient(IResourceFetcherClient resourceFetcherClient)
        {
            _resourceFetcherClient = resourceFetcherClient;
        }

        public Task<HttpResponseMessage> GetRFProxyResourceAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            string? regionName,
            int retryFlowCount,
            CancellationToken cancellationToken)
        {
            return _resourceFetcherClient.GetPacificResourceAsync(
                resourceId: resourceId, 
                tenantId: tenantId, 
                apiVersion: apiVersion, 
                clientRequestId: null, 
                cancellationToken: cancellationToken);
        }
    }
}
