namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.PartnerSolutionServiceBase.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Newtonsoft.Json;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.PartnerSolutionServiceBase.Services;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using SamplePartnerNuget.SolutionInterface;

    [TestClass]
    public class PartnerSolutionServiceTest
    {
        internal const string VirtualMachineEvent = """
            [{
              "topic": "custom domaintopic/eg topic",
              "subject": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm",
              "eventType": "Microsoft.Compute/virtualMachineScaleSets/write",
              "eventTime": "2018-11-02T21:46:13.939951Z",
              "id": "164f5e66-a908-4cfe-9499-9165f2d82b16",
              "dataVersion": "3.0",
              "metadataVersion": "1",
              "data": {
                "resourcesContainer": "Inline",
                "resourceLocation": "eastus",
                "frontdoorLocation": "",
                "publisherInfo": "Microsoft.Compute",
                "resourcesBlobInfo": null,
                "resources": [
                  {
                    "correlationId": "d82b3f83-9004-4069-9aaf-6329546d5a12",
                    "resourceId": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm",
                    "apiVersion": "2022-11-01",
                    "resourceHomeTenantId" : "72f988bf-86f1-41af-91ab-2d7cd011db47",
                    "armResource": {
                      "name": "idm",
                      "id": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm",
                      "type": "Microsoft.Compute/virtualMachineScaleSets",
                      "location": "eastus",
                      "sku": {
                        "name": "Standard_F2",
                        "tier": "Standard",
                        "capacity": 15
                      },
                      "properties": {
                        "singlePlacementGroup": true,
                        "orchestrationMode": "Uniform",
                        "upgradePolicy": {
                          "mode": "Automatic",
                          "rollingUpgradePolicy": {
                            "maxBatchInstancePercent": 20,
                            "maxUnhealthyInstancePercent": 20,
                            "maxUnhealthyUpgradedInstancePercent": 20,
                            "pauseTimeBetweenBatches": "PT0S",
                            "maxSurge": false,
                            "rollbackFailedInstancesOnPolicyBreach": false
                          },
                          "automaticOSUpgradePolicy": {
                            "enableAutomaticOSUpgrade": true,
                            "useRollingUpgradePolicy": false,
                            "disableAutomaticRollback": false
                          }
                        },
                        "provisioningState": "Succeeded",
                        "overprovision": false,
                        "doNotRunExtensionsOnOverprovisionedVMs": false,
                        "uniqueId": "6315cfd5-911f-4097-b652-a0c04c81565d",
                        "zoneBalance": true,
                        "platformFaultDomainCount": 5,
                        "timeCreated": "2022-08-17T15:23:00.25087+00:00"
                      }
                    }
                  }
                ]
              }
            }]
            """;

        private const string TestConfigValue = "TestValue";

        private ICacheClient _cacheClient;
        private SamplePartnerService _partnerInterface;
        private PartnerSolutionService _partnerSolutionService;
        private ILoggerFactory _loggerFactory;
        private ConfigurationWithCallBack _configurationWithCallBack;

        [TestInitialize]
        public void TestInitialize()
        {
            _loggerFactory = DataLabLoggerFactory.GetLoggerFactory();

            var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
            ConfigMapUtil.Initialize(configBuilder, false);

            ConfigMapUtil.Configuration[SamplePartnerService.TestConfigKey] = TestConfigValue;
            ConfigMapUtil.Configuration[SamplePartnerService.TestDelayTimeKey] = "10";

            _configurationWithCallBack = ConfigMapUtil.Configuration;

            _cacheClient = new TestCacheClient();

            _partnerInterface = new SamplePartnerService();
            _partnerInterface.SetLoggerFactory(_loggerFactory);
            _partnerInterface.SetConfiguration(_configurationWithCallBack);
            _partnerInterface.SetCacheClient(_cacheClient);
            _partnerSolutionService = new PartnerSolutionService(_partnerInterface);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
            _partnerInterface.NumReturnResources = 1;
        }

        [TestMethod]
        public void TestLogger()
        {
            Assert.AreEqual(_loggerFactory, SamplePartnerService._loggerFactory);

        }

        [TestMethod]
        public void TestCacheClient()
        {
            Assert.AreEqual(_configurationWithCallBack, SamplePartnerService._configurationWithCallBack);
            Assert.AreEqual(_cacheClient, _partnerInterface.CacheClient);
        }

        [TestMethod]
        public void TestConfiguration()
        {
            Assert.AreEqual(_configurationWithCallBack, SamplePartnerService._configurationWithCallBack);
            Assert.AreEqual(TestConfigValue, _partnerInterface.TestConfigValue);
        }

        [TestMethod]
        public void TestConfigurationCallBack()
        {
            Assert.AreEqual(_configurationWithCallBack, SamplePartnerService._configurationWithCallBack);
            Assert.AreEqual(10, _partnerInterface.DelayTime);

            // Update the value
            var newValue = 20;
            ConfigMapUtil.Configuration[SamplePartnerService.TestDelayTimeKey] = newValue.ToString();

            ConfigMapUtil.Configuration.CheckChangeAndCallBack(CancellationToken.None);

            Thread.Sleep(50);

            Assert.AreEqual(newValue, _partnerInterface.DelayTime);
        }

        [TestMethod]
        public void TestProcessMessage()
        {
            var eventGridEvent = ParseEvents(VirtualMachineEvent);
            var inputByteString = SerializationHelper.SerializeToByteString(eventGridEvent, false);

            var solutionRequest = new PartnerRequest()
            {
                TraceId = "",
                RetryCount = 1,
                ReqEpochtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Correlationid = "",
                Format = DataFormat.Arn,
                InputData = inputByteString
            };

            var response = _partnerSolutionService.ProcessMessage(solutionRequest, new TestServerCallContext()).GetAwaiter().GetResult();

            var correlationId = "d82b3f83-9004-4069-9aaf-6329546d5a12";
            var inputArmId = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var outputArmId = inputArmId + SamplePartnerUtils.ProviderAndResourceType;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            Assert.AreEqual(correlationId, response.Correlationid);
            Assert.IsTrue(response.Success.ArmId.StartsWith(outputArmId));
            Assert.AreEqual(tenantId, response.Success.TenantId);
        }

        [TestMethod]
        public async Task TestProcessStreamMessages()
        {
            var eventGridEvent = ParseEvents(VirtualMachineEvent);
            var inputByteString = SerializationHelper.SerializeToByteString(eventGridEvent, false);

            var solutionRequest = new PartnerRequest()
            {
                TraceId = "",
                RetryCount = 1,
                ReqEpochtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Correlationid = "",
                Format = DataFormat.Arn,
                InputData = inputByteString
            };

            var responseStream = new TestServerStreamWriter<PartnerResponse>();
            await _partnerSolutionService.ProcessStreamMessages(solutionRequest, responseStream, new TestServerCallContext()).ConfigureAwait(false);

            var correlationId = "d82b3f83-9004-4069-9aaf-6329546d5a12";
            var inputArmId = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var outputArmId = inputArmId + SamplePartnerUtils.ProviderAndResourceType;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            Assert.AreEqual(1, responseStream.MessageList.Count);

            var response = responseStream.MessageList[0];

            Assert.AreEqual(correlationId, response.Correlationid);
            Assert.IsTrue(response.Success.ArmId.StartsWith(outputArmId));
            Assert.AreEqual(tenantId, response.Success.TenantId);
        }

        [TestMethod]
        public async Task TestMessageAttribute()
        {
            var eventGridEvent = ParseEvents(VirtualMachineEvent);
            var inputByteString = SerializationHelper.SerializeToByteString(eventGridEvent, false);

            var solutionRequest = new PartnerRequest()
            {
                TraceId = "",
                RetryCount = 1,
                ReqEpochtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Correlationid = "",
                Format = DataFormat.Arn,
                InputData = inputByteString
            };

            _partnerInterface.ReturnInternalAttribute = true;

            var responseStream = new TestServerStreamWriter<PartnerResponse>();
            await _partnerSolutionService.ProcessStreamMessages(solutionRequest, responseStream, new TestServerCallContext()).ConfigureAwait(false);

            _partnerInterface.ReturnInternalAttribute = false;

            var correlationId = "d82b3f83-9004-4069-9aaf-6329546d5a12";
            var inputArmId = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
            var outputArmId = inputArmId + SamplePartnerUtils.ProviderAndResourceType;
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            Assert.AreEqual(1, responseStream.MessageList.Count);

            var response = responseStream.MessageList[0];

            Assert.AreEqual(correlationId, response.Correlationid);
            Assert.IsTrue(response.Success.ArmId.StartsWith(outputArmId));
            Assert.AreEqual(1, response.RespAttributes.Count);
            Assert.AreEqual(true, bool.Parse(response.RespAttributes[DataLabsARNV3Response.AttributeKey_INTERNAL]));
            Assert.AreEqual(tenantId, response.Success.TenantId);
        }

        [TestMethod]
        public void TestGetMeterAndTraceAndLoggerNames()
        {
            var list = new List<string>(1);
            list.Add(SamplePartnerService.PartnerActivitySourceName);
            Assert.AreEqual(list.ToString(), _partnerInterface.GetTraceSourceNames().ToString());

            list.Clear();
            list.Add(SamplePartnerService.PartnerMeterName);
            Assert.AreEqual(list.ToString(), _partnerInterface.GetMeterNames().ToString());

            list.Clear();
            list.Add(SamplePartnerService.CustomerMeterName);
            Assert.AreEqual(list.ToString(), _partnerInterface.GetCustomerMeterNames().ToString());

            var dict = new Dictionary<string, string>
            {
                [SamplePartnerService.SamplePartnerLogTable] = SamplePartnerService.SamplePartnerLogTable
            };
            Assert.AreEqual(dict.ToString(), _partnerInterface.GetLoggerTableNames().ToString());
        }

        [TestMethod]
        public async Task TestNotAllowedPartnerResponseStreamMessages()
        {
            _partnerInterface.NumReturnResources = 2;

            var eventGridEvent = ParseEvents(VirtualMachineEvent);
            var inputByteString = SerializationHelper.SerializeToByteString(eventGridEvent, false);

            var solutionRequest = new PartnerRequest()
            {
                TraceId = "",
                RetryCount = 1,
                ReqEpochtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Correlationid = "",
                Format = DataFormat.Arn,
                InputData = inputByteString
            };

            var responseStream = new TestServerStreamWriter<PartnerResponse>();
            await _partnerSolutionService.ProcessStreamMessages(solutionRequest, responseStream, new TestServerCallContext()).ConfigureAwait(false);

            var correlationId = "d82b3f83-9004-4069-9aaf-6329546d5a12";

            Assert.AreEqual(1, responseStream.MessageList.Count);
            var response = responseStream.MessageList[0];

            // response should be error Response
            Assert.AreEqual(correlationId, response.Correlationid);
            Assert.IsNull(response.Success);
            Assert.AreEqual(response.Error.Type, ErrorType.Poison);
            Assert.AreEqual(response.Error.FailedComponent, PartnerSolutionService.NOT_ALLOWED_RESPONSE);
        }

        private static EventGridNotification<NotificationDataV3<GenericResource>> ParseEvents(string inputEvent)
        {
            return JsonConvert.DeserializeObject<List<EventGridNotification<NotificationDataV3<GenericResource>>>>(inputEvent).First();
        }

        public class TestServerStreamWriter<T> : IServerStreamWriter<T>
        {
            public List<T> MessageList = new List<T>();

            public WriteOptions? WriteOptions { get; set; }

            public Task WriteAsync(T message)
            {
                MessageList.Add(message);
                return Task.CompletedTask;
            }
        }

        private class TestServerCallContext : ServerCallContext
        {
            protected override string MethodCore => throw new NotImplementedException();

            protected override string HostCore => throw new NotImplementedException();

            protected override string PeerCore => "10.0.0.1";

            protected override DateTime DeadlineCore => throw new NotImplementedException();

            protected override Metadata RequestHeadersCore => throw new NotImplementedException();

            protected override CancellationToken CancellationTokenCore => CancellationToken.None;

            protected override Metadata ResponseTrailersCore => throw new NotImplementedException();

            protected override Status StatusCore { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            protected override WriteOptions WriteOptionsCore { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            protected override AuthContext AuthContextCore => throw new NotImplementedException();

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options)
            {
                throw new NotImplementedException();
            }

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            {
                throw new NotImplementedException();
            }
        }
    }
}

