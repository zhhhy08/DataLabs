namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring
{
    using OpenTelemetry.Logs;
    using OpenTelemetry;
    using System.Diagnostics;

    public class MissingTraceIdLogProcessor : BaseProcessor<LogRecord>
    {
        // Geneva dgrep search complains when field is empty. Geneva exports internally adds "env_dt_traceId" inside their code. 
        // We would like to search the field with traceId but when the column has empty value, Geneva dgrep search complains such as "Symbol is not found etc.."
        // In order to avoid dgrep search's complaint, we add NullTraceId when current Activity is null

        public const string EmptyTraceIdString = "10000000000000000000000000000001"; // All zeros are not allowed in OpenTelemetry
        public const string EmptySpanIdString = "1000000000000001"; // All zeros are not allowed in OpenTelemetry

        private readonly ActivityTraceId EmptyTraceId;
        private readonly ActivitySpanId EmptySpanId;
        private readonly ActivityTraceFlags EmptyTraceFlags;

        public MissingTraceIdLogProcessor()
        {
            EmptyTraceId = ActivityTraceId.CreateFromString(EmptyTraceIdString);
            EmptySpanId = ActivitySpanId.CreateFromString(EmptySpanIdString);
            EmptyTraceFlags = ActivityTraceFlags.None;
        }

        public override void OnEnd(LogRecord data)
        {
            if (data == null)
            {
                return;
            }

            if (data.TraceId == default)
            {
                // Set TraceId to NullTraceId
                data.TraceId = EmptyTraceId;
                data.SpanId = EmptySpanId;
                data.TraceFlags = EmptyTraceFlags;
            }
        }
    }
}
