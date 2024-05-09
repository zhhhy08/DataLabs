namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;

    /// <summary>
    /// Activity monitor
    /// </summary>
    public class BasicActivityMonitor : IActivityMonitor
    {
        #region columns and dimensions

        /* 
         * For below parameters, we can't use IConfiguration because logger is first created and 
         * then we create IConfiguration and use the Logger to print any error during IConfiguration 
         * Use Environment variables to set the values for below parameters
         */
        public static readonly int ReservedStartedEventProperiesSize =
            Int32.TryParse(Environment.GetEnvironmentVariable("StartedEventProperiesSize"), out int num) ? num : 10 * 1024;

        public static readonly int ReservedCompletedEventProperiesSize =
            Int32.TryParse(Environment.GetEnvironmentVariable("CompletedEventProperiesSize"), out int num) ? num : 60 * 1024;

        public static readonly int ReservedFailedEventPropertiesSize =
            Int32.TryParse(Environment.GetEnvironmentVariable("FailedEventPropertiesSize"), out int num) ? num : 30 * 1024;

        public const string StartTime = "startTime";
        public const string Context = "context";
        public const string ActivityName = "activityName";
        public const string ActivityId = "activityId";
        public const string ParentActivityName = "parentActivityName";
        public const string ParentActivityId = "parentActivityId";
        public const string Component = "component";
        public const string Scenario = "scenario";
        public const string Properties = "properties";
        public const string IsActivityFailed = "isActivityFailed";
        public const string ElapsedMilliseconds = "elapsedMilliseconds";
        public const string DurationMilliseconds = "durationMilliseconds";
        public const string CorrelationId = "correlationId";
        public const string InputResourceId = "inputResourceId";
        public const string OutputCorrelationId = "outputCorrelationId";
        public const string OutputResourceId = "outputResourceId";
        public const string ResourceType = "resourceType";
        public const string EventType = "eventType";
        public const string Exception = "exception";

        private bool _useDataLabsEndpoint;
        private LogLevel _logLevel;
        private readonly Activity? _activity; // This is open telemetry activity. This is ncessary to set the correct trace id in the log context
        private IActivity? _previousActivityForAsyncLocal;

        protected readonly BasicActivity _basicActivity;

        #endregion

        #region Critical Metric/Loggers

        /// <summary>
        /// Critical Activity Monitor Error Logger
        /// </summary>
        private static ILogger ActivityMonitorCriticalError = DataLabLoggerFactory.CreateLogger(MonitoringConstants.ACTIVITY_CRITICAL_ERROR);

        /// <summary>
        /// Critical Activity Monitor Error Metric Counter
        /// </summary>
        public static readonly Counter<long> CriticalActivityErrorCounter =
            MetricLogger.CommonMeter.CreateCounter<long>(MonitoringConstants.CriticalActivityErrorCounterName);

        #endregion

        #region properties

        public IActivity Activity => _basicActivity;

        #endregion

        public BasicActivityMonitor(
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
            bool useDataLabsEndpoint = false)
        {
            _activity = System.Diagnostics.Activity.Current;

            parentActivity ??= IActivityMonitor.CurrentActivity;

            if (parentActivity == null || BasicActivity.IsNullOrNone(parentActivity))
            {
                // This is top level activity, create new Context
                // In order to align with ARG log, we need to create Context for TopActivity and save in context column
                var activityContext = Guid.NewGuid();
                _basicActivity = new BasicActivity(activityName, activityContext, scenario, component, correlationId, inputResourceId, outputCorrelationId, outputResourceId);
            }
            else
            {
                // context will be ignored as parent activity context will be used.
                _basicActivity = new BasicActivity(activityName, parentActivity, inheritProperties, scenario, component, correlationId, inputResourceId, outputCorrelationId, outputResourceId);
            }

            _logLevel = logLevel;
            _useDataLabsEndpoint = useDataLabsEndpoint;

            // ParentActivity can be passed as parameter. So for AsyncLocal, we should not use ParentActivity. It caused incorrect AsyncLocal current Activity.
            // We have to keep actual current Activity. It will be used when we complete/Dispose this activity
            _previousActivityForAsyncLocal = IActivityMonitor.CurrentActivity;
            IActivityMonitor.SetCurrentActivity(Activity);
        }

        public void OnStart(bool logging = true)
        {
            var curretActivity = System.Diagnostics.Activity.Current;
            System.Diagnostics.Activity.Current = _activity;

            try
            {
                Activity.Start();

                if (logging)
                {
                    ILogger ActivityStartedLogger = _useDataLabsEndpoint ? 
                        DiagnosticType.DataLabsActivityStartedLogger : 
                        DiagnosticType.DefaultActivityStartedLogger;
                    
                    ActivityStartedLogger.Log(
                        logLevel: _logLevel,
                        eventId: default,
                        state: GetBaseLoggingColumns(ReservedStartedEventProperiesSize),
                        exception: null,
                        formatter: (state, ex) => "Empty"
                    );
                }
            }
            catch (Exception ex)
            {
                // Should never happen
                ActivityMonitorCriticalError.LogCritical(ex, "OnStart: " + ex.ToString());
            }
            finally
            {
                System.Diagnostics.Activity.Current = curretActivity;
            }
        }

        public void OnError(
            Exception ex,
            bool recordDurationMetric = true,
            bool isCriticalLevel = false)
        {
            var curretActivity = System.Diagnostics.Activity.Current;
            System.Diagnostics.Activity.Current = _activity;

            try
            {
                var columns = GetBaseLoggingColumns(ReservedFailedEventPropertiesSize);
                columns.Add(new KeyValuePair<string, object?>(ElapsedMilliseconds, Activity.Elapsed.TotalMilliseconds.ToString("F2")));
                columns.Add(new KeyValuePair<string, object?>(Exception, ex.ToString()));

                var logLevel = isCriticalLevel ? LogLevel.Critical :
                    (_logLevel > LogLevel.Error ? _logLevel : LogLevel.Error);

                ILogger ActivityFailedLogger = _useDataLabsEndpoint ?
                    DiagnosticType.DataLabsActivityFailedLogger :
                    DiagnosticType.DefaultActivityFailedLogger;

                ActivityFailedLogger.Log(
                logLevel: logLevel,
                    eventId: default,
                    state: columns,
                    exception: ex,
                    formatter: (state, ex) => $"{ex?.StackTrace ?? "empty"}"
                );

                if (recordDurationMetric)
                {
                    AddDurationMetric(true);
                }

                if (logLevel == LogLevel.Critical)
                {
                    AddCriticalErrorMetric();
                }

                PopCurrentActivity();
            }
            catch (Exception internalEx)
            {
                // Should never happen
                ActivityMonitorCriticalError.LogCritical(internalEx, "OnError: " + internalEx.ToString());
            }
            finally
            {
                System.Diagnostics.Activity.Current = curretActivity;
            }
        }

        public void OnCompleted(bool logging = true, bool recordDurationMetric = true)
        {
            var curretActivity = System.Diagnostics.Activity.Current;
            System.Diagnostics.Activity.Current = _activity;

            try
            {
                if (logging)
                {
                    var columns = GetBaseLoggingColumns(ReservedCompletedEventProperiesSize);
                    columns.Add(new KeyValuePair<string, object?>(ElapsedMilliseconds, Activity.Elapsed.TotalMilliseconds.ToString("F2")));

                    ILogger ActivityCompletedLogger = _useDataLabsEndpoint ?
                        DiagnosticType.DataLabsActivityCompletedLogger :
                        DiagnosticType.DefaultActivityCompletedLogger;

                    ActivityCompletedLogger.Log(
                        logLevel: _logLevel,
                        eventId: default,
                        state: columns,
                        exception: null,
                        formatter: (state, ex) => "Empty"
                    );
                }
				
                if (recordDurationMetric)
                {
                    AddDurationMetric(false);
                }

                PopCurrentActivity();
            }
            catch (Exception internalEx)
            {
                // Should never happen
                ActivityMonitorCriticalError.LogCritical(internalEx, "OnCompleted: " + internalEx.ToString());
            }
            finally
            {
                System.Diagnostics.Activity.Current = curretActivity;
            }
        }

        private void AddCriticalErrorMetric()
        {
            TagList tagList = default;
            tagList.Add(ActivityName, Activity.ActivityName);
            tagList.Add(ParentActivityName, Activity.ParentActivity.ActivityName);
            tagList.Add(Component, Activity.Component);
            tagList.Add(Scenario, Activity.Scenario);

            CriticalActivityErrorCounter.Add(1, tagList);
        }

        private void AddDurationMetric(bool isActivityFailed)
        {
            TagList tagList = default;
            tagList.Add(ActivityName, Activity.ActivityName);
            tagList.Add(ParentActivityName, Activity.ParentActivity.ActivityName);
            tagList.Add(Component, Activity.Component);
            tagList.Add(Scenario, Activity.Scenario);
            tagList.Add(IsActivityFailed, isActivityFailed);

            Histogram<double> ActivityDurationHistogram = _useDataLabsEndpoint ? 
                DiagnosticType.DataLabsActivityDurationHistogram : DiagnosticType.DefaultActivityDurationHistogram;
            ActivityDurationHistogram.Record(Activity.Elapsed.TotalMilliseconds, tagList);
        }

        protected virtual double GetDurationFromTopActivity()
        {
            // We have to use DurationFromCreation instead of Elapsed because onStart() is optional, Elpased could be 0 if there is no onStart() called
            return Activity.TopLevelActivity.DurationFromCreation.TotalMilliseconds;
        }

        private List<KeyValuePair<string, object?>> GetBaseLoggingColumns(int lengthLimit)
        {
            List<KeyValuePair<string, object?>> kpList = new(20);

            // Add Duration for all Start/OnCompleted/OnError so that we can sort by it.
            kpList.Add(new KeyValuePair<string, object?>(DurationMilliseconds, GetDurationFromTopActivity().ToString("F2")));

            // Add startTime for activity sequence purpose
            var startTime = Activity.StartDateTime == default ? Activity.CreationDateTime : Activity.StartDateTime;
            kpList.Add(new KeyValuePair<string, object?>(StartTime, startTime));
            
            // Create separate columns for correlationId and resourceId
            var mandatoryKey = CorrelationId;
            var mandatoryValue = Activity.CorrelationId;
            mandatoryValue = string.IsNullOrWhiteSpace(mandatoryValue) ? BasicActivity.NoneString : mandatoryValue;
            kpList.Add(new KeyValuePair<string, object?>(mandatoryKey, mandatoryValue));

            mandatoryKey = InputResourceId;
            mandatoryValue = Activity.InputResourceId;
            mandatoryValue = string.IsNullOrWhiteSpace(mandatoryValue) ? BasicActivity.NoneString : mandatoryValue;
            kpList.Add(new KeyValuePair<string, object?>(mandatoryKey, mandatoryValue));

            mandatoryKey = OutputCorrelationId;
            mandatoryValue = Activity.OutputCorrelationId;
            mandatoryValue = string.IsNullOrWhiteSpace(mandatoryValue) ? BasicActivity.NoneString : mandatoryValue;
            kpList.Add(new KeyValuePair<string, object?>(mandatoryKey, mandatoryValue));

            mandatoryKey = OutputResourceId;
            mandatoryValue = Activity.OutputResourceId;
            mandatoryValue = string.IsNullOrWhiteSpace(mandatoryValue) ? BasicActivity.NoneString : mandatoryValue;
            kpList.Add(new KeyValuePair<string, object?>(mandatoryKey, mandatoryValue));

            kpList.Add(new KeyValuePair<string, object?>(Context, Activity.Context));
            kpList.Add(new KeyValuePair<string, object?>(ActivityName, Activity.ActivityName));
            kpList.Add(new KeyValuePair<string, object?>(ActivityId, Activity.ActivityId));
            kpList.Add(new KeyValuePair<string, object?>(ParentActivityId, Activity.ParentActivity.ActivityId));
            kpList.Add(new KeyValuePair<string, object?>(ParentActivityName, Activity.ParentActivity.ActivityName));
            kpList.Add(new KeyValuePair<string, object?>(Component, Activity.Component));
            kpList.Add(new KeyValuePair<string, object?>(Scenario, Activity.Scenario));

            ReadOnlySpan<char> propertiesSpan = Activity.ToPropertyStringSpan(lengthLimit);
            kpList.Add(new KeyValuePair<string, object?>(Properties, propertiesSpan.ToString()));

            return kpList;
        }

        private void PopCurrentActivity()
        {
            if (Activity == IActivityMonitor.CurrentActivity)
            {
                IActivityMonitor.SetCurrentActivity(_previousActivityForAsyncLocal);
            }
        }

        public void Dispose()
        {
            PopCurrentActivity();
        }
    }
}