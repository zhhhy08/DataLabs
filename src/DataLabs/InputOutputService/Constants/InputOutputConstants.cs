namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using System.Collections.Generic;

    public static class InputOutputConstants
    {
        // Input Task timeout
        public const string InputEventHubTaskTimeOutDuration = "InputEventHubTaskTimeOutDuration";
        // EventHub Message elpased Duration without checkpoint
        public const string InputEventHubMessageMaxDurationWithoutCheckPoint = "InputEventHubMessageMaxDurationWithoutCheckPoint";
        // ServiceBus Queue Task Timeout
        public const string ServiceBusTaskTimeOutDurationSuffix = "TaskTimeOutDuration";

        // Admin Service Bus Controller
        public const string DelayBeforeRecreateQueueInMilliseconds = "DelayBeforeRecreateQueueInMilliseconds";
        public const string QueueOperationTimeOut = "QueueOperationTimeOut";

        /* IOEventTaskType */
        public const string EventHubSingleInputEventTask = "EventHubSingleInputEventTask";
        public const string EventHubRawInputEventTask = "EventHubRawInputEventTask";
        public const string EventHubMaxDurationExpireTask = "EventHubMaxDurationExpireTask";

        public const string RawInputChildEventTask = "RawInputChildEventTask";
        public const string PartnerInputChildEventTask = "PartnerInputChildEventTask";
        public const string StreamResponseChildEventTask = "StreamResponseChildEventTask";

        public const string ServiceBusSingleInputEventTaskSuffix = "SingleInputEventTask";
        public const string ServiceBusRawInputEventTaskSuffix = "RawInputEventTask";

        public const string RetryQueueServiceBusSingleInputEventTask = ServiceBusRetryQueueLogicalName + ServiceBusSingleInputEventTaskSuffix;

        public const string EventHubMaxDurationExpireTaskTimeOut = "EventHubMaxDurationExpireTaskTimeOut";

        public const string DelaySecsForEHLoadBalancing = "DelayForEHLoadBalancing";

        public const string InputEventHubPartitions = "InputEventHubPartitions";
        public const string InputEventHubNameSpaceAndName = "InputEventHubNameSpaceAndName"; //idmappingeh1/benidmapping-input-1
        public const string InputEventHubConnectionString = "InputEventHubConnectionString";
        public const string InputEventHubCheckPointIntervalInSec = "InputEventHubCheckPointIntervalInSec";
        public const string InputEventHubCheckPointTimeoutInSec = "InputEventHubCheckPointTimeoutInSec";
        public const string InputChannelActive = "InputChannelActive";

        public const string BackupInputEventHubNameSpaceAndName = "BackupInputEventHubNameSpaceAndName"; //idmappingeh1/benidmapping-input-1
        public const string BackupInputEventHubConnectionString = "BackupInputEventHubConnectionString";
        public const string BackupInputChannelActive = "BackupInputChannelActive";
        public const string StartBackupInputProvidersAtStartup = "StartBackupInputProvidersAtStartup";

        public const string DefaultConsumerGroupName = "$Default";
        public const string SecondaryConsumerGroupName = "secondary";

        public const string EventHubStorageAccountName = "EventHubStorageAccountName";
        public const string EventHubStorageAccountConnectionString = "EventHubStorageAccountConnectionString";

        public const string InputEventHubConsumerGroup = "InputEventHubConsumerGroup";
        public const string SolutionName = "SolutionName";

        public const string OutputEventHubNameSpaceAndName = "OutputEventHubNameSpaceAndName"; //idmappingeh1/benidmapping-output-1
        public const string OutputEventHubConnectionString = "OutputEventHubConnectionString";

        public const string ServiceBusQueueConnectionString = "ServiceBusQueueConnectionString";
        public const string ServiceBusNameSpaceAndName = "ServiceBusNameSpaceAndName"; //benidmappingservicebus1/benidmappingsbqueue1
        public const string ServiceBusSubJobQueueName = "ServiceBusSubJobQueueName";

        public const string ServiceBusMaxAutoRenewDuration = "ServiceBusMaxAutoRenewDuration";

        public const string ServiceBusQueuePrefix = "ServiceBus";
        public const string ServiceBusQueueConcurrencySuffix = "Concurrency";
        public const string ServiceBusQueuePrefetchCount = "PrefetchCount";

        public const string ServiceBusRetryQueueLogicalName = "RetryQueue";
        public const string ServiceBusSubJobQueueLogicalName = "SubJobQueue";

        public const string EventHubMaxOutputSize = "EventHubMaxOutputSize";
        public const string EventHubWriteFailRetryDelayInMsec = "EventHubWriteFailRetryDelayInMsec";

        public const string EnableGrpcTrace = "EnableGrpcTrace";
        public const string EnableHttpClientTrace = "EnableHttpClientTrace";
        public const string EnableAzureSDKActivity = "EnableAzureSDKActivity";
        public const string IncludeRunTimeMetrics = "IncludeRunTimeMetrics";
        public const string IncludeHttpClientMetrics = "IncludeHttpClientMetrics";

        public const string DependentResourceTypes = "DependentResourceTypes";
        public const string InputCacheTypes = "InputCacheTypes";
        public const string AllowedOutputTypes = "AllowedOutputTypes";

        public const string ParentTask = "ParentTask";
        public const string ChildTask = "ChildTask";
        public const string ChildTaskId = "ChildTaskId";
        public const string ChildTaskTraceId = "ChildTaskTraceId";
        public const string InternalResponse = "InternalResponse";

        /* Source Of Truth Related */
        public const string UseSourceOfTruth = "UseSourceOfTruth";
        public const string SourceOfTruthConflictRetryDelayInMsec = "SourceOfTruthConflictRetryDelayInMsec";
        public const string SourceOfTruthUseOutputTimeTagCondition = "SourceOfTruthUseOutputTimeTagCondition";

        /* Output Cache Related */
        public const string UseSyncOutputCache = "UseSyncOutputCache";
        public const string DeleteCacheAfterETagConflict = "DeleteCacheAfterETagConflict";
        public const string UseOutputTimeStampInCache = "UseOutputTimeStampInCache";

        /* Raw Channel Payload Related */
        public const string MaxBatchedChildBeforeMoveToRetryQueue = "MaxBatchedChildBeforeMoveToRetryQueue";

        /* Partner Blob Client Related */
        public const string PartnerBlobNonRetryableCodes = "PartnerBlobNonRetryableCodes";

        /* Channel Related */
        public const string GlobalConcurrency = "GlobalConcurrency";
        public const string RawInputChannelConcurrency = "RawInputChannelConcurrency";
        public const string InputChannelConcurrency = "InputChannelConcurrency";
        public const string InputCacheChannelConcurrency = "InputCacheChannelConcurrency";
        public const string PartnerChannelConcurrency = "PartnerChannelConcurrency";
        public const string SourceOfTruthChannelConcurrency = "SourceOfTruthChannelConcurrency";
        public const string OutputCacheChannelConcurrency = "OutputCacheChannelConcurrency";
        public const string BufferedChannelPartitionKeySuffix = "PartitionKey";

        #region EventHub Properties

        /*
         * This is contract between ARN EventHub writer and DataLabs for EventHub Property Tags
         * Below contract string will always be synced with ARN EventHub Writer
         */
        public const string EventHub_Property_EventType = "EventType"; // string
        public const string EventHub_Property_CorrelationId = "CorrelationId"; // string
        public const string EventHub_Property_ResourceId = "ResourceId"; // string 
        public const string EventHub_Property_TenantId = "TenantId"; // string
        public const string EventHub_Property_Compressed = "Compressed"; // bool
        public const string EventHub_Property_hasURL = "HasURL"; // bool
        public const string EventHub_Property_NumResources = "NumResources"; // int
        public const string EventHub_Property_EventTime = "EventTime"; // long, unixMilliSecond timestamp
        public const string EventHub_Property_ResourceLocation = "ResourceLocation"; // string

        // This is internally used to avoid unnecessary deserialization for empty tenantId
        // When we get single inline notification with EventHub annotatnion,
        // if there is no or empty tenantId annotation with valid resourceId,
        // we assume that there is really no tenantId in the resource and then this is used to mark it
        // Same thing will be applied to ResourceLocation
        public const string EmptyField = "empty";
        #endregion

        #region Activity Tag

        public const string EventHub_Property_Log_Prefix = "EH_Property_";
        public const string EventHub_Property_DataFormat_Log = EventHub_Property_Log_Prefix + SolutionConstants.DataFormat;
        public const string EventHub_Property_EventType_Log = EventHub_Property_Log_Prefix + EventHub_Property_EventType;
        public const string EventHub_Property_CorrelationId_Log = EventHub_Property_Log_Prefix + EventHub_Property_CorrelationId;
        public const string EventHub_Property_ResourceId_Log = EventHub_Property_Log_Prefix + EventHub_Property_ResourceId;
        public const string EventHub_Property_TenantId_Log = EventHub_Property_Log_Prefix + EventHub_Property_TenantId;
        public const string EventHub_Property_EventTime_Log = EventHub_Property_Log_Prefix + EventHub_Property_EventTime;
        public const string EventHub_Property_ResourceLocation_Log = EventHub_Property_Log_Prefix + EventHub_Property_ResourceLocation;
        public const string EventHub_Property_NumResources_Log = EventHub_Property_Log_Prefix + EventHub_Property_NumResources;
        public const string EventHub_Property_hasURL_Log = EventHub_Property_Log_Prefix + EventHub_Property_hasURL;
        public const string EventHub_Property_Compressed_Log = EventHub_Property_Log_Prefix + EventHub_Property_Compressed;

        public const string ServiceBus_Property_Log_Prefix = "SB_Property_";
        public const string ServiceBus_Property_DataFormat_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.DataFormat;
        public const string ServiceBus_Property_ActivityId_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.ActivityId;
        public const string ServiceBus_Property_ParentTraceId_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.ParentTraceId;
        public const string ServiceBus_Property_CorrelationId_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.CorrelationId;
        public const string ServiceBus_Property_EventType_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.EventType;
        public const string ServiceBus_Property_ResourceId_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.ResourceId;
        public const string ServiceBus_Property_TenantId_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.TenantId;
        public const string ServiceBus_Property_EventTime_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.EventTime;
        public const string ServiceBus_Property_TopActivityStartTime_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.TopActivityStartTime;
        public const string ServiceBus_Property_RetryCount_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.RetryCount;
        public const string ServiceBus_Property_FirstEnqueuedTime_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.FirstEnqueuedTime;
        public const string ServiceBus_Property_FirstPickedUpTime_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.FirstPickedUpTime;
        public const string ServiceBus_Property_PartnerSpentTime_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.PartnerSpentTime;
        public const string ServiceBus_Property_HasInput_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.HasInput;
        public const string ServiceBus_Property_HasOutput_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.HasOutput;
        public const string ServiceBus_Property_HasSourceOfTruthConflict_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.HasSourceOfTruthConflict;
        public const string ServiceBus_Property_HasSuccessInputCacheWrite_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.HasSuccessInputCacheWrite;
        public const string ServiceBus_Property_SingleInlineResource_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.SingleInlineResource;
        public const string ServiceBus_Property_Compressed_Log = ServiceBus_Property_Log_Prefix + SolutionConstants.Compressed;
        public const string ServiceBus_Property_RegionName = ServiceBus_Property_Log_Prefix + SolutionConstants.RegionName;

        public const string E2EDuration = "E2EDuration";
        public const string InputSize = "InputSize";

        public const string DelayFromEventTimeToInputEnqueue = "DelayFromEventTimeToInputEnqueue";
        public const string DelayFromInputEnqueueToPickup = "DelayFromInputEnqueueToPickup";
        public const string DelayFromInputEnqueueToStart = "DelayFromInputEnqueueToStart";
        public const string StartDelay = "StartDelay";

        public const string EventHubWriteSuccess = "EventHubWriteSuccess";
        public const string ArnPublishSuccess = "ArnPublishSuccess";
        public const string BlobPayloadRoutingSuccess = "BlobPayloadRoutingSuccess";
        public const string RetryQueueWriteSuccess = "RetryQueueWriteSuccess";
        public const string PoisonQueueWriteSuccess = "PoisonQueueWriteSuccess";

        public const string EventHubBatchSize = "EventHubBatchSize";
        public const string ArnPublishBatchSize = "ArnPublishBatchSize";
        public const string BlobPayloadRoutingBatchSize = "BlobPayloadRoutingBatchSize";
        public const string RetryQueueBatchSize = "RetryQueueBatchSize";
        public const string PoisonQueueBatchSize = "PoisonQueueBatchSize";

        public const string EventHubWriteFailedCountInSameBatch = "EventHubWriteFailedCountInSameBatch";
        public const string EventHubWriteDuration = "EventHubWriteDuration";
        public const string ArnPublishDuration = "ArnPublishDuration";
        public const string BlobPayloadRoutingDuration = "BlobPayloadRoutingDuration";
        public const string RetryQueueFailedCountInSameBatch = "RetryQueueFailedCountInSameBatch";
        public const string RetryQueueWriteDuration = "RetryQueueWriteDuration";
        public const string PoisonQueueFailedCountInSameBatch = "PoisonQueueFailedCountInSameBatch";
        public const string PoisonQueueWriteDuration = "PoisonQueueWriteDuration";
        public const string PoisonInsideRetry = "PoisonInsideRetry";

        public const string OutputTimeStamp = "OutputTimeStamp";
        public const string OutputCacheCommand = "OutputCacheCommand";

        public const string ServiceBusHasInputTag = "ServiceBusHasInputTag";
        public const string ServiceBusHasOutputTag = "ServiceBusHasOutputTag";
        public const string ServiceBusHasRawInput = "ServiceBusHasRawInput";
        public const string ServiceBusHasSingleInput = "ServiceBusHasSingleInput";
        public const string ServiceBusHasOutput = "ServiceBusHasOutput";
        public const string ServiceBusEntryChannel = "ServiceBusEntryChannel";
        public const string ServiceBusHasPartnerChannel = "ServiceBusHasPartnerChannel";
        public const string ServiceBusPartnerChannelName = "ServiceBusPartnerChannelName";
        public const string RetrySourceOfTruthConflictTag = "RetrySourceOfTruthConflictTag";
        public const string RetrySuccessInputCacheWriteTag = "RetrySuccessInputCacheWriteTag";

        public const string PoisonInsidePoison = "PoisonInsidePoison";

        public const string TotalChild = "TotalChild";
        public const string TotalChildSuccess = "TotalChildSuccess";
        public const string TotalChildMovedToRetry = "TotalChildMovedToRetry";
        public const string TotalChildMovedToPoison = "TotalChildMovedToPoison";
        public const string TotalChildDropped = "TotalChildDropped";
        public const string TotalChildCancelled = "TotalChildCancelled";
        public const string TotalChildTaskError = "TotalChildTaskError";
        public const string TotalChildTimeout = "TotalChildTimeout";
        public const string AllChildTaskCancalled = "AllChildTaskCancalled";

        public const string AllChildTasksCancelReason = "AllChildTasksCancelReason";
        public const string AllChildTasksCancelReasonDetail = "AllChildTasksCancelReasonDetail";
        public const string SubJobCreationTime = "SubJobCreationTime";

        #endregion

        #region Event Names

        public const string EventName_InputCacheInsert = "InputCacheInsert";
        public const string EventName_FailedInputCacheInsert = "FailedInputCacheInsert";
        public const string EventName_OutputCacheInsert = "OutputCacheInsert";
        public const string EventName_OutputCacheDelete = "OutputCacheDelete";

        public const string EventName_TaskMovingToRetry = "MovingToRetry";
        public const string EventName_TaskMovedToRetry = "MovedToRetry";
        public const string EventName_TaskFailedToMoveToRetry = "FailedToMoveToRetry";

        public const string EventName_TaskMovingToPoison = "MovingToPoison";
        public const string EventName_TaskMovedToPoison = "MovedToPoison";
        public const string EventName_TaskFailedToMoveToPoison = "FailedToMoveToPoison";

        public const string EventName_OutputMessageAdded = "OutputMessageAdded";
        public const string EventName_BlobSourceOfTruthUploaded = "BlobSourceOfTruthUploaded";
        public const string EventName_BlobSourceOfTruthEtagConflict = "BlobSourceOfTruthEtagConflict";
        public const string EventName_BlobSourceOfTruthOutputTimeStampConflict = "BlobSourceOfTruthOutputTimeStampConflict";

        #endregion

        #region Retry PropertyTag

        public static IDictionary<string, object> DisableDeadLetterMarkDictionary =
            new Dictionary<string, object>()
            {
                { PropertyTag_DeadLetterMark, false }
            }.AsReadOnly();

        public const string PropertyTag_DeadLetterMark = "DQ_";

        /* Arranged in alphabetical order for the property tags in the retry queue, grouped by starting alphabet */
        public const string PropertyTag_ActivityId = "AI";
        public const string PropertyTag_ChannelType = "CT";

        public const string PropertyTag_FailedReason = "FR";
        public const string PropertyTag_FailedDescription = "FD";
        public const string PropertyTag_First_EnqueuedTime = "FE";
        public const string PropertyTag_First_PickedUpTime = "FP";

        public const string PropertyTag_HasInput = "HI";
        public const string PropertyTag_HasOutput = "HO";

        public const string PropertyTag_Input_CorrrelationId = "IC";
        public const string PropertyTag_Input_Tenant_Id = "ID";
        public const string PropertyTag_Input_EventType = "IE";
        public const string PropertyTag_Input_Resource_Id = "II";
        public const string PropertyTag_Input_ResourceLocation = "IL";
        public const string PropertyTag_Input_HasCompressed = "IM";
        public const string PropertyTag_Input_EventTime = "IT";

        public const string PropertyTag_Output_CorrrelationId = "OC";
        public const string PropertyTag_Output_Tenant_Id = "OD";
        public const string PropertyTag_Output_EventType = "OE";
        public const string PropertyTag_Output_ResourceFormat = "OF";
        public const string PropertyTag_Output_ETag = "OG";
        public const string PropertyTag_Output_ResourceLocation = "OL";
        public const string PropertyTag_Output_Resource_Id = "OR";
        public const string PropertyTag_Output_TimeStamp = "OT";

        public const string PropertyTag_PartnerChannelName = "PC";
        public const string PropertyTag_ParentTraceId = "PT";
        public const string PropertyTag_Partner_SpentTime = "PS";

        public const string PropertyTag_RetryCount = "RC";
        public const string PropertyTag_RetryDelay = "RD";
        public const string PropertyTag_RegionName = "RN";
        public const string PropertyTag_RespProperties = "RP";

        public const string PropertyTag_SingleResource = "SR";
        public const string PropertyTag_SourceOfTruthConflict = "SC";
        public const string PropertyTag_SuccessInputCacheWrite = "SI";

        public const string PropertyTag_TopActivityStartTime = "TS";
        /*--------------------------------------------------------------*/

        #endregion

        #region Arn Publish

        public const string PublishOutputToArn = "PublishOutputToArn";
        public const string ArnPublishWriteFailRetryDelayInMsec = "ArnPublishWriteFailRetryDelayInMsec";

        #endregion

        #region Blob Payload Routing

        public const string EnableBlobPayloadRouting = "EnableBlobPayloadRouting";
        public const string BlobPayloadRoutingTypes = "BlobPayloadRoutingTypes";
        public const string ArnRoutingLocation = "arn.routing.location";

        #endregion

        public const string DropPoisonMessage = "DropPoisonMessage";

        /// <summary>
        /// Traffic tuner rule key for IO config map.
        /// </summary>
        public const string TrafficTunerRuleKey = "TrafficTunerRule";
        public const string PartnerTrafficTunerRuleKey = "PartnerTrafficTunerRule";
        public const string InputTrafficTunerCounterName = "InputTrafficTuner";
        public const string PartnerTrafficTunerCounterName = "PartnerTrafficTuner";
        public const string BackupProviderInputTrafficTunerRuleKey = "BackupProviderInputTrafficTunerRule";
        public const string BackupProviderPartnerTrafficTunerRuleKey = "BackupProviderPartnerTrafficTunerRule";
        public const string BackupProviderInputTrafficTunerCounterName = "BackupProviderInputTrafficTuner";
        public const string BackupProviderPartnerTrafficTunerCounterName = "BackupProviderPartnerTrafficTuner";
    }
}