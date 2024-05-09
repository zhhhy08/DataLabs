namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;

    internal class QfdGetResourceClient : IRFProxyGetResourceClient
    {
        private static volatile QfdGetResourceClient? _instance;
        private static readonly object SyncRoot = new();

        public static IRFProxyGetResourceClient Create(IQFDClient qfdClient)
        {
            if (_instance == null)
            {
                lock (SyncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new QfdGetResourceClient(qfdClient);
                    }
                }
            }
            return _instance;
        }

        private readonly IQFDClient _qfdClient;

        private QfdGetResourceClient(IQFDClient qfdClient)
        {
            _qfdClient = qfdClient;
        }

        public Task<HttpResponseMessage> GetRFProxyResourceAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            string? regionName,
            int retryFlowCount,
            CancellationToken cancellationToken)
        {
            return _qfdClient.GetPacificResourceAsync(
                resourceId: resourceId, 
                tenantId: tenantId, 
                apiVersion: apiVersion, 
                clientRequestId: null, 
                cancellationToken: cancellationToken);
        }
    }
}
