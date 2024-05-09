namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing
{
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    /// <summary>
    /// Activity monitor
    /// </summary>
    public sealed class TaskAwareActivityMonitor : BasicActivityMonitor
    {
        public TaskAwareActivityMonitor(
            string activityName,
            IActivity? parentActivity = null,
            bool inheritProperties = false,
            string? scenario = null,
            string? component = null,
            string? correlationId = null,
            string? inputResourceId = null,
            string? outputCorrelationId = null,
            string? outputResourceId = null,
            LogLevel logLevel = LogLevel.Information,
            bool useDataLabsEndpoint = false) : base(
                activityName: activityName,
                parentActivity: parentActivity,
                inheritProperties: inheritProperties,
                scenario: scenario,
                component: component,
                correlationId: correlationId,
                inputResourceId: inputResourceId,
                outputCorrelationId: outputCorrelationId,
                outputResourceId: outputResourceId,
                logLevel: logLevel,
                useDataLabsEndpoint: useDataLabsEndpoint)
        {
        }

        protected override double GetDurationFromTopActivity()
        {
            // In ActivityMonitor, 
            // elapsedMilliseconds: time elapsed in the activity
            // durationMilliseconds: time elapsed from the top Activity

            // Here we return elapsed time from top OpenTelemetry Activity using traceId
            // So we can know elapsed time from top Activity using same traceId
            var eventTaskActivity = OpenTelemetryActivityWrapper.Current;
            if (eventTaskActivity != null)
            {
                return eventTaskActivity.DurationFromTopActivity.TotalMilliseconds;
            }

            return base.GetDurationFromTopActivity();
        }
    }
}