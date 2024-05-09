namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client
{
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class EndPointSelector : IEndPointSelector
    {
        private static readonly ILogger<EndPointSelector> Logger = DataLabLoggerFactory.CreateLogger<EndPointSelector>();

        public Uri[] GetPrimaryEndPoints => _primaryEndPointUris;
        public Uri[]? GetBackupEndPoints => _backupEndPointUris;

        private Uri[] _primaryEndPointUris;
        private Uri[]? _backupEndPointUris;
        private readonly string? _primaryConfigKey;
        private string? _primaryConfigValue;
        private readonly string? _backupConfigKey;
        private string? _backupConfigValue;

        public EndPointSelector(string[] primaryEndpoints, string[]? backupEndPoints = null)
        {
            GuardHelper.ArgumentNotNullOrEmpty(primaryEndpoints, nameof(primaryEndpoints));
            _primaryConfigKey = null;
            _backupConfigKey = null;

            var primaryEndPointUris = new Uri[primaryEndpoints.Length];
            for (int i = 0; i < primaryEndpoints.Length; i++)
            {
                primaryEndPointUris[i] = new Uri(primaryEndpoints[i]);
            }
            Interlocked.Exchange(ref _primaryEndPointUris, primaryEndPointUris);

            if (backupEndPoints?.Length > 0)
            {
                var backEndPointUris = new Uri[backupEndPoints.Length];
                for (int i = 0; i < backupEndPoints.Length; i++)
                {
                    backEndPointUris[i] = new Uri(backupEndPoints[i]);
                }
                Interlocked.Exchange(ref _backupEndPointUris, backEndPointUris);
            }
        }

        public EndPointSelector(string primaryEndPointConfigKey, string? backupEndPointConfigKey, IConfiguration configuration)
        {
            _primaryConfigKey = primaryEndPointConfigKey;
            _primaryConfigValue = configuration.GetValueWithCallBack<string>(_primaryConfigKey, UpdatePrimaryEndPoints, string.Empty);
            GuardHelper.ArgumentNotNullOrEmpty(_primaryConfigValue);

            var primaryEndPointSet = _primaryConfigValue.ConvertToSet(caseSensitive: false);
            var primaryEndpoints = primaryEndPointSet.ToArray();
            var primaryEndPointUris = new Uri[primaryEndpoints.Length];
            for (int i = 0; i < primaryEndpoints.Length; i++)
            {
                primaryEndPointUris[i] = new Uri(primaryEndpoints[i]);
            }
            Interlocked.Exchange(ref _primaryEndPointUris, primaryEndPointUris);

            _backupConfigKey = backupEndPointConfigKey;
            if (_backupConfigKey != null)
            {
                _backupConfigValue = configuration.GetValueWithCallBack<string>(_backupConfigKey, UpdateBackupEndPoints, string.Empty);
                if (string.IsNullOrWhiteSpace(_backupConfigValue))
                {
                    _backupConfigValue = null;
                }
                else
                {
                    var backupEndPointSet = _backupConfigValue.ConvertToSet(caseSensitive: false);
                    var backupEndpoints = backupEndPointSet.ToArray();
                    var backupEndPointUris = new Uri[backupEndpoints.Length];
                    for (int i = 0; i < backupEndpoints.Length; i++)
                    {
                        backupEndPointUris[i] = new Uri(backupEndpoints[i]);
                    }
                    Interlocked.Exchange(ref _backupEndPointUris, backupEndPointUris);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Uri GetEndPoint(out int outIndex)
        {
            var endPointUris = _primaryEndPointUris;
            outIndex = endPointUris.Length == 1 ? 0 : ThreadSafeRandom.Next(endPointUris.Length);
            return endPointUris[outIndex];
        }

        private Task UpdatePrimaryEndPoints(string newEndpointString)
        {
            if (string.IsNullOrWhiteSpace(newEndpointString))
            {
                return Task.CompletedTask;
            }

            var endPointSet = newEndpointString.ConvertToSet(caseSensitive: false);
            if (endPointSet.Count == 0)
            {
                Logger.LogError("{config} must have valid endpoints", _primaryConfigKey);
                return Task.CompletedTask;
            }

            var oldValue = _primaryConfigValue;
            var newValue = newEndpointString;

            var newEndpoints = endPointSet.ToArray();
            var newEndPointUris = new Uri[newEndpoints.Length];
            for (int i = 0; i < newEndpoints.Length; i++)
            {
                newEndPointUris[i] = new Uri(newEndpoints[i]);
            }
            
            Interlocked.Exchange(ref _primaryEndPointUris, newEndPointUris);
            Interlocked.Exchange(ref _primaryConfigValue, newEndpointString);

            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                _primaryConfigKey, oldValue, newValue);

            return Task.CompletedTask;
        }

        private Task UpdateBackupEndPoints(string newEndpointString)
        {
            var endPointSet = newEndpointString.ConvertToSet(caseSensitive: false);
            var oldValue = _backupConfigValue;
            var newValue = newEndpointString;

            if (endPointSet.Count == 0)
            {
                Interlocked.Exchange(ref _backupEndPointUris, null);
            }
            else
            {
                var newEndpoints = endPointSet.ToArray();
                var newEndPointUris = new Uri[newEndpoints.Length];
                for (int i = 0; i < newEndpoints.Length; i++)
                {
                    newEndPointUris[i] = new Uri(newEndpoints[i]);
                }
                Interlocked.Exchange(ref _backupEndPointUris, newEndPointUris);
            }
            Interlocked.Exchange(ref _backupConfigValue, newEndpointString);

            Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                _backupConfigKey, oldValue, newValue);

            return Task.CompletedTask;
        }
    }
}
