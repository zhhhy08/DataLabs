namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient
{
    using System;
    using System.Net;
    using System.Net.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class ResourceFetcherClientOptions : RestClientOptions
    {
        public required IEndPointSelector EndPointSelector { get; init; }
        public required string ResourceFetcherTokenResource { get; init; }
        public required string ResourceFetcherHomeTenantId { get; init; }
        public required string PartnerName { get; init; }

        public ResourceFetcherClientOptions(IConfiguration configuration) :
            base(ResourceFetcherClient.UserAgent)
        {
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5); // Resource Fetcher should support Http20. So longer idle time should be ok because we will not use many connections to Resource Fetcher Client
            SocketKeepAlivePingDelay = TimeSpan.FromSeconds(30);
            SocketKeepAlivePingTimeout = TimeSpan.FromSeconds(10);
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always;

            var clientOptionName = SolutionConstants.ResourceFetcherClientOption;
            var clientOption = configuration.GetValue<string>(clientOptionName, string.Empty);
            SetRestClientOptions(clientOption.ConvertToDictionary(caseSensitive: false));

            // Resource Fetcher Proxy must use http2.0 (h2c)
            VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            HttpRequestVersion = HttpVersion.Version20;
        }
    }
}
