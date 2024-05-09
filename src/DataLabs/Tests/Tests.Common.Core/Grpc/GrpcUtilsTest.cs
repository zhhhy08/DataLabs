namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Grpc
{
    using global::Grpc.Core;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Grpc;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PodHealth;

    [TestClass]
    public class GrpcUtilsTest
    {
        [TestInitialize]
        public void TestInitialize()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(config, false);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public void GrpcClientOptionsTest()
        {
            var options = @"LBPolicy=LOCAL;
                             MaxAttempts=9;
                             RetryInitialBackoff=00:00:03;
                             RetryMaxBackoff=00:00:08;
                             RetryBackoffMultiplier=1.6;
                             MaxReceiveMessageSizeMB=18;
                             UseMultiConnections=true;
                             ConnectTimeout=00:00:12;
                             PooledConnectionIdleTimeout=   00:01:02;;
                             SocketKeepAlivePingDelay=00:15:00;
                             SocketKeepAlivePingTimeout=00:00:12;
                             DnsRefreshInterval=00:00:32";

            var grpcClientOption = new GrpcClientOption(options.ConvertToDictionary(caseSensitive: false));
            Assert.AreEqual(GrpcLBPolicy.LOCAL, grpcClientOption.LBPolicy);
            Assert.AreEqual(9, grpcClientOption.MaxAttempts);
            Assert.AreEqual(TimeSpan.FromSeconds(3), grpcClientOption.RetryInitialBackoff);
            Assert.AreEqual(TimeSpan.FromSeconds(8), grpcClientOption.RetryMaxBackoff);
            Assert.AreEqual(1.6, grpcClientOption.RetryBackoffMultiplier);
            Assert.AreEqual(18, grpcClientOption.MaxReceiveMessageSizeMB);
            Assert.AreEqual(true, grpcClientOption.UseMultiConnections);
            Assert.AreEqual(TimeSpan.FromSeconds(12), grpcClientOption.ConnectTimeout);
            Assert.AreEqual(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(2)), grpcClientOption.PooledConnectionIdleTimeout);
            Assert.AreEqual(TimeSpan.FromMinutes(15), grpcClientOption.SocketKeepAlivePingDelay);
            Assert.AreEqual(TimeSpan.FromSeconds(12), grpcClientOption.SocketKeepAlivePingTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(32), grpcClientOption.DnsRefreshInterval);
        }

        [TestMethod]
        public void DefaultGrpcClientOptionsTest()
        {
            var options = "";

            var grpcClientOption = new GrpcClientOption(options.ConvertToDictionary(caseSensitive: false));
            Assert.AreEqual(GrpcLBPolicy.ROUND_ROBIN, grpcClientOption.LBPolicy);
            Assert.AreEqual(3, grpcClientOption.MaxAttempts);
            Assert.AreEqual(TimeSpan.FromSeconds(1), grpcClientOption.RetryInitialBackoff);
            Assert.AreEqual(TimeSpan.FromSeconds(5), grpcClientOption.RetryMaxBackoff);
            Assert.AreEqual(1.5, grpcClientOption.RetryBackoffMultiplier);
            Assert.AreEqual(8, grpcClientOption.MaxReceiveMessageSizeMB);
            Assert.AreEqual(false, grpcClientOption.UseMultiConnections);
            Assert.AreEqual(TimeSpan.FromSeconds(5), grpcClientOption.ConnectTimeout);
            Assert.IsTrue(TimeSpan.Zero.Equals(grpcClientOption.PooledConnectionIdleTimeout));
            Assert.AreEqual(TimeSpan.FromSeconds(30), grpcClientOption.DnsRefreshInterval);
        }

        [TestMethod]
        public void TestGrpcClientOption()
        {
            var optionsString = @"
                    LBPolicy=LOCAL;
                     MaxAttempts=3;
                     RetryInitialBackoff=00:00:04;
                     RetryMaxBackoff=00:00:12;
                     RetryBackoffMultiplier=1.6;
                     MaxReceiveMessageSizeMB=16;
                     UseMultiConnections=true;
                     ConnectTimeout=00:00:12;
                     PooledConnectionIdleTimeout=00:02:00;
                     SocketKeepAlivePingDelay=00:00:45;
                     SocketKeepAlivePingTimeout=00:00:12;
                     DnsRefreshInterval=00:00:28";

            var options = new GrpcClientOption(optionsString.ConvertToDictionary(caseSensitive: false));

            Assert.AreEqual(GrpcLBPolicy.LOCAL, options.LBPolicy);
            Assert.AreEqual(3, options.MaxAttempts);
            Assert.AreEqual(TimeSpan.FromSeconds(4), options.RetryInitialBackoff);
            Assert.AreEqual(TimeSpan.FromSeconds(12), options.RetryMaxBackoff);
            Assert.AreEqual(1.6, options.RetryBackoffMultiplier);
            Assert.AreEqual(16, options.MaxReceiveMessageSizeMB);
            Assert.AreEqual(true, options.UseMultiConnections);
            Assert.AreEqual(TimeSpan.FromSeconds(12), options.ConnectTimeout);
            Assert.AreEqual(TimeSpan.FromMinutes(2), options.PooledConnectionIdleTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(28), options.DnsRefreshInterval);
        }

        [TestMethod]
        public void CreateGrpcChannelWithAddrTest()
        {
            // DNS reolved is not called during constructor. So, this test will pass.
            var addr = "dns:///solution-partner.default.testdomain:5071";
            using var channel = GrpcUtils.CreateGrpcChannel(addr, new GrpcClientOption(null), new PodHealthManager("test"));
            Assert.IsNotNull(channel);
        }

        [TestMethod]
        public void CreateGrpcChannelWithIpAndPortTest()
        {
            var hostIp = "127.0.0.1";
            var port = "65123";
            using var channel = GrpcUtils.CreateGrpcChannel(hostIp, port, new GrpcClientOption(null), new PodHealthManager("test"));
            Assert.IsNotNull(channel);
        }

        [TestMethod]
        public void IsOperationCanceledTest()
        {
            Assert.IsTrue(GrpcUtils.IsOperationCanceled(new OperationCanceledException()));
            var exception = new RpcException(new Status(StatusCode.DeadlineExceeded, null));
            Assert.IsTrue(GrpcUtils.IsOperationCanceled(exception));
        }
    }
}

