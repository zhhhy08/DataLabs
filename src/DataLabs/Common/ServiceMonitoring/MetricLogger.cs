// <copyright file="MonitoringUtils.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring
{
    using global::OpenTelemetry;
    using global::OpenTelemetry.Exporter;
    using global::OpenTelemetry.Exporter.Geneva;
    using global::OpenTelemetry.Metrics;
    using global::OpenTelemetry.Trace;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using System.Linq;

    [ExcludeFromCodeCoverage]
    public class MetricLogger
    {
        public const string Debug_DoNotUseMeters = "Debug_DoNotUseMeters";
        public const string CommonMeterName = "ARG.DataLabs.Common";
        public static readonly Meter CommonMeter = new(CommonMeterName, "1.0");
        
        public const string PartnerAccountMeterName = "ActivityTracing.Partner";
        public static readonly Meter PartnerAccountMeter = new(PartnerAccountMeterName, "1.0");

        public static void CreateDataLabsMeterProvider(params string[] sourceNames)
        {
            sourceNames = sourceNames.Concat(new[] { CommonMeterName }).ToArray();
            CreateMeterProvider(MonitoringConstants.OTLP_EXPORTER_TYPE_VALUE,
                MonitoringConstants.MDM_DATALABS_ENDPOINT_VALUE, sourceNames);
        }

        public static void CreatePartnerMeterProvider(params string[] sourceNames)
        {
            sourceNames = sourceNames.Concat(new[] { PartnerAccountMeterName }).ToArray();
            CreateMeterProvider(MonitoringConstants.OTLP_EXPORTER_TYPE_VALUE,
                MonitoringConstants.MDM_PARTNER_ENDPOINT_VALUE, sourceNames);
        }

        public static void CreateCustomerMeterProvider(params string[] sourceNames)
        {
            CreateMeterProvider(MonitoringConstants.OTLP_EXPORTER_TYPE_VALUE,
                MonitoringConstants.MDM_CUSTOMER_ENDPOINT_VALUE, sourceNames);
        }

        private static MeterProvider? CreateMeterProvider(
            string exporterType, string endpoint, string[] meterNames)
        {
            // For local debugging
            if (!ConfigMapUtil.RunningInContainer)
            {
                var noUseMeters = ConfigMapUtil.Configuration.GetValue<bool>(Debug_DoNotUseMeters, true);
                if (noUseMeters)
                {
                    // By default, don't use metric in console because it is too noisy
                    return null;
                }
            }

            bool includeRunTimeMetrics = ConfigMapUtil.Configuration.GetValue<bool>(
                SolutionConstants.IncludeRunTimeMetrics, false);
            bool includeHttpClientMetrics = ConfigMapUtil.Configuration.GetValue<bool>(
                SolutionConstants.IncludeHttpClientMetrics, false);

            var providerBuilder = Sdk.CreateMeterProviderBuilder();
            if (includeRunTimeMetrics)
            {
                providerBuilder = providerBuilder.AddRuntimeInstrumentation();
            }

            if (includeHttpClientMetrics)
            {
                providerBuilder = providerBuilder.AddHttpClientInstrumentation();
            }
            
            if (meterNames != null)
            {
                providerBuilder = providerBuilder.AddMeter(meterNames);
            }

            // TODO
            // Revisit, this need to be changed when there are many metrics. 
            /*
            providerBuilder.SetMaxMetricStreams(1000); // default 1000 based on the doc
            providerBuilder.SetMaxMetricPointsPerMetricStream(2000) // default 2000 based on the doc
            */

            if (exporterType == "GENEVA")
            {
                Console.WriteLine("Geneva Meter");
                Console.WriteLine(endpoint);

                var dimensions = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(MonitoringConstants.REGION))
                {
                    dimensions[SolutionConstants.REGION] = MonitoringConstants.REGION;
                }
                if (!string.IsNullOrEmpty(MonitoringConstants.SCALE_UNIT))
                {
                    dimensions[SolutionConstants.SCALE_UNIT] = MonitoringConstants.SCALE_UNIT;
                }
                if (!string.IsNullOrEmpty(MonitoringConstants.SERVICE))
                {
                    dimensions[SolutionConstants.SERVICE] = MonitoringConstants.SERVICE;
                }
                if (!string.IsNullOrEmpty(MonitoringConstants.POD_NAME))
                {
                    dimensions[SolutionConstants.POD_NAME] = MonitoringConstants.POD_NAME;
                }
                if (!string.IsNullOrEmpty(MonitoringConstants.NODE_NAME))
                {
                    dimensions[SolutionConstants.NODE_NAME] = MonitoringConstants.NODE_NAME;
                }
                if (!string.IsNullOrEmpty(MonitoringConstants.BUILD_VERSION))
                {
                    dimensions[SolutionConstants.BUILD_VERSION] = MonitoringConstants.BUILD_VERSION;
                }

                providerBuilder = providerBuilder.AddGenevaMetricExporter(options =>
                {
                    options.ConnectionString = endpoint;
                    if (dimensions.Count > 0)
                    {
                        options.PrepopulatedMetricDimensions = dimensions;
                    };
                });
            }
            else if (exporterType == "OTLP")
            {
                providerBuilder = providerBuilder
                .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri(endpoint);
                    opt.Protocol = OtlpExportProtocol.Grpc;
                });
            }
            else
            {
                providerBuilder = providerBuilder.AddConsoleExporter();
            }
            
            return providerBuilder.Build();
        }
    }
}
