namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.RFProxyClients.OutputSourceOfTruth
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Utils;

    internal class RFProxyOutputSourceOfTruthClient : IRFProxyOutputSourceOfTruthClient
    {
        private static readonly ActivityMonitorFactory RFProxyOutputSourceOfTruthClientGetRFProxyResourceAsync =
            new ("RFProxyOutputSourceOfTruthClient.GetRFProxyResourceAsync");

        private readonly string _defaultRegionName;

        public RFProxyOutputSourceOfTruthClient(IConfiguration configuration)
        {
            _defaultRegionName = configuration.GetValue<string>(SolutionConstants.PrimaryRegionName)!;
            GuardHelper.ArgumentNotNullOrEmpty(_defaultRegionName, SolutionConstants.PrimaryRegionName);
        }

        public async Task<HttpResponseMessage> GetRFProxyResourceAsync(
            string resourceId, 
            string? tenantId, 
            string apiVersion, 
            string? regionName,
            int retryFlowCount,
            CancellationToken cancellationToken)
        {
            using var monitor = RFProxyOutputSourceOfTruthClientGetRFProxyResourceAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                if (string.IsNullOrEmpty(regionName))
                {
                    monitor.Activity[SolutionConstants.UsingDefaultRegionName] = true;
                    regionName = _defaultRegionName;
                }

                var regionConfigData = RegionConfigManager.GetRegionConfig(regionName);
                monitor.Activity[SolutionConstants.RegionName] = regionConfigData.RegionLocationName;

                var outputBlobClient = regionConfigData.outputBlobClient;
                GuardHelper.ArgumentNotNull(outputBlobClient);

                var blobResponse = await outputBlobClient.DownloadContentAsync(
                    resourceId: resourceId,
                    tenantId: tenantId,
                    exceptionOnNotFound: false,
                    retryFlowCount: retryFlowCount,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                RFProxyHttpResponseMessage rfProxyHttpResponseMessage;

                int resourceSize = blobResponse.binaryData == null ? 0 : blobResponse.binaryData.ToMemory().Length;
                if (resourceSize > 0)
                {
                    rfProxyHttpResponseMessage = new RFProxyHttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ReadOnlyMemoryHttpContent(blobResponse.binaryData!.ToMemory())
                    };

                    rfProxyHttpResponseMessage.DataETag = blobResponse.etag.HasValue ? blobResponse.etag.Value.ToString() : null;
                    rfProxyHttpResponseMessage.DataFormat = ResourceCacheDataFormat.ARN;
                    rfProxyHttpResponseMessage.DataTimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    rfProxyHttpResponseMessage.InsertionTimeStamp = 0;

                    monitor.Activity[SolutionConstants.ResourceSize] = resourceSize;
                    monitor.Activity[SolutionConstants.ETag] = rfProxyHttpResponseMessage.DataETag;
                    monitor.Activity[SolutionConstants.DataFormat] = rfProxyHttpResponseMessage.DataFormat.FastEnumToString();
                    monitor.Activity[SolutionConstants.DataTimeStamp] = rfProxyHttpResponseMessage.DataTimeStamp;
                    monitor.Activity[SolutionConstants.InsertionTimeStamp] = rfProxyHttpResponseMessage.InsertionTimeStamp;
                }
                else
                {
                    // Not Found
                    rfProxyHttpResponseMessage = new RFProxyHttpResponseMessage(HttpStatusCode.NotFound);
                }

                monitor.Activity[SolutionConstants.HttpStatusCode] = (int)rfProxyHttpResponseMessage.StatusCode;

                monitor.OnCompleted();
                return rfProxyHttpResponseMessage;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }
    }
}
