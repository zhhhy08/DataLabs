namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceProxyClient
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Data;
    using System.Net;

    [TestClass]
    public class CallARMGenericRequestFlowTests
    {
        private ResourceProxyFlowTestManager _resourceProxyFlowTestManager;
        private IResourceProxyClient _resourceProxyClient;

        public CallARMGenericRequestFlowTests()
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

        [TestMethod]
        public async Task ResourceFetcherARMGeneric200()
        {
            // Update config
            var allowedTypesInProxy = "/providers/Microsoft.Authorization/policySetDefinitions:resourcefetcher_arm";
            var allowedTypesInFetcher = "/providers/Microsoft.Authorization/policySetDefinitions|2021-06-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.CallARMGenericRequestAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armGenericResourceString = "TestData";

            // Set content to TestARMClient
            _resourceProxyFlowTestManager.ARMClient.TestArmGenericResource = armGenericResourceString;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();
            var uriPath = "/providers/Microsoft.Authorization/policySetDefinitions";
            var queryParams = new Dictionary<string, string?>()
            {
                { "api-version", "2021-06-01" },
                { "$expand", "BuiltInCapability" },
                { "$filter", "PolicyType eq 'BuiltInCapability'" }
            };
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            var request = new DataLabsARMGenericRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                uriPath: uriPath,
                queryParams: queryParams, 
                tenantId: tenantId);

            var dataLabsARMGenericResponse = await _resourceProxyClient.CallARMGenericRequestAsync(
                request: request,
                cancellationToken: default).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsARMGenericResponse.DataSource);
            Assert.IsTrue(dataLabsARMGenericResponse.Attributes == null || dataLabsARMGenericResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsARMGenericResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsARMGenericResponse.CorrelationId);

            Assert.IsNull(dataLabsARMGenericResponse.ErrorResponse);
            Assert.IsNotNull(dataLabsARMGenericResponse.SuccessResponse);
            Assert.IsTrue(dataLabsARMGenericResponse.SuccessResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsTrue(dataLabsARMGenericResponse.SuccessResponse?.Response?.Length > 0);
        }

        [TestMethod]
        public async Task DirectARMGeneric200()
        {
            // Update config
            var allowedTypesInProxy = "/providers/Microsoft.Authorization/policySetDefinitions:arm|2021-06-01";
            var allowedTypesInFetcher = "/providers/Microsoft.Authorization/policySetDefinitions|2021-06-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.CallARMGenericRequestAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            // ResourceFetcherProxyConfig
            ConfigMapUtil.Configuration[SolutionConstants.CallARMGenericRequestAllowedTypes] = "/providers/Microsoft.Authorization/policySetDefinitions:arm|2021-06-01";
            // ResourceFetcherConfig
            ConfigMapUtil.Configuration["testsolution-armAllowedGenericURIPaths"] = "/providers/Microsoft.Authorization/policySetDefinitions|2021-06-01";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);
            await Task.Delay(50).ConfigureAwait(false);

            var armGenericResourceString = "TestData";

            // Set content to TestARMClient
            _resourceProxyFlowTestManager.ARMClient.TestArmGenericResource = armGenericResourceString;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();
            var uriPath = "/providers/Microsoft.Authorization/policySetDefinitions";
            var queryParams = new Dictionary<string, string?>()
            {
                { "api-version", "2021-06-01" },
                { "$expand", "BuiltInCapability" },
                { "$filter", "PolicyType eq 'BuiltInCapability'" }
            };
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            var request = new DataLabsARMGenericRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                uriPath: uriPath,
                queryParams: queryParams,
                tenantId: tenantId);

            var dataLabsARMGenericResponse = await _resourceProxyClient.CallARMGenericRequestAsync(
                request: request,
                cancellationToken: default).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsARMGenericResponse.DataSource);
            Assert.IsTrue(dataLabsARMGenericResponse.Attributes == null || dataLabsARMGenericResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsARMGenericResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsARMGenericResponse.CorrelationId);

            Assert.IsNull(dataLabsARMGenericResponse.ErrorResponse);
            Assert.IsNotNull(dataLabsARMGenericResponse.SuccessResponse);
            Assert.IsTrue(dataLabsARMGenericResponse.SuccessResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsTrue(dataLabsARMGenericResponse.SuccessResponse?.Response?.Length > 0);
        }
        
        [TestMethod]
        public async Task ResourceFetcherARMGeneric500()
        {
            // Update config
            var allowedTypesInProxy = "/providers/Microsoft.Authorization/policySetDefinitions:resourcefetcher_arm";
            var allowedTypesInFetcher = "/providers/Microsoft.Authorization/policySetDefinitions|2021-06-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.CallARMGenericRequestAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            // Set ErrorStatus to TestARMClient
            var errorHttpStatusCode = HttpStatusCode.InternalServerError;
            _resourceProxyFlowTestManager.ARMClient.ErrStatusCode = errorHttpStatusCode;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();
            var uriPath = "/providers/Microsoft.Authorization/policySetDefinitions";
            var queryParams = new Dictionary<string, string?>()
            {
                { "api-version", "2021-06-01" },
                { "$expand", "BuiltInCapability" },
                { "$filter", "PolicyType eq 'BuiltInCapability'" }
            };
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            var request = new DataLabsARMGenericRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                uriPath: uriPath,
                queryParams: queryParams,
                tenantId: tenantId);

            var dataLabsARMGenericResponse = await _resourceProxyClient.CallARMGenericRequestAsync(
                request: request,
                cancellationToken: default).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsARMGenericResponse.DataSource);
            Assert.IsTrue(dataLabsARMGenericResponse.Attributes == null || dataLabsARMGenericResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsARMGenericResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsARMGenericResponse.CorrelationId);

            Assert.IsNotNull(dataLabsARMGenericResponse.ErrorResponse);
            Assert.AreEqual(DataLabsErrorType.RETRY, dataLabsARMGenericResponse.ErrorResponse.ErrorType);
            Assert.AreEqual(0, dataLabsARMGenericResponse.ErrorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual((int)errorHttpStatusCode, dataLabsARMGenericResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(errorHttpStatusCode.FastEnumToString(), dataLabsARMGenericResponse.ErrorResponse.ErrorDescription);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm.FastEnumToString(), dataLabsARMGenericResponse.ErrorResponse.FailedComponent);
        }
    }
}