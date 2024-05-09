namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using StackExchange.Redis;
    using System;

    public interface IConnectionMultiplexerWrapperFactory
    {
        public IConnectionMultiplexerWrapper CreateConnectionMultiplexerWrapper(DataLabCacheNode dataLabCacheNode);
    }
}
