namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ActivityTracing
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using System.Diagnostics.Metrics;

    [TestClass]
    public class DiagnosticTypeTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);
            PartnerActivityTracingLoggerFactory.ReInitialize();
            DiagnosticType.ReInitialize();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public void NoPartnerEndpointTest()
        {
            PartnerActivityTracingLoggerFactory.ReInitialize();
            DiagnosticType.ReInitialize();
            Assert.AreEqual(DiagnosticEndpoint.DataLabs, DiagnosticType.CurrentDefaultDiagnosticTypeName);
            
            // Should print out a failure and default to datalabs
            DiagnosticType.SetDefaultDiagnosticEndpoint(DiagnosticEndpoint.Partner);

            Assert.AreEqual(DiagnosticEndpoint.DataLabs, DiagnosticType.CurrentDefaultDiagnosticTypeName);
        }

        [TestMethod]
        public void NoPartnerEndpointLoggerDefaultTest()
        {
            Assert.AreEqual(DiagnosticEndpoint.DataLabs, DiagnosticType.CurrentDefaultDiagnosticTypeName);

            Assert.AreSame(DiagnosticType.DefaultActivityStartedLogger, DiagnosticType.DataLabsActivityStartedLogger);
            Assert.AreSame(DiagnosticType.DefaultActivityFailedLogger, DiagnosticType.DataLabsActivityFailedLogger);
            Assert.AreSame(DiagnosticType.DefaultActivityCompletedLogger, DiagnosticType.DataLabsActivityCompletedLogger);
            Assert.AreSame(DiagnosticType.DefaultActivityDurationHistogram, DiagnosticType.DataLabsActivityDurationHistogram);
        }

        [TestMethod]
        public void PartnerEndpointExistsTest()
        {
            ConfigMapUtil.Configuration[SolutionConstants.MDSD_PARTNER_ENDPOINT] = "DUMMY";
            PartnerActivityTracingLoggerFactory.ReInitialize();
            DiagnosticType.ReInitialize();

            Assert.AreEqual(DiagnosticEndpoint.Partner, DiagnosticType.CurrentDefaultDiagnosticTypeName);
        }

        [TestMethod]
        public void PartnerEndpointSwapTest()
        {
            ConfigMapUtil.Configuration[SolutionConstants.MDSD_PARTNER_ENDPOINT] = "DUMMY";
            PartnerActivityTracingLoggerFactory.ReInitialize();
            DiagnosticType.ReInitialize();

            DiagnosticType.SetDefaultDiagnosticEndpoint(DiagnosticEndpoint.DataLabs);
            Assert.AreEqual(DiagnosticEndpoint.DataLabs, DiagnosticType.CurrentDefaultDiagnosticTypeName);
        }

        [TestMethod]
        public void SwitchDiagnosticEndpointChangesLoggersAndMetrics()
        {
            ConfigMapUtil.Configuration[SolutionConstants.MDSD_PARTNER_ENDPOINT] = "DUMMY";
            PartnerActivityTracingLoggerFactory.ReInitialize();
            DiagnosticType.ReInitialize();

            DiagnosticType.SetDefaultDiagnosticEndpoint(DiagnosticEndpoint.DataLabs);
            Assert.AreEqual(DiagnosticEndpoint.DataLabs, DiagnosticType.CurrentDefaultDiagnosticTypeName);

            ILogger dataLabsActivityStartedLogger = DiagnosticType.DefaultActivityStartedLogger;
            ILogger dataLabsActivityFailedLogger = DiagnosticType.DefaultActivityFailedLogger;
            ILogger dataLabsActivityCompletedLogger = DiagnosticType.DefaultActivityCompletedLogger;
            Histogram<double> dataLabsActivityDurationHistogram = DiagnosticType.DefaultActivityDurationHistogram;

            DiagnosticType.SetDefaultDiagnosticEndpoint(DiagnosticEndpoint.Partner);
            Assert.AreEqual(DiagnosticEndpoint.Partner, DiagnosticType.CurrentDefaultDiagnosticTypeName);

            ILogger partnerActivityStartedLogger = DiagnosticType.DefaultActivityStartedLogger;
            ILogger partnerActivityFailedLogger = DiagnosticType.DefaultActivityFailedLogger;
            ILogger partnerActivityCompletedLogger = DiagnosticType.DefaultActivityCompletedLogger;
            Histogram<double> partnerActivityDurationHistogram = DiagnosticType.DefaultActivityDurationHistogram;

            Assert.AreNotSame(dataLabsActivityStartedLogger, partnerActivityStartedLogger);
            Assert.AreNotSame(dataLabsActivityFailedLogger, partnerActivityFailedLogger);
            Assert.AreNotSame(dataLabsActivityCompletedLogger, partnerActivityCompletedLogger);
            Assert.AreNotSame(dataLabsActivityDurationHistogram, partnerActivityDurationHistogram);
        }

        [DataTestMethod]
        [DataRow(DiagnosticEndpoint.DataLabs, DiagnosticEndpoint.Partner, DisplayName = "NonDefaultAndDefaultAreSameWithDifferentEndpoints_DataLabs_Defult")]
        [DataRow(DiagnosticEndpoint.Partner, DiagnosticEndpoint.DataLabs, DisplayName = "NonDefaultAndDefaultAreSameWithDifferentEndpoints_Partner_Default")]
        public void NonDefaultAndDefaultAreSameWithDifferentEndpoints(DiagnosticEndpoint defaultEndpoint, DiagnosticEndpoint nonDefaultEndpoint)
        {
            ConfigMapUtil.Configuration[SolutionConstants.MDSD_PARTNER_ENDPOINT] = "DUMMY";
            PartnerActivityTracingLoggerFactory.ReInitialize();
            DiagnosticType.ReInitialize();

            DiagnosticType.SetDefaultDiagnosticEndpoint(defaultEndpoint);
            Assert.AreEqual(defaultEndpoint, DiagnosticType.CurrentDefaultDiagnosticTypeName);

            ILogger defaultActivityStartedLogger = DiagnosticType.DefaultActivityStartedLogger;
            ILogger defaultActivityFailedLogger = DiagnosticType.DefaultActivityFailedLogger;
            ILogger defaultActivityCompletedLogger = DiagnosticType.DefaultActivityCompletedLogger;
            Histogram<double> defaultActivityDurationHistogram = DiagnosticType.DefaultActivityDurationHistogram;

            DiagnosticType.SetDefaultDiagnosticEndpoint(nonDefaultEndpoint);
            Assert.AreEqual(nonDefaultEndpoint, DiagnosticType.CurrentDefaultDiagnosticTypeName);

            Assert.AreNotSame(defaultActivityStartedLogger, DiagnosticType.DefaultActivityStartedLogger);
            Assert.AreNotSame(defaultActivityFailedLogger, DiagnosticType.DefaultActivityFailedLogger);
            Assert.AreNotSame(defaultActivityCompletedLogger, DiagnosticType.DefaultActivityCompletedLogger);
            Assert.AreNotSame(defaultActivityDurationHistogram, DiagnosticType.DefaultActivityDurationHistogram);
        }
    }
}