namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;

    public interface IDataLabsInterface
    {
        /*
         * 
         * During the initialization, the following methods will be called in order.
         * GetTraceSourceNames and GetMeterNames are called to get OpenTelemetry AcitivtySource Names and Metric Meter Names
         * 
         * SetLoggerFactory, SetConfiguration, and SetResourceProxyClient will be called one time.
         * Partner can use the loggerFactory to create logger.
         * Partner can use the configuration to get the configuration value and register the callback.
         * 
         * 1. GetMeterNames will be called first
         * 2. GetCustomerMeterNames will be called next if applicable.
         * 3. GetTraceSourceNames will be called next.
         * 4. GetLoggerTableNames will be called next.
         * 5. SetLoggerFactory will be called next.
         * 6. SetConfiguration will be called next.
         * 7. SetCacheClient will be called next if applicable. SetCacheClient will not be called when there is no Partner Cache
         * 8. SetResourceProxyClient will be called next.
         * 
         * Then each request will be processed by GetResponseAsync or GetResponsesAsync.
         * Which Method to be called depends on the Onboarding configuration. 
         * If Partner need one request and one response. then GetResponseAsync will be called.
         * If Partner need one request and multiple responses. then GetResponsesAsync will be called.
         * 
         */

        public List<string> GetTraceSourceNames();
        public List<string> GetMeterNames();
        public List<string> GetCustomerMeterNames();
        // key: categoryName (Set in LoggerFactory.CreateLogger), value: Geneva Table Name
        public Dictionary<string, string> GetLoggerTableNames();
        public void SetLoggerFactory(ILoggerFactory loggerFactory);
        public void SetConfiguration(IConfigurationWithCallBack configurationWithCallBack);
        public void SetCacheClient(ICacheClient? cacheClient) { }
        public void SetResourceProxyClient(IResourceProxyClient resourceProxyClient);
        public Task<DataLabsARNV3Response> GetResponseAsync(DataLabsARNV3Request request, CancellationToken cancellationToken);
        public IAsyncEnumerable<DataLabsARNV3Response> GetResponsesAsync(DataLabsARNV3Request request, CancellationToken cancellationToken);
    }
}
