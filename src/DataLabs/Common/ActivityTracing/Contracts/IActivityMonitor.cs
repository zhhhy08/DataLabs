// This file is copied from Mgmt-Governance-ResourcesCache repo (ARG)
// There should not be any modifications to fields copied to maintain backward compatibility, though additional fields can be added.
// 
// TODO Align the monitoring with ARG.
namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts
{
    using System;

    /// <summary>
    /// Activity monitor
    /// </summary>
    public partial interface IActivityMonitor : IDisposable
    {
        #region Properties

        static IActivity? CurrentActivity { get => _currentActivity.Value; }

        /// <summary>
        /// Gets activity being monitored
        /// </summary>
        IActivity Activity { get; }

        #endregion

        /// <summary>
        /// Executes instrumentation when activity started
        /// </summary>
        /// <param name="logging">Logging.</param>
        void OnStart(bool logging = true);

        /// <summary>
        /// Executes instrumentation when activity completed successfully
        /// </summary>
        /// <param name="logging">Logging.</param>
        /// <param name="recordDurationMetric">Record duration metric.</param>
        void OnCompleted(bool logging = true, bool recordDurationMetric = true);

        /// <summary>
        /// Executes instrumentation when activity failed
        /// </summary>
        /// <param name="ex">Exception thrown by activity execution code</param>
        /// <param name="recordDurationMetric">Record duration metric.</param>
        /// <param name="isCriticalLevel">Is critical level.</param>
        void OnError(
            Exception ex,
            bool recordDurationMetric = true,
            bool isCriticalLevel = false);
    }
}