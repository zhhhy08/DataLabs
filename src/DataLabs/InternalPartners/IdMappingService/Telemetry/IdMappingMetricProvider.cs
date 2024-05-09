namespace Microsoft.WindowsAzure.IdMappingService.Services.Telemetry
{
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using System.Runtime.CompilerServices;

    [ExcludeFromCodeCoverage]
    public class IdMappingMetricProvider
    {
        #region constants

        private const string ResourceTypeDimension = "ResourceType";
        private const string EventTypeDimension = "EventType";
        private const string ArmProvisioningStateDimension = "ArmProvisioningState";
        private const string InternalIdPathDimension = "InternalIdPath";
        private const string ArmIdPathDimension = "ArmIdPath";
        public const string IdMappingServiceMeter = "IdMappingService";

        #endregion

        internal static readonly Meter IdMappingMeter = new(IdMappingServiceMeter, "1.0");

        #region Idmapping Metric

        internal static readonly Counter<long> PayLoadNotPresentMetric = IdMappingMeter.CreateCounter<long>("PayLoadNotPresent", description: "Count of payload fail to retrieve internal id due to PayLoad is not present");
        internal static readonly Counter<long> InternalIdNotPresentMetric = IdMappingMeter.CreateCounter<long>("InternalIdNotPresent", description: "Count of payload fail to retrieve internal id due to internal id is not present");
        internal static readonly Counter<long> WrongApiVersionMetric = IdMappingMeter.CreateCounter<long>("WrongAPIVersion", description: "Count of payload fail to retrieve internal id due to api version");
        internal static readonly Counter<long> ArmIdNotPresentMetric = IdMappingMeter.CreateCounter<long>("ArmIdNotPresent", description: "Count of failure to retrieve arm id from property");
        internal static readonly Counter<long> UnproccessableNotificationMetric = IdMappingMeter.CreateCounter<long>("UnproccessableNotification", description: "Count of notifications that are missing required information for IdMapping to process them like resourceId, resourceType or payload. These notifications always result in error responses.");
        internal static readonly Counter<long> SuccessResponseMetric = IdMappingMeter.CreateCounter<long>("SuccessResponse", description: "Count of successfull responses outputted from IdMapping");
        internal static readonly Counter<long> ErrorResponseMetric = IdMappingMeter.CreateCounter<long>("ErrorResponse", description: "Count of errored responses outputted from IdMapping");
        internal static readonly Histogram<long> SuccessfulResponseRequestDurationMetric = IdMappingMeter.CreateHistogram<long>("SuccessfulResponseRequestDurationMetric", "ms", description:"Time from IdMapping recieving request to returning successful response ");
        internal static readonly Histogram<long> ErrorResponseRequestDurationMetric = IdMappingMeter.CreateHistogram<long>("ErrorResponseRequestDurationMetric", "ms", description: "Time from IdMapping recieving request to returning error response ");

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportPayLoadNotPresentMetric(string ResourceType)
        {
            PayLoadNotPresentMetric.Add(1,
                new KeyValuePair<string, object>(ResourceTypeDimension, ResourceType));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportInternalIdNotPresentMetric(string ResourceType, string InternalIdPath)
        {
            InternalIdNotPresentMetric.Add(1,
                new KeyValuePair<string, object>(ResourceTypeDimension, ResourceType),
                new KeyValuePair<string, object>(InternalIdPathDimension, InternalIdPath));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportWrongApiVersionMetric(string ResourceType, string InternalIdPath)
        {
            WrongApiVersionMetric.Add(1,
                new KeyValuePair<string, object>(ResourceTypeDimension, ResourceType),
                new KeyValuePair<string, object>(InternalIdPathDimension, InternalIdPath));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportArmIdNotPresentMetric(string ResourceType, string ArmIdPath)
        {
            ArmIdNotPresentMetric.Add(1,
                new KeyValuePair<string, object>(ResourceTypeDimension, ResourceType),
                new KeyValuePair<string, object>(ArmIdPathDimension, ArmIdPath));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportSuccessResponseMetric(string resourceType, string eventType)
        {
            SuccessResponseMetric.Add(1,
                new KeyValuePair<string, object>(ResourceTypeDimension, resourceType),
                new KeyValuePair<string, object>(EventTypeDimension, eventType));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportSuccessfulResponseRequestDurationMetric(long durationValue, string resourceType, string eventType)
        {
            SuccessfulResponseRequestDurationMetric.Record(durationValue,
                new KeyValuePair<string, object>(ResourceTypeDimension, resourceType),
                new KeyValuePair<string, object>(EventTypeDimension, eventType));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportErrorResponseMetric(string resourceType, string eventType)
        {
            ErrorResponseMetric.Add(1,
                new KeyValuePair<string, object>(ResourceTypeDimension, resourceType),
                new KeyValuePair<string, object>(EventTypeDimension, eventType));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportErrorResponseRequestDurationMetric(long durationValue, string resourceType, string eventType)
        {
            ErrorResponseRequestDurationMetric.Record(durationValue,
                new KeyValuePair<string, object>(ResourceTypeDimension, resourceType),
                new KeyValuePair<string, object>(EventTypeDimension, eventType));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReportUnproccessableNotificationMetric(string resourceType, string provisioningState)
        {
            UnproccessableNotificationMetric.Add(1,
                new KeyValuePair<string, object>(ResourceTypeDimension, resourceType),
                new KeyValuePair<string, object>(ArmProvisioningStateDimension, provisioningState));
        }
    }
}
