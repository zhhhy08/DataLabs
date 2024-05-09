namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using Microsoft.Extensions.Configuration;

    public class IOCacheClient : CacheClient
    {
        public IOCacheClient(IConfiguration configuration, IConnectionMultiplexerWrapperFactory connectionMultiplexerWrapperFactory) : 
            base(dataLabsCacheType: DataLabsCacheType.INPUT_OUTPUT_CACHE, 
                configuration: configuration, 
                connectionMultiplexerWrapperFactory: connectionMultiplexerWrapperFactory, 
                preCreateConnections: true)
        {
        }
    }
}
