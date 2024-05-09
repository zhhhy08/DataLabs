namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;

    /// <summary>
    /// Instrumentation activity
    /// </summary>
    public class BasicActivity : IActivity
    {
        #region Members

        // Below Codes are copied from ARG's DefaultActivity class
        // ARG has been using ThreadLocal for property bag to reduce memory footprint

        /// <summary>
        /// Property bag is unlikely to have 10k chars, assuming 2k threads, at most 10k * 2 * 2k = 40 MB memory will be used, which is fine.
        /// </summary>
        private static readonly ThreadLocal<char[]> PropertyBagCharArray = new ThreadLocal<char[]>();

        public readonly static IActivity Null = new BasicActivity(Guid.Empty, "[activity-name:null]");

        public const string NoneString = "[none]";

        #endregion

        #region Fields

        private readonly IActivity? parentActivity;

        private ISet<string>? nonInheritableProperties;

        // Time Related (All TimeStamps are returned from StopWatch, not milliseconds)
        private readonly long creationStopWatchTimeStamp;
        private long startStopWatchTimeStamp;
        
        protected readonly IDictionary<string, object?> activityProperties =
            new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        #region Id/Name

        /// <summary>
        /// Gets unique activity id
        /// </summary>
        public Guid ActivityId { get; }

        /// <summary>
        /// Gets activity name
        /// </summary>
        public string ActivityName { get; }

        #endregion

        #region Inherited 

        /// <summary>
        /// Gets execution context
        /// </summary>
        public Guid Context { get; }

        /// <summary>
        /// Gets top-level activity
        /// </summary>
        public IActivity TopLevelActivity { get; }

        /// <summary>
        /// Gets parent activity
        /// </summary>
        public IActivity ParentActivity => this.parentActivity ?? Null;

        #endregion

        #region Context Properties

        /// <summary>
        /// The SLO driven Scenario this activity is representing
        /// </summary>
        public string? Scenario { get; }

        /// <summary>
        /// The component this activity is representing
        /// </summary>
        public string? Component { get; }

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
        public DateTime StartDateTime { get; private set; }

        #endregion

        /// <summary>
        /// Gets or sets properties of current activity
        /// </summary>
        public object? this[string key]
        {
            get => this.Properties[key];
            set => this.Properties[key] = value;
        }

        /// <summary>
        /// Sets properties of current activity with an option to inherit
        /// </summary>
        public object? this[string key, bool inherit]
        {
            set
            {
                this.SetProperty(key, value, inherit);
            }
        }

        public IDictionary<string, object?> Properties => this.activityProperties;

        public TimeSpan Elapsed => startStopWatchTimeStamp > 0 ? Stopwatch.GetElapsedTime(startStopWatchTimeStamp) : TimeSpan.Zero;
        public TimeSpan DurationFromCreation => Stopwatch.GetElapsedTime(creationStopWatchTimeStamp);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Activity"/> class.
        /// </summary>
        /// <param name="activityName">Name of the activity.</param>
        public BasicActivity(string activityName)
            : this(Guid.NewGuid(), activityName, Guid.NewGuid(), null, null, null, null, null, null)
        {
        }

        public BasicActivity(string activityName, Guid context,
          string? scenario, string? component, string? correlationId, string? inputResourceId, string? outputCorrelationId, string? outputResourceId)
          : this(Guid.NewGuid(), activityName, context, scenario, component, correlationId, inputResourceId, outputCorrelationId, outputResourceId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Activity"/> class.
        /// </summary>
        /// <param name="activityId">The activity identifier.</param>
        /// <param name="activityName">Name of the activity.</param>
        public BasicActivity(Guid activityId, string activityName)
            : this(activityId, activityName, Guid.NewGuid(), null, null, null, null, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Activity"/> class.
        /// </summary>
        /// <param name="activityId">The activity identifier.</param>
        /// <param name="activityName">Name of the activity.</param>
        /// <param name="context">The context.</param>
        /// <param name="scenario">Scenario of the activity.</param>
        /// <param name="component">Component of the activity.</param>
        public BasicActivity(Guid activityId, string activityName, Guid context,
            string? scenario, string? component, string? correlationId, string? inputResourceId, string? outputCorrelationId, string ? outputResourceId)
        {
            creationStopWatchTimeStamp = Stopwatch.GetTimestamp();
            CreationDateTime = DateTime.UtcNow;

            this.ActivityId = activityId;
            this.ActivityName = activityName ?? Null.ActivityName;

            this.Context = context;
            this.parentActivity = null;
            this.TopLevelActivity = this;

            this.Scenario = scenario;
            this.Component = component;
            this.CorrelationId = correlationId;
            this.InputResourceId = inputResourceId;
            this.OutputCorrelationId = outputCorrelationId;
            this.OutputResourceId = outputResourceId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Activity" /> class.
        /// </summary>
        /// <param name="activityName">Name of the activity.</param>
        /// <param name="parentActivity">The parent activity.</param>
        /// <param name="inheritProperties">if set to <c>true</c> [interit properties].</param>
        /// <param name="scenario">Scenario of the activity.</param>
        /// <param name="component">Component of the activity.</param>
        public BasicActivity(
            string activityName, IActivity? parentActivity, bool inheritProperties,
            string? scenario, string? component, string? correlationId, string? inputResourceId, string? outputCorrelationId, string? outputResourceId)
            : this(Guid.NewGuid(), activityName, parentActivity, inheritProperties, scenario, component, correlationId, inputResourceId, outputCorrelationId, outputResourceId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Activity" /> class.
        /// </summary>
        /// <param name="activityId">The activity identifier.</param>
        /// <param name="activityName">Name of the activity.</param>
        /// <param name="parentActivity">The parent activity.</param>
        /// <param name="inheritProperties">if set to <c>true</c> [interit properties].</param>
        /// <param name="scenario">Scenario of the activity.</param>
        /// <param name="component">Component of the activity.</param>
        public BasicActivity(Guid activityId,
            string activityName, IActivity? parentActivity, bool inheritProperties,
            string? scenario, string? component, string? correlationId, string? inputResourceId, string? outputCorrelationId, string? outputResourceId)
        {
            creationStopWatchTimeStamp = Stopwatch.GetTimestamp();
            CreationDateTime = DateTime.UtcNow;

            // also make sure reading activity properties does not produce nulls
            this.ActivityId = activityId;
            this.ActivityName = activityName ?? Null.ActivityName;

            this.parentActivity = parentActivity;
            this.Context = this.ParentActivity.Context;
            this.TopLevelActivity = this.ParentActivity.TopLevelActivity;

            // inherit parent scenario by default
            this.Scenario = scenario ?? this.ParentActivity.Scenario;
            this.Component = component ?? this.ParentActivity.Component;
            this.CorrelationId = correlationId ?? this.ParentActivity.CorrelationId;
            this.InputResourceId = inputResourceId ?? this.ParentActivity.InputResourceId;
            this.OutputCorrelationId = outputCorrelationId ?? this.ParentActivity.OutputCorrelationId;
            this.OutputResourceId = outputResourceId ?? this.ParentActivity.OutputResourceId;

            // inherit parent properties
            if (inheritProperties && parentActivity != null)
            {
                foreach (var kv in parentActivity.GetProperties(true))
                {
                    this.Properties[kv.Key] = parentActivity.Properties[kv.Key];
                }
            }
        }

        #endregion

        #region IActivity Methods

        /// <summary>
        /// Starts or restarts measuring elapsed time for an activity
        /// </summary>
        public void Start()
        {
            startStopWatchTimeStamp = Stopwatch.GetTimestamp();
            StartDateTime = DateTime.UtcNow;
        }

        public virtual ReadOnlySpan<char> ToPropertyStringSpan(int lengthLimit)
        {
            return ToPropertyStringSpan(Properties, null, lengthLimit);
        }

        public static ReadOnlySpan<char> ToPropertyStringSpan(
            ICollection<KeyValuePair<string, object?>> keyValuePairs, 
            ISet<string>? excludeKeys,
            int lengthLimit)
        {
            if (keyValuePairs == null || keyValuePairs.Count == 0)
            {
                return NoneString;
            }

            lengthLimit = Math.Max(lengthLimit, 3);
            var charArray = PropertyBagCharArray.Value;
            if (charArray == null || charArray.Length < lengthLimit)
            {
                charArray = new char[lengthLimit];
                PropertyBagCharArray.Value = charArray;
            }
            var length = 0;
            foreach (var kvp in keyValuePairs)
            {
                var key = kvp.Key;
                if (excludeKeys != null && excludeKeys.Contains(key))
                {
                    continue;
                }

                if (!TryAddToCharArray(charArray, "* ", ref length) ||
                    !TryAddToCharArray(charArray, kvp.Key, ref length) ||
                    !TryAddToCharArray(charArray, "=[", ref length) ||
                    !TryAddToCharArray(charArray, kvp.Value?.ToString() ?? "null", ref length) ||
                    !TryAddToCharArray(charArray, "]\n", ref length))
                {
                    break;
                }
            }
            return length == 0 ? NoneString: charArray.AsSpan(0, length);
        }

        /// <summary>Sets property to a given key</summary>
        /// <param name="key">Property name</param>
        /// <param name="value">Property value</param>
        /// <param name="inherit">if set to <c>true</c> [inherit].</param>
        /// <returns>Resulting value for the key</returns>
        public object? SetProperty(string key, object? value, bool inherit = true)
        {
            if (!inherit)
            {
                if (this.nonInheritableProperties == null)
                {
                    this.nonInheritableProperties = new
                        HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                this.nonInheritableProperties.Add(key);
            }

            return this.Properties[key] = value;
        }

        /// <summary>Gets the properties.</summary>
        /// <param name="skipNonInherited">if set to <c>true</c> [skip non inherited].</param>
        public IEnumerable<KeyValuePair<string, object?>> GetProperties(bool skipNonInherited = false)
        {
            foreach (var kv in this.Properties)
            {
                if (skipNonInherited &&
                    nonInheritableProperties != null &&
                    nonInheritableProperties.Contains(kv.Key))
                {
                    continue;
                }

                yield return kv;
            }
        }

        #endregion 

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "<[{0}], id [{1}]; parent => [{2}], id [{3}]>",
                this.ActivityName,
                this.ActivityId,
                this.ParentActivity.ActivityName,
                this.ParentActivity.ActivityId);
        }

        /// <summary>
        /// Checks if activity is null or none
        /// </summary>
        public static bool IsNullOrNone(IActivity activity)
        {
            return activity == Null || activity.ActivityId.Equals(Guid.Empty);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool TryAddToCharArray(char[] chars, string input, ref int length)
        {
            const string Ellipses = "...";
            if (length + input.Length > chars.Length)
            {
                var charsToFill = chars.Length - length - Ellipses.Length;
                if (charsToFill > 0)
                {
                    input.CopyTo(0, chars, length, charsToFill);
                }
                Ellipses.CopyTo(0, chars, chars.Length - Ellipses.Length, Ellipses.Length);
                length = chars.Length;
                return false;
            }
            input.CopyTo(0, chars, length, input.Length);
            length += input.Length;
            return true;
        }
    }
}