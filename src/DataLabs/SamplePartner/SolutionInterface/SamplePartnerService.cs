using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;

namespace SamplePartnerNuget.SolutionInterface
{
    public class SamplePartnerService : IDataLabsInterface
    {
        /* OpenTelemetry Trace */
        public const string PartnerActivitySourceName = "SamplePartner";
        public static readonly ActivitySource PartnerActivitySource = new ActivitySource(PartnerActivitySourceName);

        /* OpenTelemetry Metric */
        public const string PartnerMeterName = "SamplePartner";
        public static readonly Meter PartnerMeter = new(PartnerMeterName, "1.0");
        private static readonly Histogram<int> DurationMetric = PartnerMeter.CreateHistogram<int>("Duration");
        private static readonly Counter<long> RequestCounter = PartnerMeter.CreateCounter<long>("Request");

        public const string CustomerMeterName = "SamplePartnerCustomer";
        public static readonly Meter CustomerMeter = new(CustomerMeterName);
        private static readonly Counter<long> CustomerCounter = CustomerMeter.CreateCounter<long>("Customer");

        /* For Configuration */
        public const string TestConfigKey = "TestKey";
        public const string TestDelayTimeKey = "TestDelayTime";
        public static IConfigurationWithCallBack? _configurationWithCallBack;

        /* For Logger */
        public static ILoggerFactory _loggerFactory;
        private static ILogger _logger;
        private static ILogger _loggerTable;
        public const string SamplePartnerLogTable = "SamplePartnerLogTable";

        /* For Internal Unit Test */
        public ICacheClient CacheClient;
        public IResourceProxyClient ResourceProxyClient;
        public string TestConfigValue;
        public int DelayTime;
        public bool ReturnInternalAttribute;
        public int NumReturnResources = 1;

        public SamplePartnerService()
        {
        }

        public async Task<DataLabsARNV3Response> GetResponseAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("GetResponseAsync is here");
            _loggerTable.LogInformation("Logging in SamplePartnerLog");

            using (Activity activity = PartnerActivitySource.StartActivity("GetResponseAsync", ActivityKind.Consumer, request.TraceId))
            {
                activity?.SetTag("RequestTime", request.RequestTime);
                activity?.SetTag("RetryCount", request.RetryCount);
                activity?.SetTag("CorrelationId", request.CorrelationId);
                activity?.SetTag("SamplePartnerTest", "SomethingRandom");

                // Add Request Counter
                RequestCounter.Add(1);

                // Mesure duration
                var stopWatchStartTime = Stopwatch.GetTimestamp();

                // For this Sample Partner, just put some delay here
                // this is just for demo purpose to how to use the configuration's hotconfig update (change DelayTime)
                if (DelayTime > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(DelayTime), cancellationToken).ConfigureAwait(false);
                }

                var outputData = SamplePartnerUtils.CloneEventGridNotificationWithNewId(request.InputResource, NumReturnResources);

                var duration = Stopwatch.GetElapsedTime(stopWatchStartTime).TotalMilliseconds;

                // Report Duration Metric
                DurationMetric.Record((int)duration);

                var successResponse = new DataLabsARNV3SuccessResponse(
                    outputData,
                    DateTimeOffset.UtcNow,
                    null);

                IDictionary<string, string>? attributes = null;
                if (ReturnInternalAttribute)
                {
                    attributes ??= new Dictionary<string, string>();
                    attributes.Add(DataLabsARNV3Response.AttributeKey_INTERNAL, true.ToString());
                }

                return new DataLabsARNV3Response(
                    DateTimeOffset.UtcNow,
                    outputData.Data.Resources[0].CorrelationId,
                    successResponse,
                    null,
                    attributes);
            }
        }

        public async IAsyncEnumerable<DataLabsARNV3Response> GetResponsesAsync(DataLabsARNV3Request request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var response = await GetResponseAsync(request, cancellationToken).ConfigureAwait(false);
            yield return response;
        }

        public void SetConfiguration(IConfigurationWithCallBack configurationWithCallBack)
        {
            _configurationWithCallBack = configurationWithCallBack;
            TestConfigValue = _configurationWithCallBack.GetValue<string>(TestConfigKey);
            DelayTime = _configurationWithCallBack.GetValueWithCallBack<int>(TestDelayTimeKey, UpdateDelay, 0);
        }

        public void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<SamplePartnerService>();
            _loggerTable = loggerFactory.CreateLogger("SamplePartnerLog");
        }

        private Task UpdateDelay(int newDelay)
        {
            var oldDelay = DelayTime;
            if (newDelay == oldDelay)
            {
                return Task.CompletedTask;
            }

            Interlocked.Exchange(ref DelayTime, newDelay);
            
            _logger.LogWarning("Delay is changed, Old: {oldVal}, New: {newVal}", oldDelay, newDelay);
            return Task.CompletedTask;
        }

        public List<string> GetTraceSourceNames()
        {
            var list = new List<string>(1);
            list.Add(PartnerActivitySourceName);
            return list;
        }

        public List<string> GetMeterNames()
        {
            var list = new List<string>(1);
            list.Add(PartnerMeterName);
            return list;
        }

        public List<string> GetCustomerMeterNames()
        {
            return new List<string> { CustomerMeterName };
        }

        public Dictionary<string, string> GetLoggerTableNames()
        {
            return new Dictionary<string, string>
            {
                [SamplePartnerLogTable] = SamplePartnerLogTable
            };
        }

        public void SetCacheClient(ICacheClient cacheClient)
        {
            CacheClient = cacheClient;
        }

        public void SetResourceProxyClient(IResourceProxyClient resourceProxyClient)
        {
            ResourceProxyClient = resourceProxyClient;
        }
    }
}
