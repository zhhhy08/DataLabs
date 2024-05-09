namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    using System;

    /// <summary>
    /// CatchAllErrorStrategy
    /// </summary>
    public class CatchAllErrorStrategy : IErrorStrategy
    {
        /// <summary>
        /// Determines whether [is transient error] [the specified exception].
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <returns>
        ///   <c>true</c> if [is transient error] [the specified exception]; otherwise, <c>false</c>.
        /// </returns>
        public bool IsTransientError(Exception exception)
        {
            return true;
        }
    }
}