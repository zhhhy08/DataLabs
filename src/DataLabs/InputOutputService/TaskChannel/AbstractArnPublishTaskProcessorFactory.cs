namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel
{
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Clients.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts.Data;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class AbstractArnPublishTaskProcessorFactory<TInput> : IBufferedTaskProcessorFactory<IOEventTaskContext<TInput>>
        where TInput: IInputMessage
    {
        #region Fields

        private readonly IOComponent _component;
        private readonly IArnNotificationClient _arnNotificationClient;
        private readonly string _logSuccessTag;
        private readonly string _logDurationTag;
        private readonly string _logBatchSizeTag;
        private readonly IOEventTaskFlag _taskSuccessFlag;
        private readonly IOEventTaskFlag _taskFailFlag;
        private readonly ILogger<AbstractArnPublishTaskProcessorFactory<TInput>> _logger;
        private readonly AdditionalGroupingProperties _additionalGroupingProperties;

        protected bool _arnPublishWriteToPairedRegion;
        protected TimeSpan _arnPublishBatchWriteTimeout;
        protected int _arnPublishFailRetryDelayInMsec;

        #endregion

        #region Constructors

        protected AbstractArnPublishTaskProcessorFactory(
            IOComponent component,
            IArnNotificationClient arnNotificationClient,
            ILogger<AbstractArnPublishTaskProcessorFactory<TInput>> logger,
            AdditionalGroupingProperties additionalGroupingProperties)
        {
            GuardHelper.ArgumentConstraintCheck(component == IOComponent.ArnPublish || component == IOComponent.BlobPayloadRoutingChannel);

            _component = component;
            _arnNotificationClient = arnNotificationClient;
            _logger = logger;
            _additionalGroupingProperties = additionalGroupingProperties;

            if (component == IOComponent.ArnPublish)
            {
                _logSuccessTag = InputOutputConstants.ArnPublishSuccess;
                _logDurationTag = InputOutputConstants.ArnPublishDuration;
                _logBatchSizeTag = InputOutputConstants.ArnPublishBatchSize;
                _taskSuccessFlag = IOEventTaskFlag.ArnPublishSuccess;
                _taskFailFlag = IOEventTaskFlag.ArnPublishFail;
            }
            else
            {
                _logSuccessTag = InputOutputConstants.BlobPayloadRoutingSuccess;
                _logDurationTag = InputOutputConstants.BlobPayloadRoutingDuration;
                _logBatchSizeTag = InputOutputConstants.BlobPayloadRoutingBatchSize;
                _taskSuccessFlag = IOEventTaskFlag.BlobPayloadRoutingSuccess;
                _taskFailFlag = IOEventTaskFlag.BlobPayloadRoutingFail;
            }

            _arnPublishFailRetryDelayInMsec =
                ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                    InputOutputConstants.ArnPublishWriteFailRetryDelayInMsec, UpdateFailRetryDelay, 1000);

            var writePairedRegionConfig = SolutionConstants.ArnPublishPrefix + SolutionConstants.PairedRegionWrite;
            _arnPublishWriteToPairedRegion = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(writePairedRegionConfig, UpdateWriteToPairedRegion, false);

            var configName = SolutionConstants.ArnPublishPrefix + SolutionConstants.EventBatchWriterTimeOutInSecSuffix;
            var batchWriteTimeoutInSec = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                configName, UpdateEventBatchWriterTimeOutInSec, 10);
            GuardHelper.ArgumentConstraintCheck(batchWriteTimeoutInSec > 0, configName);
            _arnPublishBatchWriteTimeout = TimeSpan.FromSeconds(batchWriteTimeoutInSec);
        }

        #endregion

        #region Public Methods

        public abstract IBufferedTaskProcessor<IOEventTaskContext<TInput>> CreateBufferedTaskProcessor();

        public void Dispose()
        {
            return;
        }

        #endregion

        #region Protected Methods

        protected Task PublishToArn(
            IList<ResourceOperationBase> resourceOperations,
            DataBoundary? dataBoundary,
            FieldOverrides fieldOverrides,
            CancellationToken cancellationToken)
        {
            return _arnNotificationClient.PublishToArn(resourceOperations, dataBoundary, fieldOverrides, _arnPublishWriteToPairedRegion, cancellationToken, _additionalGroupingProperties);
        }

        protected Task UpdateFailRetryDelay(int newDelayInMs)
        {
            if (newDelayInMs <= 0)
            {
                _logger.LogError("{config} must be larger than 0", InputOutputConstants.ArnPublishWriteFailRetryDelayInMsec);
                return Task.CompletedTask;
            }

            //ConnectErrorDelaymsForNextTry 
            var oldDelay = _arnPublishFailRetryDelayInMsec;
            if (Interlocked.CompareExchange(ref _arnPublishFailRetryDelayInMsec, newDelayInMs, oldDelay) == oldDelay)
            {
                _logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    InputOutputConstants.ArnPublishWriteFailRetryDelayInMsec, oldDelay, newDelayInMs);
            }

            return Task.CompletedTask;
        }

        protected Task UpdateEventBatchWriterTimeOutInSec(int newVal)
        {
            if (newVal <= 0)
            {
                _logger.LogError("{prefix}BatchWriterTimeOutInSec must be larger than 0", SolutionConstants.ArnPublishPrefix);
                return Task.CompletedTask;
            }

            var oldTimeout = _arnPublishBatchWriteTimeout;
            if (newVal == oldTimeout.TotalSeconds)
            {
                return Task.CompletedTask;
            }

            _arnPublishBatchWriteTimeout = TimeSpan.FromSeconds(newVal);

            _logger.LogWarning("{prefix}BatchWriterTimeOutInSec is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.ArnPublishPrefix, oldTimeout.TotalSeconds, newVal);

            return Task.CompletedTask;
        }

        protected Task UpdateWriteToPairedRegion(bool newValue)
        {
            var oldValue = _arnPublishWriteToPairedRegion;
            if (oldValue == newValue)
            {
                return Task.CompletedTask;
            }
            _arnPublishWriteToPairedRegion = newValue;
            _logger.LogWarning("{configValue} is changed, Old: {oldVal}, New: {newVal}",
                   SolutionConstants.ArnPublishPrefix + SolutionConstants.PairedRegionWrite, oldValue, newValue);

            return Task.CompletedTask;
        }

        #endregion

        #region Private Methods

        private void HandleWriteSuccess(
            IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts,
            long writeDurationInMilli,
            long endStopWatchTimestamp)
        {
            if (writeDurationInMilli < 0)
            {
                writeDurationInMilli = 0;
            }

            int batchSize = eventTaskContexts.Count;

            IOServiceOpenTelemetry.ArnPublishSuccessDuration.Record((int)writeDurationInMilli);
            IOServiceOpenTelemetry.ArnPublishSuccessBatchSizeMetric.Record(batchSize);
            IOServiceOpenTelemetry.ArnPublishSuccessCounter.Add(batchSize);

            for (var i = 0; i < batchSize; i++)
            {
                var eventTaskContext = eventTaskContexts[i].TaskContext;
                eventTaskContext.IOEventTaskFlags |= _taskSuccessFlag;
                eventTaskContext.TaskMovedToArn(writeDurationInMilli, batchSize, endStopWatchTimestamp, 
                    _component, _logSuccessTag, _logDurationTag, _logBatchSizeTag);
            }
        }

        private void HandleWriteFailure(
            IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts,
            long writeDurationInMilli,
            Exception ex)
        {
            int batchSize = eventTaskContexts.Count;

            IOServiceOpenTelemetry.ArnPublishFinalFailCounter.Add(batchSize);

            if (writeDurationInMilli >= 0)
            {
                // It means that error/failure happens during actual calling publishToArn
                // if error/failure happens during conversion, writeDuration is set to -1
                IOServiceOpenTelemetry.ArnPublishFailDuration.Record((int)writeDurationInMilli);
            }

            for (var i = 0; i < batchSize; i++)
            {
                var eventTaskContext = eventTaskContexts[i].TaskContext;
                eventTaskContext.IOEventTaskFlags |= _taskFailFlag;
                eventTaskContext.TaskFailedToMoveToArn(ex, writeDurationInMilli, batchSize, _arnPublishFailRetryDelayInMsec, 
                    _component, _logSuccessTag, _logDurationTag, _logBatchSizeTag);
            }
        }

        #endregion

        #region Wrapping Class

        protected abstract class AbstractArnPublishTaskProcessor : IBufferedTaskProcessor<IOEventTaskContext<TInput>>
        {
            #region Fields

            private readonly AbstractArnPublishTaskProcessorFactory<TInput> _taskProcessorFactory;
            private readonly List<EventGridNotification<NotificationDataV3<GenericResource>>> _eventGridNotifications;
            protected readonly ActivityMonitorFactory _activityMonitorFactory;

            #endregion

            public AbstractArnPublishTaskProcessor(
                AbstractArnPublishTaskProcessorFactory<TInput> taskProcessorFactory,
                ActivityMonitorFactory activityMonitorFactory)
            {
                _taskProcessorFactory = taskProcessorFactory;
                _eventGridNotifications = new();
                _activityMonitorFactory = activityMonitorFactory;
            }

            #region Public Methods

            public async Task ProcessBufferedTasksAsync(IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts)
            {
                using var monitor = _activityMonitorFactory.ToMonitor();
                long startTimeStamp = 0; // this is used to measure the time taken in publishToARN. it excludes conversion time

                try
                {
                    monitor.OnStart(false);
                    monitor.Activity[SolutionConstants.BatchCount] = eventTaskContexts.Count;

                    ExtractEventGridNotifications(eventTaskContexts, in _eventGridNotifications);

                    monitor.Activity["NotificationCount"] = _eventGridNotifications.Count;

                    List<ResourceOperationBase> resourceOperations = new(_eventGridNotifications.Count);

                    foreach (var notif in _eventGridNotifications)
                    {
                        resourceOperations.AddRange(ArnNotificationUtils.ConvertArnNotificaitonV3ToResourceOperation(notif));
                    }

                    using var cancellationTokenSource = new CancellationTokenSource(_taskProcessorFactory._arnPublishBatchWriteTimeout);
                    var cancellationToken = cancellationTokenSource.Token;

                    startTimeStamp = Stopwatch.GetTimestamp();
                    await _taskProcessorFactory.PublishToArn(resourceOperations, null, null, cancellationToken).IgnoreContext();
                    long endTimestamp = Stopwatch.GetTimestamp();

                    var writeDurationInMilli = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;
                    HandleWriteSuccess(eventTaskContexts, writeDurationInMilli, endTimestamp);

                    monitor.OnCompleted();
                }
                catch (Exception ex)
                {
                    long writeDurationInMilli = -1;
                    if (startTimeStamp > 0)
                    {
                        long endTimestamp = Stopwatch.GetTimestamp();
                        writeDurationInMilli = (long)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;
                    }
                    
                    HandleWriteFailure(eventTaskContexts, writeDurationInMilli, ex);

                    monitor.OnError(ex);
                }
                finally
                {
                    _eventGridNotifications.Clear();
                }
            }

            #endregion

            #region Protected Methods

            protected abstract void ExtractEventGridNotifications(
                IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts,
                in List<EventGridNotification<NotificationDataV3<GenericResource>>> eventGridNotifications);

            protected void HandleEmptyData(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
            {
                eventTaskContext.TaskContext.TaskSuccess(Stopwatch.GetTimestamp());
            }

            #endregion

            #region Private Methods

            private void HandleWriteFailure(IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts, long writeDurationInMilli, Exception exception)
            {
                _taskProcessorFactory.HandleWriteFailure(eventTaskContexts, writeDurationInMilli, exception);
            }

            private void HandleWriteSuccess(IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts, long writeDurationInMilli, long endStopWatchTimestamp)
            {
                _taskProcessorFactory.HandleWriteSuccess(eventTaskContexts, writeDurationInMilli, endStopWatchTimestamp);
            }

            #endregion
        }

        #endregion
    }
}
