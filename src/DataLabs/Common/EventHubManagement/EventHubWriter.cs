namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using global::Azure.Core;
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Producer;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;

    [ExcludeFromCodeCoverage]
    public class EventHubWriter : IEventWriter<EventData, EventDataBatch>
    {
        private static readonly ActivityMonitorFactory EventHubWriterLogPropertiesError = new("EventHubWriter.LogPropertiesError", LogLevel.Critical);
        private static readonly ActivityMonitorFactory EventHubWriterInitEventHubWriter = new ("EventHubWriter.InitEventHubWriter");

        public string Name { get; }
        public string EventHubName { get; }
        public string NameSpace { get; }

        private ConfigurableTimer _healthyCheckTimer;
        private EventHubProducerClient _eventHubClient;
        private DateTimeOffset _lastHealthCheckTime;
        private int _logPropertyCallTimeoutInSec;

        // Added by recommendation of Event hub team to ensure that on rainy day, ARG is more resilient.
        // "We need to have some operation on connection with full timeout (60s is Event hub recommended duration), since ARG small timeouts may be too small to complete end to end authorization and amqp link establishment.
        // Both _eventHubClient and _eventHubVerySlowClient shares connection object, so most of initialization will be shared.
        private EventHubProducerClient _eventHubVerySlowClient;

        private bool _disposed = false;

        public EventHubWriter(
            string connectionString,
            string eventHubName)
        {
            var connectionStringProperties = EventHubsConnectionStringProperties.Parse(connectionString);
            var fullyQualifiedNamespace = connectionStringProperties.FullyQualifiedNamespace;

            EventHubName = eventHubName;
            NameSpace = fullyQualifiedNamespace.FastSplitAndReturnFirst('.');
            Name = NameSpace + '/' + eventHubName;

            var connectionOptions = EventHubWriterOptionsUtils.CreateEventHubWriterConnectionOptions();
            var ehConnection = new EventHubConnection(connectionString, eventHubName, connectionOptions);

            InitEventHubWriter(ehConnection);
        }

        public EventHubWriter(
            string fullyQualifiedNamespace,
            string eventHubName,
            TokenCredential credential)
        {
            EventHubName = eventHubName;
            NameSpace = fullyQualifiedNamespace.FastSplitAndReturnFirst('.');
            Name = NameSpace + '/' + eventHubName;
            
            var connectionOptions = EventHubWriterOptionsUtils.CreateEventHubWriterConnectionOptions();
            var ehConnection = new EventHubConnection(fullyQualifiedNamespace, eventHubName, credential, connectionOptions);
            
            InitEventHubWriter(ehConnection);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<EventDataBatch> CreateEventDataBatch(CancellationToken cancellationToken = default)
        {
            return _eventHubClient.CreateBatchAsync(cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task SendAsync(IEnumerable<EventData> eventBatch,
                                    SendEventOptions options,
                                    CancellationToken cancellationToken = default)
        {
            return _eventHubClient.SendAsync(eventBatch, options, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task SendAsync(EventData eventData,
                                    SendEventOptions options,
                                    CancellationToken cancellationToken = default)
        {
            return _eventHubClient.SendAsync(new[] { eventData }, options, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task ValidateConnectionAsync(CancellationToken cancellationToken)
        {
            return this.LogPropertiesAsync(cancellationToken);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Initial Validate connection

            await ValidateConnectionAsync(cancellationToken).ConfigureAwait(false);

            _healthyCheckTimer.Start();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }
        
        [MemberNotNull(nameof(_healthyCheckTimer))]
        [MemberNotNull(nameof(_eventHubClient))]
        [MemberNotNull(nameof(_eventHubVerySlowClient))]
        private void InitEventHubWriter(EventHubConnection eventHubConnection)
        {
            using var monitor = EventHubWriterInitEventHubWriter.ToMonitor();
            try
            {
                monitor.Activity[SolutionConstants.EventHubName] = EventHubName;
                monitor.OnStart();

                var maxRetry = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.EventHubWriterMaxRetry, 3);
                var delayInMSec = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.EventHubWriterDelayInMsec, 800);
                var maxDelayInSec = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.EventHubWriterMaxDelayPerAttempInSec, 3);
                var timeoutPerAttempInSec = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.EventHubWriterMaxTimePerAttempInSec, 5);
                _logPropertyCallTimeoutInSec = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.EventHubLogPropertyCallTimeoutInSec, 10);

                monitor.Activity["MaxRetry"] = maxRetry;
                monitor.Activity["DelayInMSec"] = delayInMSec;
                monitor.Activity["MaxDelayInSec"] = maxDelayInSec;
                monitor.Activity["TimeoutPerAttempInSec"] = timeoutPerAttempInSec;

                _healthyCheckTimer = new ConfigurableTimer(SolutionConstants.EventHubConnectionRefreshDuration, TimeSpan.FromSeconds(30));
                _healthyCheckTimer.AddTimeEventHandlerAsyncSafely(HealthyCheckTimerHandlerAsync);

                monitor.Activity[SolutionConstants.EventHubConnectionRefreshDuration] = _healthyCheckTimer.Interval;

                var retryOptions = EventHubWriterOptionsUtils.CreateEventHubsRetryOptions(maxRetry, delayInMSec, maxDelayInSec, timeoutPerAttempInSec);

                var options = new EventHubProducerClientOptions
                {
                    ConnectionOptions = EventHubWriterOptionsUtils.CreateEventHubWriterConnectionOptions(),
                    RetryOptions = retryOptions
                };
                this._eventHubClient = new EventHubProducerClient(eventHubConnection, options);

                var verySlowOptions = new EventHubProducerClientOptions
                {
                    RetryOptions = new EventHubsRetryOptions()
                    {
                        MaximumDelay = TimeSpan.FromSeconds(60)
                    }
                };
                this._eventHubVerySlowClient = new EventHubProducerClient(eventHubConnection, verySlowOptions);

                monitor.Activity["EventHubClientIdentifier"] = _eventHubClient.Identifier;
                monitor.Activity["SlowEventHubClientIdentifier"] = _eventHubVerySlowClient.Identifier;

                MetricLogger.CommonMeter.CreateObservableGauge<int>(MonitoringConstants.EVENTHUB_SEC_SINCE_LAST_HEALTH_CHECK, GetSecSinceLastHealthCheck);

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        private Measurement<int> GetSecSinceLastHealthCheck()
        {
            var currentTime = DateTimeOffset.UtcNow;
            int sec = (int)(currentTime - _lastHealthCheckTime).TotalSeconds;

            return new Measurement<int>(sec, new KeyValuePair<string, object?>(MonitoringConstants.EventHubNameDimension, EventHubName));
        }

        private async Task LogPropertiesAsync(CancellationToken cancellationToken)
        {
            try
            {

                using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutTokenSource.CancelAfter(_logPropertyCallTimeoutInSec * 1000); // 10 secs

                var properties = await this._eventHubVerySlowClient.GetEventHubPropertiesAsync(cancellationToken).IgnoreContext();

                _lastHealthCheckTime = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                using var criticalLogMonitor = EventHubWriterLogPropertiesError.ToMonitor();
                criticalLogMonitor.OnError(ex, true);
                throw;
            }
        }

        #region TimerTask

        private async Task HealthyCheckTimerHandlerAsync(object? sender, ElapsedEventArgs e)
        {
            try
            {
                await LogPropertiesAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Log is already done in LogPropertiesAsync
            }
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _healthyCheckTimer.Dispose();
                _eventHubClient?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _eventHubVerySlowClient?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddToBatch(EventDataBatch eventDataBatch, EventData eventData)
        {
            return eventDataBatch.TryAdd(eventData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBatchCount(EventDataBatch eventDataBatch)
        {
            return eventDataBatch.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetMaxSizeInBytes(EventDataBatch eventDataBatch)
        {
            return eventDataBatch.MaximumSizeInBytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetSizeInBytes(EventDataBatch eventDataBatch)
        {
            return eventDataBatch.SizeInBytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetSizeInBytes(EventData eventData)
        {
            return eventData.Data == null ? 0 : eventData.Data.ToMemory().Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EventData CreateEventData(BinaryData binaryData)
        {
            return new EventData(binaryData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddProperty(EventData eventData, string key, object value)
        {
            if (value != null)
            {
                eventData.Properties[key] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddDelayMessageTime(EventData eventData, TimeSpan delay)
        {
            //throw new NotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task SendAsync(EventData eventData, CancellationToken cancellationToken = default)
        {
            return _eventHubClient.SendAsync(new[] { eventData }, null, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task SendAsync(IEnumerable<EventData> eventBatch, CancellationToken cancellationToken = default)
        {
            return _eventHubClient.SendAsync(eventBatch, null, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task SendBatchAsync(EventDataBatch eventDataBatch, CancellationToken cancellationToken = default)
        {
            return _eventHubClient.SendAsync(eventDataBatch, cancellationToken);
        }
    }
}
