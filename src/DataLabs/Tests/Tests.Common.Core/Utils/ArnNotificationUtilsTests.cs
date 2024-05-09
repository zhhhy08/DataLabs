namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Utils
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts;
    using Newtonsoft.Json;

    [TestClass]
    public class ArnNotificationUtilsTests
    {
        #region Fields

        private const string SnapshotEvent = @"
{
    ""id"": ""905ca8f4-435e-472e-aa03-bc66d600a713"",
    ""topic"": ""/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesTopologyintStorage/providers/Microsoft.EventGrid/domains/gov-rp-art-int-arn-publish-1/topics/arnpublish"",
    ""subject"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b"",
    ""data"":
    {
        ""resourceLocation"": ""koreacentral"",
        ""publisherInfo"": ""Microsoft.ResourceGraph"",
        ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47"",
        ""resources"":
        [
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""a0a233d9-1db0-45f3-a633-9c90e1a063cf"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig"",
                ""armResource"":
                {
                    ""id"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig"",
                    ""name"": ""RunnerConfig"",
                    ""type"": ""microsoft.automation/automationaccounts/configurations"",
                    ""location"": ""koreacentral"",
                    ""kind"": ""Runbook"",
                    ""managedby"": ""c"",
                    ""tags"":
                    {},
                    ""properties"":
                    {
                        ""provisioningState"": ""Succeeded"",
                        ""jobCount"": 0,
                        ""parameters"":
                        {},
                        ""description"": null,
                        ""source"": null,
                        ""state"": ""Published"",
                        ""creationTime"": ""2022-05-21T14:24:09.9766667Z"",
                        ""lastModifiedTime"": ""2022-05-21T14:24:09.9800000Z"",
                        ""logVerbose"": 0,
                        ""rawTags"": null,
                        ""nodeConfigurationCount"": 0
                    },
                    ""apiVersion"": ""2022-08-08""
                },
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:20.7734505"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            },
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""d93b0eb9-3a1a-43e0-9d21-62b9374d61f7"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-56baa932-9828-484b-95f4-0ede784564b7/configurations/RunnerConfig"",
                ""armResource"":
                {
                    ""id"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-56baa932-9828-484b-95f4-0ede784564b7/configurations/RunnerConfig"",
                    ""name"": ""RunnerConfig"",
                    ""type"": ""microsoft.automation/automationaccounts/configurations"",
                    ""location"": ""koreacentral"",
                    ""tags"":
                    {},
                    ""properties"":
                    {
                        ""provisioningState"": ""Succeeded"",
                        ""jobCount"": 0,
                        ""parameters"":
                        {},
                        ""description"": null,
                        ""source"": null,
                        ""state"": ""Published"",
                        ""creationTime"": ""2022-05-06T11:34:18.7600000Z"",
                        ""lastModifiedTime"": ""2022-05-06T11:34:18.7600000Z"",
                        ""logVerbose"": 0,
                        ""rawTags"": null,
                        ""nodeConfigurationCount"": 0
                    },
                    ""apiVersion"": ""2022-08-08""
                },
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:22.0329399"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            }
        ],
        ""additionalBatchProperties"":
        {
            ""$id"": ""1"",
            ""sdkVersion"": ""2.0.52"",
            ""batchSize"": 13,
            ""batchCorrelationId"": ""199f7bd3-e05b-4f00-9b0c-4fbb333112bc""
        }
    },
    ""eventType"": ""Microsoft.Automation/automationAccounts/configurations/Snapshot"",
    ""dataVersion"": ""3.0"",
    ""metadataVersion"": ""1"",
    ""eventTime"": ""2023-08-02T00:55:48.1494141Z"",
    ""EventProcessedUtcTime"": ""2023-08-02T00:58:11.0399050Z"",
    ""PartitionId"": 0,
    ""EventEnqueuedUtcTime"": ""2023-08-02T00:55:48.1660000Z""
}";

        private const string WriteEvent = @"
{
    ""id"": ""905ca8f4-435e-472e-aa03-bc66d600a713"",
    ""topic"": ""/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesTopologyintStorage/providers/Microsoft.EventGrid/domains/gov-rp-art-int-arn-publish-1/topics/arnpublish"",
    ""subject"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b"",
    ""data"":
    {
        ""resourceLocation"": ""koreacentral"",
        ""publisherInfo"": ""Microsoft.ResourceGraph"",
        ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47"",
        ""resources"":
        [
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""a0a233d9-1db0-45f3-a633-9c90e1a063cf"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig"",
                ""armResource"":
                {
                    ""id"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig"",
                    ""name"": ""RunnerConfig"",
                    ""type"": ""microsoft.automation/automationaccounts/configurations"",
                    ""location"": ""koreacentral"",
                    ""kind"": ""Runbook"",
                    ""managedby"": ""c"",
                    ""tags"":
                    {},
                    ""properties"":
                    {
                        ""provisioningState"": ""Succeeded"",
                        ""jobCount"": 0,
                        ""parameters"":
                        {},
                        ""description"": null,
                        ""source"": null,
                        ""state"": ""Published"",
                        ""creationTime"": ""2022-05-21T14:24:09.9766667Z"",
                        ""lastModifiedTime"": ""2022-05-21T14:24:09.9800000Z"",
                        ""logVerbose"": 0,
                        ""rawTags"": null,
                        ""nodeConfigurationCount"": 0
                    },
                    ""apiVersion"": ""2022-08-08""
                },
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:20.7734505"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            },
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""d93b0eb9-3a1a-43e0-9d21-62b9374d61f7"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-56baa932-9828-484b-95f4-0ede784564b7/configurations/RunnerConfig"",
                ""armResource"":
                {
                    ""id"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-56baa932-9828-484b-95f4-0ede784564b7/configurations/RunnerConfig"",
                    ""name"": ""RunnerConfig"",
                    ""type"": ""microsoft.automation/automationaccounts/configurations"",
                    ""location"": ""koreacentral"",
                    ""tags"":
                    {},
                    ""properties"":
                    {
                        ""provisioningState"": ""Succeeded"",
                        ""jobCount"": 0,
                        ""parameters"":
                        {},
                        ""description"": null,
                        ""source"": null,
                        ""state"": ""Published"",
                        ""creationTime"": ""2022-05-06T11:34:18.7600000Z"",
                        ""lastModifiedTime"": ""2022-05-06T11:34:18.7600000Z"",
                        ""logVerbose"": 0,
                        ""rawTags"": null,
                        ""nodeConfigurationCount"": 0
                    },
                    ""apiVersion"": ""2022-08-08""
                },
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:22.0329399"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            }
        ],
        ""additionalBatchProperties"":
        {
            ""$id"": ""1"",
            ""sdkVersion"": ""2.0.52"",
            ""batchSize"": 13,
            ""batchCorrelationId"": ""199f7bd3-e05b-4f00-9b0c-4fbb333112bc""
        }
    },
    ""eventType"": ""Microsoft.Automation/automationAccounts/configurations/write"",
    ""dataVersion"": ""3.0"",
    ""metadataVersion"": ""1"",
    ""eventTime"": ""2023-08-02T00:55:48.1494141Z"",
    ""EventProcessedUtcTime"": ""2023-08-02T00:58:11.0399050Z"",
    ""PartitionId"": 0,
    ""EventEnqueuedUtcTime"": ""2023-08-02T00:55:48.1660000Z""
}";

        private const string DeleteEvent = @"
{
    ""id"": ""905ca8f4-435e-472e-aa03-bc66d600a713"",
    ""topic"": ""/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesTopologyintStorage/providers/Microsoft.EventGrid/domains/gov-rp-art-int-arn-publish-1/topics/arnpublish"",
    ""subject"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b"",
    ""data"":
    {
        ""resourceLocation"": ""koreacentral"",
        ""publisherInfo"": ""Microsoft.ResourceGraph"",
        ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47"",
        ""resources"":
        [
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""a0a233d9-1db0-45f3-a633-9c90e1a063cf"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurationRunnerConfig"",
                ""armResource"": null,
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:20.7734505"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            },
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""d93b0eb9-3a1a-43e0-9d21-62b9374d61f7"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-56baa932-9828-484b-95f4-0ede784564b7/configurations/RunnerConfig"",
                ""armResource"": null,
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:22.0329399"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            }
        ],
        ""additionalBatchProperties"":
        {
            ""$id"": ""1"",
            ""sdkVersion"": ""2.0.52"",
            ""batchSize"": 13,
            ""batchCorrelationId"": ""199f7bd3-e05b-4f00-9b0c-4fbb333112bc""
        }
    },
    ""eventType"": ""Microsoft.Automation/automationAccounts/configurations/delete"",
    ""dataVersion"": ""3.0"",
    ""metadataVersion"": ""1"",
    ""eventTime"": ""2023-08-02T00:55:48.1494141Z"",
    ""EventProcessedUtcTime"": ""2023-08-02T00:58:11.0399050Z"",
    ""PartitionId"": 0,
    ""EventEnqueuedUtcTime"": ""2023-08-02T00:55:48.1660000Z""
}";

        private const string DeleteEventEmptyGuid = @"
{
    ""id"": ""905ca8f4-435e-472e-aa03-bc66d600a713"",
    ""topic"": ""/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesTopologyintStorage/providers/Microsoft.EventGrid/domains/gov-rp-art-int-arn-publish-1/topics/arnpublish"",
    ""subject"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b"",
    ""data"":
    {
        ""resourceLocation"": ""koreacentral"",
        ""publisherInfo"": ""Microsoft.ResourceGraph"",
        ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47"",
        ""resources"":
        [
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""00000000-0000-0000-0000-000000000000"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurationRunnerConfig"",
                ""armResource"": null,
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:20.7734505"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            },
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""d93b0eb9-3a1a-43e0-9d21-62b9374d61f7"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-56baa932-9828-484b-95f4-0ede784564b7/configurations/RunnerConfig"",
                ""armResource"": null,
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:22.0329399"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            }
        ],
        ""additionalBatchProperties"":
        {
            ""$id"": ""1"",
            ""sdkVersion"": ""2.0.52"",
            ""batchSize"": 13,
            ""batchCorrelationId"": ""199f7bd3-e05b-4f00-9b0c-4fbb333112bc""
        }
    },
    ""eventType"": ""Microsoft.Automation/automationAccounts/configurations/delete"",
    ""dataVersion"": ""3.0"",
    ""metadataVersion"": ""1"",
    ""eventTime"": ""2023-08-02T00:55:48.1494141Z"",
    ""EventProcessedUtcTime"": ""2023-08-02T00:58:11.0399050Z"",
    ""PartitionId"": 0,
    ""EventEnqueuedUtcTime"": ""2023-08-02T00:55:48.1660000Z""
}";

        private const string MoveEvent = @"
{
    ""id"": ""905ca8f4-435e-472e-aa03-bc66d600a713"",
    ""topic"": ""/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesTopologyintStorage/providers/Microsoft.EventGrid/domains/gov-rp-art-int-arn-publish-1/topics/arnpublish"",
    ""subject"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b"",
    ""data"":
    {
        ""resourceLocation"": ""koreacentral"",
        ""publisherInfo"": ""Microsoft.ResourceGraph"",
        ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47"",
        ""resources"":
        [
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""a0a233d9-1db0-45f3-a633-9c90e1a063cf"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig"",
                ""sourceResourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig1"",
                ""armResource"":
                {
                    ""id"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig"",
                    ""name"": ""RunnerConfig"",
                    ""type"": ""microsoft.automation/automationaccounts/configurations"",
                    ""location"": ""koreacentral"",
                    ""kind"": ""Runbook"",
                    ""managedby"": ""c"",
                    ""tags"":
                    {},
                    ""properties"":
                    {
                        ""provisioningState"": ""Succeeded"",
                        ""jobCount"": 0,
                        ""parameters"":
                        {},
                        ""description"": null,
                        ""source"": null,
                        ""state"": ""Published"",
                        ""creationTime"": ""2022-05-21T14:24:09.9766667Z"",
                        ""lastModifiedTime"": ""2022-05-21T14:24:09.9800000Z"",
                        ""logVerbose"": 0,
                        ""rawTags"": null,
                        ""nodeConfigurationCount"": 0
                    },
                    ""apiVersion"": ""2022-08-08""
                },
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:20.7734505"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            },
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""d93b0eb9-3a1a-43e0-9d21-62b9374d61f7"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-56baa932-9828-484b-95f4-0ede784564b7/configurations/RunnerConfig"",
                ""sourceResourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig1"",
                ""armResource"":
                {
                    ""id"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-56baa932-9828-484b-95f4-0ede784564b7/configurations/RunnerConfig"",
                    ""name"": ""RunnerConfig"",
                    ""type"": ""microsoft.automation/automationaccounts/configurations"",
                    ""location"": ""koreacentral"",
                    ""tags"":
                    {},
                    ""properties"":
                    {
                        ""provisioningState"": ""Succeeded"",
                        ""jobCount"": 0,
                        ""parameters"":
                        {},
                        ""description"": null,
                        ""source"": null,
                        ""state"": ""Published"",
                        ""creationTime"": ""2022-05-06T11:34:18.7600000Z"",
                        ""lastModifiedTime"": ""2022-05-06T11:34:18.7600000Z"",
                        ""logVerbose"": 0,
                        ""rawTags"": null,
                        ""nodeConfigurationCount"": 0
                    },
                    ""apiVersion"": ""2022-08-08""
                },
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:22.0329399"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            }
        ],
        ""additionalBatchProperties"":
        {
            ""$id"": ""1"",
            ""sdkVersion"": ""2.0.52"",
            ""batchSize"": 13,
            ""batchCorrelationId"": ""199f7bd3-e05b-4f00-9b0c-4fbb333112bc""
        }
    },
    ""eventType"": ""Microsoft.Automation/automationAccounts/configurations/move/action"",
    ""dataVersion"": ""3.0"",
    ""metadataVersion"": ""1"",
    ""eventTime"": ""2023-08-02T00:55:48.1494141Z"",
    ""EventProcessedUtcTime"": ""2023-08-02T00:58:11.0399050Z"",
    ""PartitionId"": 0,
    ""EventEnqueuedUtcTime"": ""2023-08-02T00:55:48.1660000Z""
}";

        private const string UndefinedEvent = @"
{
    ""id"": ""905ca8f4-435e-472e-aa03-bc66d600a713"",
    ""topic"": ""/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesTopologyintStorage/providers/Microsoft.EventGrid/domains/gov-rp-art-int-arn-publish-1/topics/arnpublish"",
    ""subject"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b"",
    ""data"":
    {
        ""resourceLocation"": ""koreacentral"",
        ""publisherInfo"": ""Microsoft.ResourceGraph"",
        ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47"",
        ""resources"":
        [
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""a0a233d9-1db0-45f3-a633-9c90e1a063cf"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig"",
                ""sourceResourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig1"",
                ""armResource"":
                {
                    ""id"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig"",
                    ""name"": ""RunnerConfig"",
                    ""type"": ""microsoft.automation/automationaccounts/configurations"",
                    ""location"": ""koreacentral"",
                    ""kind"": ""Runbook"",
                    ""managedby"": ""c"",
                    ""tags"":
                    {},
                    ""properties"":
                    {
                        ""provisioningState"": ""Succeeded"",
                        ""jobCount"": 0,
                        ""parameters"":
                        {},
                        ""description"": null,
                        ""source"": null,
                        ""state"": ""Published"",
                        ""creationTime"": ""2022-05-21T14:24:09.9766667Z"",
                        ""lastModifiedTime"": ""2022-05-21T14:24:09.9800000Z"",
                        ""logVerbose"": 0,
                        ""rawTags"": null,
                        ""nodeConfigurationCount"": 0
                    },
                    ""apiVersion"": ""2022-08-08""
                },
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:20.7734505"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            },
            {
                ""resourceSystemProperties"":
                {
                    ""changedAction"": ""Undefined""
                },
                ""correlationId"": ""d93b0eb9-3a1a-43e0-9d21-62b9374d61f7"",
                ""resourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-56baa932-9828-484b-95f4-0ede784564b7/configurations/RunnerConfig"",
                ""sourceResourceId"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-90adc92f-66e6-4379-8bce-6c9c03d8d66e/configurations/RunnerConfig1"",
                ""armResource"":
                {
                    ""id"": ""/subscriptions/2c2d82c1-642b-4a02-a44f-7c4b7037995b/resourceGroups/DVT_ARM/providers/Microsoft.Automation/automationAccounts/DVTAutomation-56baa932-9828-484b-95f4-0ede784564b7/configurations/RunnerConfig"",
                    ""name"": ""RunnerConfig"",
                    ""type"": ""microsoft.automation/automationaccounts/configurations"",
                    ""location"": ""koreacentral"",
                    ""tags"":
                    {},
                    ""properties"":
                    {
                        ""provisioningState"": ""Succeeded"",
                        ""jobCount"": 0,
                        ""parameters"":
                        {},
                        ""description"": null,
                        ""source"": null,
                        ""state"": ""Published"",
                        ""creationTime"": ""2022-05-06T11:34:18.7600000Z"",
                        ""lastModifiedTime"": ""2022-05-06T11:34:18.7600000Z"",
                        ""logVerbose"": 0,
                        ""rawTags"": null,
                        ""nodeConfigurationCount"": 0
                    },
                    ""apiVersion"": ""2022-08-08""
                },
                ""apiVersion"": ""2022-08-08"",
                ""resourceEventTime"": ""2023-08-02T00:55:22.0329399"",
                ""statusCode"": ""OK"",
                ""homeTenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47""
            }
        ],
        ""additionalBatchProperties"":
        {
            ""$id"": ""1"",
            ""sdkVersion"": ""2.0.52"",
            ""batchSize"": 13,
            ""batchCorrelationId"": ""199f7bd3-e05b-4f00-9b0c-4fbb333112bc""
        }
    },
    ""eventType"": ""Microsoft.Automation/automationAccounts/configurations/update/action"",
    ""dataVersion"": ""3.0"",
    ""metadataVersion"": ""1"",
    ""eventTime"": ""2023-08-02T00:55:48.1494141Z"",
    ""EventProcessedUtcTime"": ""2023-08-02T00:58:11.0399050Z"",
    ""PartitionId"": 0,
    ""EventEnqueuedUtcTime"": ""2023-08-02T00:55:48.1660000Z""
}";

        #endregion

        [DataRow(SnapshotEvent, ResourceAction.Snapshot)]
        [DataRow(WriteEvent, ResourceAction.Write)]
        [DataRow(DeleteEvent, ResourceAction.Delete)]
        [DataRow(DeleteEventEmptyGuid, ResourceAction.Delete)]
        [DataRow(MoveEvent, ResourceAction.Move)]
        [TestMethod]
        public void TestConvertEventGridEventSuccess(string eventString, ResourceAction resourceAction)
        {
            var ege = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(eventString);
            var resourceOperations = ArnNotificationUtils.ConvertArnNotificaitonV3ToResourceOperation(ege);
            Assert.AreEqual(2, resourceOperations.Count);
            Assert.AreEqual(resourceAction, resourceOperations[0].Action);
            Assert.AreEqual(resourceAction, resourceOperations[1].Action);
            Assert.IsNotNull(resourceOperations[0].ResourceObject);
            Assert.IsNotNull(resourceOperations[0].ResourceObject.Id);
            Assert.IsNotNull(resourceOperations[0].ResourceObject.Location);
            Assert.IsNull(resourceOperations[0].ResourceObject.Plan);
            Assert.IsNull(resourceOperations[0].ResourceObject.SystemData);
            Assert.IsNull(resourceOperations[0].ResourceObject.Identity);
            Assert.IsNull(resourceOperations[0].ResourceObject.Sku);
            Assert.AreEqual(resourceAction == ResourceAction.Delete, resourceOperations[0].ResourceObject.Kind == null);
            Assert.AreEqual(resourceAction == ResourceAction.Delete, resourceOperations[0].ResourceObject.Name == null);
            Assert.AreEqual(resourceAction == ResourceAction.Delete, resourceOperations[0].ResourceObject.ApiVersion == null);
            Assert.AreEqual(resourceAction == ResourceAction.Delete, resourceOperations[0].ResourceObject.Tags == null);
            Assert.AreEqual(resourceAction == ResourceAction.Delete, resourceOperations[0].ResourceObject.Properties == null);
            Assert.AreEqual(resourceAction == ResourceAction.Delete, resourceOperations[0].ResourceObject.ManagedBy == null);
        }

        [DataRow(UndefinedEvent, ResourceAction.Undefined)]
        [TestMethod]
        public void TestConvertEventGridEvent(string eventString, ResourceAction resourceAction)
        {
            var ege = JsonConvert.DeserializeObject<EventGridNotification<NotificationDataV3<GenericResource>>>(eventString);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ArnNotificationUtils.ConvertArnNotificaitonV3ToResourceOperation(ege));
        }
    }
}
