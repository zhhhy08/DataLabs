namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientTimeOutManager
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class ClientTimeOutManager
    {
        private static readonly ILogger<ClientTimeOutManager> Logger = DataLabLoggerFactory.CreateLogger<ClientTimeOutManager>();

        public TimeSpan DefaultNonRetryFlowTimeOut => _timeOutConfigInfo.NonRetryFlowTimeOut;
        public TimeSpan DefaultRetryFlowTimeOut => _timeOutConfigInfo.RetryFlowTimeOut;
        public IDictionary<string, TimeOutInfo>? TimeOutMap => _timeOutMap;

        private const string ClientDefaultTimeOutString = "10/30";

        private string? _timeoutMappingsString;
        private Dictionary<string, TimeOutInfo>? _timeOutMap;
        private readonly TimeOutConfigInfo _timeOutConfigInfo;
        private readonly string _configKeyForTimeOutMappings;

        public ClientTimeOutManager(string configKeyForDefaultTimeOut, string configKeyForTimeOutMappings, IConfiguration configuration)
        {
            _configKeyForTimeOutMappings = configKeyForTimeOutMappings;

            _timeOutConfigInfo = new TimeOutConfigInfo(configKeyForDefaultTimeOut, ClientDefaultTimeOutString, configuration);

            _timeoutMappingsString = configuration.GetValueWithCallBack<string>(
                configKeyForTimeOutMappings, UpdateTimeOutMappings, string.Empty);

            if (!string.IsNullOrWhiteSpace(_timeoutMappingsString))
            {
                _timeOutMap = CreateTimeOutMap(_timeoutMappingsString);
            }
        }

        private Task UpdateTimeOutMappings(string newValue)
        {
            var oldValue = _timeoutMappingsString;

            if (newValue != null && newValue.EqualsInsensitively(_timeoutMappingsString))
            {
                return Task.CompletedTask;
            }

            try
            {
                var oldMappings = _timeOutMap;
                var newMappings = string.IsNullOrWhiteSpace(newValue) ? null : CreateTimeOutMap(newValue);

                if (Interlocked.CompareExchange(ref _timeOutMap, newMappings, oldMappings) == oldMappings)
                {
                    _timeoutMappingsString = newValue;
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                        _configKeyForTimeOutMappings, oldValue, newValue);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "TimeOutMapping has invalid format. value: {newVal}", newValue);
            }

            return Task.CompletedTask;
        }

        private static Dictionary<string, TimeOutInfo> CreateTimeOutMap(string value)
        {
            var dict = new Dictionary<string, TimeOutInfo>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
            {
                return dict;
            }

            var options = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;
            var lines = value.ConvertToList();
            foreach (var line in lines)
            {
                var splitLine = line.Split('|', options);
                if (splitLine.Length != 2)
                {
                    throw new ArgumentException($"Invalid timeout format: {line}");
                }

                var matchString = splitLine[0].ToLowerInvariant();
                var (nonRetryFlowTimeout, retryFlowTimeout) = SolutionConfigMapKeyValueUtils.ParseConfigTimeOut(splitLine[1]);
                var timeoutInfo = new TimeOutInfo
                {
                    NonRetryFlowTimeOut = nonRetryFlowTimeout,
                    RetryFlowTimeOut = retryFlowTimeout
                };
                
                dict.Add(matchString, timeoutInfo);
            }

            return dict;
        }
    }

    public class TimeOutInfo
    {
        public TimeSpan NonRetryFlowTimeOut;
        public TimeSpan RetryFlowTimeOut;
    }
}
