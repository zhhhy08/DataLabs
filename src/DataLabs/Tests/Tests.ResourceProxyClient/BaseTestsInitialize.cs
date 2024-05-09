namespace Tests.ResourceProxyClient
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceProxyClient;

    [TestClass]
    public class BaseTestsInitialize
    {
        protected ResourceProxyFlowTestManager _resourceProxyFlowTestManager;
        protected IResourceProxyClient _resourceProxyClient;

        public BaseTestsInitialize()
        {
            _resourceProxyFlowTestManager = ResourceProxyFlowTestManager.Instance;
            _resourceProxyClient = _resourceProxyFlowTestManager.ResourceProxyClient;
        }

        [TestInitialize]
        public void TestInitialize()
        {
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _resourceProxyFlowTestManager!.Clear();
            // No reset configMap because it is shared between testResourceFetcherService, testResourceFetcherProxyService and ResourceProxyClient
        }
    }
}
