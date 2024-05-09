namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// LockNotReleasedException
    /// </summary>
    /// <seealso cref="Exception" />
    [Serializable]
    public class LockNotReleasedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LockNotReleasedException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public LockNotReleasedException(string message) : base(message)
        {
        }
    }
}
