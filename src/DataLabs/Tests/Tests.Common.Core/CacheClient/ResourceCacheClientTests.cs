namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.CacheClient
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient.SelectionStrategy;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using System.Text;
    using System.Threading;

    [TestClass]
    public class ResourceCacheClientTests
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

        private static CacheClientExecutor GetCacheClientExecutor(ResourceCacheClient resourceCacheClient)
        {
            return (CacheClientExecutor)PrivateFunctionAccessHelper.GetPrivateField(
                        typeof(ResourceCacheClient),
                        "_cacheClientExecutor", resourceCacheClient);
        }

        private static int GetCacheReadQuorum(ResourceCacheClient resourceCacheClient)
        {
            return (int)PrivateFunctionAccessHelper.GetPrivateField(
                        typeof(ResourceCacheClient),
                        "_cacheReadQuorum", resourceCacheClient);
        }

        private static DataLabCachePoolsManager GetDataLabCachePoolsManager(CacheClientExecutor cacheClientExecutor)
        {
            return (DataLabCachePoolsManager)PrivateFunctionAccessHelper.GetPrivateField(
                        typeof(CacheClientExecutor),
                        "_dataLabCachePoolsManager", cacheClientExecutor);
        }

        [TestMethod]
        [DataRow(CacheNodeSelectionMode.HashModular, DisplayName = "HashModular")]
        [DataRow(CacheNodeSelectionMode.JumpHash, DisplayName = "JumpHash")]
        public async Task CacheReadQuorumTest(CacheNodeSelectionMode nodeSelectionMode)
        {
            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                $"CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3278;StartOffset=0;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-1"] =
                $"CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3279;StartOffset=2;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-2"] =
                $"CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3280;StartOffset=4;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            ConfigMapUtil.Configuration[SolutionConstants.ResourceCacheReadQuorum] = "3";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);
            var cacheTTLManager = new CacheTTLManager(ConfigMapUtil.Configuration);
            var resourceCacheClient = new ResourceCacheClient(cacheClient, cacheTTLManager, ConfigMapUtil.Configuration);

            var cacheReadQuorum = GetCacheReadQuorum(resourceCacheClient);
            Assert.AreEqual(3, cacheReadQuorum);

            var cacheClientExecutor = GetCacheClientExecutor(resourceCacheClient);
            Assert.IsNotNull(cacheClientExecutor);

            var testKey = "testKey1";
            var testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue"));
            var testTenantId = "testTenantId";

            // Set Value
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var setResult = await resourceCacheClient.SetResourceAsync(
                resourceId: testKey,
                tenantId: testTenantId,
                dataFormat: ResourceCacheDataFormat.ARM,
                resource: testValue,
                timeStamp: currentTime,
                etag: null,
            expiry: null,
            cancellationToken: default).ConfigureAwait(false);
            Assert.IsTrue(setResult);

            // Set Value 
            var cacheKey = resourceCacheClient.GetCacheKey(testKey, testTenantId);
            var cacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, testValue, currentTime, null);
            var CacheClientWriteResult = await cacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheKey, cacheValue, currentTime, null, default).ConfigureAwait(false);
            Assert.AreEqual(3, CacheClientWriteResult.SelectedCacheNodes.Count);
            Assert.AreEqual(3, CacheClientWriteResult.SuccessNodeResults.Count);
            Assert.IsNull(CacheClientWriteResult.FailedNodeResults);
            Assert.AreEqual(3, CacheClientWriteResult.SuccessNodeCount);
            Assert.AreEqual(0, CacheClientWriteResult.FailedNodeCount);

            // Let's try to get value and verify if it has currentTime
            var resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
            Assert.IsTrue(resourceCacheResult.Found);
            Assert.IsNull(resourceCacheResult.Etag);
            Assert.AreEqual(currentTime, resourceCacheResult.DataTimeStamp);
            Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
            Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
            Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));

            // Let try to set value to one Node with new timestamp
            var newTime = currentTime + 2000;
            var newCacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, testValue, newTime, null);
            
            var tempResourceCacheResult = ResourceCacheUtils.DecompressCacheValue(newCacheValue.ToArray());
            Assert.IsTrue(tempResourceCacheResult.Found);
            Assert.AreEqual(ResourceCacheDataFormat.ARM, tempResourceCacheResult.DataFormat);
            Assert.AreEqual("testValue", Encoding.UTF8.GetString(tempResourceCacheResult.Content.ToArray()));
            Assert.AreEqual(newTime, tempResourceCacheResult.DataTimeStamp);
            Assert.IsTrue(tempResourceCacheResult.InsertionTimeStamp > 0);

            var firstNode = CacheClientWriteResult.SelectedCacheNodes[0];
            var firstNodeWriteResult = await CacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheNode: firstNode, key: cacheKey, readOnlyBytes: newCacheValue, greaterThanValue: newTime, expiry: null).ConfigureAwait(false);
            Assert.IsTrue(firstNodeWriteResult);

            // Let's try to get value and verify if it has newer timestamp
            for (int i = 0; i < 10; i++ )
            {
                resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
                Assert.IsTrue(resourceCacheResult.Found);
                Assert.IsNull(resourceCacheResult.Etag);
                Assert.AreEqual(newTime, resourceCacheResult.DataTimeStamp);
                Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
                Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
                Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));
            }
        }

        [TestMethod]
        [DataRow(CacheNodeSelectionMode.HashModular, DisplayName = "HashModular")]
        [DataRow(CacheNodeSelectionMode.JumpHash, DisplayName = "JumpHash")]
        public async Task CacheReadQuorumWithPartialReadFailTest(CacheNodeSelectionMode nodeSelectionMode)
        {
            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                $"CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3278;StartOffset=0;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-1"] =
                $"CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3279;StartOffset=2;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-2"] =
                $"CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3280;StartOffset=4;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            ConfigMapUtil.Configuration[SolutionConstants.ResourceCacheReadQuorum] = "2";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);
            var cacheTTLManager = new CacheTTLManager(ConfigMapUtil.Configuration);
            var resourceCacheClient = new ResourceCacheClient(cacheClient, cacheTTLManager, ConfigMapUtil.Configuration);

            var cacheReadQuorum = GetCacheReadQuorum(resourceCacheClient);
            Assert.AreEqual(2, cacheReadQuorum);

            var cacheClientExecutor = GetCacheClientExecutor(resourceCacheClient);
            Assert.IsNotNull(cacheClientExecutor);

            var testKey = "testKey1";
            var testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue"));
            var testTenantId = "testTenantId";

            // Set Value
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var setResult = await resourceCacheClient.SetResourceAsync(
                resourceId: testKey,
                tenantId: testTenantId,
                dataFormat: ResourceCacheDataFormat.ARM,
                resource: testValue,
                timeStamp: currentTime,
                etag: null,
            expiry: null,
            cancellationToken: default).ConfigureAwait(false);
            Assert.IsTrue(setResult);

            // Set Value 
            var cacheKey = resourceCacheClient.GetCacheKey(testKey, testTenantId);
            var cacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, testValue, currentTime, null);
            var CacheClientWriteResult = await cacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheKey, cacheValue, currentTime, null, default).ConfigureAwait(false);
            Assert.AreEqual(3, CacheClientWriteResult.SelectedCacheNodes.Count);
            Assert.AreEqual(3, CacheClientWriteResult.SuccessNodeResults.Count);
            Assert.IsNull(CacheClientWriteResult.FailedNodeResults);
            Assert.AreEqual(3, CacheClientWriteResult.SuccessNodeCount);
            Assert.AreEqual(0, CacheClientWriteResult.FailedNodeCount);

            // Let's try to get value and verify if it has currentTime
            var resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
            Assert.IsTrue(resourceCacheResult.Found);
            Assert.IsNull(resourceCacheResult.Etag);
            Assert.AreEqual(currentTime, resourceCacheResult.DataTimeStamp);
            Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
            Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
            Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));

            // Let try to set value to two node with new timestamp
            var newTime = currentTime + 2000;
            var newCacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, testValue, newTime, null);

            var tempResourceCacheResult = ResourceCacheUtils.DecompressCacheValue(newCacheValue.ToArray());
            Assert.IsTrue(tempResourceCacheResult.Found);
            Assert.AreEqual(ResourceCacheDataFormat.ARM, tempResourceCacheResult.DataFormat);
            Assert.AreEqual("testValue", Encoding.UTF8.GetString(tempResourceCacheResult.Content.ToArray()));
            Assert.AreEqual(newTime, tempResourceCacheResult.DataTimeStamp);
            Assert.IsTrue(tempResourceCacheResult.InsertionTimeStamp > 0);

            var firstNode = CacheClientWriteResult.SelectedCacheNodes[0];
            var firstNodeWriteResult = await CacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheNode: firstNode, key: cacheKey, readOnlyBytes: newCacheValue, greaterThanValue: newTime, expiry: null).ConfigureAwait(false);
            Assert.IsTrue(firstNodeWriteResult);

            var secondNode = CacheClientWriteResult.SelectedCacheNodes[1];
            var secondNodeWriteResult = await CacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheNode: secondNode, key: cacheKey, readOnlyBytes: newCacheValue, greaterThanValue: newTime, expiry: null).ConfigureAwait(false);
            Assert.IsTrue(secondNodeWriteResult);

            // Let's try to get value and verify if it has newer timestamp
            for (int i = 0; i < 10; i++)
            {
                resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
                Assert.IsTrue(resourceCacheResult.Found);
                Assert.IsNull(resourceCacheResult.Etag);
                Assert.AreEqual(newTime, resourceCacheResult.DataTimeStamp);
                Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
                Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
                Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));
            }

            // Let's set one of node with newer timestamp to return exception and verify if we still get newer timestamp
            var testCacheNode = testFactory.TestCacheClientMap[firstNode.CacheNodeName];
            testCacheNode.ReturnGetError = true;

            // Let's try to get value and verify if it has newer timestamp
            for (int i = 0; i < 10; i++)
            {
                resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
                Assert.IsTrue(resourceCacheResult.Found);
                Assert.IsNull(resourceCacheResult.Etag);
                Assert.AreEqual(newTime, resourceCacheResult.DataTimeStamp);
                Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
                Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
                Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));
            }

            // Let's set another node with newer timestamp to return exception and verify if we get old timestamp because two nodes with newer timestamp got exception
            testCacheNode = testFactory.TestCacheClientMap[secondNode.CacheNodeName];
            testCacheNode.ReturnGetError = true;
            for (int i = 0; i < 10; i++)
            {
                resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
                Assert.IsTrue(resourceCacheResult.Found);
                Assert.IsNull(resourceCacheResult.Etag);
                Assert.AreEqual(currentTime, resourceCacheResult.DataTimeStamp);
                Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
                Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
                Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));
            }
        }

        [TestMethod]
        [DataRow(CacheNodeSelectionMode.HashModular, DisplayName = "HashModular")]
        [DataRow(CacheNodeSelectionMode.JumpHash, DisplayName = "JumpHash")]
        public async Task CacheReadQuorumWithMajorityReadFailTest(CacheNodeSelectionMode nodeSelectionMode)
        {
            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                $"CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3278;StartOffset=0;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-1"] =
                $"CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3279;StartOffset=2;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-2"] =
                $"CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3280;StartOffset=4;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            ConfigMapUtil.Configuration[SolutionConstants.ResourceCacheReadQuorum] = "2";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);
            var cacheTTLManager = new CacheTTLManager(ConfigMapUtil.Configuration);
            var resourceCacheClient = new ResourceCacheClient(cacheClient, cacheTTLManager, ConfigMapUtil.Configuration);

            var cacheReadQuorum = GetCacheReadQuorum(resourceCacheClient);
            Assert.AreEqual(2, cacheReadQuorum);

            var cacheClientExecutor = GetCacheClientExecutor(resourceCacheClient);
            Assert.IsNotNull(cacheClientExecutor);

            var testKey = "testKey1";
            var testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue"));
            var testTenantId = "testTenantId";

            // Set Value
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var setResult = await resourceCacheClient.SetResourceAsync(
                resourceId: testKey,
                tenantId: testTenantId,
                dataFormat: ResourceCacheDataFormat.ARM,
                resource: testValue,
                timeStamp: currentTime,
                etag: null,
            expiry: null,
            cancellationToken: default).ConfigureAwait(false);
            Assert.IsTrue(setResult);

            // Set Value 
            var cacheKey = resourceCacheClient.GetCacheKey(testKey, testTenantId);
            var cacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, testValue, currentTime, null);
            var CacheClientWriteResult = await cacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheKey, cacheValue, currentTime, null, default).ConfigureAwait(false);
            Assert.AreEqual(3, CacheClientWriteResult.SelectedCacheNodes.Count);
            Assert.AreEqual(3, CacheClientWriteResult.SuccessNodeResults.Count);
            Assert.IsNull(CacheClientWriteResult.FailedNodeResults);
            Assert.AreEqual(3, CacheClientWriteResult.SuccessNodeCount);
            Assert.AreEqual(0, CacheClientWriteResult.FailedNodeCount);

            // Let's try to get value and verify if it has currentTime
            var resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
            Assert.IsTrue(resourceCacheResult.Found);
            Assert.IsNull(resourceCacheResult.Etag);
            Assert.AreEqual(currentTime, resourceCacheResult.DataTimeStamp);
            Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
            Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
            Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));

            // Let try to set value to two node with new timestamp
            var newTime = currentTime + 2000;
            var newCacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, testValue, newTime, null);

            var tempResourceCacheResult = ResourceCacheUtils.DecompressCacheValue(newCacheValue.ToArray());
            Assert.IsTrue(tempResourceCacheResult.Found);
            Assert.AreEqual(ResourceCacheDataFormat.ARM, tempResourceCacheResult.DataFormat);
            Assert.AreEqual("testValue", Encoding.UTF8.GetString(tempResourceCacheResult.Content.ToArray()));
            Assert.AreEqual(newTime, tempResourceCacheResult.DataTimeStamp);
            Assert.IsTrue(tempResourceCacheResult.InsertionTimeStamp > 0);

            var firstNode = CacheClientWriteResult.SelectedCacheNodes[0];
            var firstNodeWriteResult = await CacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheNode: firstNode, key: cacheKey, readOnlyBytes: newCacheValue, greaterThanValue: newTime, expiry: null).ConfigureAwait(false);
            Assert.IsTrue(firstNodeWriteResult);

            var secondNode = CacheClientWriteResult.SelectedCacheNodes[1];
            var secondNodeWriteResult = await CacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheNode: secondNode, key: cacheKey, readOnlyBytes: newCacheValue, greaterThanValue: newTime, expiry: null).ConfigureAwait(false);
            Assert.IsTrue(secondNodeWriteResult);

            // Let's try to get value and verify if it has newer timestamp
            for (int i = 0; i < 10; i++)
            {
                resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
                Assert.IsTrue(resourceCacheResult.Found);
                Assert.IsNull(resourceCacheResult.Etag);
                Assert.AreEqual(newTime, resourceCacheResult.DataTimeStamp);
                Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
                Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
                Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));
            }

            // Let's set one of node with newer timestamp to return exception and verify if we still get newer timestamp
            var testCacheNode = testFactory.TestCacheClientMap[firstNode.CacheNodeName];
            testCacheNode.ReturnGetError = true;

            // Let's set another node with newer timestamp to return exception and verify if we get old timestamp because two nodes with newer timestamp got exception
            testCacheNode = testFactory.TestCacheClientMap[secondNode.CacheNodeName];
            testCacheNode.ReturnGetError = true;
            for (int i = 0; i < 10; i++)
            {
                resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
                Assert.IsTrue(resourceCacheResult.Found);
                Assert.IsNull(resourceCacheResult.Etag);
                Assert.AreEqual(currentTime, resourceCacheResult.DataTimeStamp);
                Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
                Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
                Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));
            }
        }

        [TestMethod]
        public async Task CacheScaleUpTest()
        {
            CacheNodeSelectionMode nodeSelectionMode = CacheNodeSelectionMode.JumpHash;

            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                $"CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3278;StartOffset=0;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-1"] =
                $"CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3279;StartOffset=2;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-2"] =
                $"CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3280;StartOffset=4;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            ConfigMapUtil.Configuration[SolutionConstants.ResourceCacheReadQuorum] = "2";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);
            var cacheTTLManager = new CacheTTLManager(ConfigMapUtil.Configuration);
            var resourceCacheClient = new ResourceCacheClient(cacheClient, cacheTTLManager, ConfigMapUtil.Configuration);

            var cacheReadQuorum = GetCacheReadQuorum(resourceCacheClient);
            Assert.AreEqual(2, cacheReadQuorum);

            var cacheClientExecutor = GetCacheClientExecutor(resourceCacheClient);
            Assert.IsNotNull(cacheClientExecutor);

            var dataLabCachePoolsManager = GetDataLabCachePoolsManager(cacheClientExecutor);
            var cachePools = dataLabCachePoolsManager.CachePools;
            Assert.AreEqual(3, cachePools.Length);
            Assert.AreEqual(5, cachePools[0].CacheNodes.Length);
            Assert.AreEqual(5, cachePools[1].CacheNodes.Length);
            Assert.AreEqual(5, cachePools[2].CacheNodes.Length);

            var testKey = "testKey1";
            var testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue"));
            var testTenantId = "testTenantId";

            // Set Value
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var setResult = await resourceCacheClient.SetResourceIfGreaterThanAsync(
                resourceId: testKey,
                tenantId: testTenantId,
                dataFormat: ResourceCacheDataFormat.ARM,
                resource: testValue,
                timeStamp: currentTime,
                etag: null,
                expiry: TimeSpan.FromHours(2),
                cancellationToken: default).ConfigureAwait(false);
            Assert.IsTrue(setResult);

            // Set Value 
            var cacheKey = resourceCacheClient.GetCacheKey(testKey, testTenantId);
            var cacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, testValue, currentTime, null);
            var cacheClientWriteResult = await cacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheKey, cacheValue, currentTime, TimeSpan.FromHours(2), default).ConfigureAwait(false);
            Assert.AreEqual(3, cacheClientWriteResult.SelectedCacheNodes.Count);
            Assert.AreEqual(3, cacheClientWriteResult.SuccessNodeResults.Count);
            Assert.IsNull(cacheClientWriteResult.FailedNodeResults);
            Assert.AreEqual(3, cacheClientWriteResult.SuccessNodeCount);
            Assert.AreEqual(0, cacheClientWriteResult.FailedNodeCount);

            // Let's try to get value and verify if it has currentTime
            var resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
            Assert.IsTrue(resourceCacheResult.Found);
            Assert.IsNull(resourceCacheResult.Etag);
            Assert.AreEqual(currentTime, resourceCacheResult.DataTimeStamp);
            Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
            Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
            Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));

            // Let's try to scale up
            ConfigMapUtil.Configuration["CachePool-0"] =
                $"CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=10;Port=3278;StartOffset=0;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(default);
            await Task.Delay(50);

            Assert.AreEqual(3, cachePools.Length);
            Assert.AreEqual(10, cachePools[0].CacheNodes.Length);
            Assert.AreEqual(5, cachePools[1].CacheNodes.Length);
            Assert.AreEqual(5, cachePools[2].CacheNodes.Length);

            // Let try to set value with new timestamp after scale up
            var newTime = currentTime + 2000;
            var newCacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, testValue, newTime, null);
            var newCacheClientWriteResult = await cacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheKey, newCacheValue, newTime, TimeSpan.FromHours(2), default).ConfigureAwait(false);
            Assert.AreEqual(3, newCacheClientWriteResult.SelectedCacheNodes.Count);
            Assert.AreEqual(3, newCacheClientWriteResult.SuccessNodeResults.Count);
            Assert.IsNull(newCacheClientWriteResult.FailedNodeResults);
            Assert.AreEqual(3, newCacheClientWriteResult.SuccessNodeCount);
            Assert.AreEqual(0, newCacheClientWriteResult.FailedNodeCount);

            // Original cache Client Nodes and New Cache Client nodes might not be same because of scale up
            // With above cache key, it must cause different cachePool1's node
            Assert.AreNotEqual(cacheClientWriteResult.SelectedCacheNodes[0].CacheNodeName,
                newCacheClientWriteResult.SelectedCacheNodes[0].CacheNodeName);
            Assert.AreEqual(cacheClientWriteResult.SelectedCacheNodes[1].CacheNodeName,
                newCacheClientWriteResult.SelectedCacheNodes[1].CacheNodeName);
            Assert.AreEqual(cacheClientWriteResult.SelectedCacheNodes[2].CacheNodeName,
                newCacheClientWriteResult.SelectedCacheNodes[2].CacheNodeName);

            // Let's try to get value and verify if it has newer timestamp
            for (int i = 0; i < 10; i++)
            {
                resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
                Assert.IsTrue(resourceCacheResult.Found);
                Assert.IsNull(resourceCacheResult.Etag);
                Assert.AreEqual(newTime, resourceCacheResult.DataTimeStamp);
                Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
                Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
                Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));
            }
        }

        [TestMethod]
        public async Task CacheScaleDownTest()
        {
            CacheNodeSelectionMode nodeSelectionMode = CacheNodeSelectionMode.JumpHash;

            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                $"CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=10;Port=3278;StartOffset=0;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-1"] =
                $"CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=10;Port=3279;StartOffset=3;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-2"] =
                $"CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=10;Port=3280;StartOffset=6;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            var numCacheNodes = 10;

            ConfigMapUtil.Configuration[SolutionConstants.ResourceCacheReadQuorum] = "2";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);
            var cacheTTLManager = new CacheTTLManager(ConfigMapUtil.Configuration);
            var resourceCacheClient = new ResourceCacheClient(cacheClient, cacheTTLManager, ConfigMapUtil.Configuration);

            var cacheReadQuorum = GetCacheReadQuorum(resourceCacheClient);
            Assert.AreEqual(2, cacheReadQuorum);

            var cacheClientExecutor = GetCacheClientExecutor(resourceCacheClient);
            Assert.IsNotNull(cacheClientExecutor);

            var dataLabCachePoolsManager = GetDataLabCachePoolsManager(cacheClientExecutor);
            var cachePools = dataLabCachePoolsManager.CachePools;
            Assert.AreEqual(3, cachePools.Length);
            Assert.AreEqual(numCacheNodes, cachePools[0].CacheNodes.Length);
            Assert.AreEqual(numCacheNodes, cachePools[1].CacheNodes.Length);
            Assert.AreEqual(numCacheNodes, cachePools[2].CacheNodes.Length);

            var testKey = "testKey1";
            var testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue"));
            var testTenantId = "testTenantId";

            // Set Value
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var setResult = await resourceCacheClient.SetResourceIfGreaterThanAsync(
                resourceId: testKey,
                tenantId: testTenantId,
                dataFormat: ResourceCacheDataFormat.ARM,
                resource: testValue,
                timeStamp: currentTime,
                etag: null,
                expiry: TimeSpan.FromHours(2),
                cancellationToken: default).ConfigureAwait(false);
            Assert.IsTrue(setResult);

            // Set Value 
            var cacheKey = resourceCacheClient.GetCacheKey(testKey, testTenantId);
            var cacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, testValue, currentTime, null);
            var cacheClientWriteResult = await cacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheKey, cacheValue, currentTime, TimeSpan.FromHours(2), default).ConfigureAwait(false);
            Assert.AreEqual(3, cacheClientWriteResult.SelectedCacheNodes.Count);
            Assert.AreEqual(3, cacheClientWriteResult.SuccessNodeResults.Count);
            Assert.IsNull(cacheClientWriteResult.FailedNodeResults);
            Assert.AreEqual(3, cacheClientWriteResult.SuccessNodeCount);
            Assert.AreEqual(0, cacheClientWriteResult.FailedNodeCount);

            // Let's try to get value and verify if it has currentTime
            var resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
            Assert.IsTrue(resourceCacheResult.Found);
            Assert.IsNull(resourceCacheResult.Etag);
            Assert.AreEqual(currentTime, resourceCacheResult.DataTimeStamp);
            Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
            Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
            Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));

            // Let's try to scale down
            ConfigMapUtil.Configuration["CachePool-0"] =
                $"CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=4;Port=3278;StartOffset=0;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration.CheckChangeAndCallBack(default);
            await Task.Delay(50);

            Assert.AreEqual(3, cachePools.Length);
            Assert.AreEqual(4, cachePools[0].CacheNodes.Length);
            Assert.AreEqual(numCacheNodes, cachePools[1].CacheNodes.Length);
            Assert.AreEqual(numCacheNodes, cachePools[2].CacheNodes.Length);

            // Let try to set value with new timestamp after scale up
            var newTime = currentTime + 2000;
            var newCacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, testValue, newTime, null);
            var newCacheClientWriteResult = await cacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheKey, newCacheValue, newTime, TimeSpan.FromHours(2), default).ConfigureAwait(false);
            Assert.AreEqual(3, newCacheClientWriteResult.SelectedCacheNodes.Count);
            Assert.AreEqual(3, newCacheClientWriteResult.SuccessNodeResults.Count);
            Assert.IsNull(newCacheClientWriteResult.FailedNodeResults);
            Assert.AreEqual(3, newCacheClientWriteResult.SuccessNodeCount);
            Assert.AreEqual(0, newCacheClientWriteResult.FailedNodeCount);

            // Original cache Client Nodes and New Cache Client nodes might not be same because of scale up
            // With above cache key, it must cause different cachePool1's node
            Assert.AreNotEqual(cacheClientWriteResult.SelectedCacheNodes[0].CacheNodeName,
                newCacheClientWriteResult.SelectedCacheNodes[0].CacheNodeName);
            Assert.AreEqual(cacheClientWriteResult.SelectedCacheNodes[1].CacheNodeName,
                newCacheClientWriteResult.SelectedCacheNodes[1].CacheNodeName);
            Assert.AreEqual(cacheClientWriteResult.SelectedCacheNodes[2].CacheNodeName,
                newCacheClientWriteResult.SelectedCacheNodes[2].CacheNodeName);

            // Let's try to get value and verify if it has newer timestamp
            for (int i = 0; i < 10; i++)
            {
                resourceCacheResult = await resourceCacheClient.GetResourceAsync(testKey, testTenantId, default).ConfigureAwait(false);
                Assert.IsTrue(resourceCacheResult.Found);
                Assert.IsNull(resourceCacheResult.Etag);
                Assert.AreEqual(newTime, resourceCacheResult.DataTimeStamp);
                Assert.IsTrue(resourceCacheResult.InsertionTimeStamp > 0);
                Assert.AreEqual(ResourceCacheDataFormat.ARM, resourceCacheResult.DataFormat);
                Assert.AreEqual("testValue", Encoding.UTF8.GetString(resourceCacheResult.Content.ToArray()));
            }
        }

        [TestMethod]
        [DataRow(CacheNodeSelectionMode.HashModular, 5000, DisplayName = "HashModular")]
        [DataRow(CacheNodeSelectionMode.JumpHash, 5000, DisplayName = "JumpHash")]
        [DataRow(CacheNodeSelectionMode.HashModular, 50, DisplayName = "HashModular")]
        [DataRow(CacheNodeSelectionMode.JumpHash, 50, DisplayName = "JumpHash")]
        public async Task CacheMGetTest(CacheNodeSelectionMode nodeSelectionMode, int numKeys)
        {
            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                $"CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3278;StartOffset=0;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-1"] =
                $"CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3279;StartOffset=2;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePool-2"] =
                $"CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3280;StartOffset=4;NodeSelectionMode={nodeSelectionMode}";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);
            var cacheTTLManager = new CacheTTLManager(ConfigMapUtil.Configuration);
            var resourceCacheClient = new ResourceCacheClient(cacheClient, cacheTTLManager, ConfigMapUtil.Configuration);
            var cacheClientExecutor = GetCacheClientExecutor(resourceCacheClient);
            var cachePoolManager = GetDataLabCachePoolsManager(cacheClientExecutor);

            Assert.IsNotNull(cacheClientExecutor);

            var keyList = new List<string>();
            var valueList = new List<ReadOnlyMemory<byte>>();
            var testTenantId = "testTenantId";

            for (int i = 0; i < numKeys; i++)
            {
                keyList.Add($"testKey{i}");
                valueList.Add(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes($"testValue{i}")));
            }

            var cacheKeyList = new List<string>();
            var cacheValueList = new List<ReadOnlyMemory<byte>>();

            // Set Value
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = 0; i < numKeys; i++)
            {
                var cacheKey = resourceCacheClient.GetCacheKey(keyList[i], testTenantId);
                cacheKeyList.Add(cacheKey);

                var cacheValue = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, valueList[i], currentTime, null);
                cacheValueList.Add(cacheValue);

                var cacheClientWriteResult = await cacheClientExecutor.SetValueIfGreaterThanWithExpiryAsync(cacheKey, cacheValue, currentTime, null, default).ConfigureAwait(false);

                Assert.AreEqual(3, cacheClientWriteResult.SelectedCacheNodes.Count);
                Assert.AreEqual(3, cacheClientWriteResult.SuccessNodeResults.Count);
                Assert.IsNull(cacheClientWriteResult.FailedNodeResults);
                Assert.AreEqual(3, cacheClientWriteResult.SuccessNodeCount);
                Assert.AreEqual(0, cacheClientWriteResult.FailedNodeCount);
            }

            // Use MGet with Quorum
            var cacheClientExecutorResult = await cacheClientExecutor.MGetValuesAsync(cacheKeyList, readQuorum: 2, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(cacheKeyList.Count, cacheClientExecutorResult.Count);

            for (int i = 0; i < numKeys; i++)
            {
                var (indexInClientResult, clientResult) = cacheClientExecutorResult[i];
                var resultValue = clientResult.SuccessNodeResults[0].Result[indexInClientResult];

                var expectedValue = cacheValueList[i].ToArray();
                Assert.IsTrue(expectedValue.SequenceEqual(resultValue));
            }

            // Use CacheClient's MGet interface
            var cacheClientResult = await cacheClient.MGetValuesAsync(cacheKeyList, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(cacheKeyList.Count, cacheClientResult.Count);

            for (int i = 0; i < numKeys; i++)
            {
                var resultValue = cacheClientResult[i];
                var expectedValue = cacheValueList[i].ToArray();
                Assert.IsTrue(expectedValue.SequenceEqual(resultValue));
            }

            // Make two of cache node fail
            testFactory.TestCacheClientMap.Values.First().ReturnGetError = true;
            testFactory.TestCacheClientMap.Values.Last().ReturnGetError = true;

            // Use MGet with Quorum
            cacheClientExecutorResult = await cacheClientExecutor.MGetValuesAsync(cacheKeyList, readQuorum: 2, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(cacheKeyList.Count, cacheClientExecutorResult.Count);

            for (int i = 0; i < numKeys; i++)
            {
                var (indexInClientResult, clientResult) = cacheClientExecutorResult[i];
                var resultValue = clientResult.SuccessNodeResults[0].Result[indexInClientResult];

                var expectedValue = cacheValueList[i].ToArray();
                Assert.IsTrue(expectedValue.SequenceEqual(resultValue));
            }

            // Use CacheClient's MGet interface
            cacheClientResult = await cacheClient.MGetValuesAsync(cacheKeyList, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(cacheKeyList.Count, cacheClientResult.Count);

            for (int i = 0; i < numKeys; i++)
            {
                var resultValue = cacheClientResult[i];
                var expectedValue = cacheValueList[i].ToArray();
                Assert.IsTrue(expectedValue.SequenceEqual(resultValue));
            }

            // Make all of cache node fail
            foreach (var cacheNode in testFactory.TestCacheClientMap.Values)
            {
                cacheNode.ReturnGetError = true;
            }

            // Use MGet with Quorum
            cacheClientExecutorResult = await cacheClientExecutor.MGetValuesAsync(cacheKeyList, readQuorum: 2, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(cacheKeyList.Count, cacheClientExecutorResult.Count);

            for (int i = 0; i < numKeys; i++)
            {
                var (indexInClientResult, clientResult) = cacheClientExecutorResult[i];

                // All are expected as fail because all of cache nodes failed
                Assert.IsFalse(clientResult.HasSuccess);
                Assert.IsTrue(clientResult.HasFailed);
                Assert.AreEqual(-1, indexInClientResult);
            }

            // Use CacheClient's MGet interface
            cacheClientResult = await cacheClient.MGetValuesAsync(cacheKeyList, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(cacheKeyList.Count, cacheClientResult.Count);

            for (int i = 0; i < numKeys; i++)
            {
                var resultValue = cacheClientResult[i];
                Assert.AreEqual(null, resultValue);
            }
        }
    }
}