namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Grpc
{
    using global::Grpc.Net.Client.Balancer;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PodHealth;

    public class HealthAwareLoadBalancerFactory : LoadBalancerFactory
    {
        public const string HealthAwarePolicyName = "health_aware";

        // Create a PodHealthAwareLoadBalancer when the name is 'health_aware'.
        public override string Name => HealthAwarePolicyName;

        private readonly IPodHealthManager _podHealthManager;

        public HealthAwareLoadBalancerFactory(IPodHealthManager podHealthManager)
        {
            _podHealthManager = podHealthManager;
        }

        public override LoadBalancer Create(LoadBalancerOptions options)
        {
            return new HealthAwareLoadBalancer(options.Controller, options.LoggerFactory, _podHealthManager);
        }
    }

    public class HealthAwareLoadBalancer : SubchannelsLoadBalancer
    {
        private readonly IPodHealthManager _podHealthManager;

        public HealthAwareLoadBalancer(IChannelControlHelper controller, ILoggerFactory loggerFactory, IPodHealthManager podHealthManager)
            : base(controller, loggerFactory)
        {
            _podHealthManager = podHealthManager;
        }

        protected override SubchannelPicker CreatePicker(IReadOnlyList<Subchannel> readySubchannels)
        {
            return new HealthAwareSubchannelPicker(readySubchannels, _podHealthManager);
        }
    }
}
