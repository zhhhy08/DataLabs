namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SelectionStrategy;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using StackExchange.Redis;
    using System;
    using System.Collections.Generic;

    public class CacheConnectionOption
    {
        public int NumConnections { get; } = 4;

        /// The number of times to repeat the initial connect cycle if no servers respond promptly.
        public int ConnectRetry { get; } = 0;

        /// Specifies the time that should be allowed for connection
        public TimeSpan ConnectTimeout { get; } = TimeSpan.FromSeconds(5); // 5 sec, based on testing, less than 5 sec might cause connection timeout

        /// Specifies the time that the system should allow for operations 
        public TimeSpan OperationTimeout { get; } = TimeSpan.FromSeconds(5); // 5 sec

        /// Specifies the time at which connections should be pinged to ensure validity.
        public TimeSpan KeepAliveTime { get; } = TimeSpan.FromSeconds(30);

        /// The backlog policy to be used for commands when a connection is unhealthy
        /// FailFast: Failing fast and not attempting to queue and retry when a connection is available again.
        /// Default: backlog policy which will allow commands to be issues against an endpoint and queue up.
        /// Commands are still subject to their async timeout (which serves as a queue size check).
        public BacklogPolicy BacklogPolicy { get; } = BacklogPolicy.FailFast;

        public ConnectionSelectionStrategy ConnectionSelectionStrategy { get; } = ConnectionSelectionStrategy.Random;

        public CacheConnectionOption(IDictionary<string, string>? keyValuePairs)
        {
            if (keyValuePairs == null || keyValuePairs.Count == 0)
            {
                return;
            }
                
            if (keyValuePairs.TryGetValue(nameof(NumConnections), out var mapValue))
            {
                NumConnections = int.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(ConnectRetry), out mapValue))
            {
                ConnectRetry = int.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(ConnectTimeout), out mapValue))
            {
                ConnectTimeout = TimeSpan.Parse(mapValue);
                if (ConnectTimeout.TotalSeconds < 5)
                {
                    ConnectTimeout = TimeSpan.FromSeconds(5);
                    //based on testing, less than 5 sec might cause connection timeout
                }
            }

            if (keyValuePairs.TryGetValue(nameof(OperationTimeout), out mapValue))
            {
                OperationTimeout = TimeSpan.Parse(mapValue);
                if (OperationTimeout.TotalSeconds < 5)
                {
                    OperationTimeout = TimeSpan.FromSeconds(5);
                    //based on testing, less than 5 sec might cause connection timeout
                }
            }

            if (keyValuePairs.TryGetValue(nameof(KeepAliveTime), out mapValue))
            {
                KeepAliveTime = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(BacklogPolicy), out mapValue))
            {
                if (string.Equals(mapValue, "FailFast", StringComparison.OrdinalIgnoreCase))
                {
                    BacklogPolicy = BacklogPolicy.FailFast;
                }
                else
                {
                    BacklogPolicy = BacklogPolicy.Default;
                }
            }

            if (keyValuePairs.TryGetValue(nameof(ConnectionSelectionStrategy), out mapValue))
            {
                ConnectionSelectionStrategy = StringEnumCache.GetEnumIgnoreCase<ConnectionSelectionStrategy>(mapValue);
            }
        }

        public static ConfigurationOptions CreateConfigurationOptions(string dnsAddress, int port, CacheConnectionOption cacheConnectionOption)
        {
            return new ConfigurationOptions
            {
                EndPoints = { { dnsAddress, port } },
                AbortOnConnectFail = false,
                ResolveDns = false, // If enabled the ConnectionMultiplexer will not re - resolve DNS when attempting to re - connect after a connection failure.

                //// Specifies the time in seconds at which connections should be pinged to ensure validity.
                KeepAlive = (int)cacheConnectionOption.KeepAliveTime.TotalSeconds,

                /// The number of times to repeat the initial connect cycle if no servers respond promptly.
                ConnectRetry = cacheConnectionOption.ConnectRetry,

                /// Specifies the time in milliseconds that should be allowed for connection (defaults to 5 seconds unless SyncTimeout is higher).
                ConnectTimeout = (int)cacheConnectionOption.ConnectTimeout.TotalMilliseconds,

                /// Specifies the time in milliseconds that the system should allow for operations (defaults to 5 seconds).
                SyncTimeout = (int)cacheConnectionOption.OperationTimeout.TotalMilliseconds,
                AsyncTimeout = (int)cacheConnectionOption.OperationTimeout.TotalMilliseconds,

                /// FailFast: Failing fast and not attempting to queue and retry when a connection is available again.
                BacklogPolicy = cacheConnectionOption.BacklogPolicy
            };
        }

        public static ISelectionStrategy<IConnectionMultiplexerWrapper, string> CreateConnectionSelectionStrategy(ConnectionSelectionStrategy connectionSelectionStrategy)
        {
            return connectionSelectionStrategy switch
            {
                ConnectionSelectionStrategy.RoundRobin => new RoundRobinSelectionStrategy<IConnectionMultiplexerWrapper, string>(),
                ConnectionSelectionStrategy.Random => RandomSelectionStrategy<IConnectionMultiplexerWrapper, string>.Instance,
                _ => throw new ArgumentOutOfRangeException(nameof(connectionSelectionStrategy), connectionSelectionStrategy, null)
            };
        }
    }

    public enum ConnectionSelectionStrategy
    {
        RoundRobin,
        Random
    }
}
 