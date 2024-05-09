// <copyright file="SolutionConstants.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants
{
    public static class SolutionConstants
    {
        public const string ActivityId = "ActivityId";
        public const string ParentTraceId = "ParentTraceId";
        public const string ChildTraceIds = "ChildTraceIds";
        public const string TraceId = "TraceId";
        public const string DataFormat = "DataFormat";
        public const string DataTimeStamp = "DataTimeStamp";
        public const string InsertionTimeStamp = "InsertionTimeStamp";
        public const string NumResources = "NumResources";
        public const string InlinePayload = "InlinePayload";
        public const string NextChannel = "NextChannel";
        public const string TimeStamp = "TimeStamp";
        public const string TopActivityStartTime = "TopActivityStartTime";

        public const string HasDeserializedObject = "HasDeserializedObject";
        public const string EventId = "EventId";
        public const string Topic = "Topic";
        public const string Subject = "Subject";
        public const string InputEventTime = "InputEventTime";
        public const string EventTime = "EventTime";
        public const string EventType = "EventType";
        public const string Action = "Action";
        public const string ResourceLocation = "ResourceLocation";
        public const string PublisherInfo = "PublisherInfo";
        public const string HomeTenantId = "HomeTenantId";
        public const string ResourceHomeTenantId = "ResourceHomeTenantId";
        public const string ApiVersion = "ApiVersion";
        public const string DataBoundary = "DataBoundary";

        public const string CorrelationId = "CorrelationId";
        public const string ResourceId = "ResourceId";
        public const string CollidingResourceId = "CollidingResourceId";
        public const string Compressed = "Compressed";
        public const string ChannelType = "ChannelType";
        public const string OutputCorrelationId = "OutputCorrelationId";
        public const string InputTenantId = "InputTenantId";
        public const string InputEventType = "InputEventType";
        public const string InputResourceId = "InputResourceId";
        public const string InputResourceType = "InputResourceType";
        public const string OutputResourceId = "OutputResourceId";
        public const string OutputResourceType = "OutputResourceType";
        public const string OutputEventType = "OutputEventType";
        public const string OutputTimeStamp = "OutputTimeStamp";
        public const string OutputTenantId = "OutputTenantId";
        public const string OutputBlobUploadDuration = "OutputBlobUploadDuration";
        public const string OutputBlobDownloadDuration = "OutputBlobDownloadDuration";
        public const string OutputBlobNotFound = "OutputBlobNotFound";
        public const string TimeSinceCreation = "TimeSinceCreation";
        public const string NumOfEventGridNotifications = "NumOfEventGridNotifications";

        public const string Success = "Success";
        public const string Fail = "Fail";
        public const string TenantId = "TenantId";
        public const string SubscriptionId = "SubscriptionId";
        public const string Provider = "Provider";

        public const string DataSourceType = "DataSourceType";
        public const string DataSourceName = "DataSourceName";
        public const string DataEnqueuedTime = "DataEnqueuedTime";
        public const string FirstEnqueuedTime = "FirstEnqueuedTime";
        public const string NumEventGridEvents = "NumEventGridEvents";
        public const string FirstPickedUpTime = "FirstPickedUpTime";

        public const string PartnerInputCorrelationId = "PartnerInputCorrelationId";
        public const string PartnerTraceId = "PartnerTraceId";
        public const string PartnerRetryCount = "PartnerRetryCount";
        public const string PartnerReqSize = "PartnerReqSize";
        public const string PartnerOutputSize = "PartnerOutputSize";
        public const string PartnerInterfaceStartTime = "PartnerInterfaceStartTime";
        public const string PartnerRoundTrip = "PartnerRoundTrip";
        public const string PartnerRoundSuccess = "PartnerRoundSuccess";
        public const string PartnerSingleSuccessResponse = "PartnerSingleSuccessResponse";
        public const string PartnerSingleErrorResponse = "PartnerSingleErrorResponse";
        public const string PartnerSingleResponse = "PartnerSingleResponse";
        public const string PartnerResponseStream = "PartnerResponseStream";
        public const string PartnerSingleStreamResponse = "PartnerSingleStreamResponse";
        public const string PartnerMultiStreamResponses = "PartnerMultiStreamResponses";
        public const string PartnerTotalResponse = "PartnerTotalResponse";
        public const string PartnerSpentTime = "PartnerSpentTime";
        public const string EmptyOutput = "EmptyOutput";
        public const string TooLargeFailMessageSize = "TooLargeFailMessageSize";

        public const string HasEventHubARNRawInputMessage = "HasEventHubARNRawInputMessage";
        public const string MultiNotifications = "MultiNotifications";
        public const string MultiResources = "MultiResources";
        public const string HasBlobURI = "HasBlobURI";
        public const string TotalResourcesInRawInput = "TotalResourcesInRawInput";
        public const string TotalFilteredChildTasks = "TotalFilteredChildTasks";
        public const string TotalMovedToRetryChildTasks = "TotalMovedToRetryChildTasks";
        public const string TotalMovedToBlobPayloadRoutingChildTasks = "TotalMovedToBlobPayloadRoutingChildTasks";
        public const string TotalInputChannelChildTasks = "TotalInputChannelChildTasks";
        public const string TotalCreatedChildTasks = "TotalCreatedChildTasks";

        public const string IsBlobPayload = "IsBlobPayload";
        public const string EnableBlobPayloadRouting = "EnableBlobPayloadRouting";
        public const string BlobPayloadRoutingTypeMatch = "BlobPayloadRoutingTypeMatch";
        public const string ResourceRoutingLocation = "ResourceRoutingLocation";
        public const string NotificationRoutingLocation = "NotificationRoutingLocation";
        public const string MoveToBlobPayloadRoutingChannel = "MoveToBlobPayloadRoutingChannel";

        public const string RetryPropertyCount = "RetryPropertyCount";
        public const string RetryCount = "RetryCount";
        public const string ClientRequestId = "ClientRequestId";
        public const string ClientRequestTime = "ClientRequestTime";
        public const string ServerRecvTime = "ServerRecvTime";
        public const string ClientSendToServerRecvTime = "ClientSendToServerRecvTime";
        public const string ClientSendToServerDoneTime = "ClientSendToServerDoneTime";
        public const string ServerDoneToClientReceiveTime = "ServerDoneToClientReceiveTime";

        public const string PartnerErrorType = "PartnerErrorType";
        public const string PartnerRetryDelay = "PartnerRetryDelay";
        public const string PartnerErrorMessage = "PartnerErrorMessage";
        public const string PartnerErrorCode = "PartnerErrorCode";
        public const string PartnerFailedComponent = "PartnerFailedComponent";

        public const string StreamSuccessResponse = "StreamSuccessResponse";
        public const string StreamErrorResponses = "StreamErrorResponses";
        public const string StreamSameGroupResponses = "StreamSameGroupResponses";
        public const string StreamDifferentGroup = "StreamDifferentGroup";
        public const string StreamSubJob = "StreamSubJob";
        public const string StreamResourceId = "StreamResourceId";
        public const string StreamCurrGroupId = "StreamCurrGroupId";
        public const string StreamPrevGroupId = "StreamPrevGroupId";

        public const string DecodedResourceId = "DecodedResourceId";
        public const string RegionId = "RegionId";

        public const string RegionName = "RegionName";

        public const string FullyQualifiedNamespace = "FullyQualifiedNamespace";
        public const string EventHubName = "EventHubName";
        public const string EventHubMessageId = "EventHubMessageId";
        public const string PartitionId = "PartitionId";
        public const string EventHubSequenceNumber = "EventHubSequenceNumber";
        public const string EventHubOffset = "EventHubOffset";
        public const string DefaultStartingPosition = "DefaultStartingPosition";
        public const string LastEnqueuedSequenceNumber = "LastEnqueuedSequenceNumber";
        public const string CloseReason = "CloseReason";
        public const string Operation = "Operation";
        public const string Exception = "Exception";
        public const string Message = "Message";

        public const string QueueName = "QueueName";
        public const string ServiceBusSequenceNumber = "ServiceBusSequenceNumber";
        public const string ServiceBusMessageId = "ServiceBusMessageId";
        public const string DeliveryCount = "DeliveryCount";
        public const string LockedUntil = "LockedUntil";
        public const string ExpiresAt = "ExpiresAt";
        public const string AccessedAt = "AccessedAt";
        public const string TotalMessageCount = "TotalMessageCount";
        public const string ActiveMessageCount = "ActiveMessageCount";
        public const string ScheduledMessageCount = "ScheduledMessageCount";
        public const string DeadLetterMessageCount = "DeadLetterMessageCount";
        public const string DeadLetterReason = "DeadLetterReason";

        public const string ActivityEventListColumn = "EventList";
        public const string ExceptionColumn = Exception;
        public const string ExceptionEventNameColumn = "Exception.Event";
        public const string OtherExceptionColumnPrefix = "OtherException";
        public const string ContinuableExceptionColumn = "ContinuableException";
        public const string ContinuableExceptionEventNameColumn = "ContinuableException.Event";

        public const string AttributeExceptionType = "exception.type";
        public const string AttributeExceptionStacktrace = "exception.stacktrace";
        public const string AttributeExceptionMessage = "exception.message";
        public const string AttributeExceptionEventName = "exception.eventname";

        public const string ConfigMapRefreshDuration = "ConfigMapRefreshDuration";
        public const string SecretProviderRefreshDuration = "SecretProviderRefreshDuration";

        public const string StorageSuffix = "StorageSuffix";
        public const string HttpStatusCode = "HttpStatusCode";
        public const string Request = "Request";

        // Output Blob Upload
        public const string ETag = "ETag";
        public const string SourceOfTruthETagConflict = "SourceOfTruthETagConflict";
        public const string OutputTimeStampConflict = "OutputTimeStampConflict";
        public const string OutputContainerURI = "OutputContainerURI";
        public const string OutputBlobName = "OutputBlobName";
        public const string CacheUpdateFromETagConflict = "CacheUpdateFromETagConflict";
        public const string ConflictReason = "ConflictReason";
        public const string EtagConflict = "EtagConflict";
        public const string TimeStampConflict = "TimeStampConflict";

        // BlobURL
        public const string BlobURI = "BlobURI";
        public const string BlobSize = "BlobSize";
        public const string NOBlobURI = "NOBlobURI";
        public const string EmptyBlobResponse = "EmptyBlobResponse";

        // EventHub Writer
        public const string EventHubWriterMaxRetry = "EventHubWriterMaxRetry";
        public const string EventHubWriterDelayInMsec = "EventHubWriterDelayInMsec";
        public const string EventHubWriterMaxDelayPerAttempInSec = "EventHubWriterMaxDelayPerAttempInSec";
        public const string EventHubWriterMaxTimePerAttempInSec = "EventHubWriterMaxTimePerAttempInSec";
        public const string EventHubConnectionRefreshDuration = "EventHubConnectionRefreshDuration";
        public const string EventHubLogPropertyCallTimeoutInSec = "EventHubLogPropertyCallTimeoutInSec";

        // ServiceBus Writer
        public const string ServiceBusWriterDelayInMsec = "ServiceBusWriterDelayInMsec";
        public const string ServiceBusWriterMaxRetry = "ServiceBusWriterMaxRetry";
        public const string ServiceBusDLQMaxProcessCount = "ServiceBusDLQMaxProcessCount";
        public const string ServiceBusWriterMaxDelayPerAttempInSec = "ServiceBusWriterMaxDelayPerAttempInSec";
        public const string ServiceBusWriterMaxTimePerAttempInSec = "ServiceBusWriterMaxTimePerAttempInSec";
        public const string ServiceBusConnectionRefreshDuration = "ServiceBusConnectionRefreshDuration";
        public const string QueueOperationTimeOut = "QueueOperationTimeOut";

        public const string PropertyTag_ChannelType = "CT";
        public const string PropertyTag_Input_EventType = "IE";
        public const string PropertyTag_Output_EventType = "OE";

        // BufferedEventWriter related
        public const string EventHubPrefix = "EventHub";
        public const string RetryQueuePrefix = "RetryQueue";
        public const string PoisonQueuePrefix = "PoisonQueue";
        public const string SubJobQueuePrefix = "SubJobQueue";
        public const string EventBatchWriterConcurrencySuffix = "BatchWriterConcurrency";
        public const string EventBatchMaxSizeSuffix = "BatchMaxSize";
        public const string EventBatchWriterTimeOutInSecSuffix = "BatchWriterTimeOutInSec";
        public const string BatchCount = "BatchCount";
        public const string SizeInBytes = "SizeInBytes";
        public const string SizeInBytesInBatch = "SizeInBytesInBatch";
        public const string MaxAllowedSizeInBytes = "MaxAllowedSizeInBytes";
        public const string WriterName = "WriterName";
        public const string NumBatchWrites = "NumBatchWrites";
        public const string NumWriterRetry = "NumWriterRetry";
        public const string PairedRegionWrite = "PairedRegionWrite";

        // Concurrency related
        public const string TaskChannelConcurrencySuffix = "Concurrency";
        public const string TaskChannelConcurrencyWaitTimeoutInSec = "ConcurrencyWaitTimeoutInSec";
        public const string TaskChannelConcurrencyWaitTimedOutSuffix = "ConcurrencyWaitTimedOut";

        // TaskChannel related
        public const string EventName_TaskCreated = "TaskCreated";
        public const string EventName_TaskStarted = "TaskStarted";
        public const string EventName_TaskStartFailed = "TaskStartFailed";
        public const string EventName_TaskSuccess = "TaskSuccess";
        public const string EventName_TaskTimeouted = "TaskTimeouted";
        public const string EventName_TaskDropped = "TaskDropped";
        public const string EventName_TaskError = "TaskError";
        public const string EventName_AllChildTaskCancelled = "AllChildTaskCancelled";
        public const string EventName_TooLargeMessage = "TooLargeMessage";


        public const string TaskFinalStage = "FinalStage";
        public const string TaskFinalStatus = "FinalStatus";
        public const string TaskChannelBeforeSuccess = "TaskChannelBeforeSuccess";
        public const string PartnerResponseFlags = "PartnerResponseFlags";
        public const string TaskFailedComponent = "TaskFailedComponent";
        public const string TaskFailedReason = "FailedReason";
        public const string TaskFailedDescription = "FailedDescription";

        public const string TrafficTunerResult = "TrafficTunerResult";
        public const string PartnerTrafficTunerResult = "PartnerTrafficTunerResult";
        public const string TaskFiltered = "TaskFiltered";
        public const string TaskCancelled = "TaskCancelled";
        public const string ChannelName = "ChannelName";
        public const string MethodName = "MethodName";

        public const string BufferedChannelNumQueueSuffix = "NumBufferQueue";
        public const string BufferedChannelQueueLengthSuffix = "BufferQueueLength";
        public const string BufferedChannelDelaySuffix = "BufferDelay";
        public const string BufferedChannelMaxBufferedSizeSuffix = "MaxBufferedSize";

        public const string InvalidInInputCache = "InvalidInInputCache";
        public const string AddedToInputCache = "AddedToInputCache";
        public const string HasInputCacheException = "HasInputCacheException";
        public const string HasCacheClientWriteException = "HasCacheClientWriteException";
        public const string IsTimeoutExpired = "IsTimeoutExpired";
        public const string IsCancelled = "IsCancelled";
        public const string ParentTaskTimeoutExpired = "ParentTaskTimeoutExpired";
        public const string ParentTaskCancelled = "ParentTaskCancelled";
        public const string ParentTaskCancellationTokenSet = "ParentTaskCancellationTokenSet";

        // Output DataSet related
        public const string OutputDataset = "OutputDataset";

        // Output EventHub Propert Names
        public const string OutputEventHubDataSetName = "DataSet";

        // RetryPolicy
        public const string ChainedRetryBackOffInfo = "ChainedRetryBackOffInfo";

        // PartnerBlobClient
        public const string PartnerBlobClientOption = "PartnerBlobClientOption";
        public const string PartnerBlobCallMaxTimeOutInSec = "PartnerBlobCallMaxTimeOutInSec";
        public const string PartnerBlobURI = "PartnerBlobURI";

        // Blob Storage
        public const string BlobStorageAccountNames = "BlobStorageAccountNames";
        public const string BackupBlobStorageAccountNames = "BackupBlobStorageAccountNames";
        public const string BlobStorageLogsEnabled = "BlobStorageLogsEnabled";
        public const string BlobStorageTraceEnabled = "BlobStorageTraceEnabled";
        public const string UseSourceOfTruth = "UseSourceOfTruth";
        public const string MaxConcurrentBlobContainerCreation = "MaxConcurrentBlobContainerCreation";

        // OutputBlobClient
        public const string OutputBlobUploadMaxTimeOutInSec = "OutputBlobUploadMaxTimeOutInSec";
        public const string OutputBlobDownloadMaxTimeOutInSec = "OutputBlobDownloadMaxTimeOutInSec";

        //Region Pair
        public const string PrimaryRegionName = "PrimaryRegionName";
        public const string BackupRegionName = "BackupRegionName";
        public const string UsingDefaultRegionName = "UsingDefaultRegionName";

        /* Cache TTL Related */
        public const string DefaultInputCacheTTL = "DefaultInputCacheTTL";
        public const string DefaultOutputCacheTTL = "DefaultOutputCacheTTL";
        public const string DefaultNotFoundEntryCacheTTL = "DefaultNotFoundEntryCacheTTL";
        public const string ResourceTypeCacheTTLMappings = "ResourceTypeCacheTTLMappings";
        public const string ReadTTL = "ReadTTL";
        public const string ResourceCacheDataFormat = "ResourceCacheDataFormat";
        public const string InsertionTime = "InsertionTime";
        public const string ElapsedTime = "ElapsedTime";

        // ENV
        public const string LOGGER_MIN_LOG_LEVEL = "LOGGER_MIN_LOG_LEVEL";
        public const string HOST_IP = "HOST_IP";
        public const string POD_IP = "POD_IP";
        public const string DOTNET_RUNNING_IN_CONTAINER = "DOTNET_RUNNING_IN_CONTAINER";
        public const string CONFIGMAP_DIR = "CONFIGMAP_DIR";
        public const string SECRETS_STORE_DIR = "SECRETS_STORE_DIR";
        public const string CLUSTER_NAME = "CLUSTER_NAME";

        // Local Test Constants
        public const string UseTestMemoryWriter = "UseTestMemoryWriter";
        public const string NumTestMemoryWriter = "NumTestMemoryWriter";

        // Initialization
        public const string InitializeServiceTimeoutInSec = "InitializeServiceTimeoutInSec";
        public const string InitializeServiceRandomDelayInSec = "InitializeServiceRandomDelayInSec";

        // Resource Proxy related
        public const string ResourceFetcherProxyService = "ResourceFetcherProxyService";
        public const string UseCacheInResourceFetcherProxy = "UseCacheInResourceFetcherProxy";
        public const string ResourceFetcherProxyMaxTimeOutInSec = "ResourceFetcherProxyMaxTimeOutInSec";

        public const string ResourceProxyAddr = "ResourceProxyAddr";
        public const string ResourceProxyHostPort = "ResourceProxyHostPort";
        public const string ResourceProxyDefaultPort = "5073";
        public const string ResourceProxyGrpcOption = "ResourceProxyGrpcOption";
        public const string ResourceProxyCallMaxTimeOutInSec = "ResourceProxyCallMaxTimeOutInSec";

        public const string ExpiredCacheEntry = "ExpiredCacheEntry";
        public const string AddNotFoundEntryToCache = "AddNotFoundEntryToCache";

        public const string HasARMRemainingReadHeader = "HasARMRemainingReadHeader";
        public const string SubscriptionARMReadRemaining = "SubscriptionARMReadRemaining";
        public const string SubscriptionARMReadSafeLimit = "SubscriptionARMReadSafeLimit";
        public const string SubscriptionARMReadSafeLimit_BackoffMilliseconds = "SubscriptionARMReadSafeLimit_BackoffMilliseconds";
        public const string SubscriptionThrottledAddedToCache = "SubscriptionThrottledAddedToCache";
        public const string SubscriptionThrottledAddedTimeStamp = "SubscriptionThrottledAddedTimeStamp";
        public const string SubscriptionThrottledRemainingRetryMilliSecs = "SubscriptionThrottledRemainingRetryMilliSecs";

        // Parter Solution related
        public const string PartnerSolutionGrpcOption = "PartnerSolutionGrpcOption";
        public const string PartnerUseMultiResponses = "PartnerUseMultiResponses";
        public const string PartnerSingleResponseResourcesRouting = "PartnerSingleResponseResourcesRouting";
        public const string PartnerMultiResponseResourcesRouting = "PartnerMultiResponseResourcesRouting";
        public const string PartnerSolutionDefaultPort = "5072";
        public const string PartnerStreamingInputRetryDelayMS = "PartnerStreamingInputRetryDelayMS";
        public const string PartnerStreamingThresholdForRetry = "PartnerStreamingThresholdForRetry";
        public const string PartnerStreamingTaskTimeOutInSec = "PartnerStreamingTaskTimeOutInSec";

        // Object type related
        public const string ProvidersPath = "/providers/";
        public const char Comma = ',';
        public const char SemiColon = ';';

        // Certificate related
        public const string CertificateHeader = "-----BEGIN CERTIFICATE-----";
        public const string CertificateFooter = "-----END CERTIFICATE-----";
        public const string PrivateKeyHeader = "-----BEGIN PRIVATE KEY-----";
        public const string PrivateKeyFooter = "-----END PRIVATE KEY-----";

        // Monitoring related
        public const string OTLP_EXPORTER_TYPE = "OTLP_EXPORTER_TYPE";
        public const string MDSD_PARTNER_ENDPOINT = "MDSD_PARTNER_ENDPOINT";
        public const string MDSD_DATALABS_ENDPOINT = "MDSD_DATALABS_ENDPOINT";
        public const string MDM_DATALABS_ENDPOINT = "MDM_DATALABS_ENDPOINT";
        public const string MDM_PARTNER_ENDPOINT = "MDM_PARTNER_ENDPOINT";
        public const string MDM_CUSTOMER_ENDPOINT = "MDM_CUSTOMER_ENDPOINT";
        public const string IS_INTERNAL_PARTNER = "IS_INTERNAL_PARTNER";
        public const string IS_DEDICATED_PARTNER_AKS = "IS_DEDICATED_PARTNER_AKS";

        public const string NODE_NAME = "NODE_NAME";
        public const string POD_NAME = "POD_NAME";
        public const string SERVICE = "service";
        public const string SCALE_UNIT = "scaleUnit";
        public const string REGION = "region";
        public const string BUILD_VERSION = "buildVersion";

        public const string EnableGrpcTrace = "EnableGrpcTrace";
        public const string EnableHttpClientTrace = "EnableHttpClientTrace";
        public const string EnableAzureSDKActivity = "EnableAzureSDKActivity";
        public const string IncludeRunTimeMetrics = "IncludeRunTimeMetrics";
        public const string IncludeHttpClientMetrics = "IncludeHttpClientMetrics";
        public const string HistogramUnitMS = "ms";

        // CacheClient related
        public const string PARTNER_CACHE_PREFIX = "PARTNER_";
        public const string Score = "Score";
        public const string MinScore = "MinScore";
        public const string MaxScore = "MaxScore";
        public const string Member = "Member";
        public const string RangeStart = "RangeStart";
        public const string RangeEnd = "RangeEnd";
        public const string PrefixBytesToRemove = "PrefixBytesToRemove";
        public const string CacheKey = "CacheKey";
        public const string CacheResult = "CacheResult";
        public const string CacheRawResult = "CacheRawResult";
        public const string CacheExpiry = "CacheExpiry";
        public const string CacheExpiryResult = "CacheExpiryResult";
        public const string CacheCommandElapsed = "CacheCommandElapsed";
        public const string CacheExpiryCommandElapsed = "CacheExpiryCommandElapsed";
        public const string EndPoint = "EndPoint";
        public const string CacheConnectionCreated = "CacheConnectionCreated";
        public const string CacheMaxRetryToReplicas = "CacheMaxRetryToReplicas";
        public const string CacheMGetMaxBatchSize = "CacheMGetMaxBatchSize";
        public const string CacheReadQuorum = "CacheReadQuorum";
        public const string CacheNodeName = "CacheNodeName";
        public const string NumOfKeys = "NumOfKeys";
        public const string NumBatches = "NumBatches";

        public const string CacheNumPools = "CacheNumPools";
        public const string CachePoolNodeReplicationMappingPrefix = "CachePoolNodeReplicationMapping";
        public const string CachePoolDomain = "CachePoolDomain";
        public const string CachePoolPrefix = "CachePool";
        public const string CachePoolConnectionsOptionPrefix = "CachePoolConnectionsOption";

        // OutputCache Related
        public const string UseOutputCache = "UseOutputCache";
        public const string UseHashForResourceCacheKey = "UseHashForResourceCacheKey";
        public const string UseIOCacheAsPartnerCache = "UseIOCacheAsPartnerCache";
        public const string ResourceCacheReadQuorum = "ResourceCacheReadQuorum";

        // InputCache Related
        public const string TolerateInputCacheFailure = "TolerateInputCacheFailure";  // even if input cache fails, message continues to move forward to partner channel

        // ARM Safe Limit Surpassed
        public const string SubscriptionRateLimit = "SubscriptionRateLimit";
        public const string SubscriptionARMReadSafeLimitReached = "SubscriptionARMReadSafeLimitReached";

        // Other Constants
        public const string ResponseTime = "ResponseTime";
        public const string ResponseCorrelationId = "ResponseCorrelationId";
        public const string ProxyDataSource = "ProxyDataSource";
        public const string DeleteToNotFound = "DeleteToNotFound";
        public const string HasSuccess = "HasSuccess";
        public const string HasError = "HasError";
        public const string HasInput = "HasInput";
        public const string HasOutput = "HasOutput";
        public const string HasSourceOfTruthConflict = "HasSourceOfTruthConflict";
        public const string HasSuccessInputCacheWrite = "HasSuccessInputCacheWrite";
        public const string SingleInlineResource = "SingleInlineResource";
        public const string ResourceProvidersKey = "ResourceProviders";
        public const string NotFoundEntryExistInCache = "NotFoundEntryExistInCache";

        // Resource fetcher proxy and service monitoring
        public const string NumResourceFound = "NumResourceFound";
        public const string NumResourceNotFound = "NumResourceNotFound";
        public const string ResourceFound = "ResourceFound";
        public const string ResourceSize = "ResourceSize";
        public const string IsFailed = "IsFailed";
        public const string ResourceType = "ResourceType";
        public const string UseResourceGraph = "UseResourceGraph";
        public const string FromPacific = "FromPacific";
        public const string MissingResourceType = "MissingResourceType";
        public const string QueryType = "QueryType";
        public const string ResourceFetcherError = "ResourceFetcherError";
        public const string FetcherType = "FetcherType";
        public const string PartnerName = "PartnerName";
        public const string CallMethod = "CallMethod";
        public const string ErrorType = "ErrorType";
        public const string DataSource = "DataSource";
        public const string ProviderType = "ProviderType";
        public const string URLEncodedResourceId = "URLEncodedResourceId";
        public const string TimeOutValue = "TimeOutValue";
        public const string ManifestProvider = "ManifestProvider";
        public const string ApiExtension = "ApiExtension";
        public const string CacheCalled = "CacheCalled";
        public const string CacheHit = "CacheHit";
        public const string CacheFail = "CacheFail";
        public const string CacheException = "CacheException";
        public const string CacheUpdateSuccess = "CacheUpdateSuccess";
        public const string NotSupportedDataFormat = "NotSupportedDataFormat";
        public const string BlobTypeFetcher = "blob";
        public const string CacheTypeFetcher = "cache";
        public const string HttpVersion = "HttpVersion";
        public const string AuthError = "AuthError";
        public const string UseOutputCacheForRetry = "UseOutputCacheForRetry";
        public const string UseInputCacheForRetry = "UseInputCacheForRetry";
        public const string UseCacheLookupInProxyClient = "UseCacheLookupInProxyClient";
        public const string RequestURI = "RequestURI";
        public const string RequestURIAfterPacificFail = "RequestURIAfterPacificFail";
        public const string RetryAfterPacific404 = "RetryAfterPacific404";
        public const string Endpoint = "Endpoint";
        public const string ResponseBody = "ResponseBody";
        public const string URIPath = "URIPath";
        public const string QueryParams = "QueryParams";
        public const string AuthResult = "AuthResult";
        public const string ARMRemainingSubscriptionReads = "ARMRemainingSubscriptionReads";
        public const string SourceStatusCode = "SourceStatusCode";
        public const string SourceReasonPhrase = "SourceReasonPhrase";
        public const string SourceHttpVersion = "SourceHttpVersion";
        public const string ResourceProxyClient = "ResourceProxyClient";
        public const string Type = "Type";

        #region Arn Publish

        public const string ArnPublishPrefix = "ArnPublish";
        public const string ArnPublishLocalTesting = "ArnPublishLocalTesting";
        public const string IsArnClientLogDebugEnabled = "IsArnClientLogDebugEnabled";
        public const string IsArnClientLogInfoEnabled = "IsArnClientLogInfoEnabled";
        public const string NotificationReceiverEnvironment = "NotificationReceiverEnvironment";
        public const string ArnPublishStorageAccountNames = "ArnPublishStorageAccountNames";
        public const string ArnPublishMaxBatchSize = "ArnPublishMaxBatchSize";
        public const string NotificationReceiverEndPoint = "NotificationReceiverEndPoint";
        public const string ArnPublishAadAppId = "ArnPublishAadAppId";
        public const string ArnPublishEventGridDomainIds = "ArnPublishEventGridDomainIds";
        public const string ArnPublishEventGridDomainKeys = "ArnPublishEventGridDomainKeys";
        public const string PairedRegionArnPublishEventGridDomainIds = "PairedRegionArnPublishEventGridDomainIds";
        public const string ArnPublishStorageAccountConnectionStrings = "ArnPublishStorageAccountConnectionStrings";
        public const string ArnPublishEventGridDomainEndpoints = "ArnPublishEventGridDomainEndpoints";
        public const string PairedRegionArnPublishEventGridDomainEndpoints = "PairedRegionArnPublishEventGridDomainEndpoints";
        public const string ArnPublishEventGridTopics = "ArnPublishEventGridTopics";
        public const string PairedRegionArnPublishEventGridTopics = "PairedRegionArnPublishEventGridTopics";
        public const string ArnPublisherInfo = "ArnPublisherInfo";
        public const string ArnPublishPercentage = "ArnPublishPercentage";
        public const string NrDstsIsEnabled = "NrDstsIsEnabled";
        public const string NrDstsCertificateName = "NrDstsCertificateName";
        public const string NrDstsClientId = "NrDstsClientId";
        public const string NrDstsClientHome = "NrDstsClientHome";

        #endregion

        // AAD Related
        public const string AADTokenIssuer = "AADTokenIssuer";
        public const string AADTokenAudience = "AADTokenAudience";
        public const string AADAuthority = "AADAuthority";
        public const string DefaultTenantId = "DefaultTenantId";

        // AccessTokenProvider
        public const string AuthHeaderBearerScheme = "Bearer";
        public const string AppIdDimension = "AppId";
        public const string TokenSourceDimension = "TokenSource";
        public const string CacheRefreshReasonDimension = "CacheRefreshReason";
        public const string CrossRegionDimension = "CrossRegion";

        // ARM Client Related
        public const string ARMClientOption = "ARMClientOption";
        public const string ARMEndpoints = "ARMEndpoints";
        public const string ARMBackupEndpoints = "ARMBackupEndpoints";
        public const string ARMFirstPartyAppId = "ARMFirstPartyAppId";
        public const string ARMFirstPartyAppCertName = "ARMFirstPartyAppCertName";
        public const string ARMTokenResource = "ARMTokenResource";
        public const string UseCredentialTokenForArmClient = "UseCredentialTokenForArmClient";

        // Cert Client Related
        public const string CertificateName = "CertificateName";
        public const string OldCertificateThumbprint = "OldCertificateThumbprint";
        public const string NewCertificateThumbprint = "NewCertificateThumbprint";

        // DSTS Client Related
        public const string DstsClientId = "DstsClientId";
        public const string DstsServerId = "DstsServerId";
        public const string DstsClientHome = "DstsClientHome";
        public const string DstsServerHome = "DstsServerHome";
        public const string DstsServerRealm = "DstsServerRealm";

        // Cas Client Related
        public const string CasClientOption = "CasClientOption";
        public const string CasEndpoints = "CasEndpoints";
        public const string CasBackupEndpoints = "CasBackupEndpoints";
        public const string CasDstsCertificateName = "CasDstsCertificateName";
        public const string CasDstsSkipServerCertificateValidation = "CasDstsSkipServerCertificateValidation";
        public const string CasDstsClientId = "CasDstsClientId";
        public const string CasDstsServerId = "CasDstsServerId";
        public const string CasDstsClientHome = "CasDstsClientHome";
        public const string CasDstsServerHome = "CasDstsServerHome";
        public const string CasDstsServerRealm = "CasDstsServerRealm";
        public const string CasClientId = "CasClientId";

        // QFD(Pacific) Client Related
        public const string QfdClientOption = "QfdClientOption";
        public const string QfdEndpoints = "QfdEndpoints";
        public const string QfdBackupEndpoints = "QfdBackupEndpoints";
        public const string QfdDstsCertificateName = "QfdDstsCertificateName";
        public const string QfdDstsSkipServerCertificateValidation = "QfdDstsSkipServerCertificateValidation";
        public const string QfdDstsClientId = "QfdDstsClientId";
        public const string QfdDstsServerId = "QfdDstsServerId";
        public const string QfdDstsClientHome = "QfdDstsClientHome";
        public const string QfdDstsServerHome = "QfdDstsServerHome";
        public const string QfdDstsServerRealm = "QfdDstsServerRealm";

        // ARMAdmin Client Related
        public const string ArmAdminClientOption = "ArmAdminClientOption";
        public const string ArmAdminEndpoints = "ArmAdminEndpoints";
        public const string ArmAdminBackupEndpoints = "ArmAdminBackupEndpoints";
        public const string ArmAdminCertificateName = "ArmAdminCertificateName";

        // ResourceFetcher Client Related
        public const string ResourceFetcherClientOption = "ResourceFetcherClientOption";
        public const string ResourceFetcherEndpoints = "ResourceFetcherEndpoints";
        public const string ResourceFetcherBackupEndpoints = "ResourceFetcherBackupEndpoints";
        public const string ResourceFetcherTokenResource = "ResourceFetcherTokenResource";
        public const string ResourceFetcherHomeTenantId = "ResourceFetcherHomeTenantId";

        // ResourceFetcher Routes

        // ARM Client
        public const string ResourceFetcher_ArmGetResourceRoute = "armclient/getResource";
        public const string ResourceFetcher_ArmGetGenericRestApiRoute = "armclient/getgenericrestapi";

        //QueryFrontDoor Client
        public const string ResourceFetcher_QfdGetPacificResourceRoute = "qfdclient/getpacificresource";
        public const string ResourceFetcher_QfdGetPacificCollectionRoute = "qfdclient/getpacificcollection";
        public const string ResourceFetcher_QfdGetPacificIdMappingsRoute = "qfdclient/getpacificidmappings";

        // ARM Admin Client
        public const string ResourceFetcher_ArmAdminGetManifestConfigRoute = "armadminclient/getManifestconfig";
        public const string ResourceFetcher_ArmAdminGetConfigSpecsRoute = "armadminclient/getconfigspecs";

        // Cas Client
        public const string ResourceFetcher_CASGetCasCapacityCheckRoute = "casclient/getcascapacitycheck";

        // Resource Fetcher Client parameters
        public const string DL_TENANTID = "dl_tenantid";
        public const string DL_RESOURCEID = "dl_resourceid";
        public const string DL_APIVERSION = "dl_apiversion";
        public const string DL_URIPATH = "dl_uripath";
        public const string DL_PARAMETERS = "dl_parameters";
        public const string DL_MANIFESTPROVIDER = "dl_manifestprovider";
        public const string DL_APIEXTENSION = "dl_apiextension";

        // Resource Fetcher Proxy Allowed Types
        public const string GetResourceAllowedTypes = "GetResourceAllowedTypes";
        public const string CallARMGenericRequestAllowedTypes = "CallARMGenericRequestAllowedTypes";
        public const string GetCollectionAllowedTypes = "GetCollectionAllowedTypes";
        public const string GetManifestConfigAllowedTypes = "GetManifestConfigAllowedTypes";
        public const string GetConfigSpecsAllowedTypes = "GetConfigSpecsAllowedTypes";
        public const string GetCasResponseAllowedTypes = "GetCasResponseAllowedTypes";
        public const string GetIdMappingAllowedTypes = "GetIdMappingAllowedTypes";

        // Resource Fetcher Config Map
        public const string PartnerNames = "PartnerNames";
        public const string PartnerClientIdsSuffix = "-ClientIds";
        public const string ArmAllowedResourceTypesSuffix = "-ArmAllowedResourceTypes";
        public const string ArmAllowedGenericURIPathsSuffix = "-ArmAllowedGenericURIPaths";
        public const string QfdAllowedResourceTypesSuffix = "-QfdAllowedResourceTypes";
        public const string ArmAdminAllowedCallsSuffix = "-ArmAdminAllowedCalls";
        public const string CasAllowedCallsSuffix = "-CasAllowedCalls";
        public const string IdMappingAllowedCallsSuffix = "-IdMappingAllowedCalls";
        public const string SigningTokensRefreshDuration = "SigningTokensRefreshDuration";
        public const string SigningTokensFailedRefreshDuration = "SigningTokensFailedRefreshDuration";
        public const string SigningTokensRetrieveTimeOut = "SigningTokensRetrieveTimeOut";
        public const string OpenIdConfigurationURI = "OpenIdConfigurationURI";

        // Resource Fetcher TimeOut Related
        public const string DefaultArmClientGetResourceTimeOutInSec = "DefaultArmClientGetResourceTimeOutInSec";
        public const string DefaultArmClientGenericApiTimeOutInSec = "DefaultArmClientGenericApiTimeOutInSec";
        public const string DefaultArmAdminClientTimeOutInSec = "DefaultArmAdminClientTimeOutInSec";
        public const string DefaultQFDClientTimeOutInSec = "DefaultQFDClientTimeOutInSec";
        public const string DefaultCasClientTimeOutInSec = "DefaultCasClientTimeOutInSec";
        public const string ArmClientResourceTimeOutMappings = "ArmClientResourceTimeOutMappings";
        public const string ArmClientGenericApiTimeOutMappings = "ArmClientGenericApiTimeOutMappings";
        public const string ArmAdminClientTimeOutMappings = "ArmAdminClientTimeOutMappings";
        public const string QfdClientTimeOutMappings = "QfdClientTimeOutMappings";
        public const string CasClientTimeOutMappings = "CasClientTimeOutMappings";

        // Geneva Actions Handler Related
        public const string GenevaActionsHandlerDefaultPort = "Port";
        public const string GenevaActionDstsCertificateName = "GenevaActionDstsCertificateName";
        public const string DstsRealm = "DstsRealm";
        public const string DstsName = "DstsName";
        public const string ServiceDns = "ServiceDns";
        public const string ServiceName = "ServiceName";
        public const string AllowedActors = "AllowedActors";

        // Num of Threads
        public const string MinMaxThreadsConfig = "MinMaxThreadsConfig";
        public const string MinWorkerThreads = "MinWorkerThreads";
        public const string MaxWorkerThreads = "MaxWorkerThreads";
        public const string MinCompletionPortThreads = "MinCompletionPortThreads";
        public const string MaxCompletionPortThreads = "MaxCompletionPortThreads";

        // PodHealthManager
        public const string PartnerDenyList = "PartnerDenyList";
        public const string ResourceProxyDenyList = "ResourceProxyDenyList";
        public const string CachePoolDenyList = "CachePoolDenyList";
        public const string CachePodDenyList = "CachePodDenyList";
        public const string NoneValue = "none";

        // HealthAwareLoadBalancer
        public const string ClientIP = "ClientIP";
        public const string ServerIP = "ServerIP";
        public const string ServiceAddr = "ServiceAddr";
    }
}
