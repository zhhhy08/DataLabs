namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceFetcherProxyService
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common;

    [TestClass]
    public class ProxyConstantsTest
    {
        [TestInitialize]
        public void TestInitialize()
        {
        }

        [TestCleanup]
        public void TestCleanup()
        {
        }

        [TestMethod]
        public void CheckDuplicatedProxyConstants()
        {
            // Check if there is any duplicated values
            var allowedKeys = new HashSet<string>()
            {
                "ServiceMeter",
                "TraceSource"
            };

            AssertUtils.HasDuplicatedValue(typeof(ResourceFetcherProxyConstants), allowedKeys);
        }
    }
}

