namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Utils
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using System.Net;
    using System.Net.Http;

    internal class RFProxyHttpResponseMessage : HttpResponseMessage
    {
        public ResourceCacheDataFormat DataFormat { get; set; }
        public string? DataETag { get; set; }
        public long DataTimeStamp { get; set; }
        public long? InsertionTimeStamp { get; set; }

        public RFProxyHttpResponseMessage(HttpStatusCode statusCode) : base(statusCode)
        {
        }

    }
}
