namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.CacheClient
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;

    [TestClass]
    public class CacheClientTests
    {
        //private CacheClient _cacheClient;

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

        /*
        [TestMethod]
        public async Task TestSetValueAsync()
        {
            var binaryData = new BinaryData(new byte[] { 1, 2, 3, 4, 5 });
            var result = await _cacheClient.SetValueAsync("testKey", binaryData.ToMemory(), CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TestSetValueIfMatchAsync()
        {
            var binaryData = new BinaryData(new byte[] { 1, 2, 3, 4, 5 });
            var result = await _cacheClient.SetValueIfMatchAsync("testKey", binaryData.ToMemory(), 
                100, CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TestSetValueIfGreaterThanAsync()
        {
            var binaryData = new BinaryData(new byte[] { 1, 2, 3, 4, 5 });
            var result = await _cacheClient.SetValueIfGreaterThanAsync("testKey", binaryData.ToMemory(),
                100, CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TestGetValueAsync()
        {
            var binaryData = new BinaryData(new byte[] { 1, 2, 3, 4, 5 });
            var result = await _cacheClient.GetValueAsync("testKey", CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(result == null);
        }
        */

        [TestMethod]
        public void TestGetKeyWithArmId()
        {
            var resourceId1 = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var resourceId1upper = "/SUBSCRIPTIONS/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var resourceId2 = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm2";
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            var keyString1 = ResourceCacheUtils.GetLowerCaseKeyWithArmId(resourceId1, tenantId);
            var keyString1upper = ResourceCacheUtils.GetLowerCaseKeyWithArmId(resourceId1upper, tenantId);
            var keyString2 = ResourceCacheUtils.GetLowerCaseKeyWithArmId(resourceId2, tenantId);
            var keyString3 = ResourceCacheUtils.GetLowerCaseKeyWithArmId(resourceId2, null);

            Assert.AreEqual((tenantId + resourceId1).ToLowerInvariant(), keyString1);
            Assert.AreEqual((tenantId + resourceId2).ToLowerInvariant(), keyString2);
            Assert.AreNotEqual(resourceId2, keyString3);
            Assert.AreEqual(keyString1, keyString1upper);
            Assert.AreNotEqual(keyString1, keyString2);

            var keyhash1 = CacheClientExecutor.GetKeyHash(keyString1);
            var keyhash1upper = CacheClientExecutor.GetKeyHash(keyString1upper);
            var keyhash2 = CacheClientExecutor.GetKeyHash(keyString2);
            var keyhash3 = CacheClientExecutor.GetKeyHash(keyString3);
            Assert.AreEqual(keyhash1, keyhash1upper);
            Assert.AreNotEqual(keyhash1, keyhash2);
            Assert.AreNotEqual(keyhash2, keyhash3);
            Assert.AreNotEqual(keyhash3, keyhash1);

            var keyBytes1 = ResourceCacheUtils.GetKeyBytes(keyhash1);
            var keyBytes1upper = ResourceCacheUtils.GetKeyBytes(keyhash1upper);
            var keyBytes2 = ResourceCacheUtils.GetKeyBytes(keyhash2);
            var keyBytes3 = ResourceCacheUtils.GetKeyBytes(keyhash3);

            CollectionAssert.AreEqual(keyBytes1, keyBytes1upper);
            CollectionAssert.AreNotEqual(keyBytes1, keyBytes2);
            CollectionAssert.AreNotEqual(keyBytes2, keyBytes3);
            CollectionAssert.AreNotEqual(keyBytes3, keyBytes1);
        }

        [TestMethod]
        public void TestCompressTest()
        {
            var binaryData = new BinaryData(new byte[] { 1, 2, 3, 4, 5 });
            var etag = "W/\"0948fa4f-0c2b-4991-ad48-860973d6f55d\"";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var valuebytes = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, binaryData, timestamp, etag);

            var value = ResourceCacheUtils.DecompressCacheValue(valuebytes.ToArray());
            CollectionAssert.AreEqual(binaryData.ToMemory().ToArray(), value.Content.ToArray());
            Assert.AreEqual(ResourceCacheDataFormat.ARM, value.DataFormat);
            Assert.AreEqual(etag, value.Etag);
            Assert.AreEqual(timestamp, value.DataTimeStamp);
            Assert.IsTrue(value.InsertionTimeStamp > 0);
        }

        [TestMethod]
        public void TestCompressWithPrefixTest()
        {
            var binaryData = new BinaryData(new byte[] { 1, 2, 3, 4, 5 });
            var etag = "W/\"0948fa4f-0c2b-4991-ad48-860973d6f55d\"";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var valuebytes = ResourceCacheUtils.CompressCacheValue(ResourceCacheDataFormat.ARM, binaryData, timestamp, etag);

            var cacheValue = valuebytes.ToArray();
            var valueWithPrefix = new byte[8 + cacheValue.Length];

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var timeArray = BitConverter.GetBytes(currentTime);
            Array.Copy(timeArray, 0, valueWithPrefix, 0, 8);
            Array.Copy(cacheValue, 0, valueWithPrefix, 8, cacheValue.Length);

            var value = ResourceCacheUtils.DecompressCacheValue(valueWithPrefix);
            CollectionAssert.AreEqual(binaryData.ToMemory().ToArray(), value.Content.ToArray());
            Assert.AreEqual(ResourceCacheDataFormat.ARM, value.DataFormat);
            Assert.AreEqual(etag, value.Etag);
            Assert.AreEqual(timestamp, value.DataTimeStamp);
            Assert.IsTrue(value.InsertionTimeStamp > 0);
        }
    }
}

