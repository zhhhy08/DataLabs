namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ResourceProxyClient
{
    using global::Azure;
    using System.Net;
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceFetcherProxyService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Data;
    using System.Data;

    [TestClass]
    public class ResourceProxyClientTest
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
        public void SuccessARNV3SourceOfTruthResponseConvertTest()
        {
            var eventGridEvent = ResourceProxyClientTestData.ParseEvents(ResourceProxyClientTestData.VirtualMachineEvent);
            var armId = eventGridEvent.Data.Resources[0].ResourceId;
            var resourceType = ArmUtils.GetResourceType(armId);
            var correlationId = eventGridEvent.Data.Resources[0].CorrelationId;
            var tenantId = eventGridEvent.Data.Resources[0].ResourceHomeTenantId;
            var eventTime = eventGridEvent.EventTime;

            var responseByteString = SerializationHelper.SerializeToByteString(eventGridEvent, false);

            var responseEpochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var resourceResponse = new ResourceResponse()
            {
                ResponseEpochTime = responseEpochTime,
                CorrelationId = correlationId,
                Success = new SuccessResponse()
                {
                    Format = ProxyDataFormat.Arn,
                    OutputData = responseByteString,
                    Etag = null,
                    DataSource = ProxyDataSource.OutputSourceoftruth
                }
            };

            var activityMonitor = (ActivityMonitorFactory)PrivateFunctionAccessHelper.GetPrivateField(typeof(ResourceProxyClient), "ResourceProxyClientGetResourceAsync", null, true);
            using var methodMonitor = activityMonitor.ToMonitor();

            methodMonitor.OnStart();
            var dataLabResponse = (DataLabsResourceResponse)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient), "ConvertToResourceResponse", 
                new object[] { correlationId, "GetResourceAsync", 0, resourceType, resourceResponse, false, methodMonitor});
            methodMonitor.OnCompleted();

            Assert.AreEqual(correlationId, dataLabResponse.CorrelationId);
            Assert.AreEqual(responseEpochTime, dataLabResponse.ResponseTime.ToUnixTimeMilliseconds());
            Assert.IsNotNull(dataLabResponse.SuccessARNV3Response);
            Assert.IsNull(dataLabResponse.SuccessARMResponse);
            Assert.IsNull(dataLabResponse.ErrorResponse);
            Assert.IsTrue(dataLabResponse.Attributes == null || dataLabResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabResponse.DataSource == DataLabsDataSource.OUTPUTSOURCEOFTRUTH);

            var arnV3SuccessResponse = dataLabResponse.SuccessARNV3Response;
            Assert.AreEqual(eventTime, arnV3SuccessResponse.OutputTimestamp);
            Assert.AreEqual(armId, ResourceProxyClientTestData.GetARMId(arnV3SuccessResponse.Resource.Data));
            Assert.AreEqual(tenantId, ResourceProxyClientTestData.GetTenantId(arnV3SuccessResponse.Resource.Data));
            Assert.IsNull(arnV3SuccessResponse.ETag);
            Assert.IsNotNull(arnV3SuccessResponse.Resource);

            var arnV3Resource = arnV3SuccessResponse.Resource;
            Assert.AreEqual(responseByteString, SerializationHelper.SerializeToByteString(arnV3Resource, false));
        }

        [TestMethod]
        public void CacheARNV3ResponseConvertTest()
        {
            var context = Tracer.CreateNewActivityContext();
            var traceId = Tracer.ConvertToActivityId(context);

            var eventGridEvent = ResourceProxyClientTestData.ParseEvents(ResourceProxyClientTestData.VirtualMachineEvent);
            var armId = eventGridEvent.Data.Resources[0].ResourceId;
            var correlationId = eventGridEvent.Data.Resources[0].CorrelationId;
            var tenantId = eventGridEvent.Data.Resources[0].ResourceHomeTenantId;
            var eventTime = eventGridEvent.EventTime;

            var responseByteString = SerializationHelper.SerializeToByteString(eventGridEvent, false);

            var newEtag = new ETag(Guid.NewGuid().ToString());
            var request = new DataLabsResourceRequest(traceId, 0, correlationId, armId, tenantId);
            var cacheResult = new ResourceCacheResult(true, 
                ResourceCacheDataFormat.ARN,
                responseByteString.ToByteArray(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                newEtag.ToString());
                
            var activityMonitor = (ActivityMonitorFactory)PrivateFunctionAccessHelper.GetPrivateField(typeof(ResourceProxyClient), "ResourceProxyClientGetResourceAsync", null, true);
            using var methodMonitor = activityMonitor.ToMonitor();

            methodMonitor.OnStart();
            var resourceResponse = (ResourceResponse)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient),
                "ConvertCacheResultToResourceResponse", new object[] { correlationId, cacheResult });
            methodMonitor.OnCompleted();

            Assert.IsTrue(resourceResponse.ResponseEpochTime > 0);
            Assert.AreEqual(correlationId, resourceResponse.CorrelationId);
            Assert.IsNull(resourceResponse.Error);
            Assert.AreEqual(ProxyDataFormat.Arn, resourceResponse.Success.Format);
            Assert.AreEqual(newEtag.ToString(), resourceResponse.Success.Etag.ToString());
            Assert.AreEqual(ProxyDataSource.Cache, resourceResponse.Success.DataSource);
        }

        [TestMethod]
        public void CacheCollisionCheckARNSuccessTest()
        {
            var context = Tracer.CreateNewActivityContext();
            var traceId = Tracer.ConvertToActivityId(context);

            var eventGridEvent = ResourceProxyClientTestData.ParseEvents(ResourceProxyClientTestData.VirtualMachineEvent);
            var armId = eventGridEvent.Data.Resources[0].ResourceId;
            var resourceType = ArmUtils.GetResourceType(armId);
            var correlationId = eventGridEvent.Data.Resources[0].CorrelationId;
            var tenantId = eventGridEvent.Data.Resources[0].ResourceHomeTenantId;
            var eventTime = eventGridEvent.EventTime;

            var responseByteString = SerializationHelper.SerializeToByteString(eventGridEvent, false);

            var newEtag = new ETag(Guid.NewGuid().ToString());
            var request = new DataLabsResourceRequest(traceId, 0, correlationId, armId, tenantId);
            var cacheResult = new ResourceCacheResult(true,
                ResourceCacheDataFormat.ARN,
                responseByteString.ToByteArray(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                newEtag.ToString());

            var activityMonitor = (ActivityMonitorFactory)PrivateFunctionAccessHelper.GetPrivateField(typeof(ResourceProxyClient), "ResourceProxyClientGetResourceAsync", null, true);
            using var methodMonitor = activityMonitor.ToMonitor();

            methodMonitor.OnStart();
            var resourceResponse = (ResourceResponse)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient),
                "ConvertCacheResultToResourceResponse", new object[] { correlationId, cacheResult });

            var dataLabResponse = (DataLabsResourceResponse)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient), 
                "ConvertToResourceResponse",
                new object[] { correlationId, "GetResourceAsync", 0, resourceType, resourceResponse, false, methodMonitor });

            var collisionCheck = (bool)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient),
                "CacheCollisionCheck", new object[] { dataLabResponse, armId, tenantId, null });

            methodMonitor.OnCompleted();

            Assert.IsTrue(resourceResponse.ResponseEpochTime > 0);
            Assert.AreEqual(correlationId, resourceResponse.CorrelationId);
            Assert.IsNull(resourceResponse.Error);
            Assert.AreEqual(ProxyDataFormat.Arn, resourceResponse.Success.Format);
            Assert.AreEqual(newEtag.ToString(), resourceResponse.Success.Etag.ToString());
            Assert.AreEqual(ProxyDataSource.Cache, resourceResponse.Success.DataSource);
            Assert.IsFalse(collisionCheck);
        }

        [TestMethod]
        public void CacheCollisionCheckARNFailTest()
        {
            var context = Tracer.CreateNewActivityContext();
            var traceId = Tracer.ConvertToActivityId(context);

            var eventGridEvent = ResourceProxyClientTestData.ParseEvents(ResourceProxyClientTestData.VirtualMachineEvent);
            var armId = eventGridEvent.Data.Resources[0].ResourceId;
            var resourceType = ArmUtils.GetResourceType(armId);
            var correlationId = eventGridEvent.Data.Resources[0].CorrelationId;
            var tenantId = eventGridEvent.Data.Resources[0].ResourceHomeTenantId;
            var eventTime = eventGridEvent.EventTime;

            var responseByteString = SerializationHelper.SerializeToByteString(eventGridEvent, false);

            var newEtag = new ETag(Guid.NewGuid().ToString());
            var request = new DataLabsResourceRequest(traceId, 0, correlationId, armId, tenantId);
            var cacheResult = new ResourceCacheResult(true,
                ResourceCacheDataFormat.ARN,
                responseByteString.ToByteArray(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                newEtag.ToString());

            var activityMonitor = (ActivityMonitorFactory)PrivateFunctionAccessHelper.GetPrivateField(typeof(ResourceProxyClient), "ResourceProxyClientGetResourceAsync", null, true);
            using var methodMonitor = activityMonitor.ToMonitor();

            methodMonitor.OnStart();
            var resourceResponse = (ResourceResponse)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient),
                "ConvertCacheResultToResourceResponse", new object[] { correlationId, cacheResult });

            var dataLabResponse = (DataLabsResourceResponse)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient),
                "ConvertToResourceResponse",
                new object[] { correlationId, "GetResourceAsync", 0, resourceType, resourceResponse, false, methodMonitor });

            var collisionCheck = (bool)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient),
                "CacheCollisionCheck", new object[] { dataLabResponse, "testId", tenantId, null });

            methodMonitor.OnCompleted();

            Assert.IsTrue(resourceResponse.ResponseEpochTime > 0);
            Assert.AreEqual(correlationId, resourceResponse.CorrelationId);
            Assert.IsNull(resourceResponse.Error);
            Assert.AreEqual(ProxyDataFormat.Arn, resourceResponse.Success.Format);
            Assert.AreEqual(newEtag.ToString(), resourceResponse.Success.Etag.ToString());
            Assert.AreEqual(ProxyDataSource.Cache, resourceResponse.Success.DataSource);
            Assert.IsTrue(collisionCheck);
        }

        [TestMethod]
        public void CacheCollisionCheckARMSuccessTest()
        {
            var context = Tracer.CreateNewActivityContext();
            var traceId = Tracer.ConvertToActivityId(context);

            var armResource = ResourceProxyClientTestData.CreateARMResource(ResourceProxyClientTestData.VirtualMachineArmResource);
            var armId = armResource.Id;
            var resourceType = ArmUtils.GetResourceType(armId);
            var correlationId = Guid.NewGuid().ToString();
            string tenantId = null;

            var activityMonitor = (ActivityMonitorFactory)PrivateFunctionAccessHelper.GetPrivateField(typeof(ResourceProxyClient), "ResourceProxyClientGetResourceAsync", null, true);
            using var methodMonitor = activityMonitor.ToMonitor();

            methodMonitor.OnStart();

            var dataLabResponse = new DataLabsResourceResponse(
                DateTimeOffset.UtcNow, 
                correlationId,
                null,
                new DataLabsARMSuccessResponse(armResource, DateTimeOffset.UtcNow),
                null,
                null,
                DataLabsDataSource.CACHE);

            var collisionCheck = (bool)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient),
                "CacheCollisionCheck", new object[] { dataLabResponse, armId, tenantId, null });

            methodMonitor.OnCompleted();

            Assert.IsFalse(collisionCheck);
        }

        [TestMethod]
        public void CacheCollisionCheckARMFailTest()
        {
            var context = Tracer.CreateNewActivityContext();
            var traceId = Tracer.ConvertToActivityId(context);

            var armResource = ResourceProxyClientTestData.CreateARMResource(ResourceProxyClientTestData.VirtualMachineArmResource);
            var armId = armResource.Id;
            var resourceType = ArmUtils.GetResourceType(armId);
            var correlationId = Guid.NewGuid().ToString();
            string tenantId = null;

            var activityMonitor = (ActivityMonitorFactory)PrivateFunctionAccessHelper.GetPrivateField(typeof(ResourceProxyClient), "ResourceProxyClientGetResourceAsync", null, true);
            using var methodMonitor = activityMonitor.ToMonitor();

            methodMonitor.OnStart();

            var dataLabResponse = new DataLabsResourceResponse(
                DateTimeOffset.UtcNow,
                correlationId,
                null,
                new DataLabsARMSuccessResponse(armResource, DateTimeOffset.UtcNow),
                null,
                null,
                DataLabsDataSource.CACHE);

            var collisionCheck = (bool)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient),
                "CacheCollisionCheck", new object[] { dataLabResponse, "testId", tenantId, null });

            methodMonitor.OnCompleted();

            Assert.IsTrue(collisionCheck);
        }

        [TestMethod]
        public void CacheNotFoundEntryConvertTest()
        {
            var context = Tracer.CreateNewActivityContext();
            var traceId = Tracer.ConvertToActivityId(context);

            var eventGridEvent = ResourceProxyClientTestData.ParseEvents(ResourceProxyClientTestData.VirtualMachineEvent);
            var armId = eventGridEvent.Data.Resources[0].ResourceId;
            var correlationId = eventGridEvent.Data.Resources[0].CorrelationId;
            var tenantId = eventGridEvent.Data.Resources[0].ResourceHomeTenantId;
            var eventTime = eventGridEvent.EventTime;

            var request = new DataLabsResourceRequest(traceId, 0, correlationId, armId, tenantId);
            var cacheResult = new ResourceCacheResult(true,
                ResourceCacheDataFormat.NotFoundEntry,
                Array.Empty<byte>(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                null);

            var activityMonitor = (ActivityMonitorFactory)PrivateFunctionAccessHelper.GetPrivateField(typeof(ResourceProxyClient), "ResourceProxyClientGetResourceAsync", null, true);
            using var methodMonitor = activityMonitor.ToMonitor();

            methodMonitor.OnStart();
            var resourceResponse = (ResourceResponse)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient),
                "ConvertCacheResultToResourceResponse", new object[] { correlationId, cacheResult });
            methodMonitor.OnCompleted();

            Assert.IsTrue(resourceResponse.ResponseEpochTime > 0);
            Assert.AreEqual(correlationId, resourceResponse.CorrelationId);
            Assert.IsNull(resourceResponse.Success);

            Assert.AreEqual(ProxyErrorType.Retry, resourceResponse.Error.Type);
            Assert.AreEqual(0, resourceResponse.Error.RetryAfter);
            Assert.AreEqual((int)HttpStatusCode.NotFound, resourceResponse.Error.HttpStatusCode);
            Assert.AreEqual(SolutionConstants.NotFoundEntryExistInCache, resourceResponse.Error.Message);
            Assert.AreEqual(ClientProviderType.Cache.FastEnumToString(), resourceResponse.Error.FailedComponent);
            Assert.AreEqual(ProxyDataSource.Cache, resourceResponse.Error.DataSource);
        }

        [TestMethod]
        public void DeletedResourceNotFoundResponseConvertTest()
        {
            var eventGridEvent = ResourceProxyClientTestData.ParseEvents(ResourceProxyClientTestData.VirtualMachineDeletedEvent);
            var armId = eventGridEvent.Data.Resources[0].ResourceId;
            var correlationId = eventGridEvent.Data.Resources[0].CorrelationId;
            var tenantId = eventGridEvent.Data.Resources[0].ResourceHomeTenantId;
            var eventTime = eventGridEvent.EventTime;
            var resourceType = ArmUtils.GetResourceType(armId);

            var responseByteString = SerializationHelper.SerializeToByteString(eventGridEvent, false);

            var responseEpochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var resourceResponse = new ResourceResponse()
            {
                ResponseEpochTime = responseEpochTime,
                CorrelationId = correlationId,
                Success = new SuccessResponse()
                {
                    Format = ProxyDataFormat.Arn,
                    OutputData = responseByteString,
                    Etag = null,
                    DataSource = ProxyDataSource.OutputSourceoftruth
                }
            };

            var activityMonitor = (ActivityMonitorFactory)PrivateFunctionAccessHelper.GetPrivateField(typeof(ResourceProxyClient), "ResourceProxyClientGetResourceAsync", null, true);
            using var methodMonitor = activityMonitor.ToMonitor();

            methodMonitor.OnStart();
            var dataLabResponse = (DataLabsResourceResponse)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient), "ConvertToResourceResponse",
                new object[] { correlationId, "GetResourceAsync", 0, resourceType, resourceResponse, false, methodMonitor });
            methodMonitor.OnCompleted();

            Assert.AreEqual(correlationId, dataLabResponse.CorrelationId);
            Assert.IsTrue(dataLabResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.IsNull(dataLabResponse.SuccessARNV3Response);
            Assert.IsNull(dataLabResponse.SuccessARMResponse);
            Assert.IsNotNull(dataLabResponse.ErrorResponse);
            Assert.IsTrue(dataLabResponse.Attributes == null || dataLabResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabResponse.DataSource == DataLabsDataSource.OUTPUTSOURCEOFTRUTH);

            var errorResponse = dataLabResponse.ErrorResponse;
            Assert.AreEqual(DataLabsErrorType.RETRY, errorResponse.ErrorType);
            Assert.AreEqual(0, errorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual(404, errorResponse.HttpStatusCode);
            Assert.AreEqual(ResourceProxyClientError.DELETE_TO_NOT_FOUND.FastEnumToString(),errorResponse.ErrorDescription);
            Assert.AreEqual(SolutionConstants.ResourceProxyClient, errorResponse.FailedComponent);
        }

        [TestMethod]
        public void DeletedResourceNotFoundCacheResponseConvertTest()
        {
            var context = Tracer.CreateNewActivityContext();
            var traceId = Tracer.ConvertToActivityId(context);

            var eventGridEvent = ResourceProxyClientTestData.ParseEvents(ResourceProxyClientTestData.VirtualMachineDeletedEvent);
            var armId = eventGridEvent.Data.Resources[0].ResourceId;
            var resourceType = ArmUtils.GetResourceType(armId);
            var correlationId = eventGridEvent.Data.Resources[0].CorrelationId;
            var tenantId = eventGridEvent.Data.Resources[0].ResourceHomeTenantId;

            var responseByteString = SerializationHelper.SerializeToByteString(eventGridEvent, false);

            var newEtag = new ETag(Guid.NewGuid().ToString());
            var request = new DataLabsResourceRequest(traceId, 0, correlationId, armId, tenantId);

            var cacheResult = new ResourceCacheResult(true, 
                ResourceCacheDataFormat.ARN,
                responseByteString.ToByteArray(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                newEtag.ToString());
            
            var activityMonitor = (ActivityMonitorFactory)PrivateFunctionAccessHelper.GetPrivateField(typeof(ResourceProxyClient), 
                "ResourceProxyClientGetResourceAsync", null, true);
            using var methodMonitor = activityMonitor.ToMonitor();

            methodMonitor.OnStart();

            var resourceResponse = (ResourceResponse)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient),
                "ConvertCacheResultToResourceResponse", new object[] { correlationId, cacheResult });
            
            var dataLabResponse = (DataLabsResourceResponse)PrivateFunctionAccessHelper.RunStaticMethod(typeof(ResourceProxyClient), "ConvertToResourceResponse",
                new object[] { correlationId, "GetResourceAsync", 0, resourceType, resourceResponse, false, methodMonitor });
            methodMonitor.OnCompleted();

            Assert.AreEqual(correlationId, dataLabResponse.CorrelationId);
            Assert.IsTrue(dataLabResponse.ResponseTime.ToUnixTimeMilliseconds() > 0);
            Assert.IsNull(dataLabResponse.SuccessARNV3Response);
            Assert.IsNull(dataLabResponse.SuccessARMResponse);
            Assert.IsNotNull(dataLabResponse.ErrorResponse);
            Assert.IsTrue(dataLabResponse.Attributes == null || dataLabResponse.Attributes.Count == 0);
            Assert.IsTrue(dataLabResponse.DataSource == DataLabsDataSource.CACHE);

            var errorResponse = dataLabResponse.ErrorResponse;
            Assert.AreEqual(DataLabsErrorType.RETRY, errorResponse.ErrorType);
            Assert.AreEqual(0, errorResponse.RetryDelayInMilliseconds);
            Assert.AreEqual(404, errorResponse.HttpStatusCode);
            Assert.AreEqual(ResourceProxyClientError.DELETE_TO_NOT_FOUND.FastEnumToString(), errorResponse.ErrorDescription);
            Assert.AreEqual(SolutionConstants.ResourceProxyClient, errorResponse.FailedComponent);
        }
    }
}