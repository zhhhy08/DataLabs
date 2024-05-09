using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

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

        // Add ResourceProxyClientProvider
        services.AddResourceProxyClientProvider();

        var serviceProvider = services.BuildServiceProvider();

        var resourceProxyClient = serviceProvider.GetService<IResourceProxyClient>()!;
        GuardHelper.ArgumentNotNull(resourceProxyClient);

        var resourceId = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/jaeTestGroup/providers/Microsoft.Compute/virtualMachines/jaeTestVM1";
        var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

        var request = new DataLabsResourceRequest(
            traceId: string.Empty,
            retryCount: 0,
            correlationId: string.Empty,
            resourceId: resourceId,
            tenantId: tenantId);

        var dataLabsResourceResponse = await resourceProxyClient.GetResourceAsync(
            request: request,
            cancellationToken: default,
            getDeletedResource: true).ConfigureAwait(false);

        if (dataLabsResourceResponse.ErrorResponse != null)
        {
            System.Console.WriteLine("\n=========  ERROR  ==========");

            var errorResponse = dataLabsResourceResponse.ErrorResponse;
            var errorMessage =
                $"ErrorType: {errorResponse.ErrorType}, \n" +
                $"RetryDelayInMilliseconds: {errorResponse.RetryDelayInMilliseconds}, \n" +
                $"HttpStatusCode: {errorResponse.HttpStatusCode}, \n" +
                $"ErrorDescription: {errorResponse.ErrorDescription}, \n" +
                $"FailedComponent: {errorResponse.FailedComponent}";

            System.Console.WriteLine(errorMessage);
        }
        else
        {
            System.Console.WriteLine("\n=========  RESPONSE  ==========");

            var armResource = dataLabsResourceResponse.SuccessARMResponse?.Resource;
            GuardHelper.ArgumentNotNull(armResource);

            string content = SerializationHelper.SerializeToString(armResource);
            System.Console.WriteLine(content);
        }

        System.Console.WriteLine();
        System.Console.WriteLine();
        System.Console.WriteLine("ResponseTime: " + dataLabsResourceResponse.ResponseTime);
        System.Console.WriteLine("DataSource: " + dataLabsResourceResponse.DataSource);
    }
}
