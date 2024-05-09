namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.PartnerBlobClient
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient;

    [TestClass]
    public class PartnerBlobClientTest
    {
        private PartnerBlobClient _partnerBlobClient;

        [TestInitialize]
        public void TestInitialize()
        {
            ConfigMapUtil.Reset();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }

        [TestMethod]
        public void BlobClientInitializeTest()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(config, false);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IConfiguration>(ConfigMapUtil.Configuration);
            serviceCollection.AddPartnerBlobClient();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            _partnerBlobClient = (PartnerBlobClient)serviceProvider.GetService<IPartnerBlobClient>();
        }
    }
}

