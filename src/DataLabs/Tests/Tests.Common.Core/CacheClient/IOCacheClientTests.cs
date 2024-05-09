namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.CacheClient
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using System.Text;

    [TestClass]
    public class IOCacheClientTests
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

        private static CacheClientExecutor GetCacheClientExecutor(CacheClient cacheClient)
        {
            return (CacheClientExecutor)PrivateFunctionAccessHelper.GetPrivateField(
                        typeof(CacheClient),
                        "_cacheClientExecutor", cacheClient);
        }

        private static DataLabCachePoolsManager GetDataLabCachePoolsManager(CacheClientExecutor cacheClientExecutor)
        {
            return (DataLabCachePoolsManager)PrivateFunctionAccessHelper.GetPrivateField(
                        typeof(CacheClientExecutor),
                        "_dataLabCachePoolsManager", cacheClientExecutor);
        }

        [TestMethod]
        public async Task CacheGetAndSetSimpleTest()
        {
            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                 "CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3278;StartOffset=0;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            ConfigMapUtil.Configuration["CachePool-1"] =
                "CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3279;StartOffset=2;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            ConfigMapUtil.Configuration["CachePool-2"] =
                "CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3280;StartOffset=4;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);

            var testKey = "testKey1";
            var testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue"));

            // Set value
            var cacheResult = await cacheClient.SetValueWithExpiryAsync(testKey, testValue, TimeSpan.FromHours(1), CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(cacheResult);

            // Get Value
            var value = await cacheClient.GetValueAsync(testKey, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(Encoding.UTF8.GetString(testValue.ToArray()), Encoding.UTF8.GetString(value.ToArray()));
        }

        [TestMethod]
        public async Task CacheGetAndSetFullTest()
        {
            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                "CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3278;StartOffset=0;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            ConfigMapUtil.Configuration["CachePool-1"] =
                "CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3279;StartOffset=2;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            ConfigMapUtil.Configuration["CachePool-2"] =
                "CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3280;StartOffset=4;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePoolNodeReplicationMapping-2"] = "0=1";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);

            var cacheClientExecutor = GetCacheClientExecutor(cacheClient);
            var cachePoolManager = GetDataLabCachePoolsManager(cacheClientExecutor);

            // cache enabled
            Assert.IsTrue(cacheClient.CacheEnabled);
            Assert.IsTrue(cacheClientExecutor.CacheEnabled);

            // num cache pool
            Assert.AreEqual(3, cachePoolManager.CachePools.Length);

            for (int i = 0; i < cachePoolManager.CachePools.Length; i++)
            {
                var cachePool = cachePoolManager.CachePools[i];

                var cachePoolName = "iocache" + (i + 1);
                Assert.AreEqual(cacheDomain, cachePool.CacheDomain);
                Assert.AreEqual(cachePoolName, cachePool.CachePoolName);
                
                Assert.IsFalse(cachePool.IsDenyListed);
                Assert.IsTrue(cachePool.IsWriteEnabled);
                Assert.IsTrue(cachePool.IsReadEnabled);

                Assert.AreEqual(5, cachePool.CacheNodes.Length);
                Assert.AreEqual(3278 + i, cachePool.Port);

                for (int j = 0; j < cachePool.CacheNodes.Length; j++)
                {
                    var cacheNode = cachePool.CacheNodes[j];

                    var cacheNodeName = cachePool.CachePoolName + '-' + j;
                    var dnAddress = $"{cacheNodeName}.{cacheNode.CachePoolName}.{cachePool.CacheDomain}";
                    Assert.AreEqual(dnAddress, cacheNode.DnsAddress);
                    Assert.AreEqual(cachePool.CachePoolName, cacheNode.CachePoolName);
                    Assert.AreEqual(cacheNodeName, cacheNode.CacheNodeName);
                    Assert.AreEqual(cachePool.Port, cachePool.Port);

                    if (cachePoolName == "iocache3" && (j == 0 || j == 1))
                    {
                        Assert.IsNotNull(cacheNode.ReplicaNodeIds);
                        Assert.AreEqual(1, cacheNode.ReplicaNodeIds.Length);

                        if (j == 0)
                        {
                            Assert.AreEqual(1, cacheNode.ReplicaNodeIds[0]);
                        }
                        else if (j == 1)
                        {
                            Assert.AreEqual(0, cacheNode.ReplicaNodeIds[0]);
                        }
                    }
                    else
                    {
                        Assert.IsNull(cacheNode.ReplicaNodeIds);
                    }
                    
                    Assert.AreEqual(4, cacheNode.ConnectionMultiplexers.Length);
                    Assert.IsFalse(cacheNode.IsDenyListed);
                    Assert.IsTrue(cacheNode.IsReadEnabled);
                    Assert.IsTrue(cacheNode.IsWriteEnabled);
                }
            }

            // Set value
            var testKey = "testKey1";
            var testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue"));

            var setResult = await cacheClient.SetValueIfGreaterThanWithExpiryAsync(
                testKey, testValue, 0, TimeSpan.FromSeconds(10), CancellationToken.None);
            Assert.IsTrue(setResult);

            var value = await cacheClient.GetValueAsync(testKey, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(Encoding.UTF8.GetString(testValue.ToArray()), Encoding.UTF8.GetString(value.ToArray()));

            // Check if it is set to all replicas
            var selectedNodes = cacheClientExecutor.GetWriteCacheNodes(testKey);
            Assert.AreEqual(3, selectedNodes.Count);

            var poolNameSet = new HashSet<string>();
            int[] nodeIndexes = new int[3];
            foreach (var selectedNode in selectedNodes)
            {
                Assert.IsFalse(selectedNode.IsDenyListed);
                Assert.IsTrue(selectedNode.IsReadEnabled);
                Assert.IsTrue(selectedNode.IsWriteEnabled);

                value = await CacheClientExecutor.GetValueAsync(selectedNode, testKey).ConfigureAwait(false);
                Assert.AreEqual(Encoding.UTF8.GetString(testValue.ToArray()), Encoding.UTF8.GetString(value.ToArray()));

                poolNameSet.Add(selectedNode.CachePoolName);
                nodeIndexes[poolNameSet.Count - 1] = selectedNode.CacheNodeIndex;
            }
            Assert.AreEqual(3, poolNameSet.Count);

            // Check NodeIndex to make sure that offset is working
            Assert.AreNotEqual(nodeIndexes[0], nodeIndexes[1]);
            Assert.AreNotEqual(nodeIndexes[1], nodeIndexes[2]);
            Assert.AreNotEqual(nodeIndexes[2], nodeIndexes[0]);

            // Set value using nodeReplication
            testKey = "testKey2";
            testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue2"));

            setResult = await cacheClient.SetValueIfGreaterThanWithExpiryAsync(
                testKey, testValue, 0, TimeSpan.FromSeconds(10), CancellationToken.None);
            Assert.IsTrue(setResult);

            selectedNodes = cacheClientExecutor.GetWriteCacheNodes(testKey);
            Assert.AreEqual(4, selectedNodes.Count);

            int numPool1 = 0;
            int numPool2 = 0;
            int numPool3 = 0;
            foreach (var selectedNode in selectedNodes)
            {
                Assert.IsFalse(selectedNode.IsDenyListed);
                Assert.IsTrue(selectedNode.IsReadEnabled);
                Assert.IsTrue(selectedNode.IsWriteEnabled);

                value = await CacheClientExecutor.GetValueAsync(selectedNode, testKey).ConfigureAwait(false);
                Assert.AreEqual(Encoding.UTF8.GetString(testValue.ToArray()), Encoding.UTF8.GetString(value.ToArray()));

                switch(selectedNode.CachePoolName)
                {
                    case "iocache1":
                        numPool1++;
                        break;
                    case "iocache2":
                        numPool2++;
                        break;
                    case "iocache3":
                        numPool3++;
                        break;
                }
            }
            Assert.AreEqual(1, numPool1);
            Assert.AreEqual(1, numPool2);
            Assert.AreEqual(2, numPool3);
        }

        [TestMethod]
        public async Task CachePartialWriteTest()
        {
            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                "CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3278;StartOffset=0;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePool-1"] =
                "CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3279;StartOffset=2;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePool-2"] =
                "CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3280;StartOffset=4;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);

            var cacheClientExecutor = GetCacheClientExecutor(cacheClient);
            var cachePoolManager = GetDataLabCachePoolsManager(cacheClientExecutor);

            var testKey = "testKey1";
            var testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue"));

            // Get SelectedNodes
            var selectedNodes = cacheClientExecutor.GetWriteCacheNodes(testKey);
            Assert.AreEqual(3, selectedNodes.Count);

            var faultyNode = selectedNodes[2];
            var testCacheNode = testFactory.TestCacheClientMap[faultyNode.CacheNodeName];
            testCacheNode.ReturnInsertError = true;

            CacheClientWriteException<bool> exception = null;

            try
            {
                // Set value
                await cacheClient.SetValueWithExpiryAsync(testKey, testValue, TimeSpan.FromHours(1), CancellationToken.None).ConfigureAwait(false);
            }
            catch (CacheClientWriteException<bool> e)
            {
                exception = e;
            }

            Assert.IsNotNull(exception);

            var writeResult = exception.WriteResult;
            Assert.AreEqual(3, writeResult.SelectedCacheNodes.Count);
            Assert.AreEqual(2, writeResult.SuccessNodeResults.Count);
            Assert.AreEqual(1, writeResult.FailedNodeResults.Count);

            Assert.AreEqual(2, writeResult.SuccessNodeCount);
            Assert.AreEqual(1, writeResult.FailedNodeCount);

            var errorCacheNodeResult = writeResult.FailedNodeResults[0];
            Assert.AreEqual(faultyNode, errorCacheNodeResult.CacheNode);
            Assert.IsFalse(errorCacheNodeResult.Result);
            Assert.IsNotNull(errorCacheNodeResult.Exception);
            Assert.IsFalse(errorCacheNodeResult.IsSuccess);

            foreach(var sucessNodeResult in writeResult.SuccessNodeResults)
            {
                Assert.AreNotEqual(faultyNode, sucessNodeResult.CacheNode);
                Assert.IsTrue(sucessNodeResult.Result);
                Assert.IsNull(sucessNodeResult.Exception);
                Assert.IsTrue(sucessNodeResult.IsSuccess);
            }

            // Get Value
            testCacheNode.ReturnGetError = true;
            for (int i = 0; i < 10; i++)
            {
                var cacheClientReadResult = await cacheClientExecutor.GetValueAsync(testKey, 1, CancellationToken.None).ConfigureAwait(false);
                Assert.AreEqual(3, cacheClientReadResult.SelectedCacheNodes.Count);

                if (cacheClientReadResult.FailedNodeCount > 0)
                {
                    Assert.AreEqual(cacheClientReadResult.FailedNodeCount, 1);
                    Assert.IsNull(cacheClientReadResult.FailedNodeResults[0].Result);
                    Assert.IsNotNull(cacheClientReadResult.FailedNodeResults[0].Exception);
                    Assert.IsFalse(cacheClientReadResult.FailedNodeResults[0].IsSuccess);
                }
                
                Assert.IsNotNull(cacheClientReadResult.SuccessNodeResults[0].Result);
                Assert.IsNull(cacheClientReadResult.SuccessNodeResults[0].Exception);
                Assert.IsTrue(cacheClientReadResult.SuccessNodeResults[0].IsSuccess);
            }

            // CacheClient Get should returns even if one of node is faulty            
            var value = await cacheClient.GetValueAsync(testKey, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(Encoding.UTF8.GetString(testValue.ToArray()), Encoding.UTF8.GetString(value.ToArray()));
        }

        [TestMethod]
        public async Task CacheReadQuorumTest()
        {
            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                "CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3278;StartOffset=0;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePool-1"] =
                "CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3279;StartOffset=2;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePool-2"] =
                "CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3280;StartOffset=4;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);

            var cacheClientExecutor = GetCacheClientExecutor(cacheClient);
            var cachePoolManager = GetDataLabCachePoolsManager(cacheClientExecutor);

            var testKey = "testKey1";
            var testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue"));

            // Get SelectedNodes
            var selectedNodes = cacheClientExecutor.GetWriteCacheNodes(testKey);
            Assert.AreEqual(3, selectedNodes.Count);

            var faultyNode = selectedNodes[2];
            var testCacheNode = testFactory.TestCacheClientMap[faultyNode.CacheNodeName];
            testCacheNode.ReturnInsertError = true;

            CacheClientWriteException<bool> exception = null;

            try
            {
                // Set value
                await cacheClient.SetValueWithExpiryAsync(testKey, testValue, TimeSpan.FromHours(1), CancellationToken.None).ConfigureAwait(false);
            }
            catch (CacheClientWriteException<bool> e)
            {
                exception = e;
            }

            Assert.IsNotNull(exception);

            var writeResult = exception.WriteResult;
            Assert.AreEqual(3, writeResult.SelectedCacheNodes.Count);
            Assert.AreEqual(2, writeResult.SuccessNodeResults.Count);
            Assert.AreEqual(1, writeResult.FailedNodeResults.Count);

            Assert.AreEqual(2, writeResult.SuccessNodeCount);
            Assert.AreEqual(1, writeResult.FailedNodeCount);

            var errorCacheNodeResult = writeResult.FailedNodeResults[0];
            Assert.AreEqual(faultyNode, errorCacheNodeResult.CacheNode);
            Assert.IsFalse(errorCacheNodeResult.Result);
            Assert.IsNotNull(errorCacheNodeResult.Exception);
            Assert.IsFalse(errorCacheNodeResult.IsSuccess);

            foreach (var sucessNodeResult in writeResult.SuccessNodeResults)
            {
                Assert.AreNotEqual(faultyNode, sucessNodeResult.CacheNode);
                Assert.IsTrue(sucessNodeResult.Result);
                Assert.IsNull(sucessNodeResult.Exception);
                Assert.IsTrue(sucessNodeResult.IsSuccess);
            }

            // Get Value
            testCacheNode.ReturnGetError = true;
            for (int i = 0; i < 10; i++)
            {
                var cacheClientReadResult = await cacheClientExecutor.GetValueAsync(testKey, 1, CancellationToken.None).ConfigureAwait(false);
                Assert.AreEqual(3, cacheClientReadResult.SelectedCacheNodes.Count);

                if (cacheClientReadResult.FailedNodeCount > 0)
                {
                    Assert.AreEqual(cacheClientReadResult.FailedNodeCount, 1);
                    Assert.IsNull(cacheClientReadResult.FailedNodeResults[0].Result);
                    Assert.IsNotNull(cacheClientReadResult.FailedNodeResults[0].Exception);
                    Assert.IsFalse(cacheClientReadResult.FailedNodeResults[0].IsSuccess);
                }

                Assert.IsNotNull(cacheClientReadResult.SuccessNodeResults[0].Result);
                Assert.IsNull(cacheClientReadResult.SuccessNodeResults[0].Exception);
                Assert.IsTrue(cacheClientReadResult.SuccessNodeResults[0].IsSuccess);
            }

            // CacheClient Get should returns even if one of node is faulty            
            var value = await cacheClient.GetValueAsync(testKey, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(Encoding.UTF8.GetString(testValue.ToArray()), Encoding.UTF8.GetString(value.ToArray()));
        }

        [TestMethod]
        public void CacheNodeDenyListTest()
        {
            var cacheDomain = "cache-namespace.svc.cluster.local";
            ConfigMapUtil.Configuration[SolutionConstants.CachePoolDomain] = cacheDomain;
            ConfigMapUtil.Configuration[SolutionConstants.CacheNumPools] = "3";

            ConfigMapUtil.Configuration["CachePool-0"] =
                "CacheName=iocache1;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3278;StartOffset=0;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePool-1"] =
                "CacheName=iocache2;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3279;StartOffset=2;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePool-2"] =
                "CacheName=iocache3;ReadEnabled=true;WriteEnabled=true;NodeCount=5;Port=3280;StartOffset=4;NodeSelectionMode=HashModular";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-0"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-1"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";
            ConfigMapUtil.Configuration["CachePoolConnectionsOption-2"] = "NumConnections=4;ConnectRetry=0;ConnectTimeout=00:00:05;OperationTimeout=00:00:05";

            var testFactory = new TestConnectionMultiplexerWrapperFactory();
            var cacheClient = new IOCacheClient(ConfigMapUtil.Configuration, testFactory);

            var cacheClientExecutor = GetCacheClientExecutor(cacheClient);
            var cachePoolManager = GetDataLabCachePoolsManager(cacheClientExecutor);

            var testKey = "testKey1";
            var testValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("testValue"));

            // Get SelectedNodes
            var selectedNodes = cacheClientExecutor.GetWriteCacheNodes(testKey);
            Assert.AreEqual(3, selectedNodes.Count);
            selectedNodes[2].IsDenyListed = true;
            Assert.IsFalse(selectedNodes[2].IsWriteEnabled);
            Assert.IsFalse(selectedNodes[2].IsReadEnabled);

            // Get SelectedNodes After set DenyList
            selectedNodes = cacheClientExecutor.GetWriteCacheNodes(testKey);
            Assert.AreEqual(2, selectedNodes.Count);
        }
    }
}

