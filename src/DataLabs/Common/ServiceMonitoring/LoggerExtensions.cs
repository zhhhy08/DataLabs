// <copyright file="LoggerExtensions.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring
{
    using global::OpenTelemetry.Exporter;
    using global::OpenTelemetry.Logs;

    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;

    public static class LoggerExtensions
    {
        public static ILoggingBuilder AddDataLabsMonitoringLogger(this ILoggingBuilder builder)
        {
            return builder.AddMonitoringLogger(MonitoringConstants.OTLP_EXPORTER_TYPE_VALUE,
                MonitoringConstants.MDSD_DATALABS_ENDPOINT_VALUE, MonitoringConstants.DataLabsLoggerTableMappings);
        }

        public static ILoggingBuilder AddPartnerMonitoringLogger(this ILoggingBuilder builder,
            Dictionary<string, string> partnerLoggerTableMappings)
        {
            return builder.AddMonitoringLogger(MonitoringConstants.OTLP_EXPORTER_TYPE_VALUE,
                MonitoringConstants.MDSD_PARTNER_ENDPOINT_VALUE, partnerLoggerTableMappings);
        }

        private static ILoggingBuilder AddMonitoringLogger(
            this ILoggingBuilder builder, string exporterType, string endpoint, Dictionary<string, string> tableMappings)
        {
            LogLevel logLevel = LogLevel.Information;
            var envLevel = Environment.GetEnvironmentVariable(
                SolutionConstants.LOGGER_MIN_LOG_LEVEL);
            if (envLevel != null)
            {
                Enum.TryParse(envLevel, out logLevel);
            }

            builder.SetMinimumLevel(logLevel);
            builder.ClearProviders();
            builder.AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddProcessor(new MissingTraceIdLogProcessor());

                if (exporterType == "GENEVA")
                {
                    Console.WriteLine($"Geneva Logger Endpoint: {endpoint}");
                    // Geneva Exporter
                    loggerOptions.AddGenevaLogExporter(exporterOptions =>
                    {
                        exporterOptions.ConnectionString = endpoint;

                        // Adding Prepopulated Fields
                        exporterOptions.PrepopulatedFields = new Dictionary<string, object>
                        {
                            [SolutionConstants.SERVICE] = MonitoringConstants.SERVICE,
                            [SolutionConstants.POD_NAME] = MonitoringConstants.POD_NAME,
                            [SolutionConstants.BUILD_VERSION] = MonitoringConstants.BUILD_VERSION
                        };

                        // Adding Activity Tables
                        if (tableMappings != null)
                        {
                            exporterOptions.TableNameMappings = tableMappings;
                        }
                    });
                }
                else if (exporterType == "OTLP")
                {
                    // OTLP Exporter.
                    loggerOptions.AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(endpoint);
                        otlp.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
                else
                {
                    // Console Logger.
                    loggerOptions.AddConsoleExporter();
                }
            });

            return builder;
        }
    }
}
