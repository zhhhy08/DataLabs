namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Data
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using System;
    using System.Text;

    public static class ARNGeneratedNotifications
    {
        // 0: ResourceId
        // 1: ResourceType
        // 2: EventTime
        // 3: ResourceNamespace
        // 4: TenantId
        // 5: CorrelationId
        // 6: ResourceId
        // 7: EventTime
        // 8: TenantId
        // Dummy Values NOT to change: topic, apiVersion, dataVersion, metadataVersion
        private static readonly string _deletedNotificationFormat = @"{{
            ""id"": ""00000000-0000-0000-0000-000000000000"",
            ""topic"": ""DL_ARM"",
            ""subject"": ""{0}"",
            ""eventType"": ""{1}/delete"",
            ""eventTime"": ""{2}"",
            ""data"": {{
                ""resourceLocation"": ""global"",
                ""publisherInfo"": ""{3}"",
                ""homeTenantId"": ""{4}"",
                ""apiVersion"": ""2023-01-01"",
                ""resources"": [
                    {{
                        ""resourceSystemProperties"": {{
                            ""changedAction"": ""Undefined""
                        }},
                        ""correlationId"": ""{5}"",
                        ""resourceId"": ""{6}"",
                        ""resourceEventTime"": ""{7}"",
                        ""homeTenantId"": ""{8}""
                    }}
                ]
            }},
            ""dataVersion"": ""3.0"",
            ""metadataVersion"": ""1""
        }}";

        public static ReadOnlyMemory<byte> GenerateDeletedARN3Notification(
            string resourceType, string resourceId, string tenantId, string correlationId, string eventTime)
        {
            var resourceNamespace = resourceId.GetResourceNamespace();

            string value = string.Format(_deletedNotificationFormat,
                resourceId, resourceType, eventTime, resourceNamespace, tenantId, correlationId, resourceId, eventTime, tenantId);

            return new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(value));
        }
    }
}
