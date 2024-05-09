namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceProxyClient
{
    using global::Tests.ResourceProxyClient;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Data;
    using Newtonsoft.Json;
    using System.Net;
    using Zones = DataLabs.Common.Partner.DataLabsInterface.Zones;

    [TestClass]
    public class GetResourceFlowTests: BaseTestsInitialize
    {
        [TestMethod]
        public async Task ResourceFetcherARM200()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache|write/00:10:00|addNotFound/00:03:00,resourcefetcher_arm";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set content to TestARMClient
            _resourceProxyFlowTestManager.ARMClient.SetResource(resourceId, armResourceString);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsTrue(dataLabsResourceResponse.SuccessARMResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse?.Resource);

            Assert.AreEqual(armResource.Id, dataLabsResourceResponse.SuccessARMResponse?.Resource.Id);
            Assert.AreEqual(armResource.Name, dataLabsResourceResponse.SuccessARMResponse?.Resource.Name);
            Assert.AreEqual(armResource.Type, dataLabsResourceResponse.SuccessARMResponse?.Resource.Type);
            Assert.AreEqual(armResource.Location, dataLabsResourceResponse.SuccessARMResponse?.Resource.Location);
        }

        [TestMethod]
        public async Task DirectARM200()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache|write/00:10:00|addNotFound/00:03:00,arm|2022-11-01";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set content to TestARMClient
            _resourceProxyFlowTestManager.ARMClient.SetResource(resourceId, armResourceString);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsTrue(dataLabsResourceResponse.SuccessARMResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse?.Resource);

            Assert.AreEqual(armResource.Id, dataLabsResourceResponse.SuccessARMResponse?.Resource.Id);
            Assert.AreEqual(armResource.Name, dataLabsResourceResponse.SuccessARMResponse?.Resource.Name);
            Assert.AreEqual(armResource.Type, dataLabsResourceResponse.SuccessARMResponse?.Resource.Type);
            Assert.AreEqual(armResource.Location, dataLabsResourceResponse.SuccessARMResponse?.Resource.Location);
        }

        [TestMethod]
        public async Task ResourceFetcherARM200AndCacheHit()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache|write/00:10:00|addNotFound/00:03:00,resourcefetcher_arm";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set content to TestARMClient
            _resourceProxyFlowTestManager.ARMClient.SetResource(resourceId, armResourceString);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsTrue(dataLabsResourceResponse.SuccessARMResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse?.Resource);

            Assert.AreEqual(armResource.Id, dataLabsResourceResponse.SuccessARMResponse?.Resource.Id);
            Assert.AreEqual(armResource.Name, dataLabsResourceResponse.SuccessARMResponse?.Resource.Name);
            Assert.AreEqual(armResource.Type, dataLabsResourceResponse.SuccessARMResponse?.Resource.Type);
            Assert.AreEqual(armResource.Location, dataLabsResourceResponse.SuccessARMResponse?.Resource.Location);

            // Now above entry is saved in cache
            // Let's call one more to get it from cache
            correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            traceId = ResourceProxyClientTestData.CreateNewActivityId();

            request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            // Now it should be from cache
            Assert.AreEqual(DataLabsDataSource.CACHE, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsTrue(dataLabsResourceResponse.SuccessARMResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse?.Resource);

            Assert.AreEqual(armResource.Id, dataLabsResourceResponse.SuccessARMResponse?.Resource.Id);
            Assert.AreEqual(armResource.Name, dataLabsResourceResponse.SuccessARMResponse?.Resource.Name);
            Assert.AreEqual(armResource.Type, dataLabsResourceResponse.SuccessARMResponse?.Resource.Type);
            Assert.AreEqual(armResource.Location, dataLabsResourceResponse.SuccessARMResponse?.Resource.Location);
        }

        [TestMethod]
        public async Task ResourceFetcherQFD200AndCacheHit()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache|write/00:10:00|addNotFound/00:03:00,resourcefetcher_qfd";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set content to TestQFDClient
            _resourceProxyFlowTestManager.QFDClient.SetResource(resourceId, armResourceString);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.QFD, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsTrue(dataLabsResourceResponse.SuccessARMResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse?.Resource);

            Assert.AreEqual(armResource.Id, dataLabsResourceResponse.SuccessARMResponse?.Resource.Id);
            Assert.AreEqual(armResource.Name, dataLabsResourceResponse.SuccessARMResponse?.Resource.Name);
            Assert.AreEqual(armResource.Type, dataLabsResourceResponse.SuccessARMResponse?.Resource.Type);
            Assert.AreEqual(armResource.Location, dataLabsResourceResponse.SuccessARMResponse?.Resource.Location);

            // Now above entry is saved in cache
            // Let's call one more to get it from cache
            correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            traceId = ResourceProxyClientTestData.CreateNewActivityId();

            request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            // Now it should be from cache
            Assert.AreEqual(DataLabsDataSource.CACHE, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsTrue(dataLabsResourceResponse.SuccessARMResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse?.Resource);

            Assert.AreEqual(armResource.Id, dataLabsResourceResponse.SuccessARMResponse?.Resource.Id);
            Assert.AreEqual(armResource.Name, dataLabsResourceResponse.SuccessARMResponse?.Resource.Name);
            Assert.AreEqual(armResource.Type, dataLabsResourceResponse.SuccessARMResponse?.Resource.Type);
            Assert.AreEqual(armResource.Location, dataLabsResourceResponse.SuccessARMResponse?.Resource.Location);
        }

        [TestMethod]
        public async Task ResourceFetcherQFD200AndNoCacheWrite()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache,resourcefetcher_qfd";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set content to TestQFDClient
            _resourceProxyFlowTestManager.QFDClient.SetResource(resourceId, armResourceString);

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.QFD, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsTrue(dataLabsResourceResponse.SuccessARMResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse?.Resource);

            Assert.AreEqual(armResource.Id, dataLabsResourceResponse.SuccessARMResponse?.Resource.Id);
            Assert.AreEqual(armResource.Name, dataLabsResourceResponse.SuccessARMResponse?.Resource.Name);
            Assert.AreEqual(armResource.Type, dataLabsResourceResponse.SuccessARMResponse?.Resource.Type);
            Assert.AreEqual(armResource.Location, dataLabsResourceResponse.SuccessARMResponse?.Resource.Location);

            // We don't store it in cache
            // So next call should not be from cache
            correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            traceId = ResourceProxyClientTestData.CreateNewActivityId();

            request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            // Now it should be from cache
            Assert.AreEqual(DataLabsDataSource.QFD, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsTrue(dataLabsResourceResponse.SuccessARMResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse?.Resource);

            Assert.AreEqual(armResource.Id, dataLabsResourceResponse.SuccessARMResponse?.Resource.Id);
            Assert.AreEqual(armResource.Name, dataLabsResourceResponse.SuccessARMResponse?.Resource.Name);
            Assert.AreEqual(armResource.Type, dataLabsResourceResponse.SuccessARMResponse?.Resource.Type);
            Assert.AreEqual(armResource.Location, dataLabsResourceResponse.SuccessARMResponse?.Resource.Location);
        }

        [TestMethod]
        public async Task ResourceFetcherARM404()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache|write/00:10:00|addNotFound/00:03:00,resourcefetcher_arm";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // No Set content to TestARMClient
            // So TestARMClient will return 404

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);

            Assert.IsNotNull(dataLabsResourceResponse.ErrorResponse);

            Assert.AreEqual(DataLabsErrorType.RETRY, dataLabsResourceResponse.ErrorResponse.ErrorType);
            Assert.AreEqual(0, dataLabsResourceResponse.ErrorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual(404, dataLabsResourceResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound.FastEnumToString(), dataLabsResourceResponse.ErrorResponse.ErrorDescription);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm.FastEnumToString(), dataLabsResourceResponse.ErrorResponse.FailedComponent);
        }

        [TestMethod]
        public async Task ResourceFetcherARM404AndCacheHit()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache|write/00:10:00|addNotFound/00:03:00,resourcefetcher_arm";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // No Set content to TestARMClient
            // So TestARMClient will return 404

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);

            Assert.IsNotNull(dataLabsResourceResponse.ErrorResponse);

            Assert.AreEqual(DataLabsErrorType.RETRY, dataLabsResourceResponse.ErrorResponse.ErrorType);
            Assert.AreEqual(0, dataLabsResourceResponse.ErrorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual(404, dataLabsResourceResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound.FastEnumToString(), dataLabsResourceResponse.ErrorResponse.ErrorDescription);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm.FastEnumToString(), dataLabsResourceResponse.ErrorResponse.FailedComponent);

            // Now above entry (special 404 NotFound cache Entry) is saved in cache
            // Let's call one more to get it from cache
            correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            traceId = ResourceProxyClientTestData.CreateNewActivityId();

            request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            // Now it should be from cache
            Assert.AreEqual(DataLabsDataSource.CACHE, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);

            Assert.IsNotNull(dataLabsResourceResponse.ErrorResponse);

            Assert.AreEqual(DataLabsErrorType.RETRY, dataLabsResourceResponse.ErrorResponse.ErrorType);
            Assert.AreEqual(0, dataLabsResourceResponse.ErrorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual(404, dataLabsResourceResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(SolutionConstants.NotFoundEntryExistInCache, dataLabsResourceResponse.ErrorResponse.ErrorDescription);
            Assert.AreEqual(ClientProviderType.Cache.FastEnumToString(), dataLabsResourceResponse.ErrorResponse.FailedComponent);
        }

        [TestMethod]
        public async Task ResourceFetcherARM500()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache|write/00:10:00|addNotFound/00:03:00,resourcefetcher_arm";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set ErrorResponse to TestARMClient
            var httpStatusCode = HttpStatusCode.InternalServerError;
            _resourceProxyFlowTestManager.ARMClient.ErrStatusCode = httpStatusCode;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);

            Assert.IsNotNull(dataLabsResourceResponse.ErrorResponse);

            Assert.AreEqual(DataLabsErrorType.RETRY, dataLabsResourceResponse.ErrorResponse.ErrorType);
            Assert.AreEqual(0, dataLabsResourceResponse.ErrorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual((int)httpStatusCode, dataLabsResourceResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual(httpStatusCode.FastEnumToString(), dataLabsResourceResponse.ErrorResponse.ErrorDescription);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm.FastEnumToString(), dataLabsResourceResponse.ErrorResponse.FailedComponent);
        }

        // For ARM, we get throttling Header
        // "x-ms-ratelimit-remaining-subscription-reads";
        [TestMethod]
        public async Task ResourceFetcherARM200WithSmallRateLimitRemainingSubscriptionReads()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache|write/00:10:00|addNotFound/00:03:00,resourcefetcher_arm";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set content to TestARMClient
            _resourceProxyFlowTestManager.ARMClient.SetResource(resourceId, armResourceString);
            // Set Arm ratelimit remaining subscription reads to 10
            _resourceProxyFlowTestManager.ARMClient.ArmThrottlingLimit = 10;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.ErrorResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsTrue(dataLabsResourceResponse.SuccessARMResponse.OutputTimestamp.ToUnixTimeMilliseconds() > 0);
            Assert.IsNotNull(dataLabsResourceResponse.SuccessARMResponse?.Resource);

            Assert.AreEqual(armResource.Id, dataLabsResourceResponse.SuccessARMResponse?.Resource.Id);
            Assert.AreEqual(armResource.Name, dataLabsResourceResponse.SuccessARMResponse?.Resource.Name);
            Assert.AreEqual(armResource.Type, dataLabsResourceResponse.SuccessARMResponse?.Resource.Type);
            Assert.AreEqual(armResource.Location, dataLabsResourceResponse.SuccessARMResponse?.Resource.Location);

            // Now above entry is saved in cache and the small rate limit is also added to Cache
            // Next call should get 429 Throttling
            correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            traceId = ResourceProxyClientTestData.CreateNewActivityId();

            request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            // Let's skip cache read so that it will try to call to ARM but due to previous added small rate limit, it should get 429 Throttling
            dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true,
                skipCacheRead: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.CACHE, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);

            Assert.IsNotNull(dataLabsResourceResponse.ErrorResponse);

            Assert.AreEqual(DataLabsErrorType.RETRY, dataLabsResourceResponse.ErrorResponse.ErrorType);
            Assert.IsTrue(dataLabsResourceResponse.ErrorResponse.RetryDelayInMilliseconds > 0);
            Assert.AreEqual((int)HttpStatusCode.TooManyRequests, dataLabsResourceResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual("ThrottleExistInCache: 02d59989-f8a9-4b69-9919-1ef51df4eff6", dataLabsResourceResponse.ErrorResponse.ErrorDescription);
            Assert.AreEqual(ClientProviderType.Cache.FastEnumToString(), dataLabsResourceResponse.ErrorResponse.FailedComponent);
        }

        [TestMethod]
        public async Task ResourceFetcherException()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache|write/00:10:00|addNotFound/00:03:00,resourcefetcher_arm";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set Exception to TestARMClient
            _resourceProxyFlowTestManager.ARMClient.ThrowException = true;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);

            Assert.IsNotNull(dataLabsResourceResponse.ErrorResponse);

            Assert.AreEqual(DataLabsErrorType.RETRY, dataLabsResourceResponse.ErrorResponse.ErrorType);
            Assert.AreEqual(0, dataLabsResourceResponse.ErrorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual(500, dataLabsResourceResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual("{\"message\":\"Test Exception\",\"component\":\"ARMClient\"}", dataLabsResourceResponse.ErrorResponse.ErrorDescription);
            Assert.AreEqual(ClientProviderType.ResourceFetcher_Arm.FastEnumToString(), dataLabsResourceResponse.ErrorResponse.FailedComponent);
        }

        [TestMethod]
        public async Task DirectArmClientException()
        {
            // Update Config
            var allowedTypesInProxy = "Microsoft.Compute/virtualMachineScaleSets:cache|write/00:10:00|addNotFound/00:03:00,arm|2022-11-01";
            var allowedTypesInFetcher = "Microsoft.Compute/virtualMachineScaleSets|2022-11-01";

            await _resourceProxyFlowTestManager.UpdateConfigAsync(ResourceProxyAllowedConfigType.GetResourceAllowedTypes,
                valueInProxy: allowedTypesInProxy,
                valueInFetcher: allowedTypesInFetcher).ConfigureAwait(false);

            var armResourceString = ResourceProxyClientTestData.VirtualMachineArmResource;
            var armResource = ResourceProxyClientTestData.CreateARMResource(armResourceString);
            var resourceId = armResource.Id;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            // Set Exception to TestARMClient
            _resourceProxyFlowTestManager.ARMClient.ThrowException = true;

            var correlationId = ResourceProxyClientTestData.CreateNewCorrelationId();
            var traceId = ResourceProxyClientTestData.CreateNewActivityId();

            var request = new DataLabsResourceRequest(
                traceId: traceId,
                retryCount: 0,
                correlationId: correlationId,
                resourceId: resourceId,
                tenantId: tenantId);

            var dataLabsResourceResponse = await _resourceProxyClient.GetResourceAsync(
                request: request,
                cancellationToken: default,
                getDeletedResource: true).ConfigureAwait(false);

            Assert.AreEqual(DataLabsDataSource.ARM, dataLabsResourceResponse.DataSource);
            Assert.IsTrue(dataLabsResourceResponse.Attributes == null || dataLabsResourceResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabsResourceResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.AreEqual(correlationId, dataLabsResourceResponse.CorrelationId);

            Assert.IsNull(dataLabsResourceResponse.SuccessARMResponse);
            Assert.IsNull(dataLabsResourceResponse.SuccessARNV3Response);

            Assert.IsNotNull(dataLabsResourceResponse.ErrorResponse);

            Assert.AreEqual(DataLabsErrorType.RETRY, dataLabsResourceResponse.ErrorResponse.ErrorType);
            Assert.AreEqual(0, dataLabsResourceResponse.ErrorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual(0, dataLabsResourceResponse.ErrorResponse.HttpStatusCode);
            Assert.AreEqual("Test Exception", dataLabsResourceResponse.ErrorResponse.ErrorDescription);
            Assert.AreEqual(ClientProviderType.Arm.FastEnumToString(), dataLabsResourceResponse.ErrorResponse.FailedComponent);
        }
    }
}