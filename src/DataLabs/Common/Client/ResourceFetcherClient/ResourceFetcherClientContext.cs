using System.Threading;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient
{
    public class ResourceFetcherClientContext
    {
        private static readonly AsyncLocal<ResourceFetcherClientContext?> s_current = new();
        public static ResourceFetcherClientContext? Current
        {
            get { return s_current.Value; }
            set { s_current.Value = value; }
        }

        public required string? CorrelationId;
        public required string? OpenTelemetryActivityId;
        public required int RetryCount;
    }
}