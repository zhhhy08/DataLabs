namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement
{
    using global::Azure.Core;
    using global::Azure.Messaging.ServiceBus;
    using global::Azure.Messaging.ServiceBus.Administration;
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
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using System.Diagnostics;
    using Microsoft.Boost;

    public class ServiceBusAdminManager : IDisposable
    {
        #region Metrics

        private static readonly UpDownCounter<long> QueueMessageCountMetric = MetricLogger.CommonMeter.CreateUpDownCounter<long>(MonitoringConstants.QUEUE_MESSAGE_COUNT);
        private static readonly UpDownCounter<long> QueueActiveMessageCountMetric = MetricLogger.CommonMeter.CreateUpDownCounter<long>(MonitoringConstants.QUEUE_ACTIVE_MESSAGE_COUNT);
        private static readonly UpDownCounter<long> QueueScheduledMessageCountMetric = MetricLogger.CommonMeter.CreateUpDownCounter<long>(MonitoringConstants.QUEUE_SCHEDULED_MESSAGE_COUNT);
        private static readonly UpDownCounter<long> DeadLetterMessageCountMetric = MetricLogger.CommonMeter.CreateUpDownCounter<long>(MonitoringConstants.QUEUE_DEAD_LETTER_MESSAGE_COUNT);
        private static readonly UpDownCounter<long> QueueSizeInBytesMetric = MetricLogger.CommonMeter.CreateUpDownCounter<long>(MonitoringConstants.QUEUE_SIZE_IN_BYTES);
        private static readonly Counter<long> DeadLetterPurgedMessageCountMetric = MetricLogger.CommonMeter.CreateCounter<long>(MonitoringConstants.DEAD_LETTER_PURGED_MESSAGE_COUNT);
        private static readonly Counter<long> DeadLetterReplayedMessageCountMetric = MetricLogger.CommonMeter.CreateCounter<long>(MonitoringConstants.DEAD_LETTER_REPLAYED_MESSAGE_COUNT);

        #endregion

        private static readonly ILogger<ServiceBusAdminManager> Logger =
            DataLabLoggerFactory.CreateLogger<ServiceBusAdminManager>();

        private static readonly ActivityMonitorFactory ServiceBusAdminManagerLogPropertiesAsync =
            new ("ServiceBusAdminManager.LogPropertiesAsync");
        private static readonly ActivityMonitorFactory ServiceBusAdminManagerDeleteDeadLetterMessagesAsync =
            new ("ServiceBusAdminManager.DeleteDeadLetterMessagesAsync");
        private static readonly ActivityMonitorFactory ServiceBusAdminManagerReplayDeadLetterMessagesAsync =
            new ("ServiceBusAdminManager.ReplayDeadLetterMessagesAsync");

        private static readonly IRetryStrategy DefaultRetryStrategy =
            new FixedInterval(3, TimeSpan.FromSeconds(1));

        private static readonly IRetryPolicy DefaultRetryPolicy =
            new RetryPolicy(new CatchAllErrorStrategy(), DefaultRetryStrategy);

        public const string DEAD_LETTER_PATH = "/$deadletterqueue";

        private const string MessageFailedToGetQueueMetadata = "Failed to get queue metadata.";

        private const int ServiceBusReceiverMaxMessages = 100;

        private const string DLQProcessCountCustomProperty = "dlqProcessCount";

        private static readonly TimeSpan ServiceBusReceiverMaxWaitTime = TimeSpan.FromSeconds(5);

        private int _serviceBusDLQMaxProcessCount;

        private int _queueOperationTimeOut;

        private static int IsDeleteDLQMessagesRunning = 0;
        private static int IsReplayDLQMessagesRunning = 0;

        private readonly Dictionary<string, ServiceBusWriter> _serviceBusWriterMap = new();
        private readonly Dictionary<string, QueueMetricInfo> _queueInfoMap = new();

        public string? SubJobQueueName { get; private set; }
        public string? RetryQueueName { get; private set; }

        public ServiceBusWriter? SubJobQueueServiceBusWriter { get; private set; }
        public ServiceBusWriter? RetryQueueServiceBusWriter { get; private set; }
        public ServiceBusWriter? PoisonQueueServiceBusWriter { get; private set; }

        public string NameSpace { get; }

        private ServiceBusClient _readServiceBusClient;
        private ServiceBusClient _writeServiceBusClient;
        private ServiceBusAdministrationClient _administrationClient;
        private ConfigurableTimer? _healthyCheckTimer;

        private bool _disposed = false;

        public ServiceBusAdminManager(string connectionString) : this(
            new ServiceBusClient(connectionString, CreateServiceBusClientOptions()),
            new ServiceBusClient(connectionString, CreateServiceBusClientOptions()),
            new ServiceBusAdministrationClient(connectionString))
        {
        }

        // Constructor for testing where we pass mocked clients
        public ServiceBusAdminManager(ServiceBusClient readClient, ServiceBusClient writeClient, ServiceBusAdministrationClient adminClient)
        {
            _readServiceBusClient = readClient;
            _writeServiceBusClient = writeClient;
            _administrationClient = adminClient;
            _serviceBusDLQMaxProcessCount = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                SolutionConstants.ServiceBusDLQMaxProcessCount, UpdateServiceBusDLQMaxProcessCount, 3);
            _queueOperationTimeOut = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                SolutionConstants.QueueOperationTimeOut, UpdateQueueOperationTimeOut, 60*2);

            NameSpace = _readServiceBusClient.FullyQualifiedNamespace.FastSplitAndReturnFirst('.');
        }

        public ServiceBusAdminManager(string fullyQualifiedNamespace, TokenCredential credential)
        {
            _administrationClient = new ServiceBusAdministrationClient(fullyQualifiedNamespace, credential);

            var clientOptions = CreateServiceBusClientOptions();
            _readServiceBusClient = new ServiceBusClient(fullyQualifiedNamespace, credential, clientOptions);
            _writeServiceBusClient = new ServiceBusClient(fullyQualifiedNamespace, credential, clientOptions);
            _serviceBusDLQMaxProcessCount = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                SolutionConstants.ServiceBusDLQMaxProcessCount, UpdateServiceBusDLQMaxProcessCount, 3);
            _queueOperationTimeOut = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                SolutionConstants.QueueOperationTimeOut, UpdateQueueOperationTimeOut, 60*2);

            NameSpace = _readServiceBusClient.FullyQualifiedNamespace.FastSplitAndReturnFirst('.');
        }

        public async Task<QueueProperties> UpdateLockDurationAsync(string queueName, TimeSpan lockDuration, CancellationToken cancellationToken) {

            // Get current properties
            var queueProperties = await GetQueuePropertiesAsync(queueName, cancellationToken).ConfigureAwait(false);
            if (queueProperties.LockDuration.Equals(lockDuration))
            {
                return queueProperties;
            }

            // Set new LockDuration
            queueProperties.LockDuration = lockDuration;
            queueProperties = await _administrationClient.UpdateQueueAsync(queueProperties).ConfigureAwait(false);
            if (queueProperties == null)
            {
                throw new ServiceBusException(true, MessageFailedToGetQueueMetadata);
            }
            return queueProperties;
        }

        public virtual async Task CreateRetryQueueWriter(string queueName, CancellationToken cancellationToken)
        {
            RetryQueueName = queueName;

            var createQueueOptions = ServiceBusOptionsUtils.CreateQueueOptions(
               queueName: queueName,
               maxDeliveryCount: 10,
               lockDurationInSec: 60, // 1 min
               enableBatchedOperations: true,
               deadLetteringOnMessageExpiration: true,
               ttlInDays: 14, // 14 Days
               maxSizeInMegabytes: 80 * 1024 // 80GB
               );

            if (await CreateIfNotExistsAsync(createQueueOptions, cancellationToken).ConfigureAwait(false))
            {
                Logger.LogWarning("Retry Queue: {QueueName} is created in NameSpace: {NameSpace}", queueName, NameSpace);
            }

            await ValidateConnectionAsync(queueName, cancellationToken).ConfigureAwait(false);

            RetryQueueServiceBusWriter = GetOrCreateServiceBusWriter(queueName);
            PoisonQueueServiceBusWriter = RetryQueueServiceBusWriter;
        }

        public virtual async Task CreateSubJobQueueWriterAsync(string queueName, CancellationToken cancellationToken)
        {
            SubJobQueueName = queueName;

            var createQueueOptions = ServiceBusOptionsUtils.CreateQueueOptions(
                queueName: queueName,
                maxDeliveryCount: 20,
                lockDurationInSec: 3*60, // 3 min
                enableBatchedOperations: true,
                deadLetteringOnMessageExpiration: true,
                ttlInDays: 14, // 14 Days
                maxSizeInMegabytes: 80 * 1024 // 80GB
                );

            if (await CreateIfNotExistsAsync(createQueueOptions, cancellationToken).ConfigureAwait(false))
            {
                Logger.LogWarning("SubJob Queue: {QueueName} is created in NameSpace: {NameSpace}", queueName, NameSpace);
            }

            await ValidateConnectionAsync(queueName, cancellationToken).ConfigureAwait(false);

            SubJobQueueServiceBusWriter = GetOrCreateServiceBusWriter(queueName);
        }

        public ServiceBusWriter GetOrCreateServiceBusWriter(string queueName)
        {
            lock(this)
            {
                if (_serviceBusWriterMap.TryGetValue(queueName, out ServiceBusWriter? serviceBusWriter) && serviceBusWriter != null)
                {
                    return serviceBusWriter;
                }
                else
                {
                    var queueMetricInfo = new QueueMetricInfo(queueName);
                    _queueInfoMap[queueName] = queueMetricInfo;

                    serviceBusWriter = new ServiceBusWriter(_writeServiceBusClient.CreateSender(queueName));
                    _serviceBusWriterMap[queueName] = serviceBusWriter;
                    return serviceBusWriter;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Dictionary<string, ServiceBusWriter>.ValueCollection GetServiceBusWriters()
        {
            return _serviceBusWriterMap.Values;
        }

        public ServiceBusProcessor CreateServiceBusProcessor(string queueName, TimeSpan maxAutoRenewDuration,
            int concurrency, int prefetchCount, bool forDeadLetter)
        {
            ServiceBusProcessorOptions options = ServiceBusOptionsUtils.CreateServiceBusProcessorOptions(maxAutoRenewDuration, concurrency, prefetchCount);

            if (forDeadLetter)
            {
                options.SubQueue = SubQueue.DeadLetter;
            }

            return _readServiceBusClient.CreateProcessor(queueName, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ServiceBusClientOptions CreateServiceBusClientOptions()
        {
            // As ARG does, Disabling SB SDK internal retries as it is too relaxed
            var maxRetry = ConfigMapUtil.Configuration.GetValue(SolutionConstants.ServiceBusWriterMaxRetry, 0); // Like ARG, Disabling SB SDK internal retries as it is too relaxed
            var delayInMSec = ConfigMapUtil.Configuration.GetValue(SolutionConstants.ServiceBusWriterDelayInMsec, 800);
            var maxDelay = ConfigMapUtil.Configuration.GetValue(SolutionConstants.ServiceBusWriterMaxDelayPerAttempInSec, 10);
            var maxTimeOut = ConfigMapUtil.Configuration.GetValue(SolutionConstants.ServiceBusWriterMaxTimePerAttempInSec, 10);
            return ServiceBusOptionsUtils.CreateServiceBusClientOptions(maxRetry, delayInMSec, maxDelay, maxTimeOut);
        }

        public async Task<bool> ExistsAsync(string queueName, CancellationToken cancellationToken)
        {
            try
            {
                return await _administrationClient.QueueExistsAsync(queueName, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "ExistsAsync got exception. QueueName: {name}. {exception}", queueName, ex.ToString());
                throw;
            }
        }

        public virtual async Task<bool> DeleteIfExistsAsync(string queueName, CancellationToken cancellationToken)
        {
            try
            {
                if (!await ExistsAsync(queueName, cancellationToken).IgnoreContext())
                {
                    return false;
                }

                try
                {
                    await _administrationClient.DeleteQueueAsync(queueName, cancellationToken).IgnoreContext();
                    return true;
                }
                catch (Exception ex) when (ServiceBusExceptionHelper.IsAzureServiceBusEntityNotFound(ex))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "DeleteIfExistsAsync got exception. QueueName: {name}. {exception}", queueName, ex.ToString());
                throw;
            }
        }

        public virtual async Task<bool> CreateIfNotExistsAsync(CreateQueueOptions createQueueOptions, CancellationToken cancellationToken)
        {
            try
            {

                if (await ExistsAsync(createQueueOptions.Name, cancellationToken).IgnoreContext())
                {
                    return false;
                }

                try
                {
                    await DefaultRetryPolicy.ExecuteAsync(() => this._administrationClient.CreateQueueAsync(createQueueOptions, cancellationToken)).ConfigureAwait(false);
                    return true;
                }
                catch (Exception ex) when (ServiceBusExceptionHelper.IsAzureServiceBusEntityAlreadyExists(ex))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "CreateIfNotExistsAsync got exception. NameSpace: {NameSpace}, QueueName: {QueueName}. {exception}", NameSpace, createQueueOptions.Name, ex.ToString());
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<QueueRuntimeProperties> GetQueueInfoAsync(string queueName, CancellationToken cancellationToken)
        {
            var queueInfo = await _administrationClient.GetQueueRuntimePropertiesAsync(queueName, cancellationToken).ConfigureAwait(false);
            if (queueInfo == null)
            {
                throw new ServiceBusException(true, MessageFailedToGetQueueMetadata);
            }
            return queueInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<QueueProperties> GetQueuePropertiesAsync(string queueName, CancellationToken cancellationToken)
        {
            var queueInfo = await _administrationClient.GetQueueAsync(queueName, cancellationToken).ConfigureAwait(false);
            if (queueInfo == null)
            {
                throw new ServiceBusException(true, MessageFailedToGetQueueMetadata);
            }
            return queueInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task ValidateConnectionAsync(string queueName, CancellationToken cancellationToken)
        {
            return this.LogPropertiesAsync(queueName, null, cancellationToken);
        }

        public virtual async Task DeleteDeadLetterMessagesAsync(string queueName, int lookBackHours, TaskCompletionSource<bool> startedSignal)
        {
            using var monitor = ServiceBusAdminManagerDeleteDeadLetterMessagesAsync.ToMonitor();
            monitor.OnStart();

            startedSignal.TrySetResult(true);

            monitor.Activity[SolutionConstants.QueueName] = queueName;
            var receiver = _readServiceBusClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter
            });

            var lookBackTime = DateTime.UtcNow.AddHours(-lookBackHours);
            var countOfMessagesDeleted = 0;
            var continueReceiving = true;
            var amOwner = false;

            try
            {
                if (Interlocked.CompareExchange(ref IsDeleteDLQMessagesRunning, 1, 0) == 0)
                {
                    amOwner = true;

                    while (continueReceiving)
                    {
                        var batch = await receiver.ReceiveMessagesAsync(maxMessages: ServiceBusReceiverMaxMessages, maxWaitTime: ServiceBusReceiverMaxWaitTime).ConfigureAwait(false);
                        if (batch.Count == 0)
                        {
                            break;
                        }

                        foreach (var dlqMessage in batch)
                        {
                            if (dlqMessage.EnqueuedTime <= lookBackTime)
                            {
                                await receiver.CompleteMessageAsync(dlqMessage).ConfigureAwait(false);
                                countOfMessagesDeleted += 1;

                                LogDLQMessageMetrics(queueName, dlqMessage, false);
                            }
                            else
                            {
                                // We need to finally abandon the message so the message is released back to dlq
                                await receiver.AbandonMessageAsync(dlqMessage).ConfigureAwait(false);
                                continueReceiving = false;
                            }
                        }
                    }

                    monitor.Activity["Count of DLQ messages deleted"] = countOfMessagesDeleted;

                    monitor.OnCompleted();
                }
                else
                {
                    var conflictException = new ConflictException("Deleting DLQ messages is already in progress");
                    throw conflictException;
                }
            }
            catch (Exception ex)
            {
                monitor.Activity["Count of DLQ messages deleted"] = countOfMessagesDeleted;

                monitor.OnError(ex);
                throw;
            }
            finally
            {
                if (amOwner)
                {
                    Interlocked.CompareExchange(ref IsDeleteDLQMessagesRunning, 0, 1);
                }

                await receiver.CloseAsync().ConfigureAwait(false);
            }
        }

        public virtual async Task ReplayDeadLetterMessagesAsync(string queueName, int replayLookBackHours, long UtcNowFileTime, TaskCompletionSource<bool> startedSignal, bool needDelete=false, int deleteLookBackHours=48)
        {
            using var monitor = ServiceBusAdminManagerReplayDeadLetterMessagesAsync.ToMonitor();
            monitor.OnStart();

            startedSignal.TrySetResult(true);

            // For replay, we need to avoid one node replaying messages that are just replayed by another node.
            var utcNow = DateTime.FromFileTime(UtcNowFileTime);

            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.CancelAfter(TimeSpan.FromSeconds(_queueOperationTimeOut));
            var cancellationToken = cancellationSource.Token;
            var queueInfo = await GetQueueInfoAsync(queueName, cancellationToken).ConfigureAwait(false);
            var dlqMessageCount = queueInfo.DeadLetterMessageCount;

            monitor.Activity[SolutionConstants.QueueName] = queueName;
            monitor.Activity["Current DLQ messages count"] = dlqMessageCount;

            var sender = _writeServiceBusClient.CreateSender(queueName);
            var receiver = _readServiceBusClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter
            });

            var replayLookBackTime = DateTime.UtcNow.AddHours(-replayLookBackHours);
            var deleteLookBackTime = DateTime.UtcNow.AddHours(-deleteLookBackHours);
            var countOfMessagesReceived = 0;
            var countOfMessagesReplayed = 0;
            var countOfMessagesDeleted = 0;
            var continueReceiving = true;
            var amOwner = false;

            try
            {
                if (Interlocked.CompareExchange(ref IsReplayDLQMessagesRunning, 1, 0) == 0)
                {
                    amOwner = true;

                    while (continueReceiving && countOfMessagesReceived < dlqMessageCount)
                    {
                        var batch = await receiver.ReceiveMessagesAsync(maxMessages: ServiceBusReceiverMaxMessages, maxWaitTime: ServiceBusReceiverMaxWaitTime).ConfigureAwait(false);
                        countOfMessagesReceived += batch.Count;
                        if (batch.Count == 0)
                        {
                            break; // No more messages to retrieve
                        }

                        foreach (var dlqMessage in batch)
                        {
                            // We won't deal with any new messages for now
                            if (dlqMessage.EnqueuedTime >= utcNow)
                            {
                                continueReceiving = false;
                                await receiver.AbandonMessageAsync(dlqMessage).ConfigureAwait(false);
                                continue;
                            }

                            var dlqProcessCount = 1;

                            if (dlqMessage.ApplicationProperties != null && dlqMessage.ApplicationProperties.ContainsKey(DLQProcessCountCustomProperty))
                            {
                                dlqProcessCount = int.Parse(dlqMessage.ApplicationProperties[DLQProcessCountCustomProperty].ToString() ?? "1");
                            }

                            if (dlqMessage.EnqueuedTime >= replayLookBackTime && dlqProcessCount <= _serviceBusDLQMaxProcessCount)
                            {
                                // metrics for percentage
                                var resubmittableMessage = new ServiceBusMessage(dlqMessage);

                                if (resubmittableMessage.ApplicationProperties.ContainsKey(DLQProcessCountCustomProperty))
                                {
                                    resubmittableMessage.ApplicationProperties[DLQProcessCountCustomProperty] = dlqProcessCount + 1;
                                }
                                else
                                {
                                    resubmittableMessage.ApplicationProperties.Add(DLQProcessCountCustomProperty, dlqProcessCount + 1);
                                }

                                await sender.SendMessageAsync(resubmittableMessage).ConfigureAwait(false);
                                await receiver.CompleteMessageAsync(dlqMessage).ConfigureAwait(false);
                                countOfMessagesReplayed += 1;

                                LogDLQMessageMetrics(queueName, dlqMessage, true);

                                continue;
                            }

                            if (needDelete && dlqMessage.EnqueuedTime < deleteLookBackTime)
                            {
                                await receiver.CompleteMessageAsync(dlqMessage).ConfigureAwait(false);
                                countOfMessagesDeleted += 1;

                                LogDLQMessageMetrics(queueName, dlqMessage, false);

                                continue;
                            }

                            // We need to finally abandon the message so the message is released back to dlq
                            await receiver.AbandonMessageAsync(dlqMessage).ConfigureAwait(false);
                        }
                    }

                    monitor.Activity["Count of DLQ messages replayed"] = countOfMessagesReplayed;
                    monitor.Activity["Count of DLQ messages deleted"] = countOfMessagesDeleted;

                    monitor.OnCompleted();
                }
                else
                {
                    var conflictException = new ConflictException("Replaying/deleting DLQ messages is already in progress");
                    throw conflictException;
                }
            }
            catch (Exception ex)
            {
                monitor.Activity["Count of DLQ messages replayed"] = countOfMessagesReplayed;
                monitor.Activity["Count of DLQ messages deleted"] = countOfMessagesDeleted;

                monitor.OnError(ex);
                throw;
            }
            finally
            {
                if (amOwner)
                {
                    Interlocked.CompareExchange(ref IsReplayDLQMessagesRunning, 0, 1);
                }

                await receiver.CloseAsync().ConfigureAwait(false);
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            foreach(var queueName in _queueInfoMap.Keys)
            {
                await ValidateConnectionAsync(queueName, cancellationToken).ConfigureAwait(false);
            }

            _healthyCheckTimer = new ConfigurableTimer(SolutionConstants.ServiceBusConnectionRefreshDuration, TimeSpan.FromSeconds(30));
            _healthyCheckTimer.AddTimeEventHandlerAsyncSafely(HealthyCheckTimerHandlerAsync);
            _healthyCheckTimer.Start();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        private async Task LogPropertiesAsync(string queueName, QueueMetricInfo? queueMetricInfo, CancellationToken cancellationToken)
        {
            using var monitor = ServiceBusAdminManagerLogPropertiesAsync.ToMonitor(parentActivity: BasicActivity.Null);

            try
            {
                monitor.OnStart(false);

                var queueInfo = await GetQueueInfoAsync(queueName, cancellationToken).IgnoreContext();

                monitor.Activity[SolutionConstants.QueueName] = queueInfo.Name;
                monitor.Activity[SolutionConstants.AccessedAt] = queueInfo.AccessedAt;
                monitor.Activity[SolutionConstants.TotalMessageCount] = queueInfo.TotalMessageCount;
                monitor.Activity[SolutionConstants.ActiveMessageCount] = queueInfo.ActiveMessageCount;
                monitor.Activity[SolutionConstants.ScheduledMessageCount] = queueInfo.ScheduledMessageCount;
                monitor.Activity[SolutionConstants.DeadLetterMessageCount] = queueInfo.DeadLetterMessageCount;
                monitor.Activity[SolutionConstants.SizeInBytes] = queueInfo.SizeInBytes;

                if (queueMetricInfo != null)
                {
                    LogMetrics(queueInfo, queueMetricInfo);
                }

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                Logger.LogCritical(ex, "LogPropertiesAsync Failed. {exception}", ex.ToString());
                throw;
            }
        }

        private static void LogMetrics(QueueRuntimeProperties queueInfo, QueueMetricInfo prevQueueMetricInfo)
        {
            if (prevQueueMetricInfo == null || queueInfo == null)
            {
                return;
            }

            var dimension = new KeyValuePair<string, object?>(SolutionConstants.QueueName, queueInfo.Name);

            var currentMessageCount = queueInfo.TotalMessageCount - queueInfo.DeadLetterMessageCount;
            var prevMessageCount = prevQueueMetricInfo.TotalMessageCount - prevQueueMetricInfo.DeadLetterMessageCount;
            
            QueueMessageCountMetric.Add(currentMessageCount - prevMessageCount, dimension);
            QueueActiveMessageCountMetric.Add(queueInfo.ActiveMessageCount - prevQueueMetricInfo.ActiveMessageCount, dimension);
            QueueScheduledMessageCountMetric.Add(queueInfo.ScheduledMessageCount - prevQueueMetricInfo.ScheduledMessageCount, dimension);
            DeadLetterMessageCountMetric.Add(queueInfo.DeadLetterMessageCount - prevQueueMetricInfo.DeadLetterMessageCount, dimension);
            QueueSizeInBytesMetric.Add(queueInfo.SizeInBytes - prevQueueMetricInfo.SizeInBytes, dimension);

            // Update current metric info
            prevQueueMetricInfo.UpdateMetricInfo(queueInfo);
        }

        private static void LogDLQMessageMetrics(string queueName, ServiceBusReceivedMessage message, bool isReplay)
        {
            if (queueName == null)
            {
                return;
            }

            TagList dimensions = default;

            dimensions.Add(SolutionConstants.QueueName, queueName);
            dimensions.Add(SolutionConstants.DeadLetterReason, message.DeadLetterReason);

            if (message.ApplicationProperties.TryGetValue(SolutionConstants.PropertyTag_ChannelType, out var outVal))
            {
                var outStr = outVal?.ToString();
                dimensions.Add(SolutionConstants.ChannelType, outStr);
            }

            if (message.ApplicationProperties.TryGetValue(SolutionConstants.PropertyTag_Input_EventType, out outVal))
            {
                var outStr = outVal?.ToString();
                dimensions.Add(SolutionConstants.InputEventType, outStr);
            }

            if (message.ApplicationProperties.TryGetValue(SolutionConstants.PropertyTag_Output_EventType, out outVal))
            {
                var outStr = outVal?.ToString();
                dimensions.Add(SolutionConstants.OutputEventType, outStr);
            }

            if (isReplay){
                DeadLetterReplayedMessageCountMetric.Add(1, dimensions);
            }
            else
            {
                DeadLetterPurgedMessageCountMetric.Add(1, dimensions);
            }
        }

        private Task UpdateServiceBusDLQMaxProcessCount(int newValue)
        {
            if (newValue < 0)
            {
                Logger.LogError("{config} must be equal or larger than 0", _serviceBusDLQMaxProcessCount);
                return Task.CompletedTask;
            }

            var oldValue = _serviceBusDLQMaxProcessCount;
            if (oldValue != newValue)
            {
                if (Interlocked.CompareExchange(ref _serviceBusDLQMaxProcessCount, newValue, oldValue) == oldValue)
                {
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", _serviceBusDLQMaxProcessCount, oldValue, newValue);
                }
            }

            return Task.CompletedTask;
        }

        private Task UpdateQueueOperationTimeOut(int newValue)
        {
            if (newValue < 0)
            {
                Logger.LogError("{config} must be equal or larger than 0", _queueOperationTimeOut);
                return Task.CompletedTask;
            }

            var oldValue = _queueOperationTimeOut;
            if (oldValue != newValue)
            {
                if (Interlocked.CompareExchange(ref _queueOperationTimeOut, newValue, oldValue) == oldValue)
                {
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", _queueOperationTimeOut, oldValue, newValue);
                }
            }

            return Task.CompletedTask;
        }

        #region TimerTask

        private async Task HealthyCheckTimerHandlerAsync(object? sender, ElapsedEventArgs e)
        {
            try
            {
                if (_queueInfoMap.Count == 0)
                {
                    Logger.LogCritical("No registered QueueNames in ServiceBusClientWrapper");
                    return;
                }

                foreach (var keyValuePair in _queueInfoMap)
                {
                    var queueName = keyValuePair.Key;
                    var queueMetricInfo = keyValuePair.Value;
                    await LogPropertiesAsync(queueName, queueMetricInfo, default).ConfigureAwait(false);
                }

                return;
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Timer work to call LogPropertiesAsync failed. {exception}", ex.ToString());
                // It is best efforts.
                return;
            }
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _healthyCheckTimer?.Stop();

                foreach (var serviceBusWriter in _serviceBusWriterMap.Values)
                {
                    serviceBusWriter.ServiceBusSender.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }

                _writeServiceBusClient.DisposeAsync().AsTask().GetAwaiter().GetResult();

                _readServiceBusClient.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        private class QueueMetricInfo
        {
            public string QueueName;
            public long TotalMessageCount;
            public long ActiveMessageCount;
            public long ScheduledMessageCount;
            public long DeadLetterMessageCount;
            public long SizeInBytes;

            public QueueMetricInfo(string queueName)
            {
                QueueName = queueName;
            }

            public void UpdateMetricInfo(QueueRuntimeProperties queueInfo)
            {
                TotalMessageCount = queueInfo.TotalMessageCount;
                ActiveMessageCount = queueInfo.ActiveMessageCount;
                ScheduledMessageCount = queueInfo.ScheduledMessageCount;
                DeadLetterMessageCount = queueInfo.DeadLetterMessageCount;
                SizeInBytes = queueInfo.SizeInBytes;
            }
        }
    }
}
