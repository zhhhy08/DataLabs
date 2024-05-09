namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.BlobClient
{
    using global::Azure;
    using global::Azure.Storage.Blobs.Models;

    using System.Net;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient;

    [TestClass]
    public class BlobUtilTests
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

        [TestMethod]
        public void TestGetStorageAccountAndBlobContainerIndex()
        {
            var hashBlobNameProvider = new HashBlobNameProvider();

            var resourceId1 = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var resourceId2 = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm2";
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            var hashForResource1 = hashBlobNameProvider.CalculateHash(resourceId1, tenantId);
            var hashForResource2 = hashBlobNameProvider.CalculateHash(resourceId2, tenantId);

            int numStroage = 64;
            int numBlobContainer = 200;

            int storageIndex1 = BlobUtils.GetStorageAccountIndex(hashForResource1.Item1, numStroage);
            int storageIndex2 = BlobUtils.GetStorageAccountIndex(hashForResource2.Item1, numStroage);

            int blobContainerIndex1 = BlobUtils.GetBlobContainerIndex(hashForResource1.Item1, numBlobContainer);
            int blobContainerIndex2 = BlobUtils.GetBlobContainerIndex(hashForResource2.Item1, numBlobContainer);

            Assert.AreNotEqual(storageIndex1, storageIndex2);
            Assert.AreNotEqual(blobContainerIndex1, blobContainerIndex2);
        }

        [TestMethod]
        public void TestBlobUtils()
        {
            var resourceId1 = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var resourceId1upper = "/SUBSCRIPTIONS/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var resourceId2 = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm2";
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            var hashBlobNameProvider = new HashBlobNameProvider();

            var hashForResource1 = hashBlobNameProvider.CalculateHash(resourceId1, tenantId);
            var hashForResource1upper = hashBlobNameProvider.CalculateHash(resourceId1upper, tenantId);
            var hashForResource2 = hashBlobNameProvider.CalculateHash(resourceId2, tenantId);

            var blobName1 = hashBlobNameProvider.GetBlobName(resourceId1, tenantId,
                hashForResource1.Item1, hashForResource1.Item2, hashForResource1.Item3);

            var blobName1upper = hashBlobNameProvider.GetBlobName(resourceId1upper, tenantId,
                hashForResource1upper.Item1, hashForResource1upper.Item2, hashForResource1upper.Item3);

            var blobName2 = hashBlobNameProvider.GetBlobName(resourceId2, tenantId,
                hashForResource2.Item1, hashForResource2.Item2, hashForResource2.Item3);

            Assert.AreEqual(blobName1, blobName1upper);
            Assert.AreNotEqual(blobName1, blobName2);
        }

        [TestMethod]
        public void TestAzureBlobException()
        {
            var exception = new RequestFailedException((int)HttpStatusCode.NotFound, "", BlobErrorCode.BlobNotFound.ToString(), null);
            Assert.IsTrue(BlobUtils.IsAzureBlobNotFound(exception));

            exception = new RequestFailedException((int)HttpStatusCode.PreconditionFailed, "");
            Assert.IsTrue(BlobUtils.IsAzureBlobPreConditionFailed(exception));

            exception = new RequestFailedException((int)HttpStatusCode.NotFound, "", BlobErrorCode.ContainerNotFound.ToString(), null);
            Assert.IsTrue(BlobUtils.IsAzureBlobContainerNotFound(exception));

            // Actual errorCode doens't know yet
            exception = new RequestFailedException((int)HttpStatusCode.Conflict, "", BlobErrorCode.BlobAlreadyExists.ToString(), null);
            Assert.IsTrue(BlobUtils.IsAzureBlobFileAlreadyExist(exception));
        }

    }
}

