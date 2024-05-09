namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PodHealth
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    public class PodHealthManager : IPodHealthManager
    {
        private static readonly ILogger<PodHealthManager> Logger = DataLabLoggerFactory.CreateLogger<PodHealthManager>();

        public HashSet<string> DenyListedNodes => _combinedDenyListedNodes;
        public string ServiceName { get; }

        // DenyList can be updated by either configMap or dynamically by calling AddNodeToDenyList/RemoveNodeFromDenyList
        private HashSet<string> _combinedDenyListedNodes; // for every change, new instance will be created to avoid locking for every DenyList call
        private HashSet<string>? _configDenyListedNodes;
        private HashSet<string>? _dynamicDenyListedNodes;

        private string? _denyListConfigKey;
        private readonly object _updateLock = new object();

        public PodHealthManager(string serviceName) : this(serviceName, null)
        {
        }

        public PodHealthManager(string serviceName, string? denyListConfigKey)
        {
            ServiceName = serviceName;
            _denyListConfigKey = denyListConfigKey;
            _combinedDenyListedNodes = new HashSet<string>();

            if (!string.IsNullOrWhiteSpace(_denyListConfigKey))
            {
                var denyList = ConfigMapUtil.Configuration.GetValueWithCallBack<string>(_denyListConfigKey, UpdateDenyListConfig, SolutionConstants.NoneValue) ?? SolutionConstants.NoneValue;
                UpdateDenyListConfig(denyList);
            }
        }


        private Task UpdateDenyListConfig(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                // To avoid unexpected/future behavior of empty value reloading in configMap,
                // we will use explicit default value "none"
                return Task.CompletedTask;
            }

            lock (_updateLock)
            {
                var oldConfigDenyListedNodes = _configDenyListedNodes;
                HashSet<string> newConfigDenyListedNodes;

                value = value.Trim();
                if (value.EqualsInsensitively(SolutionConstants.NoneValue))
                {
                    newConfigDenyListedNodes = new HashSet<string>();
                    Logger.LogInformation($"Removed all nodes from Config DenyList");
                }
                else
                {
                    newConfigDenyListedNodes = value.ConvertToSet(caseSensitive: false);
                }

                var newCombinedDenyListedNodes = new HashSet<string>(newConfigDenyListedNodes);
                if (_dynamicDenyListedNodes != null)
                {
                    newCombinedDenyListedNodes.UnionWith(_dynamicDenyListedNodes);
                }

                var oldVal = oldConfigDenyListedNodes == null ? "" : string.Join(";", oldConfigDenyListedNodes);

                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                        _denyListConfigKey, oldVal, value);

                Interlocked.Exchange(ref _configDenyListedNodes, newConfigDenyListedNodes);
                Interlocked.Exchange(ref _combinedDenyListedNodes, newCombinedDenyListedNodes);

                return Task.CompletedTask;
            }
        }

        public void AddNodeToDenyList(IEnumerable<string> hosts)
        {
            lock (_updateLock)
            {
                _dynamicDenyListedNodes ??= new HashSet<string>();
                var oldDynamicDenyListedNodes = _dynamicDenyListedNodes;
                var newDynamicDenyListedNodes = new HashSet<string>(oldDynamicDenyListedNodes);

                foreach (var host in hosts)
                {
                    if (newDynamicDenyListedNodes.Add(host))
                    {
                        Logger.LogInformation($"Added {host} to Dynamic DenyList");
                    }
                }

                if (newDynamicDenyListedNodes.Count == oldDynamicDenyListedNodes.Count)
                {
                    // nothing changed
                    return;
                }

                var newCombinedDenyListedNodes = new HashSet<string>(newDynamicDenyListedNodes);
                if (_configDenyListedNodes != null)
                {
                    newCombinedDenyListedNodes.UnionWith(_configDenyListedNodes);
                }

                // Create new HashSet and swap it with the old one to avoid locking for every InDenyList call
                Interlocked.Exchange(ref _dynamicDenyListedNodes, newDynamicDenyListedNodes);
                Interlocked.Exchange(ref _combinedDenyListedNodes, newCombinedDenyListedNodes);
            }
        }

        public void RemoveNodeFromDenyList(IEnumerable<string> hosts)
        {
            lock (_updateLock)
            {
                if (_dynamicDenyListedNodes == null)
                {
                    return;
                }

                var oldDynamicDenyListedNodes = _dynamicDenyListedNodes;
                var newDynamicDenyListedNodes = new HashSet<string>(oldDynamicDenyListedNodes);

                foreach (var host in hosts)
                {
                    if (host.EqualsInsensitively("all"))
                    {
                        newDynamicDenyListedNodes.Clear();
                        Logger.LogInformation("Removed all from dynamic DenyList");
                        break;
                    }

                    if (newDynamicDenyListedNodes.Remove(host))
                    {
                        Logger.LogInformation($"Removed {host} from dynamic denyList");
                    }
                }

                if (newDynamicDenyListedNodes.Count == oldDynamicDenyListedNodes.Count)
                {
                    // nothing changed
                    return;
                }

                var newCombinedDenyListedNodes = new HashSet<string>(newDynamicDenyListedNodes);
                if (_configDenyListedNodes != null)
                {
                    newCombinedDenyListedNodes.UnionWith(_configDenyListedNodes);
                }

                // Create new HashSet and swap it with the old one to avoid locking for every InDenyList call
                Interlocked.Exchange(ref _dynamicDenyListedNodes, newDynamicDenyListedNodes);
                Interlocked.Exchange(ref _combinedDenyListedNodes, newCombinedDenyListedNodes);
            }
        }

        public void Dispose()
        {
        }
    }
}
