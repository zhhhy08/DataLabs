namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing
{
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;

    /// <summary>
    /// ActivityMonitorFactory
    /// </summary>
    public class ActivityMonitorFactory
    {
        public static bool UseTaskAwareActivityMonitor = false;

        #region Fields

        /// <summary>
        /// The activity name
        /// </summary>
        protected readonly string ActivityName;

        /// <summary>
        /// Activity Level
        /// </summary>
        protected readonly LogLevel LogLevel;

        /// <summary>
        /// Use the Non Default Endpoint for Logging/Metrics
        /// </summary>
        protected readonly bool UseDataLabsEndpoint;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityMonitorFactory"/> class 
        /// with default log level of LogLevel.Information
        /// </summary>
        public ActivityMonitorFactory(string activityName, LogLevel logLevel = LogLevel.Information, bool useDataLabsEndpoint = false)
        {
            this.ActivityName = activityName;
            this.LogLevel = logLevel;
            this.UseDataLabsEndpoint = useDataLabsEndpoint;
        }

        #endregion

        public IActivityMonitor ToMonitor(
            IActivity? parentActivity = null,
            bool inheritProperties = false,
            string? scenario = null,
            string? component = null,
            string? correlationId = null,
            string? inputResourceId = null,
            string? outputCorrelationId = null,
            string? outputResourceId = null)
        {
            return UseTaskAwareActivityMonitor ?
                new TaskAwareActivityMonitor(
                    activityName: ActivityName,
                    parentActivity: parentActivity,
                    inheritProperties: inheritProperties,
                    scenario: scenario,
                    component: component,
                    correlationId: correlationId,
                    inputResourceId: inputResourceId,
                    outputCorrelationId: outputCorrelationId,
                    outputResourceId: outputResourceId,
                    logLevel: LogLevel,
                    useDataLabsEndpoint: UseDataLabsEndpoint)
                :
                new BasicActivityMonitor(
                    activityName: ActivityName,
                    parentActivity: parentActivity,
                    inheritProperties: inheritProperties,
                    scenario: scenario,
                    component: component,
                    correlationId: correlationId,
                    inputResourceId: inputResourceId,
                    outputCorrelationId: outputCorrelationId,
                    outputResourceId: outputResourceId,
                    logLevel: LogLevel,
                    useDataLabsEndpoint: UseDataLabsEndpoint);
        }
    }
}