namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Grpc
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Http;
    using global::Grpc.Core;
    using global::Grpc.Net.Client;
    using global::Grpc.Net.Client.Balancer;
    using global::Grpc.Net.Client.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PodHealth;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    [ExcludeFromCodeCoverage]
    public class GrpcUtils
    {
        // TODO
        // do we need http2 flow control ?? -> experiment/investigation 
        // Task: 20847547

        private static GrpcChannelOptions CreateChannelOptions(
            GrpcClientOption grpcClientOption,
            IServiceProvider serviceProvider,
            HttpClient? httpClient)
        {
            var defaultMethodConfig = new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = grpcClientOption.MaxAttempts,
                    InitialBackoff = grpcClientOption.RetryInitialBackoff,
                    MaxBackoff = grpcClientOption.RetryMaxBackoff,
                    BackoffMultiplier = grpcClientOption.RetryBackoffMultiplier,
                    RetryableStatusCodes = { StatusCode.Unavailable }
                }
            };

            var handler = new SocketsHttpHandler
            {
                ConnectTimeout = grpcClientOption.ConnectTimeout,
                EnableMultipleHttp2Connections = grpcClientOption.UseMultiConnections,
            };

            if (!TimeSpan.Zero.Equals(grpcClientOption.PooledConnectionIdleTimeout))
            {
                // Explicitly defined timeout,
                // Let's use it. otherwise let's use just .net default value
                handler.PooledConnectionIdleTimeout = grpcClientOption.PooledConnectionIdleTimeout;
            }

            if (!TimeSpan.Zero.Equals(grpcClientOption.SocketKeepAlivePingDelay))
            {
                // Explicitly defined KeepAlivePingDelay,
                // Let's use it. otherwise let's use just .net default value
                handler.KeepAlivePingDelay = grpcClientOption.SocketKeepAlivePingDelay;
            }

            if (!TimeSpan.Zero.Equals(grpcClientOption.SocketKeepAlivePingTimeout))
            {
                // Explicitly defined KeepAlivePingTimeout
                // Let's use it. otherwise let's use just .net default value
                handler.KeepAlivePingTimeout = grpcClientOption.SocketKeepAlivePingTimeout;
            }

            var grpcChannelOptions = new GrpcChannelOptions
            {
                LoggerFactory = DataLabLoggerFactory.GetLoggerFactory(),
                MaxSendMessageSize = null, // no limit
                MaxReceiveMessageSize = grpcClientOption.MaxReceiveMessageSizeMB * 1024*1024,
                Credentials = ChannelCredentials.Insecure,
                ThrowOperationCanceledOnCancellation = true,
                MaxRetryAttempts = grpcClientOption.MaxAttempts,
                ServiceConfig = httpClient != null ? null: new ServiceConfig
                {
                    MethodConfigs = { defaultMethodConfig },
                    LoadBalancingConfigs = { new LoadBalancingConfig(HealthAwareLoadBalancerFactory.HealthAwarePolicyName) }
                    //LoadBalancingConfigs = { new RoundRobinConfig() }
                },
                ServiceProvider = serviceProvider,
                HttpHandler = httpClient != null ? null : handler,
                HttpClient = httpClient,
                DisposeHttpClient = httpClient == null
            };

            return grpcChannelOptions;
        }

        public static GrpcChannel CreateGrpcChannel(
            string addr,
            GrpcClientOption grpcClientOption,
            IPodHealthManager podHealthManager,
            HttpClient? httpClient = null)
        {
            GuardHelper.ArgumentNotNullOrEmpty(addr, nameof(addr));
            if (ConfigMapUtil.RunningInContainer)
            {
                GuardHelper.ArgumentConstraintCheck(addr.StartsWith("dns:"), "Addr should start with dns:");
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ResolverFactory>(sp => new DnsResolverFactory(refreshInterval: grpcClientOption.DnsRefreshInterval));
            serviceCollection.AddSingleton<IPodHealthManager>(podHealthManager);
            serviceCollection.AddSingleton<LoadBalancerFactory, HealthAwareLoadBalancerFactory>();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var channelOptions = CreateChannelOptions(
                grpcClientOption: grpcClientOption,
                serviceProvider: serviceProvider,
                httpClient: httpClient);

            //_channel = GrpcChannel.ForAddress("dns:///solution-partner.default.svc.cluster.local:5071", grpcChannelOptions);
            var channel = GrpcChannel.ForAddress(addr, channelOptions);
            return channel;
        }

        public static GrpcChannel CreateGrpcChannel(
            string hostIp, 
            string hostPort,
            GrpcClientOption grpcClientOption,
            IPodHealthManager podHealthManager,
            HttpClient? httpClient = null)
        {
            GuardHelper.ArgumentNotNullOrEmpty(hostIp, nameof(hostIp));
            GuardHelper.ArgumentNotNullOrEmpty(hostPort, nameof(hostPort));

            grpcClientOption.LBPolicy = GrpcLBPolicy.LOCAL;
            var grpcAddr = "http://" + hostIp + ":" + hostPort;

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IPodHealthManager>(podHealthManager);
            serviceCollection.AddSingleton<LoadBalancerFactory, HealthAwareLoadBalancerFactory>();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var channelOptions = CreateChannelOptions(
                grpcClientOption: grpcClientOption,
                serviceProvider: serviceProvider,
                httpClient: httpClient);

            var channel = GrpcChannel.ForAddress(grpcAddr, channelOptions);
            return channel;
        }

        public static bool IsOperationCanceled(Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                return true;
            }

            return ex is RpcException rpcException && rpcException.StatusCode == StatusCode.DeadlineExceeded;
        }
    }
}
