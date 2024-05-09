namespace Tests.Common.Core.Client
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    [TestClass]
    public class CasClientOptionsTest
    {
        [TestMethod]
        public void CasClientOptions_Constructor_Sets_HttpRequestVersion()
        {
            // Arrange
            var appSettingsStub = new Dictionary<string, string>
            {
                { "CasClientOption", "HttpRequestVersion=1.1;ConnectTimeout=00:00:05;EnableMultipleHttp2Connections=false" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub);

            // Act
            var casClientOptions = new CasClientOptions(config.Build())
            {
                EndPointSelector = new Mock<IEndPointSelector>().Object
            };

            // Assert
            Assert.AreEqual(HttpVersion.Version11, casClientOptions.HttpRequestVersion);
            config.Sources.Clear();
        }
    }
}
