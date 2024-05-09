using System.Text;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Constants
{
    public static class ResourcesConstants
    {
        public const string AllowedSampleOutputResourceType = "microsoft.azurebusinesscontinuity/unifiedprotecteditems";

        public const string NotAllowedSampleOutputResourceType = "microsoft.sql/servers";

        public const string NotAllowedSampleOutputResourceType2 = "microsoft.resources/subscription";

        public const string MicrosoftTenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

        public const string TestEventType = "Microsoft.Compute/virtualMachineScaleSets/write";

        public const string TestResourceLocation = "eastus";

        public const string TestSubscriptionId = "02d59989-f8a9-4b69-9919-1ef51df4eff6";

        public const string SampleResourceId =
            "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics";
        
        public const string AllowedSampleOutputResourceId = SampleResourceId + "/providers/" + AllowedSampleOutputResourceType + "/output";

        public const string NotAllowedSampleResourceId = SampleResourceId + "/providers/" + NotAllowedSampleOutputResourceType + "/output";

        public const string SampleResourceType = "Microsoft.ClassicStorage/storageAccounts";

        public const string NestedResourceId =
            "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/vmscaleset/aainteusanalytics/virtualmachine/vm1";
        
        public const string NestedResourceType = "Microsoft.ClassicStorage/vmscaleset/virtualmachine";

        public const string MultiLevelNestedResourceId =
            "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/vmscaleset/aainteusanalytics/virtualmachine/vm1/extensions/ext1";

        public const string ExtensionResourceId =
            "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-SQL-WestUS/providers/Microsoft.Sql/servers/tnzn4h7oyb/providers/Microsoft.Advisor/recommendations/973d2fe1-7452-8449-3c5d-f8b41b4b54ea";

        public const string SubscriptionRoleAssignmentResourceId = "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/providers/Microsoft.Authorization/roleAssignments/63017a1f-e4ed-4733-b032-656f27551028";

        public const string ResourceRoleAssignmentResourceId = "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics/providers/Microsoft.Authorization/roleAssignments/63017a1f-e4ed-4733-b032-656f27551028";

        public const string MgRoleAssignmentResourceId = "/providers/Microsoft.Management/managementGroups/ITAdmins/providers/Microsoft.Authorization/roleAssignments/63017a1f-e4ed-4733-b032-656f27551028";

        public const string GlobalRoleAssignmentResourceId = "/providers/Microsoft.Authorization/roleAssignments/63017a1f-e4ed-4733-b032-656f27551028";

        public const string GlobalNotSupportedRoleAssignmentResourceId = "/providers/Microsoft.PowerApps/environments/EnvName/providers/Microsoft.Authorization/roleAssignments/63017a1f-e4ed-4733-b032-656f27551028";

        public const string ManagementGroupResourceId = "/providers/Microsoft.Management/managementgroups/mg1";

        public const string SubscriptionResourceId = "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef";

        public const string ResourceGroupResourceId = "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-SQL-WestUS";

        public const string LocationResourceId = "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/locations/westus";

        public const string QuotaAndUsageResourceId =
            "/subscriptions/d1af5f8d-c2be-410e-8152-3b67724c58d7/providers/Microsoft.Compute/locations/eastus/usages/default";

        public const string SingleEventGridEventWithMultiResources = """
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
                  },
                  {
                    "correlationId": "d82b3f83-9004-4069-9aaf-6329546d5a13",
                    "resourceId": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm2",
                    "apiVersion": "2022-11-01",
                    "resourceHomeTenantId" : "72f988bf-86f1-41af-91ab-2d7cd011db47",
                    "armResource": {
                      "name": "idm",
                      "id": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm2",
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
                  },
                  {
                    "correlationId": "d82b3f83-9004-4069-9aaf-6329546d5a14",
                    "resourceId": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm3",
                    "apiVersion": "2022-11-01",
                    "resourceHomeTenantId" : "72f988bf-86f1-41af-91ab-2d7cd011db47",
                    "armResource": {
                      "name": "idm",
                      "id": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm3",
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

        public const string MultiResourcesList = """
            [
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
                  },
                  {
                    "correlationId": "d82b3f83-9004-4069-9aaf-6329546d5a13",
                    "resourceId": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm2",
                    "apiVersion": "2022-11-01",
                    "resourceHomeTenantId" : "72f988bf-86f1-41af-91ab-2d7cd011db47",
                    "armResource": {
                      "name": "idm",
                      "id": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm2",
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
                  },
                  {
                    "correlationId": "d82b3f83-9004-4069-9aaf-6329546d5a14",
                    "resourceId": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm3",
                    "apiVersion": "2022-11-01",
                    "resourceHomeTenantId" : "72f988bf-86f1-41af-91ab-2d7cd011db47",
                    "armResource": {
                      "name": "idm",
                      "id": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm3",
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
            """;

        public const string InvalidMultiResourcesList = """
            [
                  {
                    "correlationId": "d82b3f83-9004-4069-9aaf-6329546d5a12",
                    "resourceId": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm",
                    "apiVersion": "2022-11-01",
                    "resourceHomeTenantId" : "72f988bf-86f1-41af-91ab-2d7cd011db47",
                    "armResource": 
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
                  },
                  {
                    "correlationId": "d82b3f83-9004-4069-9aaf-6329546d5a13",
                    "resourceId": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm2",
                    "apiVersion": "2022-11-01",
                    "resourceHomeTenantId" : "72f988bf-86f1-41af-91ab-2d7cd011db47",
                    "armResource": {
                      "name": "idm",
                      "id": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm2",
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
                  },
                  {
                    "correlationId": "d82b3f83-9004-4069-9aaf-6329546d5a14",
                    "resourceId": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm3",
                    "apiVersion": "2022-11-01",
                    "resourceHomeTenantId" : "72f988bf-86f1-41af-91ab-2d7cd011db47",
                    "armResource": {
                      "name": "idm",
                      "id": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm3",
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
            """;

        public const string SingleEventGridEvent = """
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
            }
            """;

        public const string NoResourceNotification = """
            {
                "data": {
                    "additionalBatchProperties": {
                        "system": {
                            "contract": {
                                "CorrelationId": "326165d0-1f7d-4236-8cfa-88b6a21eba23",
                                "Version": "0.1.0"
                            }
                        }
                    },
                    "publisherInfo": "Microsoft.CapacityAllocation",
                    "resourceLocation": "global",
                    "resourcesContainer": "Blob",
                    "routingType": "ProxyOnly"
                },
                "dataVersion": "3.0",
                "eventTime": "2023-10-18T19:00:34.073806Z",
                "eventType": "Microsoft.CapacityAllocation/capacityRestrictions/restrictionsChanged/event",
                "id": "2b5a9a0f-7851-4f3e-83bc-7fa9a9423be7",
                "metadataVersion": "1",
                "subject": "subscriptions/8718b6d7-ba9e-4192-b6ea-30edc550c3fe/providers/Microsoft.CapacityAllocation/capacityRestrictions/default",
                "topic": "/subscriptions/8ae1303d-cac3-4232-9269-a7109121f58f/resourceGroups/GovernanceNotificationsDfStorage/providers/Microsoft.EventGrid/domains/gov-ntdisp-df-egdomain1/topics/microsoftresourcegraph-arn-dd728571-b646-727a-1ee2-61b4162899f7"
            }
            """;

        public const string BlobURLNotification = """
            {
               "topic": "custom domaintopic/eg topic",
               "subject": "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm",
               "eventType": "Microsoft.Compute/virtualMachineScaleSets/write",
               "eventTime": "2018-11-02T21:46:13.939951Z",
               "id": "164f5e66-a908-4cfe-9499-9165f2d82b16",
               "dataVersion": "3.0",
               "metadataVersion": "1",
                "data": {
                    "resourcesContainer": "Blob",
                    "resourceLocation": "eastus",
                    "frontdoorLocation": "",
                    "publisherInfo": "Microsoft.Compute",
                    "routingType": "ProxyOnly",
                    "resources": null,
                    "resourcesBlobInfo": {
                       "blobUri": "https://xxxspod01srs1ecsar4zyr.blob.core.windows.net/arm-ext-nt-2023-11-24/abc061a6-769f-4390-abca-663dfd342abc.txt?skoid=abc0a7f7-7dc8-43fa-abcc-0975ab16fb42&sktid=abc01921-4abc-4f8c-a055-5bdaffd5e33d&skt=2023-11-16T07%3A33%3A00Z&ske=2023-11-23T07%3A33%3A00Z&sks=b&skv=2022-11-02&sv=2022-11-02&st=2023-11-24T23%3A49%3A00Z&se=2023-11-23T07%3A33%3A00Z&sr=b&sp=r&sig=MASKED",
                       "blobSize": "1045"
                    }
                },
               
            }
            """;

        public const string SingleEventGridEventWithEventArray = 
            "[" + SingleEventGridEvent + "]";

        public static readonly BinaryData BlobURLEventGridEventBinaryData = new(Encoding.UTF8.GetBytes(BlobURLNotification));
        public static readonly BinaryData NoResourceEventGridEventBinaryData = new(Encoding.UTF8.GetBytes(NoResourceNotification));
        public static readonly BinaryData SingleEventGridEventBinaryData = new(Encoding.UTF8.GetBytes(SingleEventGridEvent));
        public static readonly BinaryData SingleEventGridEventWithEventArrayBinaryData = new(Encoding.UTF8.GetBytes(SingleEventGridEventWithEventArray));
        public static readonly BinaryData SingleEventGridEventMultiResourcesBinaryData = new(Encoding.UTF8.GetBytes(SingleEventGridEventWithMultiResources));
        public static readonly BinaryData MultiResourcesBinaryData = new(Encoding.UTF8.GetBytes(MultiResourcesList));
        public static readonly BinaryData InvalidMultiResourcesBinaryData = new(Encoding.UTF8.GetBytes(InvalidMultiResourcesList));

        public const string PartnerSingleResponseResourcesRouting = "{ \"resourceTypes\" : \"*\", \"partnerChannelAddress\":\"http://localhost:5072\", \"partnerChannelName\":\"localhost\"};";
        public const string PartnerChannelConcurrency = "localhost:3;";
    }
}
