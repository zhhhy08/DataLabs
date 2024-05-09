namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Grpc
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Collections.Generic;

    public class GrpcClientOption
    {
        // TODO
        // do we need http2 flow control ?? -> experiment/investigation 
        // Task: 20847547

        public GrpcLBPolicy LBPolicy { get; set; } = GrpcLBPolicy.ROUND_ROBIN;

        public int MaxAttempts { get; set; } = 3; // No retry, MaxAttempts include original attempt

        public TimeSpan RetryInitialBackoff { get; set; } = TimeSpan.FromSeconds(1); // 1 second

        public TimeSpan RetryMaxBackoff { get; set; } = TimeSpan.FromSeconds(5); // 5 seconds

        public double RetryBackoffMultiplier { get; set; } = 1.5;

        public int MaxReceiveMessageSizeMB { get; set; } = 8;

        public bool UseMultiConnections { get; set; } = false;

        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5); // 5 sec

        public TimeSpan PooledConnectionIdleTimeout { get; set; } = TimeSpan.Zero; // Use default IdleTimeout

        // The client will send a keep alive ping to the server if it doesn't receive any frames on a connection for this period of time.
        public TimeSpan SocketKeepAlivePingDelay { get; set; } = TimeSpan.Zero; // Use Default

        // Keep alive pings are sent when a period of inactivity exceeds the configured KeepAlivePingDelay value.
        // The client will close the connection if it doesn't receive any frames within the timeout.
        public TimeSpan SocketKeepAlivePingTimeout { get; set; } = TimeSpan.Zero; // Use Default

        public TimeSpan DnsRefreshInterval { get; set; } = TimeSpan.FromSeconds(30); // 30 seconds

        public GrpcClientOption(IDictionary<string, string>? keyValuePairs)
        {
            if (keyValuePairs == null || keyValuePairs.Count == 0)
            {
                // use all default value
                return;
            }

            if (keyValuePairs.TryGetValue(nameof(LBPolicy), out var mapValue))
            {
                LBPolicy = StringEnumCache.GetEnumIgnoreCase<GrpcLBPolicy>(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(MaxAttempts), out mapValue))
            {
                MaxAttempts = int.Parse(mapValue);
                if (MaxAttempts < 2)
                {
                    MaxAttempts = 2; // At this time, we do always at least one time retry based on Unavailable status code
                }
            }

            if (keyValuePairs.TryGetValue(nameof(RetryInitialBackoff), out mapValue))
            {
                RetryInitialBackoff = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(RetryMaxBackoff), out mapValue))
            {
                RetryMaxBackoff = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(RetryBackoffMultiplier), out mapValue))
            {
                RetryBackoffMultiplier = double.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(MaxReceiveMessageSizeMB), out mapValue))
            {
                MaxReceiveMessageSizeMB = int.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(UseMultiConnections), out mapValue))
            {
                UseMultiConnections = bool.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(ConnectTimeout), out mapValue))
            {
                ConnectTimeout = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(PooledConnectionIdleTimeout), out mapValue))
            {
                PooledConnectionIdleTimeout = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(SocketKeepAlivePingDelay), out mapValue))
            {
                SocketKeepAlivePingDelay = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(SocketKeepAlivePingTimeout), out mapValue))
            {
                SocketKeepAlivePingTimeout = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(DnsRefreshInterval), out mapValue))
            {
                DnsRefreshInterval = TimeSpan.Parse(mapValue);
            }
        }
    }
}
