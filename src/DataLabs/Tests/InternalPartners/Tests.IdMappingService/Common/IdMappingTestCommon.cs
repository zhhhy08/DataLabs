namespace Microsoft.WindowsAzure.IdMappingService.Tests.Common
{
    internal class IdMappingTestCommon
    {
        #region Json Event Constants
        internal const string VirtualMachineScaleSetEvent = """
            {
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
            }
            """;

        internal const string InsightsComponentEvent = """
            {
              "id": "6684adbc-129f-44f4-a88a-9293ca3f1e28",
              "subject": "/subscriptions/82506e98-9fdb-41f5-ab67-031005041a26/resourceGroups/armdata-rg-sync-df-eastus/providers/Microsoft.Insights/components/armdata-fn-sync-df-eastus-1",
              "data": {
                "resourceLocation": "eastus",
                "publisherInfo": "Microsoft.Resources",
                "resources": [
                  {
                    "correlationId": "a0e00320-40fa-40e7-b25b-367914100077",
                    "resourceId": "/subscriptions/82506e98-9fdb-41f5-ab67-031005041a26/resourceGroups/armdata-rg-sync-df-eastus/providers/Microsoft.Insights/components/armdata-fn-sync-df-eastus-1",
                    "apiVersion": "2020-02-02",
                    "statusCode": "OK",
                    "armResource": {
                      "id": "/subscriptions/82506e98-9fdb-41f5-ab67-031005041a26/resourceGroups/armdata-rg-sync-df-eastus/providers/microsoft.insights/components/armdata-fn-sync-df-eastus-1",
                      "name": "armdata-fn-sync-df-eastus-1",
                      "type": "microsoft.insights/components",
                      "location": "eastus",
                      "tags": {},
                      "properties": {
                        "ApplicationId": "armdata-fn-sync-df-eastus-1",
                        "AppId": "509417b5-e854-4323-a9d0-8917f1b6c716",
                        "Application_Type": "web",
                        "Flow_Type": null,
                        "Request_Source": "rest",
                        "InstrumentationKey": "5cafe77b-4ccb-4fd7-adcb-7bc676612e33",
                        "ConnectionString": "InstrumentationKey=5cafe77b-4ccb-4fd7-adcb-7bc676612e33;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/",
                        "Name": "armdata-fn-sync-df-eastus-1",
                        "CreationDate": "2023-02-08T04:00:43.2543808Z",
                        "TenantId": "82506e98-9fdb-41f5-ab67-031005041a26",
                        "provisioningState": "Succeeded",
                        "SamplingPercentage": null,
                        "RetentionInDays": 30,
                        "Retention": "P30D",
                        "WorkspaceResourceId": "/subscriptions/82506e98-9fdb-41f5-ab67-031005041a26/resourceGroups/armdata-rg-sync-df-eastus/providers/Microsoft.OperationalInsights/workspaces/armdata-logAnalytics-sync-eastus",
                        "IngestionMode": "LogAnalytics",
                        "publicNetworkAccessForIngestion": "Enabled",
                        "publicNetworkAccessForQuery": "Enabled",
                        "Ver": "v2"
                      },
                      "kind": "web"
                    },
                    "additionalResourceProperties": {
                      "armLinkedNotificationSource": "ResourceControllerFastPath",
                      "sourceOperation": "NotSpecified",
                      "provisioningState": "Succeeded"
                    }
                  }
                ],
                "frontdoorLocation": "westcentralus",
                "resourcesContainer": "Inline",
                "homeTenantId": "72f988bf-86f1-41af-91ab-2d7cd011db47",
                "resourceHomeTenantId": "72f988bf-86f1-41af-91ab-2d7cd011db47",
                "routingType": "Default"
              },
              "eventType": "Microsoft.Insights/components/write",
              "dataVersion": "3.0",
              "metadataVersion": "1",
              "eventTime": "2023-03-08T22:46:16.2290503Z",
              "topic": "/subscriptions/daf583ec-071c-4583-8a05-235077270d6b/resourceGroups/GovernanceNotificationsEusStorage/providers/Microsoft.EventGrid/domains/gov-ntdisp-eus-egdomain4arm/topics/microsoftresourcegraph-arn-cf4b5abc-08a5-ed00-1ed6-ca4c529c3ae1"
            }
            """;

        internal const string VirtualMachineDeleteEvent = """
            {
              "topic": "custom domaintopic/eg topic",
              "subject": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm",
              "eventType": "Microsoft.Compute/virtualMachineScaleSets/delete",
              "eventTime": "2018-11-02T21:46:15.939951Z",
              "id": "164f5e66-a908-4cfe-9499-9165f2d82b17",
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
                    "statusCode": "OK",
                    "armResource": {
                      "id": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm",
                    },
                    "additionalResourceProperties": {
                      "armLinkedNotificationSource": "LongOperationJob",
                      "sourceOperation": "Delete",
                      "provisioningState": "Succeeded"
                    }
                  }
                ]
              }
            }
            """;


        internal const string MissingResourcesEvent = """
            {
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
                "resources": []
              }
            }
            """;

        internal const string PayloadNotPopulatedEvent = """
            {
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
                    "armResource": {
                    }
                  }
                ]
              }
            }
            """;

        internal const string VirtualMachineScaleSetEventWithoutInternalId = """
            {
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
                        "zoneBalance": true,
                        "platformFaultDomainCount": 5,
                        "timeCreated": "2022-08-17T15:23:00.25087+00:00"
                      }
                    }
                  }
                ]
              }
            }
            """;

        internal const string ServiceBusNamespaceEventWithUrlMetricId = """
            {
              "topic": "custom domaintopic/eg topic",
              "subject": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.ServiceBus/namespaces/test-sb",
              "eventType": "Microsoft.ServiceBus/namespaces/write",
              "eventTime": "2018-11-02T21:46:13.939951Z",
              "id": "164f5e66-a908-4cfe-9499-9165f2d82b17",
              "dataVersion": "3.0",
              "metadataVersion": "1",
              "data": {
                "resourcesContainer": "Inline",
                "resourceLocation": "eastus",
                "frontdoorLocation": "",
                "publisherInfo": "Microsoft.ServiceBus",
                "resourcesBlobInfo": null,
                "resources": [
                  {
                    "correlationId": "d82b3f83-9004-4069-9aaf-6329546d5a12",
                    "resourceId": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.ServiceBus/namespaces/test-sb",
                    "apiVersion": "2022-11-01",
                    "armResource": {
                      "name": "idm",
                      "id": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.ServiceBus/namespaces/test-sb",
                      "type": "Microsoft.ServiceBus/namespaces",
                      "location": "eastus",
                      "properties": {
                        "metricId": "https://test-sb.servicebus.windows.net:443/",
                        "serviceBusEndpoint": "https://test-sb.servicebus.windows.net:443/",
                        "provisioningState": "Succeeded",
                      }
                    }
                  }
                ]
              }
            }
            """;

        internal const string ServiceBusNamespaceEventWithMetricIdUrlQueryParam = """
            {
              "topic": "custom domaintopic/eg topic",
              "subject": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.ServiceBus/namespaces/test-sb",
              "eventType": "Microsoft.ServiceBus/namespaces/write",
              "eventTime": "2018-11-02T21:46:13.939951Z",
              "id": "164f5e66-a908-4cfe-9499-9165f2d82b17",
              "dataVersion": "3.0",
              "metadataVersion": "1",
              "data": {
                "resourcesContainer": "Inline",
                "resourceLocation": "eastus",
                "frontdoorLocation": "",
                "publisherInfo": "Microsoft.ServiceBus",
                "resourcesBlobInfo": null,
                "resources": [
                  {
                    "correlationId": "d82b3f83-9004-4069-9aaf-6329546d5a12",
                    "resourceId": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.ServiceBus/namespaces/test-sb",
                    "apiVersion": "2022-11-01",
                    "armResource": {
                      "name": "idm",
                      "id": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.ServiceBus/namespaces/test-sb",
                      "type": "Microsoft.ServiceBus/namespaces",
                      "location": "eastus",
                      "properties": {
                        "metricId": "https://test-sb.servicebus.windows.net:4040?SomeQueryParameter=abc",
                        "serviceBusEndpoint": "https://test-sb.servicebus.windows.net:4040?SomeQueryParameter=abc",
                        "provisioningState": "Succeeded",
                      }
                    }
                  }
                ]
              }
            }
            """;

        #endregion
    }
}
