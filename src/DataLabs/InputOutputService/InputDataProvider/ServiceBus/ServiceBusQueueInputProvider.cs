namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.InputDataProvider.ServiceBus
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.ServiceBus;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;

    [ExcludeFromCodeCoverage]
    internal class ServiceBusQueueInputProvider : IInputProvider
    {
        private static readonly ILogger<ServiceBusQueueInputProvider> Logger =
            DataLabLoggerFactory.CreateLogger<ServiceBusQueueInputProvider>();

        private static readonly ActivityMonitorFactory ServiceBusQueueInputProviderStartAsync =
            new ActivityMonitorFactory("ServiceBusQueueInputProvider.StartAsync");

        private readonly string _logicalQueueName;
        private readonly ServiceBusAdminManager _serviceBusAdminManager;
        private readonly ServiceBusTaskManager _serviceBusTaskManager;
        private readonly ServiceBusReader _serviceBusReader;

        private TimeSpan _maxAutoRenewDuration;
        private int _queueConcurrency;
        private int _queuePrefetchCount;

        private int _started;
        private int _stopping;

        private readonly string _concurrencyConfig;
        private readonly string _prefetchConfig;

        public string Name { get; }

        public ServiceBusQueueInputProvider(
            string serviceBusQueueName, 
            ServiceBusAdminManager serviceBusAdminManager, 
            string logicalQueueName, 
            int initQueueConcurrency,
            int initPrefetchCount,
            TimeSpan initTaskTimeout,
            Counter<long> readMessageCounter)
        {
            GuardHelper.IsArgumentPositive(initQueueConcurrency);
            GuardHelper.IsArgumentPositive(initPrefetchCount);
            GuardHelper.ArgumentConstraintCheck(initTaskTimeout != default);
            GuardHelper.ArgumentNotNull(readMessageCounter);

            _serviceBusAdminManager = serviceBusAdminManager;

            var shortNameSpace = _serviceBusAdminManager.NameSpace;
            Name = shortNameSpace + '/' + serviceBusQueueName;

            _logicalQueueName = logicalQueueName;

            _maxAutoRenewDuration = ConfigMapUtil.Configuration.GetValue<TimeSpan>(InputOutputConstants.ServiceBusMaxAutoRenewDuration, TimeSpan.FromMinutes(5));

            _concurrencyConfig = InputOutputConstants.ServiceBusQueuePrefix + logicalQueueName + InputOutputConstants.ServiceBusQueueConcurrencySuffix;
            _prefetchConfig = InputOutputConstants.ServiceBusQueuePrefix + logicalQueueName + InputOutputConstants.ServiceBusQueuePrefetchCount;

            _queueConcurrency = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(_concurrencyConfig, UpdateQueueConcurrency, initQueueConcurrency, allowMultiCallBacks: true);
            _queuePrefetchCount = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(_prefetchConfig, UpdateQueuePrefetchCount, initPrefetchCount, allowMultiCallBacks: true);

            var timeoutConfigName = InputOutputConstants.ServiceBusQueuePrefix + logicalQueueName  + InputOutputConstants.ServiceBusTaskTimeOutDurationSuffix;
            _serviceBusTaskManager = new ServiceBusTaskManager(shortNameSpace, serviceBusQueueName, timeoutConfigName, initTaskTimeout, readMessageCounter, logicalQueueName);

            var processor = serviceBusAdminManager.CreateServiceBusProcessor(
                serviceBusQueueName, 
                _maxAutoRenewDuration, 
                _queueConcurrency, 
                _queuePrefetchCount, 
                false);
            _serviceBusReader = new ServiceBusReader(processor, _serviceBusTaskManager);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var monitor = ServiceBusQueueInputProviderStartAsync.ToMonitor();
            monitor.Activity.Properties["QueueName"] = Name;
            monitor.OnStart();

            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            {
                monitor.Activity.Properties["AlreadyStarted"] = true;
                monitor.OnCompleted();
                return;
            }

            try
            {
                Logger.LogWarning("ServiceBusQueueInputProvider is started. QueueName: {QueueName}", Name);

                await _serviceBusReader.StartAsync(cancellationToken).IgnoreContext();

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _started, 0);

                Logger.LogCritical(ex, "ServiceBusQueueInputProvider StartAsync got Exception. QueueName: {QueueName}. {exception}", 
                    Name, ex.ToString());
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_started == 0 || Interlocked.CompareExchange(ref _stopping, 1, 0) != 0)
            {
                return;
            }

            try
            {
                Logger.LogWarning("ServiceBusQueueInputProvider is stopped. QueueName: {QueueName}", Name);
                await _serviceBusReader.StopAsync(cancellationToken).IgnoreContext();
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "ServiceBusQueueInputProvider StopAsync got Exception. QueueName: {QueueName}. {exception}", 
                    Name, ex.ToString());
                throw;
            }
            finally
            {
                Interlocked.Exchange(ref _stopping, 0);
                Interlocked.Exchange(ref _started, 0);
            }
        }

        private Task UpdateQueueConcurrency(int newConcurrency)
        {
            if (newConcurrency <= 0)
            {
                Logger.LogError("{config} must be larger than 0", _concurrencyConfig);
                return Task.CompletedTask;
            }

            var oldVal = _queueConcurrency;
            if (Interlocked.CompareExchange(ref _queueConcurrency, newConcurrency, oldVal) == oldVal)
            {
                _serviceBusReader.UpdateConcurrency(newConcurrency);

                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                _concurrencyConfig, oldVal, newConcurrency);
            }

            return Task.CompletedTask;
        }

        private Task UpdateQueuePrefetchCount(int newPrefetchCount)
        {
            if (newPrefetchCount <= 0)
            {
                Logger.LogError("{config} must be larger than 0", _queuePrefetchCount);
                return Task.CompletedTask;
            }

            var oldVal = _queuePrefetchCount;
            if (Interlocked.CompareExchange(ref _queuePrefetchCount, newPrefetchCount, oldVal) == oldVal)
            {
                _serviceBusReader.UpdatePrefetchCount(newPrefetchCount);

                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                _queuePrefetchCount, oldVal, newPrefetchCount);
            }

            return Task.CompletedTask;
        }

        public static void AddRetryQueueInputProviders()
        {
            var serviceBusAdminManagers = SolutionInputOutputService.ServiceBusAdminManagers;
            var logicalQueueName = InputOutputConstants.ServiceBusRetryQueueLogicalName;
            var counterMetricName = logicalQueueName + IOServiceOpenTelemetry.QUEUE_READ_MESSAGES_SUFFIX;
            var retryQueueReadMessageCounter = IOServiceOpenTelemetry.IOServiceNameMeter.CreateCounter<long>(counterMetricName);
            int initConcurrency = 20;
            int initPrefetchCount = 300;
            TimeSpan initTaskTimeout = TimeSpan.FromMinutes(2);

            foreach (var serviceBusAdminManager in serviceBusAdminManagers)
            {
                var queueName = serviceBusAdminManager.RetryQueueName;

                // Create RetryQueue Reader
                var retryQueueInputProvider = new ServiceBusQueueInputProvider(
                    queueName, 
                    serviceBusAdminManager, 
                    logicalQueueName,
                    initConcurrency,
                    initPrefetchCount,
                    initTaskTimeout,
                    retryQueueReadMessageCounter);

                SolutionInputOutputService.RetryInputProviders.Add(retryQueueInputProvider);
            }
        }

        public static void AddSubJobQueueInputProviders()
        {
            var serviceBusAdminManagers = SolutionInputOutputService.ServiceBusAdminManagers;
            var logicalQueueName = InputOutputConstants.ServiceBusSubJobQueueLogicalName;
            var counterMetricName = logicalQueueName + IOServiceOpenTelemetry.QUEUE_READ_MESSAGES_SUFFIX;
            var subJobQueueReadMessageCounter = IOServiceOpenTelemetry.IOServiceNameMeter.CreateCounter<long>(counterMetricName);
            int initConcurrency = 10;
            int initPrefetchCount = 100;
            TimeSpan initTaskTimeout = TimeSpan.FromMinutes(3);

            foreach (var serviceBusAdminManager in serviceBusAdminManagers)
            {
                var queueName = serviceBusAdminManager.SubJobQueueName;

                // Create SubJobQueue Reader
                var subJobQueueInputProvider = new ServiceBusQueueInputProvider(
                    queueName,
                    serviceBusAdminManager,
                    logicalQueueName,
                    initConcurrency,
                    initPrefetchCount,
                    initTaskTimeout,
                    subJobQueueReadMessageCounter);

                SolutionInputOutputService.SubJobInputProviders.Add(subJobQueueInputProvider);
            }
        }
    }
}
