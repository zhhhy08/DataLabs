namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using System;

    public class CacheClientReadException<T> : Exception
    {
        public CacheClientReadResult<T> ReadResult { get; }

        public CacheClientReadException(string message,
            CacheClientReadResult<T> cacheClientReadResult) : base(message)
        {
            ReadResult = cacheClientReadResult;
        }
    }
}
