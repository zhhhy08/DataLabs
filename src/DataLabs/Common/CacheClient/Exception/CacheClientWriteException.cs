namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using System;

    public class CacheClientWriteException<T> : Exception
    {
        public CacheClientWriteResult<T> WriteResult { get; }

        public CacheClientWriteException(string message,
            CacheClientWriteResult<T> cacheClientWriteResult) : base(message)
        {
            WriteResult = cacheClientWriteResult;
        }
    }
}
