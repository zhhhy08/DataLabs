namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    public class CacheTTLManager : ICacheTTLManager
    {
        private static readonly ILogger<CacheTTLManager> Logger = DataLabLoggerFactory.CreateLogger<CacheTTLManager>();

        private const char LINE_DELIMETER = ';';
        private const char KEY_DELIMETER = '|';

        public bool HasResourceTypeMap => _resourceTypeTTLMap != null && _resourceTypeTTLMap.Count > 0;

        private TimeSpan _defaultInputCacheTTL;
        private TimeSpan _defaultOutputCacheTTL;
        private TimeSpan _defaultNotFoundEntryCacheTTL;

        private string? _resourceTypeTTLConfigValue;
        private IDictionary<string, TimeSpan> _resourceTypeTTLMap;

        private readonly List<ICacheTTLManagerUpdateListener> _updateListeners = new();
        private object _lockObject = new();

        public CacheTTLManager(IConfiguration configuration)
        {
            _defaultInputCacheTTL = configuration.GetValueWithCallBack<TimeSpan>(
                SolutionConstants.DefaultInputCacheTTL, UpdateInputCacheTTL, TimeSpan.Zero);
            _defaultOutputCacheTTL = configuration.GetValueWithCallBack<TimeSpan>(
                SolutionConstants.DefaultOutputCacheTTL, UpdateOutputCacheTTL, TimeSpan.Zero);
            _defaultNotFoundEntryCacheTTL = configuration.GetValueWithCallBack<TimeSpan>(
                SolutionConstants.DefaultNotFoundEntryCacheTTL, UpdateNotFoundEntryCacheTTL, TimeSpan.Zero);

            // TTL Support per resource Type
            _resourceTypeTTLConfigValue = configuration.GetValueWithCallBack<string>(
                SolutionConstants.ResourceTypeCacheTTLMappings, UpdateResourceTypeTTLMappings, string.Empty);

            _resourceTypeTTLMap = CreateResourceTypeTTLMap(_resourceTypeTTLConfigValue);
        }

        public TimeSpan GetCacheTTL(string? resourceType, bool inputType)
        {
            if (resourceType != null && 
                _resourceTypeTTLMap?.Count > 0 && 
                _resourceTypeTTLMap.TryGetValue(resourceType, out var resourceTypeTTL))
            {
                return resourceTypeTTL;
            }
            return inputType ? _defaultInputCacheTTL : _defaultOutputCacheTTL;
        }

        public TimeSpan GetCacheTTLForNotFoundEntry(string? resourceType)
        {
            if (resourceType != null && 
                _resourceTypeTTLMap?.Count > 0 &&
                _resourceTypeTTLMap.TryGetValue(resourceType, out var resourceTypeTTL))
            {
                return resourceTypeTTL;
            }
            return _defaultNotFoundEntryCacheTTL;
        }

        public void AddUpdateListener(ICacheTTLManagerUpdateListener updateListener)
        {
            _updateListeners.Add(updateListener);
        }

        private void NotifyUpdateListeners()
        {
            foreach (var Listener in _updateListeners)
            {
                Listener.NotifyUpdatedConfig(this);
            }
        }

        private Task UpdateResourceTypeTTLMappings(string? newConfigVal)
        {
            string? oldConfigVal = _resourceTypeTTLConfigValue;

            if (string.IsNullOrWhiteSpace(newConfigVal) || newConfigVal.Equals(oldConfigVal, StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            lock (_lockObject)
            {
                try
                {
                    var newMappings = CreateResourceTypeTTLMap(newConfigVal);
                    var oldMappings = _resourceTypeTTLMap;

                    if (Interlocked.CompareExchange(ref _resourceTypeTTLMap, newMappings, oldMappings) == oldMappings)
                    {
                        Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                            SolutionConstants.ResourceTypeCacheTTLMappings, oldConfigVal, newConfigVal);

                        _resourceTypeTTLConfigValue = newConfigVal;

                        NotifyUpdateListeners();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{config} has invalid format. value: {newVal}", SolutionConstants.ResourceTypeCacheTTLMappings, newConfigVal);
                }

                return Task.CompletedTask;
            }
        }

        private static IDictionary<string, TimeSpan> CreateResourceTypeTTLMap(string? value)
        {
            var dict = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
            {
                return dict;
            }

            var options = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;
            var lines = value.Split(LINE_DELIMETER, options);

            foreach (var line in lines)
            {
                var splitLine = line.Split(KEY_DELIMETER, options);
                if (splitLine.Length != 2)
                {
                    throw new ArgumentException($"Invalid value: {line}. it must be X|Y format");
                }

                string resourceType = splitLine[0].ToLowerInvariant();
                var ttl = TimeSpan.Parse(splitLine[1]);
                dict.Add(resourceType, ttl);
            }

            return dict;
        }

        private Task UpdateInputCacheTTL(TimeSpan newInterval)
        {
            lock (_lockObject)
            {
                var oldInterval = _defaultInputCacheTTL;
                if (newInterval != oldInterval)
                {
                    _defaultInputCacheTTL = newInterval;
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                        SolutionConstants.DefaultInputCacheTTL,
                        oldInterval.ToString(), newInterval.ToString());

                    NotifyUpdateListeners();
                }
                return Task.CompletedTask;
            }
        }

        private Task UpdateOutputCacheTTL(TimeSpan newInterval)
        {
            lock (_lockObject)
            {
                var oldInterval = _defaultOutputCacheTTL;
                if (newInterval != oldInterval)
                {
                    _defaultOutputCacheTTL = newInterval;
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                        SolutionConstants.DefaultOutputCacheTTL,
                        oldInterval.ToString(), newInterval.ToString());

                    NotifyUpdateListeners();
                }
                return Task.CompletedTask;
            }
        }

        private Task UpdateNotFoundEntryCacheTTL(TimeSpan newInterval)
        {
            lock(_lockObject)
            {
                var oldInterval = _defaultNotFoundEntryCacheTTL;
                if (newInterval != oldInterval)
                {
                    _defaultNotFoundEntryCacheTTL = newInterval;
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                        SolutionConstants.DefaultNotFoundEntryCacheTTL,
                        oldInterval.ToString(), newInterval.ToString());

                    NotifyUpdateListeners();
                }
                return Task.CompletedTask;
            }
            
        }
    }
}