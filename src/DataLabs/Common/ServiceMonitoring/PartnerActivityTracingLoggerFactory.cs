// <copyright file="DataLabLoggerFactory.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;

    [ExcludeFromCodeCoverage]
    public class PartnerActivityTracingLoggerFactory
    {
        private static ILoggerFactory? _partnerActivityTracingLoggerFactory;
        private static bool _hasPartnerEndpoint = !string.IsNullOrEmpty(MonitoringConstants.MDSD_PARTNER_ENDPOINT_VALUE);

        static PartnerActivityTracingLoggerFactory()
        {
            Initialize();
        }

        private static void Initialize()
        {
            // Non-partner pod scenarios
            if (!_hasPartnerEndpoint)
            {
                Console.WriteLine($"OTLP PartnerEndpoint not defined, PartnerActivityTracingLoggerFactory will not be initialized");
                _partnerActivityTracingLoggerFactory = null;
                return;
            }
            // Partner pod scenarios
            try
            {
                Console.WriteLine($"Initializing PartnerActivityTracingLoggerFactory");
                var partnerActivityTracingLoggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddPartnerMonitoringLogger(MonitoringConstants.DataLabsLoggerTableMappings);
                });
                _partnerActivityTracingLoggerFactory = partnerActivityTracingLoggerFactory;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PartnerActivityTracingLoggerFactory Error: {ex.Message}\n {ex.StackTrace}");
                throw;
            }
        }

        // For Unit Testing
        public static void ReInitialize()
        {
            _hasPartnerEndpoint = !string.IsNullOrEmpty(ConfigMapUtil.Configuration[SolutionConstants.MDSD_PARTNER_ENDPOINT]);
            Console.WriteLine($"PartnerActivityTracingLoggerFactory.Reinitialize: hasPartnerEndpoint={_hasPartnerEndpoint}");
            Initialize();
        }

        public static ILoggerFactory? GetLoggerFactory() => _partnerActivityTracingLoggerFactory;

        public static ILogger<T>? CreateLogger<T>()
        {
            if (_partnerActivityTracingLoggerFactory == null)
            {
                Console.WriteLine("PartnerLoggerFactory is null");
                return null;
            }
            return _partnerActivityTracingLoggerFactory.CreateLogger<T>();
        }

        public static ILogger? CreateLogger(string name)
        {
            if (_partnerActivityTracingLoggerFactory == null)
            {
                Console.WriteLine("PartnerLoggerFactory is null");
                return null;
            }
            return _partnerActivityTracingLoggerFactory.CreateLogger(name);
        }
    }
}