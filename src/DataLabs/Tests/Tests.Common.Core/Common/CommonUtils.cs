namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Common
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    public static class CommonUtils
    {
        public static void SetupRegionConfigManager(IOutputBlobClient outputBlobClient)
        {
            var primaryRegionName = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.PrimaryRegionName);
            var backupRegionName = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.BackupRegionName);

            var regionConfig1 = new RegionConfig
            {
                RegionLocationName = primaryRegionName,
                sourceOfTruthStorageAccountNames = "primarystorageaccountname",
                outputBlobClient = outputBlobClient
            };
            var regionConfig2 = new RegionConfig
            {
                RegionLocationName = backupRegionName,
                sourceOfTruthStorageAccountNames = "backupstorageaccountname",
                outputBlobClient = outputBlobClient
            };
            RegionConfigManager.Initialize(ConfigMapUtil.Configuration, regionConfig1, regionConfig2);
        }

        public static X509Certificate2 CreateDummyCert()
        {
            using (RSA rsa = RSA.Create(2048))
            {
                CertificateRequest certificateRequest = new CertificateRequest(
                    "CN=DummyCertificate",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                X509Certificate2 certificate = certificateRequest.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddYears(1));

                return new X509Certificate2(certificate.Export(X509ContentType.Pkcs12));
            }
        }

        public static string GetInlinePayloadEventString(
            string eventGridTopic = "custom domaintopic/eg topic",
            string subscriptionId = "02d59989-f8a9-4b69-9919-1ef51df4eff6",
            string tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47",
            string resourceGroup = "AzureResourcesCacheidm-Int-Solution-a",
            string type = "Microsoft.Compute/virtualMachineScaleSets",
            string name = "idm",
            string action = "write",
            string publisher = "Microsoft.Compute",
            string resourceLocation = "eastus",
            string additionalResourcePropertyKey = "key",
            string additionalResourcePropertyValue = "value",
            string additionalBatchPropertyKey = "key",
            string additionalBatchPropertyValue = "value",
            string resourcePropertyKey = "key",
            string resourcePropertyValue = "value")
        {
            return $@"
            {{
              ""topic"": ""{eventGridTopic}"",
              ""subject"": ""/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{type}/{name}"",
              ""eventType"": ""{type}/{action}"",
              ""eventTime"": ""2018-11-02T21:46:13.939951Z"",
              ""id"": ""164f5e66-a908-4cfe-9499-9165f2d82b16"",
              ""dataVersion"": ""3.0"",
              ""metadataVersion"": ""1"",
              ""data"": {{
                ""resourcesContainer"": ""Inline"",
                ""resourceLocation"": ""{resourceLocation}"",
                ""frontdoorLocation"": """",
                ""publisherInfo"": ""{publisher}"",
                ""resourcesBlobInfo"": null,
                ""additionalBatchProperties"": {{ ""{additionalBatchPropertyKey}"": ""{additionalBatchPropertyValue}"" }},
                ""resources"": [
                  {{
                    ""correlationId"": ""d82b3f83-9004-4069-9aaf-6329546d5a12"",
                    ""resourceId"": ""/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{type}/{name}"",
                    ""apiVersion"": ""2022-11-01"",
                    ""resourceHomeTenantId"" : ""{tenantId}"",
                    ""armResource"": {{
                      ""name"": ""idm"",
                      ""id"": ""/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{type}/{name}"",
                      ""type"": ""{type}"",
                      ""location"": ""{resourceLocation}"",
                      ""sku"": {{
                        ""name"": ""Standard_F2"",
                        ""tier"": ""Standard"",
                        ""capacity"": 15
                      }},
                      ""properties"": {{ ""{resourcePropertyKey}"": ""{resourcePropertyValue}"" }}
                    }},
                    ""additionalResourceProperties"": {{ ""{additionalResourcePropertyKey}"": ""{additionalResourcePropertyValue}"" }}
                  }}
                ]
              }}
            }}";
        }

        public static string GetBlobPayloadEventString(
            string eventGridTopic = "custom domaintopic/eg topic",
            string subscriptionId = "02d59989-f8a9-4b69-9919-1ef51df4eff6",
            string tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47",
            string resourceGroup = "AzureResourcesCacheidm-Int-Solution-a",
            string type = "Microsoft.Compute/virtualMachineScaleSets",
            string name = "idm",
            string action = "write",
            string publisher = "Microsoft.Compute",
            string resourceLocation = "eastus",
            string additionalBatchPropertyKey = "key",
            string additionalBatchPropertyValue = "value",
            string blobUri = "https://localhost/a",
            string blobSize = "100000")
        {
            return $@"
            {{
              ""topic"": ""{eventGridTopic}"",
              ""subject"": ""/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{type}/{name}"",
              ""eventType"": ""{type}/{action}"",
              ""eventTime"": ""2018-11-02T21:46:13.939951Z"",
              ""id"": ""164f5e66-a908-4cfe-9499-9165f2d82b16"",
              ""dataVersion"": ""3.0"",
              ""metadataVersion"": ""1"",
              ""data"": {{
                ""resourcesContainer"": ""Blob"",
                ""resourceHomeTenantId"": ""{tenantId}"",
                ""resourceLocation"": ""{resourceLocation}"",
                ""frontdoorLocation"": """",
                ""publisherInfo"": ""{publisher}"",
                ""resourcesBlobInfo"": {{
                    ""blobUri"": ""{blobUri}"",
                    ""blobSize"": ""{blobSize}""
                }},
                ""additionalBatchProperties"": {{ ""{additionalBatchPropertyKey}"": ""{additionalBatchPropertyValue}"" }}
              }}
            }}";
        }

        public static BinaryData ToBinaryData(this string input)
        {
            return new BinaryData(input);
        }

        public static EventGridNotification<NotificationDataV3<GenericResource>> ToV3Event(this string input)
        {
            return SerializationHelper.Deserialize<EventGridNotification<NotificationDataV3<GenericResource>>>(input);   
        }
    }
}
