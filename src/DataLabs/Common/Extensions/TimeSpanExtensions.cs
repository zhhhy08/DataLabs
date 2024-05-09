// ReSharper disable once CheckNamespace
using System;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    /// <summary>
    /// TimeSpan extensions
    /// </summary>
    public static class TimeSpanExtensions
    {
        /// <summary>
        /// Gets the rounded milliseconds.
        /// </summary>
        /// <param name="timeSpan">The time span.</param>
        public static int RoundedMilliseconds(this TimeSpan timeSpan)
        {
            return (int)Math.Round(timeSpan.TotalMilliseconds);
        }

        /// <summary>
        /// Multiplies timespan by a scalar vlaue.
        /// </summary>
        /// <param name="timeSpan">The time span.</param>
        /// <param name="value">The value.</param>
        public static TimeSpan MultiplyBy(this TimeSpan timeSpan, double value)
        {
            return TimeSpan.FromMilliseconds(timeSpan.TotalMilliseconds * value);
        }

        /// <summary>
        /// Rounds the time span up to seconds.
        /// </summary>
        /// <param name="timeSpan">The time span.</param>
        public static TimeSpan RoundUpToSeconds(this TimeSpan timeSpan)
        {
            return TimeSpan.FromSeconds(Math.Ceiling(timeSpan.TotalSeconds));
        }
    }
}