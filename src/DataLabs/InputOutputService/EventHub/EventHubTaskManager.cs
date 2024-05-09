namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub
{
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Consumer;
    using global::Azure.Messaging.EventHubs.Primitives;
    using global::Azure.Messaging.EventHubs.Processor;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;

    [ExcludeFromCodeCoverage]
    internal class EventHubTaskManager : IEventHubTaskManager
    {
        private static readonly ILogger<EventHubTaskManager> Logger = DataLabLoggerFactory.CreateLogger<EventHubTaskManager>();

        private static readonly ActivityMonitorFactory EventHubTaskManagerCriticalError = new("EventHubTaskManager.CriticalError", LogLevel.Critical);
        private static readonly ActivityMonitorFactory EventHubTaskManagerProcessBatchEventDataAsync = new("EventHubTaskManager.ProcessBatchEventDataAsync");
        private static readonly ActivityMonitorFactory EventHubTaskManagerProcessEventDataAsync = new ("EventHubTaskManager.ProcessEventDataAsync");
        private static readonly ActivityMonitorFactory EventHubTaskManagerMoveRawInputToPoisonAsync = new ("EventHubTaskManager.MoveRawInputToPoisonAsync", LogLevel.Critical);
        private static readonly ActivityMonitorFactory EventHubTaskManagerPartitionInitializingHandlerAsync = new("EventHubTaskManager.PartitionInitializingHandlerAsync");
        private static readonly ActivityMonitorFactory EventHubTaskManagerPartitionClosingHandlerAsync = new("EventHubTaskManager.PartitionClosingHandlerAsync");

        public const string ActivityName = nameof(EventHubTaskManager);
        public const string ActivityComponentName = "EventHubTaskManager";

        internal string NameSpaceAndEventHubName { get; }
        internal string ConsumerGroupName { get; }
        internal RegionConfig RegionConfigData { get; } // indicates if this is the primary region or the backup region input for paired regions

        // Assuming partitionId is int
        private readonly EventHubAsyncTaskInfoQueue[] _taskInfoQueuePerPartition;
        private int _checkPointIntervalInSec;
        private int _checkPointTimeoutInSec;
        private Func<string, long, long?, CancellationToken, Task> _updateCheckpointAsyncFunc;
        
        private static TimeSpan _taskTimeOut;
        private static object _updateLock = new();

        static EventHubTaskManager()
        {
            _taskTimeOut = ConfigMapUtil.Configuration.GetValueWithCallBack<TimeSpan>(
                InputOutputConstants.InputEventHubTaskTimeOutDuration, UpdateTaskTimeOutDuration, TimeSpan.FromMinutes(1));
        }

        public EventHubTaskManager(string nameSpace, string eventHubName, string consumerGroupName, int numPartitions, RegionConfig regionConfig)
        {
            NameSpaceAndEventHubName = nameSpace + '/' + eventHubName;
            ConsumerGroupName = consumerGroupName;
            RegionConfigData = regionConfig;

            _checkPointIntervalInSec = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                InputOutputConstants.InputEventHubCheckPointIntervalInSec, UpdateCheckpointInterval, 20, allowMultiCallBacks: true);

            _checkPointTimeoutInSec = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                InputOutputConstants.InputEventHubCheckPointTimeoutInSec, UpdateCheckpointTimeout, 10, allowMultiCallBacks: true);

            _taskInfoQueuePerPartition = new EventHubAsyncTaskInfoQueue[numPartitions];

            IOServiceOpenTelemetry.IOServiceNameMeter.CreateObservableGauge<int>(IOServiceOpenTelemetry.EVENTHUB_TASK_QUEUE_LENGTH, GetTaskInfoQueueLengths);
            IOServiceOpenTelemetry.IOServiceNameMeter.CreateObservableGauge<long>(IOServiceOpenTelemetry.EVENTHUB_WAITING_MESSAGES, GetWaitingMessages);
            IOServiceOpenTelemetry.IOServiceNameMeter.CreateObservableGauge<long>(IOServiceOpenTelemetry.EVENTHUB_CHECK_POINT_PENDING_MESSAGES, GetCheckPointPendingMessages);
            IOServiceOpenTelemetry.IOServiceNameMeter.CreateObservableGauge<int>(IOServiceOpenTelemetry.EVENTHUB_SEC_SINCE_LAST_CHECKPOINT, GetSecSinceLastCheckPoint);
            IOServiceOpenTelemetry.IOServiceNameMeter.CreateObservableGauge<int>(IOServiceOpenTelemetry.EVENTHUB_SEC_SINCE_LAST_READ, GetSecSinceLastRead);
        }

        private IEnumerable<Measurement<int>> GetTaskInfoQueueLengths()
        {
            for (int i = 0; i < _taskInfoQueuePerPartition.Length; i++)
            {
                var taskInfoQueue = _taskInfoQueuePerPartition[i];
                if (taskInfoQueue == null || taskInfoQueue.NumReadEvents == 0)
                {
                    continue;
                }

                var queueLength = taskInfoQueue.TaskInfoQueueLength;
                if (queueLength < 0)
                {
                    continue;
                }

                yield return new Measurement<int>(queueLength,
                    new KeyValuePair<string, object>(MonitoringConstants.COMPONENT_DIMENSION, ActivityComponentName),
                    new KeyValuePair<string, object>(MonitoringConstants.EventHubNameDimension, NameSpaceAndEventHubName),
                    new KeyValuePair<string, object>(MonitoringConstants.PartitionIdDimension, i));
            }
        }

        private IEnumerable<Measurement<long>> GetWaitingMessages()
        {
            for (int i = 0; i < _taskInfoQueuePerPartition.Length; i++)
            {
                var taskInfoQueue = _taskInfoQueuePerPartition[i];
                if (taskInfoQueue == null || taskInfoQueue.NumReadEvents == 0)
                {
                    continue;
                }

                var waitingMessages = taskInfoQueue.NumOfWaitingMessages;
                if (waitingMessages < 0)
                {
                    continue;
                }

                yield return new Measurement<long>(waitingMessages,
                    new KeyValuePair<string, object>(MonitoringConstants.COMPONENT_DIMENSION, ActivityComponentName),
                    new KeyValuePair<string, object>(MonitoringConstants.EventHubNameDimension, NameSpaceAndEventHubName),
                    new KeyValuePair<string, object>(MonitoringConstants.PartitionIdDimension, i));
            }
        }

        private IEnumerable<Measurement<long>> GetCheckPointPendingMessages()
        {
            for (int i = 0; i < _taskInfoQueuePerPartition.Length; i++)
            {
                var taskInfoQueue = _taskInfoQueuePerPartition[i];
                if (taskInfoQueue == null || taskInfoQueue.NumReadEvents == 0)
                {
                    continue;
                }

                var numPendingMessages = taskInfoQueue.NumPendingMessagesForCheckPoint();
                if (numPendingMessages < 0)
                {
                    continue;
                }

                yield return new Measurement<long>(numPendingMessages,
                    new KeyValuePair<string, object>(MonitoringConstants.COMPONENT_DIMENSION, ActivityComponentName),
                    new KeyValuePair<string, object>(MonitoringConstants.EventHubNameDimension, NameSpaceAndEventHubName),
                    new KeyValuePair<string, object>(MonitoringConstants.PartitionIdDimension, i));
            }
        }

        private IEnumerable<Measurement<int>> GetSecSinceLastCheckPoint()
        {
            var currentTime = DateTimeOffset.UtcNow;

            for (int i = 0; i < _taskInfoQueuePerPartition.Length; i++)
            {
                var taskInfoQueue = _taskInfoQueuePerPartition[i];
                if (taskInfoQueue == null)
                {
                    continue;
                }

                var lastCheckPointedTime = taskInfoQueue.LastCheckPointedTime;
                if (lastCheckPointedTime == default)
                {
                    continue;
                }

                int sec = (int)(currentTime - taskInfoQueue.LastCheckPointedTime).TotalSeconds;
                if (sec < 0)
                {
                    sec = 0;
                }

                yield return new Measurement<int>(sec,
                    new KeyValuePair<string, object>(MonitoringConstants.COMPONENT_DIMENSION, ActivityComponentName),
                    new KeyValuePair<string, object>(MonitoringConstants.EventHubNameDimension, NameSpaceAndEventHubName),
                    new KeyValuePair<string, object>(MonitoringConstants.PartitionIdDimension, i));
            }
        }

        private IEnumerable<Measurement<int>> GetSecSinceLastRead()
        {
            var currentTime = DateTimeOffset.UtcNow;

            for (int i = 0; i < _taskInfoQueuePerPartition.Length; i++)
            {
                var taskInfoQueue = _taskInfoQueuePerPartition[i];
                if (taskInfoQueue == null)
                {
                    continue;
                }

                var lastPartitionReadTime = taskInfoQueue.LastPartitionReadTime;
                if (lastPartitionReadTime == default)
                {
                    continue;
                }

                int sec = (int)(currentTime - taskInfoQueue.LastPartitionReadTime).TotalSeconds;
                if (sec < 0)
                {
                    sec = 0;
                }

                yield return new Measurement<int>(sec,
                    new KeyValuePair<string, object>(MonitoringConstants.COMPONENT_DIMENSION, ActivityComponentName),
                    new KeyValuePair<string, object>(MonitoringConstants.EventHubNameDimension, NameSpaceAndEventHubName),
                    new KeyValuePair<string, object>(MonitoringConstants.PartitionIdDimension, i));
            }
        }

        public void SetUpdateCheckpointAsyncFunc(Func<string, long, long?, CancellationToken, Task> updateCheckpointAsyncFunc)
        {
            _updateCheckpointAsyncFunc = updateCheckpointAsyncFunc;
        }

        public async Task ProcessBatchEventDataAsync(IEnumerable<EventData> events, EventProcessorPartition partition, LastEnqueuedEventProperties lastEnqueuedEventProperties, CancellationToken cancellationToken)
        {
            using var methodMonitor = EventHubTaskManagerProcessBatchEventDataAsync.ToMonitor(
                parentActivity: BasicActivity.Null,
                component: ActivityComponentName);

            try
            {
                long lastEnqueuedSequenceNumber = (long)(lastEnqueuedEventProperties.SequenceNumber.HasValue ? lastEnqueuedEventProperties.SequenceNumber : -1);

                methodMonitor.Activity[SolutionConstants.EventHubName] = NameSpaceAndEventHubName;
                methodMonitor.Activity[SolutionConstants.PartitionId] = partition.PartitionId;
                methodMonitor.Activity[SolutionConstants.LastEnqueuedSequenceNumber] = lastEnqueuedSequenceNumber;

                methodMonitor.OnStart(false);

                EventHubAsyncTaskInfoQueue taskInfoQueue = null;
                if (int.TryParse(partition.PartitionId, out var partitionIdInt) && partitionIdInt < _taskInfoQueuePerPartition.Length)
                {
                    taskInfoQueue = GetOrAddEventHubAsyncTaskInfoQueue(partition.PartitionId, partitionIdInt);
                }
                else
                {
                    throw new Exception("PartitionId:" + partition.PartitionId + " is not expected format/range");
                }

                var eventReadTime = DateTimeOffset.UtcNow;
                taskInfoQueue.LastPartitionReadTime = eventReadTime;

                // Attempt to process each event in the batch, marking if the batch was non-empty.  Exceptions during
                // processing should be logged, as the batch must be processed completely to avoid losing events.

                var emptyBatch = true;

                // Per Partition, ProcessBatchEventDataAsync is called in single thread.
                // That is, next ProcessBatchEventDataAsync is called after this method returns
                // So we don't need any locking for taskInfoQueue


                long lastSequenceNumber = 0;

                foreach (var eventData in events)
                {
                    emptyBatch = false;

                    cancellationToken.ThrowIfCancellationRequested();

                    lastSequenceNumber = eventData.SequenceNumber;

                    taskInfoQueue.LastReadSequenceNumber = lastSequenceNumber;
                    taskInfoQueue.NumReadEvents++;

                    await ProcessEventDataAsync(eventData, partition, taskInfoQueue, eventReadTime).ConfigureAwait(false);
                }

                if (emptyBatch)
                {
                    taskInfoQueue.UpdateCompletedTasks();
                }
                else
                {
                    // PartitionLastReceivedSequenceNumber could be -1 when TrackLastEnqueuedEventProperties is set to false
                    if (lastSequenceNumber > 0 && lastEnqueuedSequenceNumber > 0)
                    {
                        var numOfWaitingMessages = lastEnqueuedSequenceNumber - lastSequenceNumber;
                        if (numOfWaitingMessages >= 0) // just in case
                        {
                            taskInfoQueue.NumOfWaitingMessages = numOfWaitingMessages;
                        }
                    }
                }

                methodMonitor.OnCompleted();
            }
            catch (Exception ex)
            {
                // If cancellation was requested, then either partition ownership was lost or the processor is
                // shutting down.  In either case, dispatching of events to be handled should cease.  Since this
                // flow is entirely internal, there's no benefit to throwing a cancellation exception; instead,
                // just exit the loop.
                if (!ex.IsException<OperationCanceledException>())
                {
                    methodMonitor.OnError(ex, true);
                    throw;
                }

                // OperationCanceledException -> no exception throw
                methodMonitor.OnError(ex);
            }
        }

        private async Task ProcessEventDataAsync(
            EventData eventData, 
            EventProcessorPartition partition, 
            EventHubAsyncTaskInfoQueue taskInfoQueue, 
            DateTimeOffset eventReadTime)
        {
            ActivityContext parentActivityContext = Activity.Current != null ? Activity.Current.Context : default;

            // We create new TraceId here. This traceId will be able to track end to end
            // However if there is case where we create childTask, each ChildTask will have separate TaskId
            using var activity = new OpenTelemetryActivityWrapper(IOServiceOpenTelemetry.IOActivitySource, ActivityName,
                ActivityKind.Consumer, parentActivityContext, createNewTraceId: true, default);

            // ActivityMonitor should be created after OpenTelemetry Activity so that it will get TraceId
            using var methodMonitor = EventHubTaskManagerProcessEventDataAsync.ToMonitor(
                parentActivity: BasicActivity.Null,
                component: ActivityComponentName);
            
            EventHubAsyncTaskInfo eventHubAsyncTaskInfo = null;
            Exception exceptionForPoision = null;
            string inputCorrelationId = null;
            string inputEventType = null;
            string tenantId = null;
            string resourceLocation = null;
            DateTimeOffset inputEventTime = default;
            var hasCompressed = false;

            try
            {
                methodMonitor.OnStart(false);

                IOServiceOpenTelemetry.EventHubReadMessageCounter.Add(1,
                    new KeyValuePair<string, object>(MonitoringConstants.EventHubNameDimension, NameSpaceAndEventHubName),
                    new KeyValuePair<string, object>(MonitoringConstants.PartitionIdDimension, partition.PartitionId),
                    new KeyValuePair<string, object>(MonitoringConstants.ConsumerGroupNameDimension, ConsumerGroupName));

                var dataLabEventHubProperties = DataLabEventHubProperties.Create(eventData);
                dataLabEventHubProperties.ToLog(activity);

                inputCorrelationId = dataLabEventHubProperties.CorrelationId;
                inputEventType = dataLabEventHubProperties.EventType;
                inputEventTime = dataLabEventHubProperties.EventTime;
                tenantId = dataLabEventHubProperties.TenantId;
                resourceLocation = dataLabEventHubProperties.ResourceLocation;
                hasCompressed = dataLabEventHubProperties.HasCompressed ?? false;

                // Let's set correlation from eventHub property
                // When there is single resource, this will be overwritten with the property's correlationId if any
                activity.InputCorrelationId = inputCorrelationId;
                activity.EventType = inputEventType;
                methodMonitor.Activity.CorrelationId = inputCorrelationId;

                // Check Data Format
                SolutionDataFormat dataFormat = SolutionDataFormat.ARN;
                string dataFormatPropValue = dataLabEventHubProperties.DataFormat;
                if (dataFormatPropValue != null)
                {
                    if (!StringEnumCache.TryGetEnumIgnoreCase(dataFormatPropValue, out dataFormat))
                    {
                        // Do not throw exception. 
                        // We should not throw any exception inside EventHubProcessor
                        // Keep going assuming default format ARN
                        using var criticalLogMonitor = EventHubTaskManagerCriticalError.ToMonitor();
                        criticalLogMonitor.Activity[SolutionConstants.MethodName] = "ProcessEventDataAsync";
                        criticalLogMonitor.Activity[SolutionConstants.EventHubName] = NameSpaceAndEventHubName;
                        criticalLogMonitor.Activity[SolutionConstants.PartitionId] = partition.PartitionId;
                        criticalLogMonitor.Activity[SolutionConstants.DataFormat] = dataFormatPropValue;
                        var exception = new Exception("Not supported Data Format: " + dataFormatPropValue + ". Try to use ARN Format");
                        criticalLogMonitor.OnError(exception);
                    }
                }

                if (dataFormat != SolutionDataFormat.ARN)
                {
                    // should not happen but just in case
                    // Move to Poison Channel
                    using (var criticalLogMonitor = EventHubTaskManagerCriticalError.ToMonitor())
                    {
                        var outStr = dataFormat.FastEnumToString();
                        criticalLogMonitor.Activity[SolutionConstants.MethodName] = "ProcessEventDataAsync";
                        criticalLogMonitor.Activity[SolutionConstants.EventHubName] = NameSpaceAndEventHubName;
                        criticalLogMonitor.Activity[SolutionConstants.PartitionId] = partition.PartitionId;
                        criticalLogMonitor.Activity[SolutionConstants.DataFormat] = outStr;
                        var exception = new NotImplementedException("Not Implemented Data Format: " + outStr + ". Try to use ARN Format");
                        criticalLogMonitor.OnError(exception);
                    }

                    dataFormat = SolutionDataFormat.ARN;
                }

                activity.SetTag(SolutionConstants.DataFormat, dataFormat.FastEnumToString());
                activity.SetTag(SolutionConstants.DataEnqueuedTime, eventData.EnqueuedTime);
                activity.SetTag(SolutionConstants.EventHubName, NameSpaceAndEventHubName);
                activity.SetTag(SolutionConstants.PartitionId, partition.PartitionId);
                activity.SetTag(SolutionConstants.EventHubSequenceNumber, eventData.SequenceNumber);
                activity.SetTag(SolutionConstants.EventHubOffset, eventData.Offset);
                activity.SetTag(SolutionConstants.EventHubMessageId, eventData.MessageId);
                activity.SetTag(SolutionConstants.RegionName, RegionConfigData.RegionLocationName);

                // Check if EventData has expected annotation/properties
                var singleInlineResource = false;
                var inlineResource = true; // default is inline resource

                if (dataLabEventHubProperties.HasURL.HasValue)
                {
                    // hasURL is explicitly defined
                    inlineResource = !dataLabEventHubProperties.HasURL.Value;
                    activity.SetTag(SolutionConstants.InlinePayload, inlineResource);
                }

                if (dataLabEventHubProperties.NumResources.HasValue)
                {
                    var numResources = dataLabEventHubProperties.NumResources.Value;
                    activity.SetTag(SolutionConstants.NumResources, numResources);
                    if (numResources == 1 && inlineResource)
                    {
                        singleInlineResource = true;
                    }
                }

                eventHubAsyncTaskInfo = new EventHubAsyncTaskInfo(
                    dataSourceName: NameSpaceAndEventHubName,
                    messageId: eventData.MessageId,
                    enqueuedTime: eventData.EnqueuedTime,
                    partitionId: partition.PartitionId,
                    sequenceNumber: eventData.SequenceNumber,
                    offset: eventData.Offset,
                    messageData: eventData.Data,
                    hasCompressed: hasCompressed,
                    activityContext: activity.Context,
                    regionName: RegionConfigData.RegionLocationName);

                taskInfoQueue.AddTaskInfo(eventHubAsyncTaskInfo);

                ARNSingleInputMessage singleInputMessage = null;
                ARNRawInputMessage rawInputMessage = null;
                
                if (!singleInlineResource)
                {
                    // Check if inputMessage has only one inline resource so that we can use directly ARNSingleInputMessage
                    rawInputMessage = ARNRawInputMessage.CreateRawInputMessage(
                        binaryData: eventData.Data,
                        rawInputCorrelationId: inputCorrelationId,
                        eventTime: inputEventTime,
                        eventType: inputEventType,
                        tenantId: tenantId,
                        resourceLocation: resourceLocation,
                        deserialize: true,
                        hasCompressed: hasCompressed,
                        taskActivity: activity);

                    // Get correlation Id from RawInput
                    inputCorrelationId = rawInputMessage.CorrelationId ?? inputCorrelationId;

                    singleInputMessage = ARNRawInputMessage.TryConvertToSingleMessage(rawInputMessage, activity);

                    // Get correlation Id again from singleInputMessage if rawMessage has only one resource
                    inputCorrelationId = singleInputMessage?.CorrelationId ?? inputCorrelationId;

                    IOServiceOpenTelemetry.EventHubRawInputMessageCounter.Add(1,
                        new KeyValuePair<string, object>(MonitoringConstants.EventHubNameDimension, NameSpaceAndEventHubName),
                        singleInputMessage != null ? 
                            MonitoringConstants.RawInputToSingleInputTrueDimension : 
                            MonitoringConstants.RawInputToSingleInputFalseDimension);
                }
                else
                {
                    IOServiceOpenTelemetry.EventHubSingleInlineMessageCounter.Add(1, 
                        new KeyValuePair<string, object>(MonitoringConstants.EventHubNameDimension, NameSpaceAndEventHubName));

                    singleInputMessage = ARNSingleInputMessage.CreateSingleInputMessage(
                        eventData, 
                        activity, 
                        in dataLabEventHubProperties);

                    inputCorrelationId = singleInputMessage.CorrelationId ?? inputCorrelationId;
                }

                // Set Correlation to propogate through channels
                activity.InputCorrelationId = inputCorrelationId;
                methodMonitor.Activity.CorrelationId = inputCorrelationId;
                methodMonitor.Activity.Properties[SolutionConstants.RegionName] = this.RegionConfigData.RegionLocationName;

                if (singleInputMessage != null)
                {
                    // Set Resource Id to propogate through channels
                    activity.InputResourceId = singleInputMessage.ResourceId;
                    methodMonitor.Activity.InputResourceId = singleInputMessage.ResourceId;

                    // Let's check TrafficTuner as early as possible so that we don't need to create unncessary resource
                    (TrafficTunerResult result, TrafficTunerNotAllowedReason reason) trafficTunerResult;
                    if (RegionConfigManager.IsBackupRegionPairName(this.RegionConfigData.RegionLocationName))
                    {
                          trafficTunerResult = SolutionInputOutputService.BackupEventhubsTrafficTuner.InputTrafficTuner.EvaluateTunerResult(singleInputMessage, 0);
                    } 
                    else
                    {
                        trafficTunerResult = SolutionInputOutputService.InputEventhubsTrafficTuner.InputTrafficTuner.EvaluateTunerResult(singleInputMessage, 0);
                    }

                    if (trafficTunerResult.result != TrafficTunerResult.Allowed)
                    {
                        // We need to filter this notification here
                        // Set EventHubTaskInfo here so that EventHub checkpoint will proceed
                        eventHubAsyncTaskInfo.TaskFiltered();

                        activity.SetTag(SolutionConstants.TaskFiltered, true);
                        activity.SetTag(SolutionConstants.TrafficTunerResult, trafficTunerResult.reason.FastEnumToString());
                        
                        // Export to ActivityMonitor before returning 
                        activity.ExportToActivityMonitor(methodMonitor.Activity);

                        methodMonitor.OnCompleted();
                        return;
                    }

                    var eventTaskContext = new IOEventTaskContext<ARNSingleInputMessage>(
                        InputOutputConstants.EventHubSingleInputEventTask,
                        DataSourceType.InputEventHub,
                        NameSpaceAndEventHubName,
                        firstEnqueuedTime: eventData.EnqueuedTime,
                        firstPickedUpTime: eventReadTime,
                        dataEnqueuedTime: eventData.EnqueuedTime,
                        eventTime: singleInputMessage.EventTime,
                        singleInputMessage,
                        eventHubAsyncTaskInfo,
                        retryCount: 0,
                        SolutionInputOutputService.RetryStrategy,
                        activity.Context,
                        activity.TopActivityStartTime,
                        createNewTraceId: false,
                        regionConfigData: RegionConfigData,
                        CancellationToken.None,
                        SolutionInputOutputService.ARNMessageChannels.RetryChannelManager,
                        SolutionInputOutputService.ARNMessageChannels.PoisonChannelManager,
                        SolutionInputOutputService.ARNMessageChannels.FinalChannelManager,
                        SolutionInputOutputService.GlobalConcurrencyManager);

                    IOServiceOpenTelemetry.ReportInputIndividualResourceCounter(singleInputMessage.EventAction);

                    // Add to InputChannel directly
                    var nextChannel = SolutionInputOutputService.ARNMessageChannels.InputChannelManager;
                    activity.SetTag(SolutionConstants.NextChannel, nextChannel.ChannelName);

                    // Export to ActivityMonitor before adding to TaskChannel
                    activity.ExportToActivityMonitor(methodMonitor.Activity);

                    // Start Task
                    eventHubAsyncTaskInfo.SetCancellableTask(eventTaskContext);
                    eventTaskContext.SetTaskTimeout(_taskTimeOut);
                    await eventTaskContext.StartEventTaskAsync(nextChannel, false, methodMonitor.Activity).ConfigureAwait(false);

                    // Do not put any code after StartTaskAsync because the task might already finish inside it.
                    // so EventTaskActivity might already be disposed

                    methodMonitor.OnCompleted();
                    return;
                }
                else
                {
                    var eventTaskContext = new IOEventTaskContext<ARNRawInputMessage>(
                        InputOutputConstants.EventHubRawInputEventTask,
                        DataSourceType.InputEventHub,
                        NameSpaceAndEventHubName,
                        firstEnqueuedTime: eventData.EnqueuedTime,
                        firstPickedUpTime: eventReadTime,
                        dataEnqueuedTime: eventData.EnqueuedTime,
                        eventTime: rawInputMessage.EventTime,
                        rawInputMessage,
                        eventHubAsyncTaskInfo,
                        retryCount: 0,
                        SolutionInputOutputService.RetryStrategy,
                        activity.Context,
                        activity.TopActivityStartTime,
                        createNewTraceId: false,
                        regionConfigData: RegionConfigData,
                        CancellationToken.None,
                        SolutionInputOutputService.ARNMessageChannels.RawInputRetryChannelManager,
                        SolutionInputOutputService.ARNMessageChannels.RawInputPoisonChannelManager,
                        SolutionInputOutputService.ARNMessageChannels.RawInputFinalChannelManager,
                        SolutionInputOutputService.GlobalConcurrencyManager);

                    // Set EventHubARNRawInputMessage
                    activity.SetTag(SolutionConstants.HasEventHubARNRawInputMessage, true);
                    
                    // Add to RawInputChannelManager
                    var nextChannel = SolutionInputOutputService.ARNMessageChannels.RawInputChannelManager;
                    activity.SetTag(SolutionConstants.NextChannel, nextChannel.ChannelName);

                    // Export to ActivityMonitor before adding to TaskChannel
                    activity.ExportToActivityMonitor(methodMonitor.Activity);

                    // Start Task
                    eventHubAsyncTaskInfo.SetCancellableTask(eventTaskContext);
                    eventTaskContext.SetTaskTimeout(_taskTimeOut);
                    await eventTaskContext.StartEventTaskAsync(nextChannel, false, methodMonitor.Activity).ConfigureAwait(false);

                    // Do not put any code after StartTaskAsync because the task might already finish inside it.
                    // so EventTaskActivity might already be disposed

                    methodMonitor.OnCompleted();
                    return;
                }
            }
            catch (Exception ex)
            {
                exceptionForPoision = ex;

                activity.RecordException("ProcessEventDataAsync", ex);
                activity.SetStatus(ActivityStatusCode.Error);

                activity.ExportToActivityMonitor(methodMonitor.Activity);
                methodMonitor.OnError(ex);
            }

            if (exceptionForPoision != null)
            {
                await MoveRawInputToPoisonAsync(
                    eventData: eventData,
                    partition: partition, 
                    taskInfoQueue: taskInfoQueue,
                    taskInfo: eventHubAsyncTaskInfo,
                    eventReadTime: eventReadTime,
                    correlationId: inputCorrelationId,
                    inputEventTime: inputEventTime,
                    inputEventType: inputEventType,
                    tenantId: tenantId,
                    resourceLocation: resourceLocation,
                    hasCompressed: hasCompressed, 
                    activity, 
                    exceptionForPoision).ConfigureAwait(false);
            }
        }

        private async Task MoveRawInputToPoisonAsync(EventData eventData,
            EventProcessorPartition partition,
            EventHubAsyncTaskInfoQueue taskInfoQueue,
            EventHubAsyncTaskInfo taskInfo,
            DateTimeOffset eventReadTime,
            string correlationId,
            DateTimeOffset inputEventTime,
            string inputEventType,
            string tenantId,
            string resourceLocation,
            bool hasCompressed,
            OpenTelemetryActivityWrapper parentActivity, 
            Exception taskException)
        {
            using var methodMonitor = EventHubTaskManagerMoveRawInputToPoisonAsync.ToMonitor();

            try
            {
                methodMonitor.OnStart(false);

                if (taskInfo == null)
                {
                    taskInfo = new EventHubAsyncTaskInfo(
                        dataSourceName: NameSpaceAndEventHubName,
                        messageId: eventData.MessageId,
                        enqueuedTime: eventData.EnqueuedTime,
                        partitionId: partition.PartitionId,
                        sequenceNumber: eventData.SequenceNumber,
                        offset: eventData.Offset,
                        messageData: eventData.Data,
                        hasCompressed: hasCompressed,
                        activityContext: parentActivity.Context,
                        regionName: RegionConfigData.RegionLocationName);

                    taskInfoQueue.AddTaskInfo(taskInfo);
                }

                var rawInputMessage = ARNRawInputMessage.CreateRawInputMessage(
                    binaryData: eventData.Data,
                    rawInputCorrelationId: correlationId,
                    eventTime: inputEventTime,
                    eventType: inputEventType,
                    tenantId: tenantId,
                    resourceLocation: resourceLocation,
                    deserialize: false,
                    hasCompressed: hasCompressed,
                    taskActivity: null);

                rawInputMessage.NoDeserialize = true;

                var eventTaskContext = new IOEventTaskContext<ARNRawInputMessage>(
                    InputOutputConstants.EventHubRawInputEventTask,
                    DataSourceType.InputEventHub,
                    NameSpaceAndEventHubName,
                    firstEnqueuedTime: eventData.EnqueuedTime,
                    firstPickedUpTime: eventReadTime,
                    dataEnqueuedTime: eventData.EnqueuedTime,
                    eventTime: rawInputMessage.EventTime,
                    rawInputMessage,
                    taskInfo,
                    retryCount: 0,
                    SolutionInputOutputService.RetryStrategy,
                    parentActivity.Context,
                    parentActivity.TopActivityStartTime,
                    createNewTraceId: false,
                    regionConfigData: RegionConfigData,
                    CancellationToken.None,
                    null,
                    SolutionInputOutputService.ARNMessageChannels.RawInputPoisonChannelManager,
                    SolutionInputOutputService.ARNMessageChannels.RawInputFinalChannelManager,
                    null);

                // SetTaskTimeOut
                taskInfo.SetCancellableTask(eventTaskContext);
                eventTaskContext.SetTaskTimeout(_taskTimeOut);

                // Start Task for internal initialization
                // We should not provide startChannel here
                await eventTaskContext.StartEventTaskAsync(null, false, methodMonitor.Activity).ConfigureAwait(false);

                // Add to RawInputPoisonChannel
                eventTaskContext.TaskMovingToPoison(SolutionUtils.GetExceptionTypeSimpleName(taskException), null, "EventHubTaskManager", taskException);
                await eventTaskContext.StartNextChannelAsync().ConfigureAwait(false);

                // Do not put any code after StartTaskAsync because the task might already finish inside it.
                // so EventTaskActivity might already be disposed

                methodMonitor.OnCompleted();
                return;
            }
            catch(Exception ex)
            {
                // Should not reach here
                methodMonitor.OnError(ex, isCriticalLevel: true);
                throw;
            }
        }

        private EventHubAsyncTaskInfoQueue GetOrAddEventHubAsyncTaskInfoQueue(string partitionIdStr, int partitionIdInt)
        {
            var taskInfoQueue = _taskInfoQueuePerPartition[partitionIdInt];
            if (taskInfoQueue != null)
            {
                return taskInfoQueue;
            }

            taskInfoQueue = new EventHubAsyncTaskInfoQueue(NameSpaceAndEventHubName, partitionIdStr, _updateCheckpointAsyncFunc, _checkPointIntervalInSec, _checkPointTimeoutInSec);
            var prevTaskInfoQueue = Interlocked.CompareExchange(ref _taskInfoQueuePerPartition[partitionIdInt], taskInfoQueue, null);
            if (prevTaskInfoQueue != null)
            {
                // Someone already created. Let's close just created TaskInfoQueue
                taskInfoQueue.Dispose();
                return prevTaskInfoQueue;
            }
            else
            {
                taskInfoQueue.StartCheckPointTimer();
                return taskInfoQueue;
            }
        }

        public Task PartitionInitializingHandlerAsync(string partitionId)
        {
            using var methodMonitor = EventHubTaskManagerPartitionInitializingHandlerAsync.ToMonitor();

            try
            {
                methodMonitor.Activity[SolutionConstants.EventHubName] = NameSpaceAndEventHubName;
                methodMonitor.Activity[SolutionConstants.PartitionId] = partitionId;
                methodMonitor.OnStart();

                if (int.TryParse(partitionId, out var partitionIdInt) && partitionIdInt < _taskInfoQueuePerPartition.Length)
                {
                    var taskInfoQueue = _taskInfoQueuePerPartition[partitionIdInt];
                    if (taskInfoQueue != null)
                    {
                        using var criticalLogMonitor = EventHubTaskManagerCriticalError.ToMonitor();
                        criticalLogMonitor.Activity[SolutionConstants.MethodName] = "PartitionInitializingHandlerAsync";
                        criticalLogMonitor.Activity[SolutionConstants.EventHubName] = NameSpaceAndEventHubName;
                        criticalLogMonitor.Activity[SolutionConstants.PartitionId] = partitionId;
                        var ex = new Exception("PartitionInitializingHandlerAsync is called without previous PartitionClose");
                        criticalLogMonitor.OnError(ex);
                        // OK to continue
                    }

                    GetOrAddEventHubAsyncTaskInfoQueue(partitionId, partitionIdInt);
                }
                else
                {
                    using var criticalLogMonitor = EventHubTaskManagerCriticalError.ToMonitor();
                    criticalLogMonitor.Activity[SolutionConstants.MethodName] = "PartitionInitializingHandlerAsync";
                    criticalLogMonitor.Activity[SolutionConstants.EventHubName] = NameSpaceAndEventHubName;
                    criticalLogMonitor.Activity[SolutionConstants.PartitionId] = partitionId;
                    var ex = new Exception("PartitionId is not expected format/range");
                    criticalLogMonitor.OnError(ex);
                    throw ex; // throw exception to fail to initialize
                }

                methodMonitor.OnCompleted();
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
                throw; // throw exception to fail to initialize
            }

            return Task.CompletedTask;
        }

        public Task PartitionClosingHandlerAsync(PartitionClosingEventArgs args)
        {
            using var methodMonitor = EventHubTaskManagerPartitionClosingHandlerAsync.ToMonitor();

            try
            {
                var partitionId = args.PartitionId;
                methodMonitor.Activity[SolutionConstants.EventHubName] = NameSpaceAndEventHubName;
                methodMonitor.Activity[SolutionConstants.PartitionId] = partitionId;
                methodMonitor.OnStart();

                if (int.TryParse(args.PartitionId, out var partitionIdInt) && partitionIdInt < _taskInfoQueuePerPartition.Length)
                {
                    var taskInfoQueue = _taskInfoQueuePerPartition[partitionIdInt];
                    if (taskInfoQueue != null)
                    {
                        Interlocked.Exchange(ref _taskInfoQueuePerPartition[partitionIdInt], null);
                        taskInfoQueue.CloseQueue();
                    }
                    else
                    {
                        using var criticalLogMonitor = EventHubTaskManagerCriticalError.ToMonitor();
                        criticalLogMonitor.Activity[SolutionConstants.MethodName] = "ProcessBatchEventDataAsync";
                        criticalLogMonitor.Activity[SolutionConstants.EventHubName] = NameSpaceAndEventHubName;
                        criticalLogMonitor.Activity[SolutionConstants.PartitionId] = args.PartitionId;
                        var ex = new Exception("PartitionClosingHandlerAsync is called without previous PartitionInitializing");
                        criticalLogMonitor.OnError(ex);
                    }

                }
                else
                {
                    using var criticalLogMonitor = EventHubTaskManagerCriticalError.ToMonitor();
                    criticalLogMonitor.Activity[SolutionConstants.MethodName] = "ProcessBatchEventDataAsync";
                    criticalLogMonitor.Activity[SolutionConstants.EventHubName] = NameSpaceAndEventHubName;
                    criticalLogMonitor.Activity[SolutionConstants.PartitionId] = args.PartitionId;
                    var ex = new Exception("PartitionId is not valid");
                    criticalLogMonitor.OnError(ex);
                }

                methodMonitor.OnCompleted();
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
                // closing. no throw exception
            }
            
            return Task.CompletedTask;
        }

        private Task UpdateCheckpointInterval(int newIntervalInSec)
        {
            if (newIntervalInSec <= 0)
            {
                Logger.LogError("CheckPoint Interval must be larger than 0");
                return Task.CompletedTask;
            }

            var oldCheckPointInterval = _checkPointIntervalInSec;
            if (Interlocked.CompareExchange(ref _checkPointIntervalInSec, newIntervalInSec, oldCheckPointInterval) == oldCheckPointInterval)
            {
                Logger.LogWarning("CheckpointInterval is changed, Old: {oldVal}, New: {newVal}", oldCheckPointInterval, newIntervalInSec);

                var taskInfoQueues = _taskInfoQueuePerPartition;
                for (int i = 0; i < taskInfoQueues.Length; i++)
                {
                    var eventHubAsyncTaskInfoQueue = taskInfoQueues[i];
                    eventHubAsyncTaskInfoQueue?.UpdateCheckPointInterval(newIntervalInSec);
                }
            }

            return Task.CompletedTask;
        }

        private Task UpdateCheckpointTimeout(int newTimeoutInSec)
        {
            if (newTimeoutInSec <= 0)
            {
                Logger.LogError("Timeout must be larger than 0");
                return Task.CompletedTask;
            }

            var oldCheckPointTimeout = _checkPointTimeoutInSec;
            if (Interlocked.CompareExchange(ref _checkPointTimeoutInSec, newTimeoutInSec, oldCheckPointTimeout) == oldCheckPointTimeout)
            {
                Logger.LogWarning("CheckpointTimeout is changed, Old: {oldVal}, New: {newVal}", oldCheckPointTimeout, newTimeoutInSec);

                var taskInfoQueues = _taskInfoQueuePerPartition;
                for (int i = 0; i < taskInfoQueues.Length; i++)
                {
                    var eventHubAsyncTaskInfoQueue = taskInfoQueues[i];
                    eventHubAsyncTaskInfoQueue?.UpdateCheckPointTimeout(newTimeoutInSec);
                }
            }

            return Task.CompletedTask;
        }

        private static Task UpdateTaskTimeOutDuration(TimeSpan newTimeOut)
        {
            if (newTimeOut.TotalMilliseconds <= 0)
            {
                Logger.LogError("{config} must be larger than 0", InputOutputConstants.InputEventHubTaskTimeOutDuration);
                return Task.CompletedTask;
            }

            lock (_updateLock)
            {
                var oldTimeOut = _taskTimeOut;
                if (newTimeOut.TotalMilliseconds == oldTimeOut.TotalMilliseconds)
                {
                    return Task.CompletedTask;
                }

                _taskTimeOut = newTimeOut;

                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    InputOutputConstants.InputEventHubTaskTimeOutDuration,
                    oldTimeOut, newTimeOut);
            }

            return Task.CompletedTask;
        }
    }
}
