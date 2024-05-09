namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using Microsoft.Extensions.Configuration;

    /* 
     * PartnerCacheClient is a class that provides a client for Partner's interacting with a cache. 
     * It is used to read and write data to the cache.
     */
    public class PartnerCacheClient : CacheClient
    {
        public PartnerCacheClient(IConfiguration configuration, IConnectionMultiplexerWrapperFactory connectionMultiplexerWrapperFactory) : 
            base(dataLabsCacheType: DataLabsCacheType.PARTNER_CACHE, 
                configuration: configuration, 
                connectionMultiplexerWrapperFactory: connectionMultiplexerWrapperFactory, 
                preCreateConnections: true)
        {
        }
    }
}
