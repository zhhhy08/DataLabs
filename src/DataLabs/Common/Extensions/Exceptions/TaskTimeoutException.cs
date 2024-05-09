namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Task timeout exception
    /// </summary>
    /// <seealso cref="System.Exception" />
    [Serializable]
    public class TaskTimeoutException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TaskTimeoutException"/> class.
        /// </summary>
        public TaskTimeoutException()
            : base("The task timed out.")
        {
        }
    }
}