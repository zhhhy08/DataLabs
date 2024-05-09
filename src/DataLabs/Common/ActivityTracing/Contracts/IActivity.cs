// This file is copied from Mgmt-Governance-ResourcesCache repo (ARG)
// There should not be any modifications to fields copied to maintain backward compatibility, though additional fields can be added.
// 
// TODO Align the monitoring with ARG.
namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Activity - target of instrumentation
    /// </summary>
    public interface IActivity
    {
        #region Properties

        #region Id/Name

        /// <summary>
        /// Gets unique activity id
        /// </summary>
        Guid ActivityId { get; }

        /// <summary>
        /// Gets activity name
        /// </summary>
        string ActivityName { get; }

        #endregion

        #region Inherited

        /// <summary>
        /// Gets execution context
        /// </summary>
        Guid Context { get; }

        /// <summary>
        /// Gets parent activity
        /// </summary>
        IActivity ParentActivity { get; }

        /// <summary>
        /// Gets top-level activity
        /// </summary>
        IActivity TopLevelActivity { get; }

        #endregion

        #region Context Properties

        /// <summary>
        /// The SLO driven Scenario this activity is representing
        /// </summary>
        string? Scenario { get; }

        /// <summary>
        /// The component this activity is representing
        /// </summary>
        string? Component { get; }

        /// <summary>
        /// The CorrelationId related to this activity
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// ResourceId related to this activity
        /// </summary>
        public string? InputResourceId { get; set; }

        /// <summary>
        /// The Output Resource's CorrelationId related to this activity
        /// </summary>
        public string? OutputCorrelationId { get; set; }

        /// <summary>
        /// ResourceId related to this activity
        /// </summary>
        public string? OutputResourceId { get; set; }

        #endregion

        #region Sequence Tracking

        /// <summary>
        /// CreationTime of this Activity
        /// </summary>
        public DateTime CreationDateTime { get; }

        /// <summary>
        /// StartTime of this Activity
        /// </summary>
        public DateTime StartDateTime { get; }

        #endregion

        /// <summary>
        /// Gets the total elapsed time from last OnStart() in the current instance
        /// </summary>
        TimeSpan Elapsed { get; }

        /// <summary>
        /// Gets the total elapsed time from creation in the current instance
        /// </summary>
        TimeSpan DurationFromCreation { get; }

        /// <summary>
        /// Gets or sets properties of current activity
        /// </summary>
        object? this[string key] { get; set; }

        /// <summary>
        /// Sets properties of current activity with an option to inherit
        /// </summary>
        object? this[string key, bool inherit] { set; }

        /// <summary>
        /// Gets properties of current activity
        /// </summary>
        IDictionary<string, object?> Properties { get; }

        #endregion

        /// <summary>
        /// Starts or restarts measuring elapsed time for an activity
        /// </summary>
        void Start();

        ReadOnlySpan<char> ToPropertyStringSpan(int lengthLimit);

        /// <summary>Sets property to a given key</summary>
        /// <param name="key">Property name</param>
        /// <param name="value">Property value</param>
        /// <param name="inherit">if set to <c>true</c> [inherit].</param>
        /// <returns>Resulting value for the key</returns>
        object? SetProperty(string key, object? value, bool inherit = true);

        /// <summary>Gets the properties.</summary>
        /// <param name="skipNonInherited">if set to <c>true</c> [skip non inherited].</param>
        IEnumerable<KeyValuePair<string, object?>> GetProperties(bool skipNonInherited = false);
    }
}