// <copyright file="DataLabLoggerFactory.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Extensions.Logging;
    
    [ExcludeFromCodeCoverage]
    public class DataLabLoggerFactory
    {
        private static ILoggerFactory _loggerFactory;

        static DataLabLoggerFactory()
        {
            try
            {
                var datalabFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddDataLabsMonitoringLogger();
                });
                _loggerFactory = datalabFactory;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoggerFactory Error: {ex.Message}\n {ex.StackTrace}");
                throw;
            }
        }

        public static ILoggerFactory GetLoggerFactory() => _loggerFactory;

        public static ILogger<T> CreateLogger<T>()
        {
            return _loggerFactory.CreateLogger<T>();
        }

        public static ILogger CreateLogger(string name)
        {
            return _loggerFactory.CreateLogger(name);
        }
    }
}