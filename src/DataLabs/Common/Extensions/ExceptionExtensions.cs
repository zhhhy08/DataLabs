namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    using System;

    /// <summary>
    /// ExceptionExtensions.
    /// </summary>
    public static class ExceptionExtensions
    {
        public static bool IsException<TException>(this Exception ex)
        {
            var exception = ex;
            while (exception != null)
            {
                if (exception is TException)
                {
                    return true;
                }

                exception = exception.InnerException;
            }

            return false;
        }

        public static Exception? GetFirstInnerException(this Exception ex)
        {
            if (ex == null)
            {
                return null;
            }

            var exception = ex;
            if (exception is AggregateException aggregateException && aggregateException.InnerExceptions?.Count > 0)
            {
                return aggregateException.InnerExceptions[0];
            }
            return exception.InnerException ?? exception;
        }
    }
}
