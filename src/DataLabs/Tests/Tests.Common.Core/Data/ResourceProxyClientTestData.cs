namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Data
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Newtonsoft.Json;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    public static class ResourceProxyClientTestData
    {
        public const string VirtualMachineEvent = """
            [{
              "topic": "custom domaintopic/eg topic",
              "subject": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm",
              "eventType": "Microsoft.Compute/virtualMachineScaleSets/write",
              "eventTime": "2018-11-02T21:46:13.100Z",
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

        public const string VirtualMachineDeletedEvent = """
            [{
              "topic": "custom domaintopic/eg topic",
              "subject": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm",
              "eventType": "Microsoft.Compute/virtualMachineScaleSets/delete",
              "eventTime": "2018-11-02T21:46:13.100Z",
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

        public const string VirtualMachineArmResource = """
            {
                "name": "idm",
                "resourceHomeTenantId" : "72f988bf-86f1-41af-91ab-2d7cd011db47",
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
            """;

        public const string CasRestrictionsResource = @"{
	        ""responseCode"": ""OK"",
	        ""restrictionsByOfferFamily"": {
		        ""STANDARDD15V2"": [{
				        ""offerTerms"": [
					        ""Ondemand""
				        ],
				        ""location"": ""CANADACENTRAL"",
				        ""physicalAvailabilityZones"": []
			        },
			        {
				        ""offerTerms"": [
					        ""Ondemand""
				        ],
				        ""location"": ""EASTUS"",
				        ""physicalAvailabilityZones"": []
			        },
			        {
				        ""offerTerms"": [
					        ""Ondemand""
				        ],
				        ""location"": ""SOUTHINDIA"",
				        ""physicalAvailabilityZones"": []
			        },
			        {
				        ""offerTerms"": [
					        ""Ondemand""
				        ],
				        ""location"": ""WESTUS2"",
				        ""physicalAvailabilityZones"": []
			        }
		        ]
	        },
	        ""restrictedSkuNamesToOfferFamily"": {
		        ""STANDARDD15V2"": ""STANDARDD15V2""
	        }
        }";

        public const string AfecCollectionGetArmResource = @"{
            ""value"": [
                {
                    ""apiVersion"": ""2021-07-01"",
                    ""id"": ""/subscriptions/30033233-7071-43b7-abd4-dee283630133/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/DsmsSecrets"",
                    ""name"": ""Microsoft.Compute/DsmsSecrets"",
                    ""type"": ""microsoft.features/featureproviders/subscriptionfeatureregistrations"",
                    ""location"": ""global"",
                    ""properties"": {
                        ""subscriptionId"": ""30033233-7071-43b7-abd4-dee283630133"",
                        ""providerNamespace"": ""Microsoft.Compute"",
                        ""featureName"": ""DsmsSecrets"",
                        ""state"": ""Registered"",
                        ""metaData"": null,
                        ""createdTime"": ""2023-06-28T16:21:55.143Z"",
                        ""changedTime"": ""2023-10-18T14:48:30.359Z""
                    }
                },
                {
                    ""apiVersion"": ""2021-07-01"",
                    ""id"": ""/subscriptions/30033233-7071-43b7-abd4-dee283630133/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/FabricOverrideSettings"",
                    ""name"": ""Microsoft.Compute/FabricOverrideSettings"",
                    ""type"": ""microsoft.features/featureproviders/subscriptionfeatureregistrations"",
                    ""location"": ""global"",
                    ""properties"": {
                        ""subscriptionId"": ""30033233-7071-43b7-abd4-dee283630133"",
                        ""providerNamespace"": ""Microsoft.Compute"",
                        ""featureName"": ""FabricOverrideSettings"",
                        ""state"": ""Registered"",
                        ""metaData"": null,
                        ""createdTime"": ""2023-06-28T16:19:01.511Z"",
                        ""changedTime"": ""2023-10-18T14:45:18.631Z""
                    }
                },
                {
                    ""apiVersion"": ""2021-07-01"",
                    ""id"": ""/subscriptions/30033233-7071-43b7-abd4-dee283630133/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/MRProfile"",
                    ""name"": ""Microsoft.Compute/MRProfile"",
                    ""type"": ""microsoft.features/featureproviders/subscriptionfeatureregistrations"",
                    ""location"": ""global"",
                    ""properties"": {
                        ""subscriptionId"": ""30033233-7071-43b7-abd4-dee283630133"",
                        ""providerNamespace"": ""Microsoft.Compute"",
                        ""featureName"": ""MRProfile"",
                        ""state"": ""Registered"",
                        ""metaData"": null,
                        ""createdTime"": ""2023-06-28T16:17:33.536Z"",
                        ""changedTime"": ""2023-10-18T14:43:52.433Z""
                    }
                },
                {
                    ""apiVersion"": ""2021-07-01"",
                    ""id"": ""/subscriptions/30033233-7071-43b7-abd4-dee283630133/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/UsePrivilegedMR"",
                    ""name"": ""Microsoft.Compute/UsePrivilegedMR"",
                    ""type"": ""microsoft.features/featureproviders/subscriptionfeatureregistrations"",
                    ""location"": ""global"",
                    ""properties"": {
                        ""subscriptionId"": ""30033233-7071-43b7-abd4-dee283630133"",
                        ""providerNamespace"": ""Microsoft.Compute"",
                        ""featureName"": ""UsePrivilegedMR"",
                        ""state"": ""Registered"",
                        ""metaData"": null,
                        ""createdTime"": ""2023-06-28T16:20:25.093Z"",
                        ""changedTime"": ""2023-10-18T14:46:45.943Z""
                    }
                }
            ]
        }
        ";

       public const string IdMappingGetArmIdsByResourceAlias = """
            [{
              "aliasResourceId": "/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44",
              "armIds": ["/subscriptions/0a93027e-d914-4d56-90ff-22b8a5ea5688/resourceGroups/wilful_turquoise_magpie/providers/Microsoft.Compute/virtualMachines/only_purple_moose"],
              "statusCode": "OK",
              "errorMessage": null
            }]
            """;

        public static EventGridNotification<NotificationDataV3<GenericResource>> ParseEvents(string inputEvent)
        {
            return JsonConvert.DeserializeObject<List<EventGridNotification<NotificationDataV3<GenericResource>>>>(inputEvent)[0];
        }

        public static GenericResource CreateARMResource(string armResource)
        {
            return JsonConvert.DeserializeObject<GenericResource>(armResource);
        }

        public static string SerializeObject<T>(T obj)
        {
            return SerializationHelper.SerializeObject(obj);
        }

        public static string GetTenantId(NotificationDataV3<GenericResource> notificationData)
        {
            var resourceData = notificationData.Resources[0];
            return resourceData.ResourceHomeTenantId ?? resourceData.HomeTenantId ?? notificationData.ResourceHomeTenantId ?? notificationData.HomeTenantId;
        }

        public static string GetARMId(NotificationDataV3<GenericResource> notificationData)
        {
            return notificationData.Resources[0].ResourceId;
        }

        public static DataLabsResourceCollectionSuccessResponse ParseCollectionResource(string resource)
        {
            return JsonConvert.DeserializeObject<DataLabsResourceCollectionSuccessResponse>(resource);
        }

        public static DataLabsCasResource ParseCasResource(string casResource)
        {
            return JsonConvert.DeserializeObject<DataLabsCasResource>(casResource);
        }

        public static string CreateNewCorrelationId()
        {
            return Guid.NewGuid().ToString();
        }

        public static string CreateNewActivityId()
        {
            var activityContext = Tracer.CreateNewActivityContext();
            return Tracer.ConvertToActivityId(activityContext);
        }
    }
}