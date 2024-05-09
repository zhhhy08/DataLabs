namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel.SubTasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerService.V1;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Newtonsoft.Json;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using global::Azure;

    public class TestPartnerDispatcherTaskFactory : ISubTaskFactory<IOEventTaskContext<ARNSingleInputMessage>>
    {
        public const string TestOutputType = "microsoft.azurebusinesscontinuity/unifiedprotecteditems";

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

        public string SubTaskName => "TestPartnerDispatcher";
        public bool CanContinueToNextTaskOnException => false;

        private readonly PartnerDispatcherTask _partnerDispatcherTask; // singleTon
        public bool ReturnEmpty { get; set; }
        public bool ReturnError { get; set; }
        public bool ReturnNotAllowedResponse { get; set; }
        public bool UseMultiResponses { get; set; }
        public bool UseSameGroups { get; set; }
        public bool UseFirstInternal { get; set; }
        public bool UseSubJob { get; set; }
        public int DelayInMilliSecForTimeOutTest { get; set; }
        public int ReturnParentErrorAfterNum;
        public int NumStreamResponses = 4;

        public TestPartnerDispatcherTaskFactory()
        {
            _partnerDispatcherTask = new PartnerDispatcherTask(this);
        }

        public ISubTask<IOEventTaskContext<ARNSingleInputMessage>> CreateSubTask(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            return _partnerDispatcherTask;
        }

        public void Dispose()
        {
        }

        private static EventGridNotification<NotificationDataV3<GenericResource>> ParseEvents(string inputEvent)
        {
            return JsonConvert.DeserializeObject<List<EventGridNotification<NotificationDataV3<GenericResource>>>>(inputEvent).First();

        }
        private static PartnerResponse CreateSamplePartnerResponse(string armId)
        {
            var eventGridEvent = ParseEvents(VirtualMachineEvent);
            var outputBinary = SerializationHelper.SerializeToByteString(eventGridEvent, false);
            var newEtag = new ETag(Guid.NewGuid().ToString());

            return new PartnerResponse
            {
                RespEpochtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PartnerResponseEpochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Correlationid = "",
                Success = new SuccessResponse
                {
                    Format = DataFormat.Arn,
                    OutputTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ArmId = armId,
                    TenantId = "testTenantId",
                    OutputData = outputBinary,
                    Etag = newEtag.ToString(),
                    EventType = "Microsoft.Compute/virtualMachineScaleSets/write",
                    ResourceLocation = "eastus",
                }
            };
        }

        internal void Clear()
        {
            ReturnEmpty = false;
            ReturnError = false;
            ReturnNotAllowedResponse = false;
            UseMultiResponses = false;
            UseSameGroups = false;
            UseSubJob = false;
            UseFirstInternal = false;
            ReturnParentErrorAfterNum = 0;
            DelayInMilliSecForTimeOutTest = 0;
        }

        private class PartnerDispatcherTask : ISubTask<IOEventTaskContext<ARNSingleInputMessage>>
        {
            public bool UseValueTask => false;
            public TestPartnerDispatcherTaskFactory _taskFactory;

            public PartnerDispatcherTask(TestPartnerDispatcherTaskFactory taskFactory)
            {
                _taskFactory = taskFactory;
            }

            public async Task ProcessEventTaskContextAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                var ioEventTaskContext = eventTaskContext.TaskContext;

                if (_taskFactory.DelayInMilliSecForTimeOutTest > 0)
                {
                    await Task.Delay(_taskFactory.DelayInMilliSecForTimeOutTest).ConfigureAwait(false);
                }

                if (_taskFactory.ReturnEmpty)
                {
                    ioEventTaskContext.AddOutputMessage(null);
                    ioEventTaskContext.TaskSuccess(Stopwatch.GetTimestamp());
                }else if (_taskFactory.ReturnError)
                {
                    throw new Exception("PartnerError");
                }else if (!_taskFactory.UseMultiResponses)
                {
                    var outputData = ioEventTaskContext.BaseInputMessage.SerializedData;

                    IDictionary<string, string> respProperties = null;

                    if (_taskFactory.UseSubJob)
                    {
                        respProperties = new Dictionary<string, string>();
                        respProperties.Add(DataLabsARNV3Response.AttributeKey_SUBJOB, "true");
                    }

                    var outputResourceId = !_taskFactory.ReturnNotAllowedResponse ? ResourcesConstants.AllowedSampleOutputResourceId : ResourcesConstants.NotAllowedSampleResourceId;
                    var outputMessage = new OutputMessage(
                    SolutionDataFormat.ARN,
                    outputData,
                    "testCorrelationId",
                    outputResourceId,
                    ResourcesConstants.MicrosoftTenantId,
                    ResourcesConstants.TestEventType,
                    ResourcesConstants.TestResourceLocation,
                    null,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    respProperties,
                    ioEventTaskContext);

                    ioEventTaskContext.AddOutputMessage(outputMessage);

                    var isSubJob = SolutionUtils.IsSubJobResponse(ioEventTaskContext.OutputMessage.RespProperties);

                    if (isSubJob)
                    {
                        ioEventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.PartnerSubJobResponse;
                        SolutionInputOutputService.SetNextChannelToSubJobChannel(eventTaskContext);
                    }
                    else
                    {
                        SolutionInputOutputService.SetNextChannelToSourceOfTruthChannel(eventTaskContext);
                    }
                }
                else
                {
                    var streamResponseProcessor = new PartnerStreamResponseProcessor(
                      eventTaskContext,
                      TimeSpan.FromSeconds(10),
                      1000,
                      1000);

                    // Starting Add Child Event
                    streamResponseProcessor.StartIteration();
                    for (int i = 0; i < _taskFactory.NumStreamResponses; i++)
                    {
                        if (_taskFactory.ReturnParentErrorAfterNum > 0 && _taskFactory.ReturnParentErrorAfterNum == i)
                        {
                            throw new Exception("Response Error");
                        }

                        var outputResourceId = !_taskFactory.ReturnNotAllowedResponse ? ResourcesConstants.AllowedSampleOutputResourceId : ResourcesConstants.NotAllowedSampleResourceId;
                        var response = CreateSamplePartnerResponse(outputResourceId + i);

                        if (_taskFactory.UseSubJob)
                        {
                            response.RespAttributes.Add(DataLabsARNV3Response.AttributeKey_SUBJOB, "true");
                        }

                        if (_taskFactory.UseSameGroups)
                        {
                            response.RespAttributes.Add(DataLabsARNV3Response.AttributeKey_GROUPID, "1");
                        }

                        if (_taskFactory.UseFirstInternal && i == 0)
                        {
                            response.RespAttributes.Add(DataLabsARNV3Response.AttributeKey_INTERNAL, "true");
                        }

                        var result = await streamResponseProcessor.AddPartnerResponseAsync(response).ConfigureAwait(false);
                        if (!result)
                        {
                            throw new Exception("Response Error");
                        }
                    }

                    await streamResponseProcessor.EndIterationAsync(null).ConfigureAwait(false);
                }
            }

            public ValueTask ProcessEventTaskContextValueAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                throw new NotImplementedException();
            }
        }
    }
}