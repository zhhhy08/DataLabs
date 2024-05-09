namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Hosting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    public static class MonitoringConstants
    {
        private const string ActionSnapshot = "snapshot";

        // To align with ARG Activity Monitor dimension to avoid confusion true/false in dashboard
        // Let's use same dimension name
        public static readonly KeyValuePair<string, object?> SuccessDimension = new(BasicActivityMonitor.IsActivityFailed, false);
        public static readonly KeyValuePair<string, object?> FailDimension = new(BasicActivityMonitor.IsActivityFailed, true);

        public const string SnapshotInputDimension = "SnapshotInput";
        public static readonly KeyValuePair<string, object?> SnapshotDimension = new(SnapshotInputDimension, true);
        public static readonly KeyValuePair<string, object?> NonSnapshotDimension = new(SnapshotInputDimension, false);
        
	// Notice:
        // Environment vairable should be defined with readonly to avoid altering during processing
        // Deployment Endpoint Values
        public static readonly string OTLP_EXPORTER_TYPE_VALUE = Environment.GetEnvironmentVariable(SolutionConstants.OTLP_EXPORTER_TYPE) ?? "";
        public static readonly string MDSD_PARTNER_ENDPOINT_VALUE = Environment.GetEnvironmentVariable(SolutionConstants.MDSD_PARTNER_ENDPOINT) ?? "";
        public static readonly string MDSD_DATALABS_ENDPOINT_VALUE = Environment.GetEnvironmentVariable(SolutionConstants.MDSD_DATALABS_ENDPOINT) ?? "";
        public static readonly string MDM_DATALABS_ENDPOINT_VALUE = Environment.GetEnvironmentVariable(SolutionConstants.MDM_DATALABS_ENDPOINT) ?? "";
        public static readonly string MDM_PARTNER_ENDPOINT_VALUE = Environment.GetEnvironmentVariable(SolutionConstants.MDM_PARTNER_ENDPOINT) ?? "";
        public static readonly string MDM_CUSTOMER_ENDPOINT_VALUE = Environment.GetEnvironmentVariable(SolutionConstants.MDM_CUSTOMER_ENDPOINT) ?? "";
        public static readonly bool IS_INTERNAL_PARTNER = "true".EqualsInsensitively(Environment.GetEnvironmentVariable(SolutionConstants.IS_INTERNAL_PARTNER));
        public static readonly bool IS_DEDICATED_PARTNER_AKS = "true".EqualsInsensitively(Environment.GetEnvironmentVariable(SolutionConstants.IS_DEDICATED_PARTNER_AKS));

        // Deployment AKS Instance Specifiers
        public static readonly string NODE_NAME = Environment.GetEnvironmentVariable(SolutionConstants.NODE_NAME) ?? "";
        public static readonly string POD_NAME = Environment.GetEnvironmentVariable(SolutionConstants.POD_NAME) ?? "";
        public static readonly string REGION = Environment.GetEnvironmentVariable(SolutionConstants.REGION) ?? "";
        public static readonly string SCALE_UNIT = Environment.GetEnvironmentVariable(SolutionConstants.SCALE_UNIT) ?? "";
        public static readonly string SERVICE = Environment.GetEnvironmentVariable(SolutionConstants.SERVICE) ?? "";
        public static readonly string BUILD_VERSION = Environment.GetEnvironmentVariable(SolutionConstants.BUILD_VERSION) ?? "";

        // IP Address
        public static readonly string POD_IP = Environment.GetEnvironmentVariable(SolutionConstants.POD_IP) ?? "";

        // Local Development for Debugging
        public static readonly bool IsLocalDevelopment =
            Environments.Development.EqualsInsensitively(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));

        // Data Labs Logging Constants
        public const string COMPONENT_DIMENSION = "component";
        public const string ACTIVITY_STARTED = "ActivityStarted";
        public const string ACTIVITY_COMPLETED = "ActivityCompleted";
        public const string ACTIVITY_FAILED = "ActivityFailed";
        public const string ACTIVITY_CRITICAL_ERROR = "ActivityCriticalError";

        // Data Labs Metric Constants
        public const string ActivityDurationName = "ActivityDuration";
        public const string CriticalActivityErrorCounterName = "CriticalActivityErrorCounter";
        public const string NameDimension = "Name";

        // Service Bus Queue
        public const string QUEUE_MESSAGE_COUNT = "QueueMessageCount";
        public const string QUEUE_ACTIVE_MESSAGE_COUNT = "QueueActiveMessageCount";
        public const string QUEUE_SCHEDULED_MESSAGE_COUNT = "QueueScheduledMessageCount";
        public const string QUEUE_DEAD_LETTER_MESSAGE_COUNT = "QueueDeadLetterMessageCount";
        public const string QUEUE_SIZE_IN_BYTES = "QueueSizeInBytes";
        public const string QueueNameDimension = "Name";
        public const string DEAD_LETTER_PURGED_MESSAGE_COUNT = "DeadLetterPurgedMessageCount";
        public const string DEAD_LETTER_REPLAYED_MESSAGE_COUNT = "DeadLetterReplayedMessageCount";

        // Concurrency Managers
        public const string CONCURRENCY_MANAGER_RUNNING_PREFIX = "Running";
        public const string CONCURRENCY_MANAGER_AVAILABLE_PREFIX = "Available";

        // EventHub
        public const string EVENTHUB_SEC_SINCE_LAST_HEALTH_CHECK = "EventHubSecSinceLastHealthCheck";
        public const string EventHubNameDimension = "Name";
        public const string PartitionIdDimension = "Id";
        public const string RawInputToSingleInputDimension = "RawToSingle";
        public const string ConsumerGroupNameDimension = "ConsumerGroup";

        // IOEventTaskContext
        public const string EventTaskTypeDimension = "EventTaskType";
        public const string ReasonDimension = "Reason";
        public const string RetryCountDimension = "RetryCount";
        public const string IsRetryDimension = "IsRetry";
        public const string DataSourceTypeDimension = "DataSourceType";
        public const string DataSourceNameDimension = "Name";

        // EventWriter
        public const string EventWriterNameDimension = "Name";
        public const string FirstFailedWriterNameDimension = "FirstFailedWriter";
        public const string EventWriterExceptionDimension = "Exception";
        public const string RetryCountWithNextEventWriterDimension = "RetryCountWithNextEventWriter";
        
        // SLO Dimension
        public const string OutputResourceTypeDimension = "OutputResourceType";
        public const string OutputActionDimension = "OutputAction";
        public const string InputActionDimension = "InputAction";
        public const string IsPartnerDecision = "IsPartnerDecision";

        // Traffic tuner
        public const string AllowAllTenants = "AllowAllTenants";
        public const string SubscriptionId = "SubscriptionId";
        public const string ResourceType = "ResourceType";
        public const string ResourceId = "ResourceId";
        public const string TenantId = "TenantId";
        public const string MessageRetryCount = "MessageRetryCount";
        public const string Allowed = "Allowed";
        public const string NotAllowedReason = "NotAllowedReason";
        public const string ResourceLocation = "ResourceLocation";

        public static readonly Dictionary<string, string> DataLabsLoggerTableMappings = new Dictionary<string, string>
        {
            [ACTIVITY_STARTED] = ACTIVITY_STARTED,
            [ACTIVITY_FAILED] = ACTIVITY_FAILED,
            [ACTIVITY_COMPLETED] = ACTIVITY_COMPLETED
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static KeyValuePair<string, object?> GetSuccessDimension(bool success)
        {
            return success ? SuccessDimension : FailDimension;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static KeyValuePair<string, object?> IsSnapshotInputDimension(string action)
        {
            return action != null && action.EqualsInsensitively(ActionSnapshot) ? SnapshotDimension : NonSnapshotDimension;
        }

        public static readonly KeyValuePair<string, object> RawInputToSingleInputTrueDimension = 
            new(RawInputToSingleInputDimension, true);

        public static readonly KeyValuePair<string, object> RawInputToSingleInputFalseDimension = 
            new(RawInputToSingleInputDimension, false);
    }
}
