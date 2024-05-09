using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
using System.Diagnostics.CodeAnalysis;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
using System.Text;
using System.Threading;

[ExcludeFromCodeCoverage]
internal class Program
{
    private static async Task Main(string[] args)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable(SolutionConstants.IS_DEDICATED_PARTNER_AKS, "true");

        // 1. Initialize Logger first so that other can use logger
        using var loggerFactory = DataLabLoggerFactory.GetLoggerFactory();

        // 2.  Initialize ConfigMapUtil so that other can use configuration
        ConfigMapUtil.Reset();
        var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
        ConfigMapUtil.Initialize(configBuilder);
        SolutionUtils.InitializeProgram(ConfigMapUtil.Configuration, minWorkerThreads: 1000, minCompletionThreads: 1000);

        // 3. Initialize Tracer
        Tracer.CreateDataLabsTracerProvider(Array.Empty<string>());
        // 4. Initialize Meter
        MetricLogger.CreateDataLabsMeterProvider(Array.Empty<string>());

        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(ConfigMapUtil.Configuration);
        services.AddSingleton<IConfigurationWithCallBack>(ConfigMapUtil.Configuration);
        services.AddIOResourceCacheClient();

        var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider.GetService<IConfiguration>()!;
        var connectionMultiplexerWrapperFactory = serviceProvider.GetService<IConnectionMultiplexerWrapperFactory>()!;
        var cacheClient = (IOCacheClient)serviceProvider.GetService<ICacheClient>()!;
        var resourceCacheClient = serviceProvider.GetService<IResourceCacheClient>()!;
        if (!cacheClient.CacheEnabled)
        {
            System.Console.WriteLine("Cache is not enabled");
            return;
        }

        int loop = 1;

        for (int i = 0; i < loop; i++)
        {
            try
            {
                await resourceCacheClient.SetResourceIfGreaterThanAsync(
                    resourceId: "testKey" + i,
                    tenantId: null,
                    dataFormat: ResourceCacheDataFormat.ARMAdmin,
                    resource: Encoding.UTF8.GetBytes("testValue" + i),
                    timeStamp: 0,
                    etag: null,
                    expiry: TimeSpan.FromMinutes(100),
                    cancellationToken: default).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
            }
        }

        for (int i = 0; i < loop; i++)
        {
            try
            {
                var resourceCacheResult = await resourceCacheClient.GetResourceAsync(
                    resourceId: "testKey" + i,
                    tenantId: null,
                    cancellationToken: default).ConfigureAwait(false);


                if (resourceCacheResult.Found)
                {
                    Console.WriteLine("DataFormat: " + resourceCacheResult.DataFormat.FastEnumToString());
                    Console.WriteLine("Value: " + Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));

                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(resourceCacheResult.InsertionTimeStamp);
                    Console.WriteLine("InsertionTime: " + dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                    dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(resourceCacheResult.DataTimeStamp);
                    Console.WriteLine("DataTimeStamp: " + dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
            }
        }

        Thread.Sleep(1000);

        for (int i = 0; i < loop; i++)
        {
            try
            {
                await resourceCacheClient.SetResourceIfGreaterThanAsync(
                    resourceId: "testKey" + i,
                    tenantId: null,
                    dataFormat: ResourceCacheDataFormat.ARMAdmin,
                    resource: Encoding.UTF8.GetBytes("testValue" + i),
                    timeStamp: 0,
                    etag: null,
                    expiry: TimeSpan.FromMinutes(100),
                    cancellationToken: default).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
            }
        }

        for (int i = 0; i < loop; i++)
        {
            try
            {
                var resourceCacheResult = await resourceCacheClient.GetResourceAsync(
                    resourceId: "testKey" + i,
                    tenantId: null,
                    cancellationToken: default).ConfigureAwait(false);


                if (resourceCacheResult.Found)
                {
                    Console.WriteLine("DataFormat: " + resourceCacheResult.DataFormat.FastEnumToString());
                    Console.WriteLine("Value: " + Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));

                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(resourceCacheResult.InsertionTimeStamp);
                    Console.WriteLine("InsertionTime: " + dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                    dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(resourceCacheResult.DataTimeStamp);
                    Console.WriteLine("DataTimeStamp: " + dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
            }
        }

        /*
        var cacheConnectionOption = new CacheConnectionOption(null);

        var cacheNode =  new DataLabCacheNode(
            cachePoolName: "testCache",
            nodeIndex: 0,
            cacheDomain: "cache-namespace.svc.cluster.local",
            port: 3278,
            cacheConnectionOption: cacheConnectionOption);

        System.Console.WriteLine("======= CacheConnectionsOption =======");
        System.Console.WriteLine(JsonConvert.SerializeObject(cacheConnectionOption));
        System.Console.WriteLine();

        while(true)
        {
            var multiplexerWrappers = cacheNode.ConnectionMultiplexers;
            for(int i = 0; i < multiplexerWrappers.Length; i++)
            {
                System.Console.WriteLine("======= " + i + " =======");
                System.Console.WriteLine();

                var multiplexerWrapper = multiplexerWrappers[i];

                var startStopWatchTimeStamp = Stopwatch.GetTimestamp();

                var multiplexer = await multiplexerWrapper.CreateConnectionMultiplexerAsync(activity: null, default).ConfigureAwait(false);
                System.Console.WriteLine();

                int elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                System.Console.WriteLine("Elapsed Time in GetConnectionMultiplexerAsync: " + elapsed);

                if (multiplexer == null)
                {
                    System.Console.WriteLine("\nFailed to get connection Multiplexer");
                    continue;
                }

                var cacheKey = "testKey";
                var cacheValue = "testValue";
                try
                {
                    startStopWatchTimeStamp = Stopwatch.GetTimestamp();

                    var database = multiplexer.GetDatabase();
                    bool result = await database.StringSetAsync(cacheKey, cacheValue).ConfigureAwait(false);
                    if (result)
                    {
                        System.Console.WriteLine("\n========= StringSetAsync SUCCESS  ==========");
                    }
                    else
                    {
                        System.Console.WriteLine("\n========= StringSetAsync ERROR  ==========");
                    }

                    elapsed = (int)Stopwatch.GetElapsedTime(startStopWatchTimeStamp).TotalMilliseconds;
                    System.Console.WriteLine("Elapsed Time in StringSetAsync: " + elapsed);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine(ex);
                }

                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
        }
        */
    }
}
