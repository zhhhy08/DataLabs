namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Grpc;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;

    /// <summary>
    /// Rest client options class.
    /// </summary>
    public class RestClientOptions
    {
        /// <summary>
        /// Gets or sets User Agent
        /// </summary>
        public string UserAgent { get; }

        /// <summary>
        /// Gets or sets retry count for the same endpoint for transient error
        /// Because we are using Polly library, it shows 
        //
        // Summary:
        //     We are using Polly.PolicyBuilder to configure a Polly.Policy which will handle
        //     System.Net.Http.HttpClient requests that fail with conditions indicating a transient
        //     failure.
        //
        //     The built-in conditions configured to be handled are:
        //     • Network failures (as System.Net.Http.HttpRequestException)
        //     • HTTP 5XX status codes (server errors)
        //     • HTTP 408 status code (request timeout)
        // (HttpResponseMessage response) =>
        //     response.StatusCode >= HttpStatusCode.InternalServerError ||
        //     response.StatusCode == HttpStatusCode.RequestTimeout;
        /// 
        /// If you want to add more status codes in addition to above built-in codes, you can add them in AdditionalHttpStatusCodesForRetry
        /// 
        /// </summary>
        public HashSet<int>? AdditionalHttpStatusCodesForRetry { get; set; }

        /// <summary>
        /// When an endpoint fails with above transient HttpStatus code, retry to the same endPoint
        /// </summary>
        public int SameEndPointRetryCount { get; set; } = 0;

        /// <summary>
        /// When an endpoint fails with above transient HttpStatus code, next endpoint (if any) will be retired up to this number
        /// </summary>
        public int MaxDifferentEndPointRetryCount { get; set; } = 2;

        /// <summary>
        /// Gets or sets request timeout.
        /// This timeout will be applied only when above SameEndPointRetryCount is set.
        /// Once this value is set, each request will be timedout after specified period of time and will be retried
        /// </summary>
        public TimeSpan RequestTimeoutForRetry { get; set; }

        /// <summary>
        /// Http Request Version
        /// </summary>
        public Version HttpRequestVersion = HttpVersion.Version11;
        public HttpVersionPolicy VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

        /* The maximum time for a connection to be in the pool. The default value for this property in SocketsHttpHandler is InfiniteTimeSpan. */
        // This property defines maximal connection lifetime in the pool regardless of whether the connection is idle or active. 
        public static readonly TimeSpan DefaultPooledConnectionLifetime = TimeSpan.FromHours(1);
        public TimeSpan PooledConnectionLifetime = DefaultPooledConnectionLifetime;

        /* The maximum idle time for a connection in the pool. The default value for this property is 1 minute in .NET 6 and later versions; the default value is 2 minutes in .NET Core and .NET 5 */
        public static readonly TimeSpan DefaultPooledConnectionIdleTimeout = TimeSpan.FromMinutes(1);
        public TimeSpan PooledConnectionIdleTimeout = DefaultPooledConnectionIdleTimeout;

        public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(5);
        public TimeSpan ConnectTimeout = DefaultConnectTimeout;

        public TimeSpan RequestTimeout = TimeSpan.FromMinutes(1); // Max Timeout for each request 

        /* The maximum number of simultaneous TCP connections allowed to a single server. */
        public static int DefaultMaxConnectionsPerServer = 512;
        public int MaxConnectionsPerServer = DefaultMaxConnectionsPerServer;

        // The client will send a keep alive ping to the server if it doesn't receive any frames on a connection for this period of time.
        // Delay value must be greater than or equal to 1 second.Set to InfiniteTimeSpan to disable the keep alive ping.
        public static readonly TimeSpan DefaultSocketKeepAlivePingDelay = Timeout.InfiniteTimeSpan;
        public TimeSpan SocketKeepAlivePingDelay { get; set; } = DefaultSocketKeepAlivePingDelay;

        // Keep alive pings are sent when a period of inactivity exceeds the configured KeepAlivePingDelay value.
        // The client will close the connection if it doesn't receive any frames within the timeout.
        public TimeSpan SocketKeepAlivePingTimeout { get; set; } = TimeSpan.FromSeconds(10); // 10 seconds

        public HttpKeepAlivePingPolicy KeepAlivePingPolicy { get; set; } = HttpKeepAlivePingPolicy.WithActiveRequests;

        public bool EnableMultipleHttp2Connections = false;

        public RestClientOptions(string userAgent)
        {
            UserAgent = userAgent;
        }

        // This method can throw exception when format is not correct
        public void SetRestClientOptions(IDictionary<string, string>? keyValuePairs)
        {
            if (keyValuePairs == null || keyValuePairs.Count == 0)
            {
                // use all default value
                return;
            }

            StringSplitOptions stringSplitOptions = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;

            if (keyValuePairs.TryGetValue(nameof(AdditionalHttpStatusCodesForRetry), out var mapValue))
            {
                AdditionalHttpStatusCodesForRetry ??= new HashSet<int>(4);

                var statusCodes = mapValue.Split(',', stringSplitOptions);
                foreach (var statusCode in statusCodes)
                {
                    AdditionalHttpStatusCodesForRetry.Add(int.Parse(statusCode));
                }
            }

            if (keyValuePairs.TryGetValue(nameof(SameEndPointRetryCount), out mapValue))
            {
                SameEndPointRetryCount = int.Parse(mapValue);
                if (SameEndPointRetryCount < 0)
                {
                    SameEndPointRetryCount = 0;
                }
            }

            if (keyValuePairs.TryGetValue(nameof(MaxDifferentEndPointRetryCount), out mapValue))
            {
                MaxDifferentEndPointRetryCount = int.Parse(mapValue);
                if (MaxDifferentEndPointRetryCount < 0)
                {
                    MaxDifferentEndPointRetryCount = 0;
                }
            }

            if (keyValuePairs.TryGetValue(nameof(RequestTimeoutForRetry), out mapValue))
            {
                RequestTimeoutForRetry = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(HttpRequestVersion), out mapValue) && 
                !string.IsNullOrWhiteSpace(mapValue))
            {
                HttpRequestVersion = Version.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(PooledConnectionLifetime), out mapValue))
            {
                PooledConnectionLifetime = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(PooledConnectionIdleTimeout), out mapValue))
            {
                PooledConnectionIdleTimeout = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(ConnectTimeout), out mapValue))
            {
                ConnectTimeout = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(RequestTimeout), out mapValue))
            {
                RequestTimeout = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(MaxConnectionsPerServer), out mapValue))
            {
                MaxConnectionsPerServer = int.Parse(mapValue);
                if (MaxConnectionsPerServer <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxConnectionsPerServer), MaxConnectionsPerServer, "MaxConnectionsPerServer must be greater than 0");
                }
            }

            if (keyValuePairs.TryGetValue(nameof(SocketKeepAlivePingDelay), out mapValue))
            {
                SocketKeepAlivePingDelay = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(SocketKeepAlivePingTimeout), out mapValue))
            {
                SocketKeepAlivePingTimeout = TimeSpan.Parse(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(KeepAlivePingPolicy), out mapValue))
            {
                KeepAlivePingPolicy = StringEnumCache.GetEnumIgnoreCase<HttpKeepAlivePingPolicy>(mapValue);
            }

            if (keyValuePairs.TryGetValue(nameof(EnableMultipleHttp2Connections), out mapValue))
            {
                EnableMultipleHttp2Connections = bool.Parse(mapValue);
            }
        }
    }
}