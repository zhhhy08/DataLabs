// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WindowsAzure.Governance.DataLabs.GarnetServer
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Garnet server entry point
    /// </summary>
    [ExcludeFromCodeCoverage]
    class Program
    {
        static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // 1. Initialize Logger first so that other can use logger
            using var loggerFactory = DataLabLoggerFactory.GetLoggerFactory();

            // 2.  Initialize ConfigMapUtil so that other can use configuration
            ConfigMapUtil.Reset();
            ConfigMapUtil.Initialize(builder.Configuration);

            // 3. Initialize Tracer
            Tracer.CreateDataLabsTracerProvider(GarnetConstants.GarnetTraceSource);

            // 4. Initialize Meter
            MetricLogger.CreateDataLabsMeterProvider(GarnetConstants.GarnetMeter);

            GarnetServerWorker.CommandArgs = args;

            var serviceCollection = builder.Services.AddHostedService<GarnetServerWorker>();

            using var host = builder.Build();
            host.Run();
        }
    }
}
