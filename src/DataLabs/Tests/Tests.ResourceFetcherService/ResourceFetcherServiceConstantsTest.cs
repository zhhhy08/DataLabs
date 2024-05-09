namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceFetcherService
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common;

    [TestClass]
    public class ResourceFetcherServiceConstantsTest
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
        public void CheckDuplicatedConfigMapConstants()
        {
            // Check if there is any duplicated values
            var allowedKeys = new HashSet<string>()
            {

            };

            AssertUtils.HasDuplicatedValue(typeof(ResourceFetcherConstants), allowedKeys);
        }

        [TestMethod]
        public void CheckDuplicatedResourceFetcherConstants()
        {
            // Check if there is any duplicated values
            var allowedKeys = new HashSet<string>()
            {
                "ServiceMeter",
                "TraceSource"
            };

            AssertUtils.HasDuplicatedValue(typeof(ResourceFetcherConstants), allowedKeys);
        }
    }
}