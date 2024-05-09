namespace Microsoft.WindowsAzure.Governance.DataLabs.GarnetServer
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Garnet;
    using Garnet.common;
    using Microsoft.Extensions.Logging;

    [ExcludeFromCodeCoverage]
    class ServerMonitor
    {
        readonly GarnetServer server;
        readonly TimeSpan pollingPeriod;
        readonly TimeSpan resettingPeriod;
        readonly ILogger logger;

        /// <summary>
        /// Create new server monitor instance
        /// </summary>
        public ServerMonitor(GarnetServer server, TimeSpan pollingPeriod, TimeSpan resettingPeriod, ILogger logger = null)
        {
            this.server = server;
            this.pollingPeriod = pollingPeriod;
            this.resettingPeriod = resettingPeriod;
            this.logger = logger;
        }

        /// <summary>
        /// Start server monitor task
        /// </summary>
        public void Start()
        {
            Task.Run(DisplayMetrics);
            Task.Run(ResetLatencyMetricsMetrics);
        }

        async Task DisplayMetrics()
        {
            while (true)
            {
                try
                {
                    foreach (var (type, items) in server.Metrics.GetInfoMetrics())
                    {
                        this.logger?.LogInformation($"Info {GetSectionRespInfo(type, items)}");
                    }

                    foreach (var (type, items) in server.Metrics.GetLatencyMetrics())
                    {
                        this.logger?.LogInformation($"Latency {GetLatencyInfo(type, items)}");
                    }
                }
                catch (Exception ex)
                {
                    this.logger?.LogError(ex, "Error occurred while fetching/logging server stats/metrics");
                }

                // Print metrics every polling period
                await Task.Delay(pollingPeriod);
            }
        }


        async Task ResetLatencyMetricsMetrics()
        {
            while (true)
            {
                server.Metrics.ResetLatencyMetrics();
                // Reset metrics every polling period
                await Task.Delay(resettingPeriod);
            }
        }


        public static string GetSectionHeader(InfoMetricsType infoType)
        {
            switch (infoType)
            {
                case InfoMetricsType.SERVER:
                    return "Server";
                case InfoMetricsType.MEMORY:
                    return "Memory";
                case InfoMetricsType.CLUSTER:
                    return "Cluster";
                case InfoMetricsType.REPLICATION:
                    return "Replication";
                case InfoMetricsType.STATS:
                    return "Stats";
                case InfoMetricsType.STORE:
                    return "Main Store";
                case InfoMetricsType.OBJECTSTORE:
                    return "Object Store";
                case InfoMetricsType.STOREHASHTABLE:
                    return "Store Hash Table Distribution";
                case InfoMetricsType.OBJECTSTOREHASHTABLE:
                    return "Object Store Hash Table Distribution";
                case InfoMetricsType.PERSISTENCE:
                    return "Persistence";
                case InfoMetricsType.CLIENTS:
                    return "Clients";
                case InfoMetricsType.KEYSPACE:
                    return "Keyspace";
                default:
                    return "Default";
            }
        }


        private string GetSectionRespInfo(InfoMetricsType infoType, MetricsItem[] info)
        {
            if (info == null) return "";
            string section = $"# {GetSectionHeader(infoType)}\r\n";
            for (int i = 0; i < info.Length; i++)
                section += $"{info[i].Name}:{info[i].Value}\r\n";
            return section;
        }

        public string GetLatencyInfo(LatencyMetricsType eventType, MetricsItem[] info)
        {
            if (info == null) return "";
            string section = $"# {eventType}\r\n";
            for (int i = 0; i < info.Length; i++)
                section += $"{info[i].Name}:{info[i].Value}\r\n";
            return section;
        }
    }
}
