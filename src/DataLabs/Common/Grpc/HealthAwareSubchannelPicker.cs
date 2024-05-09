namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Grpc
{
    using global::Grpc.Net.Client.Balancer;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PodHealth;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;

    [ExcludeFromCodeCoverage]
    public class HealthAwareSubchannelPicker : SubchannelPicker
    {
        public const string HealthAwareSubchannelPickerCounterName = "HealthAwareSubchannelPickerCounter";
        public const string HealthAwareSubchannelPickerCreationCounterName = "HealthAwareSubchannelPickerCreationCounter";
        public const string HealthAwareSubchannelPickerUpdateDenyListCounterName = "HealthAwareSubchannelPickerUpdateDenyListCounter";

        private static readonly Counter<long> HealthAwareSubchannelPickerCounter =
            MetricLogger.CommonMeter.CreateCounter<long>(HealthAwareSubchannelPickerCounterName);

        private static readonly Counter<long> HealthAwareSubchannelPickerCreationCounter =
            MetricLogger.CommonMeter.CreateCounter<long>(HealthAwareSubchannelPickerCreationCounterName);

        private static readonly Counter<long> HealthAwareSubchannelPickerUpdateDenyListCounter =
            MetricLogger.CommonMeter.CreateCounter<long>(HealthAwareSubchannelPickerUpdateDenyListCounterName);

        private readonly IReadOnlyList<Subchannel> _subchannels;
        private readonly IPodHealthManager _podHealthManger;
        
        private HashSet<string> _currentDenyListedNodes;
        private IReadOnlyList<Subchannel> _allowedList;

        private readonly object _updateLock = new object();
        private readonly KeyValuePair<string, object?> _serviceAddrDimension;

        public HealthAwareSubchannelPicker(IReadOnlyList<Subchannel> subchannels, IPodHealthManager podHealthManger)
        {
            _serviceAddrDimension = new KeyValuePair<string, object?>(SolutionConstants.ServiceAddr, podHealthManger.ServiceName);
            HealthAwareSubchannelPickerCreationCounter.Add(1, _serviceAddrDimension);

            _subchannels = subchannels;
            _podHealthManger = podHealthManger;
            _currentDenyListedNodes = podHealthManger.DenyListedNodes;
            
            _allowedList = CreateAllowedChannels(subchannels, _currentDenyListedNodes);
        }

        private void UpdateAllowedList()
        {
            lock (_updateLock)
            {
                var newDenyListedNodes = _podHealthManger.DenyListedNodes;
                var newAllowedList = CreateAllowedChannels(_subchannels, newDenyListedNodes);

                Interlocked.Exchange(ref _currentDenyListedNodes, newDenyListedNodes);
                Interlocked.Exchange(ref _allowedList, newAllowedList);

                HealthAwareSubchannelPickerUpdateDenyListCounter.Add(1, _serviceAddrDimension);
            }
        }

        public override PickResult Pick(PickContext context)
        {
            if (!ReferenceEquals(_currentDenyListedNodes, _podHealthManger.DenyListedNodes))
            {
                // DenyListedNodes has been updated
                UpdateAllowedList();
            }

            // Because of HotConfig, it is better to create a local copy of the variables
            // Pick a random subchannel
            var allowedList = _allowedList;
            var index = allowedList.Count > 1 ? Random.Shared.Next(0, allowedList.Count) : 0;
            var subChannel =  allowedList[index];
            var host = subChannel.CurrentAddress?.EndPoint?.Host;

            // One more checking for DenyList
            if (_currentDenyListedNodes.Count > 0 && !string.IsNullOrEmpty(host) && _currentDenyListedNodes.Contains(host))
            {
                // This is not expected flow but just incase
                UpdateAllowedList();

                allowedList = _allowedList;
                index = allowedList.Count > 1 ? Random.Shared.Next(0, allowedList.Count) : 0;
                subChannel = allowedList[index];
                host = subChannel.CurrentAddress?.EndPoint?.Host;
            }

            // Metric
            var clientIp = MonitoringConstants.POD_IP;
            
            HealthAwareSubchannelPickerCounter.Add(1,
                _serviceAddrDimension,
                new KeyValuePair<string, object?>(SolutionConstants.ClientIP, clientIp),
                new KeyValuePair<string, object?>(SolutionConstants.ServerIP, host ?? "Unknown"));

            return PickResult.ForSubchannel(subChannel);
        }

        private static IReadOnlyList<Subchannel> CreateAllowedChannels(IReadOnlyList<Subchannel> subchannels, HashSet<string> denyListedNodes)
        {
            if (denyListedNodes == null || denyListedNodes.Count == 0)
            {
                return subchannels;
            }

            var allowedList = new List<Subchannel>(subchannels.Count);

            foreach (var subchannel in subchannels)
            {
                var host = subchannel.CurrentAddress?.EndPoint?.Host;
                if (!string.IsNullOrEmpty(host) && denyListedNodes.Contains(host))
                {
                    continue;
                }

                allowedList.Add(subchannel);
            }

            return allowedList;
        }
    }
}
