namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Logging;
    using System;

    public class ArnNotificationClientLogger : ILog
    {
        #region Tracing

        private static readonly ActivityMonitorFactory ArnNotificationClientLoggerInfo =
            new ActivityMonitorFactory("ArnNotificationClientLogger.Info");

        private static readonly ActivityMonitorFactory ArnNotificationClientLoggerDebug =
            new ActivityMonitorFactory("ArnNotificationClientLogger.Debug");

        private static readonly ActivityMonitorFactory ArnNotificationClientLoggerWarn =
            new ActivityMonitorFactory("ArnNotificationClientLogger.Warn");

        private static readonly ActivityMonitorFactory ArnNotificationClientLoggerError =
            new ActivityMonitorFactory("ArnNotificationClientLogger.Error");

        private static readonly ActivityMonitorFactory ArnNotificationClientLoggerFatal =
            new ActivityMonitorFactory("ArnNotificationClientLogger.Fatal");

        #endregion

        public bool IsDebugEnabled { get; private set; }

        public bool IsInfoEnabled { get; private set; }

        public bool IsErrorEnabled => true;

        public bool IsFatalEnabled => true;

        public bool IsWarnEnabled => true;

        #region Fields

        private const int MaxMessageLength = 20480;

        #endregion

        #region Constructors

        public ArnNotificationClientLogger(bool isDebugEnabled, bool isInfoEnabled)
        {
            this.IsDebugEnabled = isDebugEnabled;
            this.IsInfoEnabled = isInfoEnabled;
        }

        #endregion

        #region Public Methods

        public void Debug(Guid correlationId, object obj)
        {
            if (!this.IsDebugEnabled)
            {
                return;
            }

            this.LogData(correlationId, obj, null, ArnNotificationClientLoggerDebug, "Debug");
        }

        public void Debug(Guid correlationId, object obj, Exception exception)
        {
            if (!this.IsDebugEnabled)
            {
                return;
            }

            this.LogData(correlationId, obj, exception, ArnNotificationClientLoggerDebug, "Debug");
        }

        public void Error(Guid correlationId, object errorObject)
        {
            if (!this.IsErrorEnabled)
            {
                return;
            }

            this.LogData(correlationId, errorObject, null, ArnNotificationClientLoggerError, "Error");
        }

        public void Error(Guid correlationId, object errorObject, Exception exception)
        {
            if (!this.IsErrorEnabled)
            {
                return;
            }

            this.LogData(correlationId, errorObject, exception, ArnNotificationClientLoggerError, "Error");
        }

        public void Fatal(Guid correlationId, object errorObject)
        {
            if (!this.IsFatalEnabled)
            {
                return;
            }

            this.LogData(correlationId, errorObject, null, ArnNotificationClientLoggerFatal, "Fatal");
        }

        public void Fatal(Guid correlationId, object errorObject, Exception exception)
        {
            if (!this.IsFatalEnabled)
            {
                return;
            }

            this.LogData(correlationId, errorObject, exception, ArnNotificationClientLoggerFatal, "Fatal");
        }

        public void Info(Guid correlationId, object informationData)
        {
            if (!this.IsInfoEnabled)
            {
                return;
            }

            this.LogData(correlationId, informationData, null, ArnNotificationClientLoggerInfo, "Information");
        }

        public void Info(Guid correlationId, object informationData, Exception exception)
        {
            if (!this.IsInfoEnabled)
            {
                return;
            }

            this.LogData(correlationId, informationData, exception, ArnNotificationClientLoggerInfo, "Information");
        }

        public void Warn(Guid correlationId, object warningData)
        {
            if (!this.IsWarnEnabled)
            {
                return;
            }

            this.LogData(correlationId, warningData, null, ArnNotificationClientLoggerWarn, "Warning");
        }

        public void Warn(Guid correlationId, object warningData, Exception exception)
        {
            if (!this.IsWarnEnabled)
            {
                return;
            }

            this.LogData(correlationId, warningData, exception, ArnNotificationClientLoggerWarn, "Warning");
        }

        #endregion

        #region Helpers

        internal void UpdateLoggingConfig(bool isDebugEnabled, bool isInfoEnabled)
        {
            this.IsDebugEnabled = isDebugEnabled;
            this.IsInfoEnabled = isInfoEnabled;
        }

        private void LogData(Guid correlationId, object data, Exception? exception, ActivityMonitorFactory monitor, string type)
        {
            using var methodMonitor = monitor.ToMonitor(correlationId: correlationId.ToString());

            if (data != null)
            {
                methodMonitor.Activity.Properties[type] = SerializationHelper.SerializeObject(data).TruncateWithEllipsis(MaxMessageLength);
            }

            if (exception == null)
            {
                methodMonitor.OnCompleted();
            }
            else
            {
                methodMonitor.OnError(exception);
            }
        }

        #endregion
    }
}
