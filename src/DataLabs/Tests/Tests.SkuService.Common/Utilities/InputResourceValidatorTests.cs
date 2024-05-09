namespace Tests.SkuService.Common.Utilities
{
    using System.Text;
    using global::SkuService.Common.Utilities;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Moq;
    using Newtonsoft.Json;
    using Tests.ArmDataCacheIngestionService.Helpers;

    [TestClass]
    public class InputResourceValidatorTests
    {
        private static readonly Mock<ICacheClient> mockCacheClient = new();

        readonly InputResourceValidator inputResourceValidator = new InputResourceValidator(mockCacheClient.Object);

        [TestCleanup]
        public void Cleanup()
        {
            mockCacheClient.Reset();
        }

        [TestMethod]
        public async Task TestIsProcessingRequiredForInputResourceAsync()
        {
            //afec
            DataLabsARNV3Request request = GetDataLabsRequest(Datasets.afecPendingNotification);
            Assert.IsFalse(await inputResourceValidator.IsProcessingRequiredForInputResourceAsync(request, CancellationToken.None));

            // Internal props
            request = GetDataLabsRequest(Datasets.subInternalPropDeleteEvent);
            Assert.IsFalse(await inputResourceValidator.IsProcessingRequiredForInputResourceAsync(request, CancellationToken.None));

            //Global sku
            request = GetDataLabsRequest(Datasets.globalSku);
            
            byte[] globalSkuByteArray = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request.InputResource!.Data.Resources[0].ArmResource));
            byte[] prefixBytes = BitConverter.GetBytes(request.InputResource!.Data!.Resources[0]!.ResourceEventTime!.Value.ToUnixTimeMilliseconds());
            byte[] resultByteArray = prefixBytes.Concat(globalSkuByteArray).ToArray();
            mockCacheClient.Setup(x => x.GetValueAsync(request.InputResource!.Id, CancellationToken.None)).Returns(Task.FromResult<byte[]?>(resultByteArray));
            Assert.IsTrue(await inputResourceValidator.IsProcessingRequiredForInputResourceAsync(request, CancellationToken.None));
          
            prefixBytes = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            resultByteArray = prefixBytes.Concat(globalSkuByteArray).ToArray();
            mockCacheClient.Setup(x => x.GetValueAsync(request.InputResource!.Id, CancellationToken.None)).Returns(Task.FromResult<byte[]?>(resultByteArray));
            Assert.IsFalse(await inputResourceValidator.IsProcessingRequiredForInputResourceAsync(request, CancellationToken.None));


            request = GetDataLabsRequest(Datasets.globalSku2);
            prefixBytes = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            resultByteArray = prefixBytes.Concat(globalSkuByteArray).ToArray();
            mockCacheClient.Setup(x => x.GetValueAsync(request.InputResource!.Id, CancellationToken.None)).Returns(Task.FromResult<byte[]?>(resultByteArray));
            Assert.IsTrue(await inputResourceValidator.IsProcessingRequiredForInputResourceAsync(request, CancellationToken.None));
        }

        private DataLabsARNV3Request GetDataLabsRequest(string input)
        {
            EventGridNotification<NotificationDataV3<GenericResource>>? inputResource = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(input);
            return new DataLabsARNV3Request(
                                                    DateTimeOffset.Now,
                                                    "traceId",
                                                    1,
                                                    "correlationID",
                                                    inputResource!,
                                                    null);
        }
    }
}
