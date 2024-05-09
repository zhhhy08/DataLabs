using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
using System;
using System.Diagnostics.Metrics;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing
{
    public enum DiagnosticEndpoint
    {
        DataLabs,
        Partner
    }

    public static class DiagnosticType
    {
        #region Meters/Loggers

        private static ILogger<DiagnosticEndpoint> _logger = DataLabLoggerFactory.CreateLogger<DiagnosticEndpoint>();

        private static readonly ILogger _activityStartedLogger = DataLabLoggerFactory.CreateLogger(MonitoringConstants.ACTIVITY_STARTED);
        private static readonly ILogger _activityFailedLogger = DataLabLoggerFactory.CreateLogger(MonitoringConstants.ACTIVITY_FAILED);
        private static readonly ILogger _activityCompletedLogger = DataLabLoggerFactory.CreateLogger(MonitoringConstants.ACTIVITY_COMPLETED);
        
        private static ILogger? _partnerActivityStartedLogger = PartnerActivityTracingLoggerFactory.CreateLogger(MonitoringConstants.ACTIVITY_STARTED);
        private static ILogger? _partnerActivityFailedLogger = PartnerActivityTracingLoggerFactory.CreateLogger(MonitoringConstants.ACTIVITY_FAILED);
        private static ILogger? _partnerActivityCompletedLogger = PartnerActivityTracingLoggerFactory.CreateLogger(MonitoringConstants.ACTIVITY_COMPLETED);

        private static readonly Histogram<double> _activityDurationHistogram =
            MetricLogger.CommonMeter.CreateHistogram<double>(MonitoringConstants.ActivityDurationName);
        private static readonly Histogram<double> _partnerActivityDurationHistogram =
            MetricLogger.PartnerAccountMeter.CreateHistogram<double>(MonitoringConstants.ActivityDurationName);

        #endregion

        #region DefaultDiagnosticEndpoint


        private static bool _canUsePartnerDiagnostics = !string.IsNullOrEmpty(MonitoringConstants.MDSD_PARTNER_ENDPOINT_VALUE);
        private static DiagnosticEndpoint _defaultDiagnosticEndpoint = _canUsePartnerDiagnostics ?
            DiagnosticEndpoint.Partner : DiagnosticEndpoint.DataLabs;

        static DiagnosticType()
        {
            Console.WriteLine($"_canUsePartnerDiagnostics: {_canUsePartnerDiagnostics}, _defaultDiagnosticEndpoint: {_defaultDiagnosticEndpoint.FastEnumToString()}");
        }

        public static void SetDefaultDiagnosticEndpoint(DiagnosticEndpoint diagnosticEndpoint)
        {
            if (diagnosticEndpoint == DiagnosticEndpoint.Partner && !_canUsePartnerDiagnostics)
            {
                Console.WriteLine("ERROR! Cannot use Partner Endpoint as it is not set in configuration.");
                return;
            }
            Console.WriteLine($"Setting DiagnosticEndpoint to {diagnosticEndpoint.FastEnumToString()}.");
            _defaultDiagnosticEndpoint = diagnosticEndpoint;
        }

        // For Unit Testing
        public static void ReInitialize()
        {
            _canUsePartnerDiagnostics = !string.IsNullOrEmpty(ConfigMapUtil.Configuration[SolutionConstants.MDSD_PARTNER_ENDPOINT]);
            _defaultDiagnosticEndpoint = _canUsePartnerDiagnostics ?
                DiagnosticEndpoint.Partner : DiagnosticEndpoint.DataLabs;
            _logger.LogWarning($"ReInitialize: CanUsePartnerDiagnostics={_canUsePartnerDiagnostics}, " +
                $"DefaultDiagnosticEndpoint={_defaultDiagnosticEndpoint.FastEnumToString()}");

            if (_canUsePartnerDiagnostics)
            {
                Console.WriteLine("Creating Partner Loggers");
                _partnerActivityStartedLogger = PartnerActivityTracingLoggerFactory.CreateLogger(MonitoringConstants.ACTIVITY_STARTED);
                _partnerActivityFailedLogger = PartnerActivityTracingLoggerFactory.CreateLogger(MonitoringConstants.ACTIVITY_FAILED);
                _partnerActivityCompletedLogger = PartnerActivityTracingLoggerFactory.CreateLogger(MonitoringConstants.ACTIVITY_COMPLETED);
            }
        }

        #endregion

        #region Getters

        public static DiagnosticEndpoint CurrentDefaultDiagnosticTypeName
        {
            get => _defaultDiagnosticEndpoint;
        }

        public static ILogger DefaultActivityStartedLogger
        {
            get {
                if (!_canUsePartnerDiagnostics || _partnerActivityStartedLogger == null)
                {
                    return _activityStartedLogger;
                }
                return _defaultDiagnosticEndpoint == DiagnosticEndpoint.DataLabs ? _activityStartedLogger : _partnerActivityStartedLogger;
            }
        }
        
        public static ILogger DataLabsActivityStartedLogger
        {
            get => _activityStartedLogger;
        }

        public static ILogger DefaultActivityFailedLogger
        {
            get
            {
                if (!_canUsePartnerDiagnostics || _partnerActivityFailedLogger == null)
                {
                    return _activityFailedLogger;
                }
                return _defaultDiagnosticEndpoint == DiagnosticEndpoint.DataLabs ? _activityFailedLogger : _partnerActivityFailedLogger;
            }
        }

        public static ILogger DataLabsActivityFailedLogger
        {
            get => _activityFailedLogger;
        }


        public static ILogger DefaultActivityCompletedLogger
        {
            get
            {
                if (!_canUsePartnerDiagnostics || _partnerActivityCompletedLogger == null)
                {
                    return _activityCompletedLogger;
                }
                return _defaultDiagnosticEndpoint == DiagnosticEndpoint.DataLabs ? _activityCompletedLogger : _partnerActivityCompletedLogger;
            }
        }

        public static ILogger DataLabsActivityCompletedLogger
        {
            get => _activityCompletedLogger;
        }

        public static Histogram<double> DefaultActivityDurationHistogram
        {
            get
            {
                if (!_canUsePartnerDiagnostics || _partnerActivityDurationHistogram == null)
                {
                    return _activityDurationHistogram;
                }
                return _defaultDiagnosticEndpoint == DiagnosticEndpoint.DataLabs ? _activityDurationHistogram : _partnerActivityDurationHistogram;
            }
        }

        public static Histogram<double> DataLabsActivityDurationHistogram
        {
            get => _activityDurationHistogram;
        }

        #endregion
    }
}