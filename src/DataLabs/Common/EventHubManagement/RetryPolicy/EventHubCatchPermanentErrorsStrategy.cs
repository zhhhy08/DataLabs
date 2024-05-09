namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using System;
    using System.Text.RegularExpressions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy;

    public class EventHubCatchPermanentErrorsStrategy : IErrorStrategy
    {
        #region Constants

        private const string OutOfDateCheckpointingPattern =
            @"Ignoring out of date checkpoint with offset (?<offset>\d+)/sequence number (?<sequenceNumber>\d+) because current persisted checkpoint has higher offset (?<offset>\d+)/sequence number (?<sequenceNumber>\d+)";

        #endregion

        #region Members

        private readonly static Regex OutOfDateCheckpointingRegex =
            new Regex(OutOfDateCheckpointingPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion

        /// <summary>
        /// Determine whether an exception is transient. In the context of event hubs store, there is 
        /// a type of exception which is permanent for sure so we should not retry.
        /// </summary>
        public bool IsTransientError(Exception exception)
        {
            if (!(exception is ArgumentOutOfRangeException argumentException))
            {
                return true;
            }

            return argumentException.ParamName == "offset/sequenceNumber" &&
                OutOfDateCheckpointingRegex.Matches(exception.Message).Count > 0 ? false : true;
        }
    }
}