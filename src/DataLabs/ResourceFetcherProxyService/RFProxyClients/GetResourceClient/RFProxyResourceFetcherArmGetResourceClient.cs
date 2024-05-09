namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient;

    internal class RFProxyResourceFetcherArmGetResourceClient : IRFProxyGetResourceClient
    {
        private static volatile RFProxyResourceFetcherArmGetResourceClient? _instance;
        private static readonly object SyncRoot = new();

        public static RFProxyResourceFetcherArmGetResourceClient Create(IResourceFetcherClient resourceFetcherClient)
        {
            if (_instance == null)
            {
                lock (SyncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new RFProxyResourceFetcherArmGetResourceClient(resourceFetcherClient);
                    }
                }
            }
            return _instance;
        }

        private readonly IResourceFetcherClient _resourceFetcherClient;

        private RFProxyResourceFetcherArmGetResourceClient(IResourceFetcherClient resourceFetcherClient)
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
            return _resourceFetcherClient.GetResourceAsync(
                resourceId: resourceId, 
                tenantId: tenantId, 
                apiVersion: apiVersion,
                useResourceGraph: false,
                clientRequestId: null, 
                cancellationToken: cancellationToken);
        }
    }
}
