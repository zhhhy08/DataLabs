// <copyright file="ConfigurationWithCallBack.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public class ConfigurationWithCallBack : IConfigurationWithCallBack, IDisposable
    {
        private static readonly ActivityMonitorFactory ConfigurationWithCallBackCriticalError =
            new("ConfigurationWithCallBack.CriticalError", LogLevel.Critical);

        private static readonly ILogger<ConfigurationWithCallBack> Logger =
            DataLabLoggerFactory.CreateLogger<ConfigurationWithCallBack>();

        public bool AllowMultiCallBacks { get; set; }

        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, TypeAndValue> _keyValues;

        private readonly SemaphoreSlim ConfigHandlesLock = new SemaphoreSlim(1);
        private readonly TimeSpan MaxSemaphoreWaitTimeInMinute = TimeSpan.FromMinutes(1);
    
        /// <summary>
        /// The persistent wrappers
        /// </summary>
        private readonly IList<CallBackWrapper> _persistentWrappers;

        /// <summary>
        /// The table that associates wrapper's callback targets and the wrappers.
        /// </summary>
        private readonly ConditionalWeakTable<object, IList<CallBackWrapper>> _targetToWrapperMappings;

        public ConfigurationWithCallBack(IConfiguration configuration)
        {
            _configuration = configuration;
            _keyValues = new Dictionary<string, TypeAndValue>(StringComparer.OrdinalIgnoreCase);

            _persistentWrappers = new List<CallBackWrapper>();
            _targetToWrapperMappings = new ConditionalWeakTable<object, IList<CallBackWrapper>>();
        }

        public string? this[string key]
        {
            get => _configuration[key];
            set => _configuration[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return _configuration.GetChildren();
        }

        public IChangeToken GetReloadToken()
        {
            return _configuration.GetReloadToken();
        }

        public IConfigurationSection GetSection(string key)
        {
            return _configuration.GetSection(key);
        }

        public bool HasRegisteredCallBack(string key)
        {
            if (!ConfigHandlesLock.Wait(MaxSemaphoreWaitTimeInMinute))
            {
                throw new TimeoutException("ConfigHandlesLock semaphore not entered.");
            }

            try
            {
                return _keyValues.ContainsKey(key);
            }
            finally
            {
                ConfigHandlesLock.Release();
            }

        }

        public T? GetValueWithCallBack<T>(string key, Func<T, Task> callback, T? defaultValue, bool allowMultiCallBacks = true) where T : notnull
        {
            if (!ConfigHandlesLock.Wait(MaxSemaphoreWaitTimeInMinute))
            {
                throw new TimeoutException("ConfigHandlesLock semaphore not entered.");
            }

            try
            {
                var type = typeof(T);
                var value = _configuration.GetValue(type, key, defaultValue);

                if (_keyValues.TryGetValue(key, out TypeAndValue? typeAndValue) && typeAndValue != null)
                {
                    if (!AllowMultiCallBacks && !allowMultiCallBacks)
                    {
                        // check if previous callback exist
                        throw new ArgumentException(key + " is already registered with other callback");
                    }

                    typeAndValue.Type = type;
                    typeAndValue.Value = value;
                }
                else
                {
                    typeAndValue = new TypeAndValue(type, value);
                    _keyValues[key] = typeAndValue;
                }

                var wrapper = new CallBackWrapper<T>(key, callback);
                RegisterWrapper(callback.Target, wrapper);
                return (T?)value;
            }
            finally
            {
                ConfigHandlesLock.Release();
            }
        }

        public void CheckChangeAndCallBack(CancellationToken cancellationToken)
        {
            var hasLock = false;
            try
            {
                if (!ConfigHandlesLock.Wait(MaxSemaphoreWaitTimeInMinute, cancellationToken))
                {
                    throw new TimeoutException("ConfigHandlesLock semaphore not entered.");
                }

                hasLock = true;

                var modifiedConfigs = new Dictionary<string, TypeAndValue>();
                List<string> keys = _keyValues.Keys.ToList();

                foreach (var key in keys)
                {
                    var typeAndValue = _keyValues[key];

                    // new value
                    var newValue = _configuration.GetValue(typeAndValue.Type, key, typeAndValue.Value);
                    if (newValue != null && !newValue.Equals(typeAndValue.Value))
                    {
                        // Security Warning!!!!!!!
                        // Don't print value here
                        // Value might have secure/sensitive value
                        Logger.LogWarning("Value is changed for {Key}", key);

                        // Update new value
                        typeAndValue.Value = newValue;
                        modifiedConfigs[key] = typeAndValue;
                    }
                }

                if (modifiedConfigs.Count > 0)
                {
                    ExecuteCallBacks(modifiedConfigs);
                }
            }
            catch(Exception ex)
            {
                using var criticalLogMonitor = ConfigurationWithCallBackCriticalError.ToMonitor();
                criticalLogMonitor.Activity[SolutionConstants.MethodName] = "CheckChangeAndCallBack";
                criticalLogMonitor.OnError(ex);
            }
            finally
            {
                if (hasLock)
                {
                    ConfigHandlesLock.Release();
                }
            }
        }

        public void Dispose()
        {
            ConfigHandlesLock.Dispose();
        }

        private void RegisterWrapper(object? target, CallBackWrapper wrapper)
        {
            if (target == null)
            {
                // wrapper's callback is a static method, make the wrapper persistent.
                _persistentWrappers.Add(wrapper);
            }
            else if (IsCompilerGeneratedType(target.GetType()))
            {
                // The callback is a lambda function, we do not support this as we
                // don't know whether the lambda function captures any instance object or not.
                throw new NotSupportedException(
                    "The callback cannot be a lambda function. " +
                    "Consider making it a static or instance method.");
            }
            else
            {
                // wrapper's callback is a instance method, need to bind the lifecycle
                // of the wrapper to the lifecycle of the instance to avoid memory leak.
                if (!_targetToWrapperMappings.TryGetValue(target, out var wrappers))
                {
                    wrappers = new List<CallBackWrapper>();
                    _targetToWrapperMappings.Add(target, wrappers);
                }
                wrappers.Add(wrapper);
            }
        }

        private void ExecuteCallBack(CallBackWrapper wrapper, Dictionary<string, TypeAndValue> modifiedConfigs)
        {
            if (modifiedConfigs.TryGetValue(wrapper.Key, out TypeAndValue? value) && value != null)
            {
                var newValue = value.Value;
                _ = Task.Run(() => wrapper.ReloadInternalAsync(newValue!)); //background task
            }
        }

        private void ExecuteCallBacks(Dictionary<string, TypeAndValue> modifiedConfigs)
        {
            foreach(var persistWrapper in _persistentWrappers)
            {
                ExecuteCallBack(persistWrapper, modifiedConfigs);
            }

            foreach (var targetWrappers in _targetToWrapperMappings)
            {
                var wrapperList = targetWrappers.Value;
                foreach (var wrapper in wrapperList)
                {
                    ExecuteCallBack(wrapper, modifiedConfigs);
                }
            }
        }

        /// <summary>
        /// Determine whether the type is generated by the compiler.
        /// </summary>
        private static bool IsCompilerGeneratedType(Type type)
        {
            // Alternatively we can check whether the type is decorated with
            // CompilerGeneratedAttribute, but it's slower.
            return type.Name.Contains("<") || type.Name.Contains(">");
        }

        internal class TypeAndValue
        {
            public Type Type;
            public object? Value;

            public TypeAndValue(Type type, object? value)
            {
                Type = type;
                Value = value;
            }
        }

        public abstract class CallBackWrapper
        {
            public abstract string Key { get; }
            public abstract Task ReloadInternalAsync(object value);
        }

        internal class CallBackWrapper<T> : CallBackWrapper where T : notnull
        {
            /// <summary>
            /// The callback
            /// </summary>

            public override string Key { get; }
            private readonly Func<T, Task> _callback;

            internal CallBackWrapper(
                string key, Func<T, Task> callback)
            {
                Key = key;
                _callback = callback;
            }

            public override Task ReloadInternalAsync(object value)
            {
                try
                {
                    return _callback((T)value);
                }
                catch (Exception ex)
                {
                    using var criticalLogMonitor = ConfigurationWithCallBackCriticalError.ToMonitor();
                    criticalLogMonitor.Activity[SolutionConstants.MethodName] = "ReloadInternalAsync";
                    criticalLogMonitor.OnError(ex);
                }

                return Task.CompletedTask;
            }
        }
    }
}
