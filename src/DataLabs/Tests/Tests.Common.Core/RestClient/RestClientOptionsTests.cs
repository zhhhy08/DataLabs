namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.RestClient
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using System;
    using System.Net;

    [TestClass]
    public class RestClientOptionsTests
    {
        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context)
        {
            Environment.SetEnvironmentVariable(SolutionConstants.REGION, "wus3");
            Environment.SetEnvironmentVariable(SolutionConstants.SCALE_UNIT, "sku");
            Environment.SetEnvironmentVariable(SolutionConstants.BUILD_VERSION, "2023.11.04.04");
        }

        [TestMethod]
        public void TestUserAgent()
        {
            var region = MonitoringConstants.REGION;
            var scaleUnit = MonitoringConstants.SCALE_UNIT;
            var buildVersion = MonitoringConstants.BUILD_VERSION;

            var userAgent = RestClient.CreateUserAgent("ARMClient");
            Assert.AreEqual("AzureResourceBuilder.ARMClient" + "." + region + "." + scaleUnit + "/" + buildVersion, userAgent);

            userAgent = RestClient.CreateUserAgent("ARMAdminClient");
            Assert.AreEqual("AzureResourceBuilder.ARMAdminClient" + "." + region + "." + scaleUnit + "/" + buildVersion, userAgent);

            userAgent = RestClient.CreateUserAgent("CasClient");
            Assert.AreEqual("AzureResourceBuilder.CasClient" + "." + region + "." + scaleUnit + "/" + buildVersion, userAgent);

            userAgent = RestClient.CreateUserAgent("PartnerBlobClient");
            Assert.AreEqual("AzureResourceBuilder.PartnerBlobClient" + "." + region + "." + scaleUnit + "/" + buildVersion, userAgent);

            userAgent = RestClient.CreateUserAgent("QueryFrontDoorClient");
            Assert.AreEqual("AzureResourceBuilder.QueryFrontDoorClient" + "." + region + "." + scaleUnit + "/" + buildVersion, userAgent);

            userAgent = RestClient.CreateUserAgent("ResourceFetcherClient");
            Assert.AreEqual("AzureResourceBuilder.ResourceFetcherClient" + "." + region + "." + scaleUnit + "/" + buildVersion, userAgent);
        }

        [TestMethod]
        public void TestRestClientOptions()
        {
            var options = new RestClientOptions("TestAgent");

            var optionsString = @"
                    AdditionalHttpStatusCodesForRetry=500,502,503,504;
                    SameEndPointRetryCount=1;
                    MaxDifferentEndPointRetryCount=1;
                    RequestTimeoutForRetry=00:00:03;
                    HttpRequestVersion=2.0;
                    PooledConnectionLifetime=02:00:00;
                    PooledConnectionIdleTimeout=00:02:00;
                    ConnectTimeout=00:00:15;
                    RequestTimeout=00:00:30;
                    MaxConnectionsPerServer=16;
                    SocketKeepAlivePingDelay=00:01:00;
                    SocketKeepAlivePingTimeout=00:00:30;
                    KeepAlivePingPolicy=Always;
                    EnableMultipleHttp2Connections=true";

            options.SetRestClientOptions(optionsString.ConvertToDictionary(caseSensitive: false));

            Assert.AreEqual("TestAgent", options.UserAgent);

            var expectedHashset = new HashSet<int> { 500, 502, 503, 504 };
            Assert.IsTrue(options.AdditionalHttpStatusCodesForRetry.SetEquals(expectedHashset));
            Assert.AreEqual(1, options.SameEndPointRetryCount);
            Assert.AreEqual(1, options.MaxDifferentEndPointRetryCount);
            Assert.AreEqual(TimeSpan.FromSeconds(3), options.RequestTimeoutForRetry);
            Assert.AreEqual(HttpVersion.Version20, options.HttpRequestVersion);
            Assert.AreEqual(TimeSpan.FromHours(2), options.PooledConnectionLifetime);
            Assert.AreEqual(TimeSpan.FromMinutes(2), options.PooledConnectionIdleTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(15), options.ConnectTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.RequestTimeout);
            Assert.AreEqual(16, options.MaxConnectionsPerServer);
            Assert.AreEqual(TimeSpan.FromMinutes(1), options.SocketKeepAlivePingDelay);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.SocketKeepAlivePingTimeout);
            Assert.AreEqual(HttpKeepAlivePingPolicy.Always, options.KeepAlivePingPolicy);
            Assert.AreEqual(true, options.EnableMultipleHttp2Connections);
        }
    }
}
