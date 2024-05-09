namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.GetResourceClient
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;

    internal class ArmGetResourceClient : IRFProxyGetResourceClient
    {
        private static volatile ArmGetResourceClient? _instance;
        private static readonly object SyncRoot = new();

        public static IRFProxyGetResourceClient Create(IARMClient armClient)
        {
            if (_instance == null)
            {
                lock (SyncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new ArmGetResourceClient(armClient);
                    }
                }
            }
            return _instance;
        }

        private readonly IARMClient _armClient;

        private ArmGetResourceClient(IARMClient armClient)
        {
            _armClient = armClient;
        }

        public Task<HttpResponseMessage> GetRFProxyResourceAsync(
            string resourceId,
            string? tenantId,
            string apiVersion,
            string? regionName,
            int retryFlowCount,
            CancellationToken cancellationToken)
        {
            return _armClient.GetResourceAsync(
                resourceId: resourceId, 
                tenantId: tenantId, 
                apiVersion: apiVersion,
                useResourceGraph: false,
                clientRequestId: null, 
                cancellationToken: cancellationToken);
        }
    }
}
