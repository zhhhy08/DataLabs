namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Runtime.CompilerServices;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;

    public class IOServiceOpenTelemetry
    {
        public const string IOServiceTraceSource = "ARG.DataLabs.IOService";
        public const string IOServiceMeter = IOServiceTraceSource;

        public static readonly ActivitySource IOActivitySource = new(IOServiceTraceSource);
        public static readonly Meter IOServiceNameMeter = new(IOServiceMeter, "1.0");

        /* Metric Names */
        #region IO Metrics

        public const string NUM_SINGLE_INLINE_EVENTHUB_MESSAGE = "SingleInputEHMessage";
        public const string NUM_RAW_EVENTHUB_MESSAGE = "RawInputEHMessage";

        public const string PROCESSED_REQUEST = "ProcessedRequest";
        public const string DROPPED_REQUEST = "DroppedRequest";
        public const string TIMEOUT_REQUEST = "TimeoutRequest";

        public const string BLOB_SOURCE_OF_TRUTH_UPLOAD_CONFLICT_COUNTER = "BlobSourceOfTruthUploadConflictCounter";
        public const string BLOB_SOURCE_OF_TRUTH_UPLOAD_COUNTER = "BlobSourceOfTruthUploadCounter";

        public const string QUEUE_READ_MESSAGES_SUFFIX = "QueueReadMessages";

        public const string RETRY_QUEUE_MOVING_COUNTER = "RetryQueueMovingCounter";
        public const string RETRY_QUEUE_WRITE_COUNTER = "RetryQueueWriteCounter";
        public const string RETRY_QUEUE_WRITE_FINAL_FAIL_COUNTER = "RetryQueueWriteFinalFailCounter";
        public const string POISON_QUEUE_MOVING_COUNTER = "PoisonQueueMovingCounter";
        public const string POISON_QUEUE_WRITE_COUNTER = "PoisonQueueWriteCounter";
        public const string POISON_QUEUE_WRITE_FINAL_FAIL_COUNTER = "PoisonQueueWriteFinalFailCounter";
        public const string SUBJOB_QUEUE_WRITE_COUNTER = "SubJobQueueWriteCounter";
        public const string SUBJOB_QUEUE_WRITE_FINAL_FAIL_COUNTER = "SubJobQueueWriteFinalFailCounter";

        public const string DELAY_FROM_INPUT_ENQUEUE_TO_START = "DelayFromInputEnqueueToStart";
        public const string DELAY_FROM_INPUT_ENQUEUE_TO_PICKUP = "DelayFromInputEnqueueToPickup";
        public const string DELAY_FROM_EVENTTIME_TO_INPUT_ENQUEUE = "DelayFromEventTimeToInputEnqueue";

        public const string START_DELAY = "StartDelay";
        public const string E2E_DURATION = "E2EDuration";

        public const string EVENTHUB_WRITE_COUNTER = "EventHubWriteCounter";
        public const string EVENTHUB_WRITE_FINAL_FAIL_COUNTER = "EventHubWriteFinalFailCounter";
        public const string EVENTHUB_WRITE_DURATION = "EventHubWriteDuration";
        public const string EVENTHUB_WRITE_FAIL_DURATION = "EventHubWriteFailDuration";
        public const string ARN_PUBLISH_COUNTER = "ArnPublishCounter";
        public const string ARN_PUBLISH_FINAL_FAIL_COUNTER = "ArnPublishFinalFailCounter";
        public const string ARN_PUBLISH_DURATION = "ArnPublishDuration";
        public const string ARN_PUBLISH_FAIL_DURATION = "ArnPublishFailDuration";
        public const string RETRY_WRITE_DURATION = "RetryWriteDuration";
        public const string RETRY_WRITE_FAIL_DURATION = "RetryWriteFailDuration";
        public const string POISON_WRITE_DURATION = "PoisonWriteDuration";
        public const string POISON_WRITE_FAIL_DURATION = "PoisonWriteFailDuration";
        public const string SUBJOB_WRITE_DURATION = "SubJobWriteDuration";
        public const string SUBJOB_WRITE_FAIL_DURATION = "SubJobWriteFailDuration";
        public const string EVENTHUB_BATCH_SIZE = "EventHubBatchSize";
        public const string ARN_PUBLISH_BATCH_SIZE = "ArnPublishBatchSize";
        public const string RETRY_BATCH_SIZE = "RetryBatchSize";
        public const string POISON_BATCH_SIZE = "PoisonBatchSize";
        public const string SUBJOB_BATCH_SIZE = "SubJobBatchSize";
        public const string BatchWriteFailCounterNameSuffix = "BatchWriteFailCounter";

        public const string EVENTHUB_READ_MESSAGES = "EventHubReadMessages";
        public const string EVENTHUB_WAITING_MESSAGES = "EventHubWaitingMessages";
        public const string EVENTHUB_TASK_QUEUE_LENGTH = "EventHubTaskQueueLength";
        public const string EVENTHUB_CHECK_POINT_PENDING_MESSAGES = "EventHubCheckPointPendingMessages";
        public const string EVENTHUB_SEC_SINCE_LAST_CHECKPOINT = "EventHubSecSinceLastCheckPoint";
        public const string EVENTHUB_SEC_SINCE_LAST_READ = "EventHubSecSinceLastRead";
        public const string EVENTHUB_SINGLE_INLINE_MESSAGE = "EventHubSingleInlineMessage";
        public const string EVENTHUB_RAW_INPUT_MESSAGE = "EventHubRawInputMessage";

        public const string ARN_SINGLE_INPUT_DESERIALIZER_COUNTER = "ARNSingleInputDeserializerCounter";
        public const string ARN_RAW_INPUT_DESERIALIZER_COUNTER = "ARNRawInputDeserializerCounter";

        // Blob Payload Routing related Metric
        public const string BLOB_PAYLOAD_ROUTING_FROM_EVENT_TIME = "BlobPayloadRoutingFromEventTime";
        public const string BLOB_PAYLOAD_ROUTING_FROM_INPUT_ENQUEUED_TIME = "BlobPayloadRoutingFromInputEnqueuedTime";
        public const string BLOB_PAYLOAD_ROUTING_FROM_INPUT_PICKEDUP_TIME = "BlobPayloadRoutingFromInputPickedUpTime";

        // SLO related Metric
        public const string SLO_FROM_EVENT_TIME = "SLOFromEventTime";
        public const string SLO_FROM_INPUT_ENQUEUED_TIME = "SLOFromInputEnqueuedTime";
        public const string SLO_FROM_INPUT_PICKEDUP_TIME = "SLOFromInputPickedUpTime";
        public const string SLO_EXCLUDING_PARTNER_TIME = "SLOExcludingPartnerTime";

        public const string SLO_TO_POISON_FROM_EVENT_TIME = "SLOToPoisonFromEventTime";
        public const string SLO_TO_POISON_FROM_INPUT_ENQUEUED_TIME = "SLOToPoisonFromInputEnqueuedTime";
        public const string SLO_TO_POISON_FROM_INPUT_PICKEDUP_TIME = "SLOToPoisonFromInputPickedUpTime";
        public const string SLO_TO_POISON_EXCLUDING_PARTNER_TIME = "SLOToPoisonExcludingPartnerTime";

        public const string SLO_NUM_INPUT_RESOURCE_COUNTER = "SLONumInputResource";
        public const string SLO_NUM_RETRYING_RESOURCE_COUNTER = "SLONumRetryingResource";
        public const string SLO_NUM_MOVED_TO_RETRY_COUNTER = "SLONumMovedToRetryQueue";
        public const string SLO_NUM_MOVED_TO_POISON_COUNTER = "SLONumMovedToPoisonQueue";
        public const string SLO_NUM_MOVED_TO_DROP_COUNTER = "SLONumMovedToDrop";

        #endregion

        public static readonly Counter<long> ARNSingleInputDeserializerCounter = IOServiceNameMeter.CreateCounter<long>(ARN_SINGLE_INPUT_DESERIALIZER_COUNTER);
        public static readonly Counter<long> ARNRawInputDeserializerCounter = IOServiceNameMeter.CreateCounter<long>(ARN_RAW_INPUT_DESERIALIZER_COUNTER);

        public static readonly Counter<long> EventHubReadMessageCounter = IOServiceNameMeter.CreateCounter<long>(EVENTHUB_READ_MESSAGES);
        public static readonly Counter<long> EventHubSingleInlineMessageCounter = IOServiceNameMeter.CreateCounter<long>(EVENTHUB_SINGLE_INLINE_MESSAGE);
        public static readonly Counter<long> EventHubRawInputMessageCounter = IOServiceNameMeter.CreateCounter<long>(EVENTHUB_RAW_INPUT_MESSAGE);

        public static readonly Counter<long> ProcessedCounter = IOServiceNameMeter.CreateCounter<long>(PROCESSED_REQUEST);
        public static readonly Counter<long> DroppedCounter = IOServiceNameMeter.CreateCounter<long>(DROPPED_REQUEST);
        public static readonly Counter<long> TaskTimeoutExpiredCounter = IOServiceNameMeter.CreateCounter<long>(TIMEOUT_REQUEST);

        public static readonly Counter<long> BlobSourceOfTruthUploadConflictCounter = IOServiceNameMeter.CreateCounter<long>(BLOB_SOURCE_OF_TRUTH_UPLOAD_CONFLICT_COUNTER);
        public static readonly Counter<long> BlobSourceOfTruthUploadCounter = IOServiceNameMeter.CreateCounter<long>(BLOB_SOURCE_OF_TRUTH_UPLOAD_COUNTER);
        public static readonly Counter<long> EventHubWriteSuccessCounter = IOServiceNameMeter.CreateCounter<long>(EVENTHUB_WRITE_COUNTER);
        public static readonly Counter<long> EventHubWriteFinalFailCount = IOServiceNameMeter.CreateCounter<long>(EVENTHUB_WRITE_FINAL_FAIL_COUNTER);
        public static readonly Counter<long> ArnPublishSuccessCounter = IOServiceNameMeter.CreateCounter<long>(ARN_PUBLISH_COUNTER);
        public static readonly Counter<long> ArnPublishFinalFailCounter = IOServiceNameMeter.CreateCounter<long>(ARN_PUBLISH_FINAL_FAIL_COUNTER);

        public static readonly Counter<long> RetryQueueMovingCounter = IOServiceNameMeter.CreateCounter<long>(RETRY_QUEUE_MOVING_COUNTER);
        public static readonly Counter<long> RetryQueueWriteSuccessCounter = IOServiceNameMeter.CreateCounter<long>(RETRY_QUEUE_WRITE_COUNTER);
        public static readonly Counter<long> RetryQueueWriteFinalFailCounter = IOServiceNameMeter.CreateCounter<long>(RETRY_QUEUE_WRITE_FINAL_FAIL_COUNTER);
        public static readonly Counter<long> PoisonQueueMovingCounter = IOServiceNameMeter.CreateCounter<long>(POISON_QUEUE_MOVING_COUNTER);
        public static readonly Counter<long> PoisonQueueWriteSuccessCounter = IOServiceNameMeter.CreateCounter<long>(POISON_QUEUE_WRITE_COUNTER);
        public static readonly Counter<long> PoisonQueueWriteFinalFailCounter = IOServiceNameMeter.CreateCounter<long>(POISON_QUEUE_WRITE_FINAL_FAIL_COUNTER);
        public static readonly Counter<long> SubJobQueueWriteSuccessCounter = IOServiceNameMeter.CreateCounter<long>(SUBJOB_QUEUE_WRITE_COUNTER);
        public static readonly Counter<long> SubJobQueueWriteFinalFailCounter = IOServiceNameMeter.CreateCounter<long>(SUBJOB_QUEUE_WRITE_FINAL_FAIL_COUNTER);

        public static readonly Histogram<int> DelayFromInputEnqueueToStartMetric = IOServiceNameMeter.CreateHistogram<int>(DELAY_FROM_INPUT_ENQUEUE_TO_START);
        public static readonly Histogram<int> DelayFromInputEnqueueToPickupMetric = IOServiceNameMeter.CreateHistogram<int>(DELAY_FROM_INPUT_ENQUEUE_TO_PICKUP);
        public static readonly Histogram<int> DelayFromEventTimeToInputEnqueue = IOServiceNameMeter.CreateHistogram<int>(DELAY_FROM_EVENTTIME_TO_INPUT_ENQUEUE);

        public static readonly Histogram<int> StartDelayMetric = IOServiceNameMeter.CreateHistogram<int>(START_DELAY);
        public static readonly Histogram<int> E2EDurationMetric = IOServiceNameMeter.CreateHistogram<int>(E2E_DURATION);

        public static readonly Histogram<int> EventHubWriteSuccessDuration = IOServiceNameMeter.CreateHistogram<int>(EVENTHUB_WRITE_DURATION);
        public static readonly Histogram<int> EventHubWriteFailDuration = IOServiceNameMeter.CreateHistogram<int>(EVENTHUB_WRITE_FAIL_DURATION);
        public static readonly Histogram<int> ArnPublishSuccessDuration = IOServiceNameMeter.CreateHistogram<int>(ARN_PUBLISH_DURATION);
        public static readonly Histogram<int> ArnPublishFailDuration = IOServiceNameMeter.CreateHistogram<int>(ARN_PUBLISH_FAIL_DURATION);
        public static readonly Histogram<int> RetryWriteSuccessDuration = IOServiceNameMeter.CreateHistogram<int>(RETRY_WRITE_DURATION);
        public static readonly Histogram<int> RetryWriteFailDuration = IOServiceNameMeter.CreateHistogram<int>(RETRY_WRITE_FAIL_DURATION);
        public static readonly Histogram<int> PoisonWriteSuccessDuration = IOServiceNameMeter.CreateHistogram<int>(POISON_WRITE_DURATION);
        public static readonly Histogram<int> PoisonWriteFailDuration = IOServiceNameMeter.CreateHistogram<int>(POISON_WRITE_FAIL_DURATION);
        public static readonly Histogram<int> SubJobWriteSuccessDuration = IOServiceNameMeter.CreateHistogram<int>(SUBJOB_WRITE_DURATION);
        public static readonly Histogram<int> SubJobWriteFailDuration = IOServiceNameMeter.CreateHistogram<int>(SUBJOB_WRITE_FAIL_DURATION);

        public static readonly Histogram<int> EventHubSuccessBatchSizeMetric = IOServiceNameMeter.CreateHistogram<int>(EVENTHUB_BATCH_SIZE);
        public static readonly Histogram<int> ArnPublishSuccessBatchSizeMetric = IOServiceNameMeter.CreateHistogram<int>(ARN_PUBLISH_BATCH_SIZE);
        public static readonly Histogram<int> RetrySuccessBatchSizeMetric = IOServiceNameMeter.CreateHistogram<int>(RETRY_BATCH_SIZE);
        public static readonly Histogram<int> PoisonSuccessBatchSizeMetric = IOServiceNameMeter.CreateHistogram<int>(POISON_BATCH_SIZE);
        public static readonly Histogram<int> SubJobSuccessBatchSizeMetric = IOServiceNameMeter.CreateHistogram<int>(SUBJOB_BATCH_SIZE);

        // BlobPayload Routing related Metric
        public static readonly Histogram<int> BlobPayloadRoutingFromEventTimeMetric = IOServiceNameMeter.CreateHistogram<int>(BLOB_PAYLOAD_ROUTING_FROM_EVENT_TIME);
        public static readonly Histogram<int> BlobPayloadRoutingFromInputEnqueuedTimeMetric = IOServiceNameMeter.CreateHistogram<int>(BLOB_PAYLOAD_ROUTING_FROM_INPUT_ENQUEUED_TIME);
        public static readonly Histogram<int> BlobPayloadRoutingFromInputPickedUpTimeMetric = IOServiceNameMeter.CreateHistogram<int>(BLOB_PAYLOAD_ROUTING_FROM_INPUT_PICKEDUP_TIME);

        // SLO related Metric
        public static readonly Histogram<int> SLOFromEventTimeMetric = IOServiceNameMeter.CreateHistogram<int>(SLO_FROM_EVENT_TIME);
        public static readonly Histogram<int> SLOFromInputEnqueuedTimeMetric = IOServiceNameMeter.CreateHistogram<int>(SLO_FROM_INPUT_ENQUEUED_TIME);
        public static readonly Histogram<int> SLOFromInputPickedUpTimeMetric = IOServiceNameMeter.CreateHistogram<int>(SLO_FROM_INPUT_PICKEDUP_TIME);
        public static readonly Histogram<int> SLOExcludingPartnerTimeMetric = IOServiceNameMeter.CreateHistogram<int>(SLO_EXCLUDING_PARTNER_TIME);

        public static readonly Histogram<int> SLOToPoisonFromEventTimeMetric = IOServiceNameMeter.CreateHistogram<int>(SLO_TO_POISON_FROM_EVENT_TIME);
        public static readonly Histogram<int> SLOToPoisonFromInputEnqueuedTimeMetric = IOServiceNameMeter.CreateHistogram<int>(SLO_TO_POISON_FROM_INPUT_ENQUEUED_TIME);
        public static readonly Histogram<int> SLOToPoisonFromInputPickedUpTimeMetric = IOServiceNameMeter.CreateHistogram<int>(SLO_TO_POISON_FROM_INPUT_PICKEDUP_TIME);
        public static readonly Histogram<int> SLOToPoisonExcludingPartnerTimeMetric = IOServiceNameMeter.CreateHistogram<int>(SLO_TO_POISON_EXCLUDING_PARTNER_TIME);

        public static readonly Counter<long> SLONumInputResourceCounter = IOServiceNameMeter.CreateCounter<long>(SLO_NUM_INPUT_RESOURCE_COUNTER);
        public static readonly Counter<long> SLONumRetryingResourceCounter = IOServiceNameMeter.CreateCounter<long>(SLO_NUM_RETRYING_RESOURCE_COUNTER);
        public static readonly Counter<long> SLONumMovedToRetryCounter = IOServiceNameMeter.CreateCounter<long>(SLO_NUM_MOVED_TO_RETRY_COUNTER);
        public static readonly Counter<long> SLONumMovedToPoisonCounter = IOServiceNameMeter.CreateCounter<long>(SLO_NUM_MOVED_TO_POISON_COUNTER);
        public static readonly Counter<long> SLONumMovedToDropCounter = IOServiceNameMeter.CreateCounter<long>(SLO_NUM_MOVED_TO_DROP_COUNTER);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportBlobPayloadRoutingFromEventTimeMetric(int delayFromInputEventTime, string inputAction, string resourceType, int retryCount)
        {
            if (delayFromInputEventTime <= 0)
            {
                delayFromInputEventTime = 1;
            }
            BlobPayloadRoutingFromEventTimeMetric.Record(delayFromInputEventTime,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.ResourceType, resourceType),
                new KeyValuePair<string, object>(MonitoringConstants.RetryCountDimension, retryCount),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportBlobPayloadRoutingFromInputEnqueuedTimeMetric(int delayFromInputEnqueue, string inputAction, string resourceType, int retryCount)
        {
            if (delayFromInputEnqueue <= 0)
            {
                delayFromInputEnqueue = 1;
            }
            BlobPayloadRoutingFromInputEnqueuedTimeMetric.Record(delayFromInputEnqueue,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.ResourceType, resourceType),
                new KeyValuePair<string, object>(MonitoringConstants.RetryCountDimension, retryCount),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportBlobPayloadRoutingFromInputPickedUpTimeMetric(int delayFromInputPickedUpTime, string inputAction, string resourceType, int retryCount)
        {
            if (delayFromInputPickedUpTime <= 0)
            {
                delayFromInputPickedUpTime = 1;
            }
            BlobPayloadRoutingFromInputPickedUpTimeMetric.Record(delayFromInputPickedUpTime,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.ResourceType, resourceType),
                new KeyValuePair<string, object>(MonitoringConstants.RetryCountDimension, retryCount),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportSLOFromEventTimeMetric(int delayFromInputEventTime, string inputAction, string outputResourceType, string outputAction, int retryCount)
        {
            if (delayFromInputEventTime <= 0)
            {
                delayFromInputEventTime = 1;
            }
            SLOFromEventTimeMetric.Record(delayFromInputEventTime,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.OutputResourceTypeDimension, outputResourceType),
                new KeyValuePair<string, object>(MonitoringConstants.OutputActionDimension, outputAction),
                new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, retryCount > 0),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportSLOFromInputEnqueuedTimeMetric(int delayFromInputEnqueue, string inputAction, string outputResourceType, string outputAction, int retryCount)
        {
            if (delayFromInputEnqueue <= 0)
            {
                delayFromInputEnqueue = 1;
            }
            SLOFromInputEnqueuedTimeMetric.Record(delayFromInputEnqueue,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.OutputResourceTypeDimension, outputResourceType),
                new KeyValuePair<string, object>(MonitoringConstants.OutputActionDimension, outputAction),
                new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, retryCount > 0),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportSLOFromInputPickedUpTimeMetric(int delayFromInputPickedUpTime, string inputAction, string outputResourceType, string outputAction, int retryCount)
        {
            if (delayFromInputPickedUpTime <= 0)
            {
                delayFromInputPickedUpTime = 1;
            }
            SLOFromInputPickedUpTimeMetric.Record(delayFromInputPickedUpTime,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.OutputResourceTypeDimension, outputResourceType),
                new KeyValuePair<string, object>(MonitoringConstants.OutputActionDimension, outputAction),
                new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, retryCount > 0),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportSLOExcludingPartnerTimeMetric(int elapsedTimeExcludingPartnerTime, string inputAction, string outputResourceType, string outputAction, int retryCount)
        {
            if (elapsedTimeExcludingPartnerTime <= 0)
            {
                elapsedTimeExcludingPartnerTime = 1;
            }
            SLOExcludingPartnerTimeMetric.Record(elapsedTimeExcludingPartnerTime,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.OutputResourceTypeDimension, outputResourceType),
                new KeyValuePair<string, object>(MonitoringConstants.OutputActionDimension, outputAction),
                new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, retryCount > 0),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportSLOToPoisonFromEventTimeMetric(int delayFromInputEventTime, string inputAction, bool isPartnerDecision, int retryCount)
        {
            if (delayFromInputEventTime <= 0)
            {
                delayFromInputEventTime = 1;
            }

            SLOToPoisonFromEventTimeMetric.Record(delayFromInputEventTime,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.IsPartnerDecision, isPartnerDecision),
                new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, retryCount > 0),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportSLOToPoisonFromInputEnqueuedTimeMetric(int delayFromInputEnqueue, string inputAction, bool isPartnerDecision, int retryCount)
        {
            if (delayFromInputEnqueue <= 0)
            {
                delayFromInputEnqueue = 1;
            }

            SLOToPoisonFromInputEnqueuedTimeMetric.Record(delayFromInputEnqueue,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.IsPartnerDecision, isPartnerDecision),
                new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, retryCount > 0),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportSLOToPoisonFromInputPickedUpTimeMetric(int delayFromInputPickedUpTime, string inputAction, bool isPartnerDecision, int retryCount)
        {
            if (delayFromInputPickedUpTime <= 0)
            {
                delayFromInputPickedUpTime = 1;
            }

            SLOToPoisonFromInputPickedUpTimeMetric.Record(delayFromInputPickedUpTime,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.IsPartnerDecision, isPartnerDecision),
                new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, retryCount > 0),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportSLOToPoisonExcludingPartnerTimeMetric(int elapsedTimeExcludingPartnerTime, string inputAction, bool isPartnerDecision, int retryCount)
        {
            if (elapsedTimeExcludingPartnerTime <= 0)
            {
                elapsedTimeExcludingPartnerTime = 1;
            }

            SLOToPoisonExcludingPartnerTimeMetric.Record(elapsedTimeExcludingPartnerTime,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, inputAction),
                new KeyValuePair<string, object>(MonitoringConstants.IsPartnerDecision, isPartnerDecision),
                new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, retryCount > 0),
                MonitoringConstants.IsSnapshotInputDimension(inputAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportInputIndividualResourceCounter(string eventAction)
        {
            // We can't include resource Type as dimension here
            // because we are using one Geneva account for Prod DataLabs and cardinality of resource Type is huge across all partners
            // It is better to ask Partner to create some metrics if they want to create per input resource Type
            SLONumInputResourceCounter.Add(1,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, eventAction),
                MonitoringConstants.IsSnapshotInputDimension(eventAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportRetryingIndividualResourceCounter(string eventAction)
        {
            SLONumRetryingResourceCounter.Add(1,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, eventAction),
                MonitoringConstants.IsSnapshotInputDimension(eventAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportMovedToRetryIndividualResourceCounter(string eventAction)
        {
            SLONumMovedToRetryCounter.Add(1,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, eventAction),
                MonitoringConstants.IsSnapshotInputDimension(eventAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportMovedToPoisonIndividualResourceCounter(string eventAction)
        {
            SLONumMovedToPoisonCounter.Add(1,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, eventAction),
                MonitoringConstants.IsSnapshotInputDimension(eventAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportDroppedIndividualResourceCounter(string eventAction)
        {
            SLONumMovedToDropCounter.Add(1,
                new KeyValuePair<string, object>(MonitoringConstants.InputActionDimension, eventAction),
                MonitoringConstants.IsSnapshotInputDimension(eventAction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRetryQueueEventTask(string eventTaskName)
        {
            return InputOutputConstants.RetryQueueServiceBusSingleInputEventTask == eventTaskName;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsIndividualResourceTask(string eventTaskName)
        {
            return (eventTaskName == InputOutputConstants.EventHubSingleInputEventTask ||
                eventTaskName == InputOutputConstants.RawInputChildEventTask ||
                eventTaskName == InputOutputConstants.PartnerInputChildEventTask ||
                eventTaskName == InputOutputConstants.RetryQueueServiceBusSingleInputEventTask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInputProviderRelatedTask(string eventTaskName)
        {
            return (eventTaskName == InputOutputConstants.EventHubSingleInputEventTask ||
                eventTaskName == InputOutputConstants.EventHubRawInputEventTask ||
                eventTaskName == InputOutputConstants.RawInputChildEventTask);
        }
    }
}
