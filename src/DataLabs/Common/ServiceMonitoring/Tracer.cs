// <copyright file="Tracer.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring
{
    using OpenTelemetry;
    using OpenTelemetry.Exporter;
    using OpenTelemetry.Exporter.Geneva;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Trace;

    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using System.Collections.Generic;

    public class Tracer
    {
        public static bool USING_EVENTLIST_COLUMN = true;

        // Code to support previous deployment code. Will remove in future iterations.
        public static TracerProvider? CreateDataLabsTracerProvider(params string[] sourceNames)
        {
            return CreateTracerProvider(MonitoringConstants.OTLP_EXPORTER_TYPE_VALUE, 
                MonitoringConstants.MDSD_DATALABS_ENDPOINT_VALUE, sourceNames);
        }

        public static TracerProvider? CreatePartnerTracerProvider(params string[] sourceNames)
        {
            return CreateTracerProvider(MonitoringConstants.OTLP_EXPORTER_TYPE_VALUE,
                MonitoringConstants.MDSD_PARTNER_ENDPOINT_VALUE, sourceNames);
        }

        private static TracerProvider? CreateTracerProvider(string exporterType, string endpoint, 
            string[] sourceNames)
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            bool includeGrpcClient = ConfigMapUtil.Configuration.GetValue<bool>(
                SolutionConstants.EnableGrpcTrace, false);
            bool includeHttpClient = ConfigMapUtil.Configuration.GetValue<bool>(
                SolutionConstants.EnableHttpClientTrace, false);
            bool includeAzureSDKActivity = ConfigMapUtil.Configuration.GetValue<bool>(
                SolutionConstants.EnableAzureSDKActivity, false);

            var providerBuilder = Sdk.CreateTracerProviderBuilder();
            if (includeHttpClient)
            {
                providerBuilder = providerBuilder.AddHttpClientInstrumentation();
            }
            if (includeGrpcClient)
            {
                providerBuilder = providerBuilder.AddGrpcClientInstrumentation();
            }

            providerBuilder = providerBuilder.SetSampler(new AlwaysOnSampler());
            
            // Add given sources
            if (sourceNames != null && sourceNames.Length > 0) {
                providerBuilder = providerBuilder.AddSource(sourceNames);
            }

            if (includeAzureSDKActivity)
            {
                AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
                providerBuilder = providerBuilder.AddSource("Azure.*"); // Collect all traces from Azure SDKs
            }

            providerBuilder = providerBuilder.SetErrorStatusOnException();

            if (exporterType == "GENEVA")
            {
                USING_EVENTLIST_COLUMN = true;
                Console.WriteLine("Geneva Tracer");
                Console.WriteLine(endpoint);
                providerBuilder = providerBuilder.AddGenevaTraceExporter(options =>
                {
                    options.ConnectionString = endpoint;

                    // Adding Prepopulated Fields
                    options.PrepopulatedFields = new Dictionary<string, object>
                    {
                        [SolutionConstants.SERVICE] = MonitoringConstants.SERVICE,
                        [SolutionConstants.POD_NAME] = MonitoringConstants.POD_NAME,
                        [SolutionConstants.BUILD_VERSION] = MonitoringConstants.BUILD_VERSION
                    };
                });
            }
            else if (exporterType == "OTLP")
            {
                USING_EVENTLIST_COLUMN = false;
                providerBuilder = providerBuilder.AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri(endpoint);
                    opt.Protocol = OtlpExportProtocol.Grpc;
                });
            }
            else
            {
                USING_EVENTLIST_COLUMN = true;
                providerBuilder = providerBuilder.AddConsoleExporter();
            }

            return providerBuilder.Build();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ActivityTraceId CreateActivityTraceId()
        {
            Func<ActivityTraceId>? traceIdGenerator = Activity.TraceIdGenerator;
            return traceIdGenerator == null ? ActivityTraceId.CreateRandom() : traceIdGenerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ActivitySpanId CreateSpanId()
        {
            return ActivitySpanId.CreateRandom();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ActivityContext ConvertToActivityContext(string parentId)
        {
            return ActivityContext.Parse(parentId, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ActivityContext CreateNewActivityContext()
        {
            var spandId = CreateSpanId();
            var traceId = CreateActivityTraceId();
            return new ActivityContext(traceId, spandId, ActivityTraceFlags.None);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ConvertToActivityId(ActivityContext activityContext)
        {
            return "00-" + activityContext.TraceId.ToHexString() + "-" + activityContext.SpanId.ToHexString() + "-01";
        }
    }
}
