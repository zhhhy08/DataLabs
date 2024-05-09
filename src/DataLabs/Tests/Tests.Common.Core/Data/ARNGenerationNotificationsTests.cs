namespace Tests.Common.Core.Data
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Data;

    [TestClass]
    public class ARNGenerationNotificationsTests
    {
        public readonly string expectedResult = @"{
            ""id"": ""00000000-0000-0000-0000-000000000000"",
            ""topic"": ""DL_ARM"",
            ""subject"": ""/subscriptions/0a666793-59ey-4987-w26q-59096c5219mk/providers/Microsoft.Authorization/roleAssignments/a9348fd3-e0cf-46f1-82f4-39cc0543527a"",
            ""eventType"": ""Microsoft.Authorization/roleAssignments/delete"",
            ""eventTime"": ""2020-04-07T23:53:54.5096198+00:00"",
            ""data"": {
                ""resourceLocation"": ""global"",
                ""publisherInfo"": ""Microsoft.Authorization"",
                ""homeTenantId"": ""GC49U687-15JJ-4637-9281-CEP94BB89227"",
                ""apiVersion"": ""2023-01-01"",
                ""resources"": [
                    {
                        ""resourceSystemProperties"": {
                            ""changedAction"": ""Undefined""
                        },
                        ""correlationId"": ""3a7293ft-025i-4te5-9215-f3f5cb33f10k"",
                        ""resourceId"": ""/subscriptions/0a666793-59ey-4987-w26q-59096c5219mk/providers/Microsoft.Authorization/roleAssignments/a9348fd3-e0cf-46f1-82f4-39cc0543527a"",
                        ""resourceEventTime"": ""2020-04-07T23:53:54.5096198+00:00"",
                        ""homeTenantId"": ""GC49U687-15JJ-4637-9281-CEP94BB89227""
                    }
                ]
            },
            ""dataVersion"": ""3.0"",
            ""metadataVersion"": ""1""
        }";

        [TestMethod]
        public void TestGeneratedResult()
        {
            var result = ARNGeneratedNotifications.GenerateDeletedARN3Notification(
                "Microsoft.Authorization/roleAssignments",
                "/subscriptions/0a666793-59ey-4987-w26q-59096c5219mk/providers/Microsoft.Authorization/roleAssignments/a9348fd3-e0cf-46f1-82f4-39cc0543527a",
                "GC49U687-15JJ-4637-9281-CEP94BB89227",
                "3a7293ft-025i-4te5-9215-f3f5cb33f10k",
                "2020-04-07T23:53:54.5096198+00:00");

            Assert.AreEqual(expectedResult, System.Text.Encoding.Default.GetString(result.ToArray()));
        }
    }
}
