namespace Tests.ArmDataCacheIngestionService.Helpers
{
    class Datasets
    {
        public const string afecRegistrationNotification = @"{
  ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
  ""topic"": ""System"",
  ""subject"": ""subject"",
  ""eventType"": ""Add"",
  ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
  ""metadataVersion"": """",
  ""dataVersion"": ""v1"",
  ""data"": {
    ""resourcesContainer"": ""Inline"",
    ""resourceLocation"": """",
    ""publisherInfo"": ""ARM"",
    ""resources"": [
      {
        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
        ""resourceId"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
        ""armResource"": {
          ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
          ""name"": ""Microsoft.ResourceGraph/argfeaturedatapoc"",
          ""type"": ""Microsoft.Features/featureProviders/subscriptionFeatureRegistrations"",
          ""properties"": {
          }
        },
        ""apiVersion"": ""v1"",
        ""resourceEventTime"": ""2023-03-21T18:19:00.5550107+00:00"",
        ""statusCode"": ""OK""
      }
    ],
    ""routingType"": ""Unknown"",
    ""additionalBatchProperties"": {
      ""system"": {
        ""contract"": {
          ""version"": ""0.0.3"",
          ""correlationId"": ""00000000-0000-0000-0000-000000000000""
        }
      }
    }
  }
}";

        public const string globalSku = @"{
  ""id"": ""/providers/Microsoft.Inventory/skuProviders/Microsoft.Compute/resourceTypes/virtualMachines/locations/westus/globalSkus/Standard_PB6s"",
  ""topic"": ""System"",
  ""subject"": ""subject"",
  ""eventType"": ""Add"",
  ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
  ""metadataVersion"": """",
  ""dataVersion"": ""v1"",
  ""data"": {
    ""resourcesContainer"": ""Inline"",
    ""resourceLocation"": """",
    ""publisherInfo"": ""ARM"",
    ""resources"": [
      {
        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
        ""resourceId"": ""/providers/Microsoft.Inventory/skuProviders/Microsoft.Compute/resourceTypes/virtualMachines/locations/westus/globalSkus/Standard_PB6s"",
        ""armResource"": {
          ""id"": ""/providers/Microsoft.Inventory/skuProviders/Microsoft.Compute/resourceTypes/virtualMachines/locations/westus/globalSkus/Standard_PB6s"",
          ""name"": ""Aligned"",
          ""type"": ""Microsoft.Inventory/skuProviders/resourceTypes/locations/globalSkus"",
          ""location"": ""eastus"",
          ""properties"": {
            ""ResourceType"": ""virtualMachines"",
            ""Name"": ""Aligned"",
            ""Tier"": null,
            ""Size"": null,
            ""Family"": null,
            ""Kind"": null,
            ""Locations"": [
              ""AustraliaCentral""
            ],
            ""LocationInfo"": [
              {
                ""Location"": ""AustraliaCentral"",
                ""Zones"": null,
                ""ZoneDetails"": null,
                ""ExtendedLocations"": null,
                ""Type"": null,
                ""LocationDetails"": null,
                ""IsSpotRestricted"": false,
                ""IsOndemandRestricted"": false,
                ""IsCapacityReservationRestricted"": false
              }
            ],
            ""RequiredQuotaIds"": null,
            ""RequiredFeatures"": null,
            ""Capacity"": null,
            ""Costs"": null,
            ""Capabilities"": {
			""MaximumPlatformFaultDomainCount"": ""2""
		     }
          }
        },
        ""apiVersion"": ""v1"",
        ""resourceEventTime"": ""2023-03-21T18:19:00.5550107+00:00"",
        ""statusCode"": ""OK""
      }
    ],
    ""routingType"": ""Unknown"",
    ""additionalBatchProperties"": {
      ""system"": {
        ""contract"": {
          ""version"": ""0.0.3"",
          ""correlationId"": ""00000000-0000-0000-0000-000000000000""
        }
      }
    }
  }
}";

        public const string subInternalPropEvent = @"{
  ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Inventory/subscriptionInternalProperties/default"",
  ""topic"": ""System"",
  ""subject"": ""subject"",
  ""eventType"": ""Microsoft.Inventory/subscriptionInternalProperties/write"",
  ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
  ""metadataVersion"": """",
  ""dataVersion"": ""v1"",
  ""data"": {
    ""resourcesContainer"": ""Inline"",
    ""resourceLocation"": """",
    ""publisherInfo"": ""Microsoft.Inventory"",
    ""resources"": [
      {
        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
        ""resourceId"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Inventory/subscriptionInternalProperties/default"",
        ""armResource"": {
          ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Inventory/subscriptionInternalProperties/default"",
          ""name"": ""default"",
          ""type"": ""Microsoft.Inventory/subscriptionInternalProperties"",
          ""properties"": {
              ""subscriptionId"": ""eaab1166-1e13-4370-a951-6ed345a48c15"",
              ""displayName"": ""Free Trial"",
              ""state"": ""Registered""
          }
        },
        ""apiVersion"": ""v1"",
        ""resourceEventTime"": ""2023-03-21T18:19:00.5550107+00:00"",
        ""statusCode"": ""OK""
      }
    ],
    ""routingType"": ""Unknown"",
    ""additionalBatchProperties"": {
      ""system"": {
        ""contract"": {
          ""version"": ""0.0.3"",
          ""correlationId"": ""00000000-0000-0000-0000-000000000000""
        }
      }
    }
  }
}";

        public const string subInternalPropDeleteEvent = @"{
  ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Inventory/subscriptionInternalProperties/default"",
  ""topic"": ""System"",
  ""subject"": ""subject"",
  ""eventType"": ""Microsoft.Inventory/subscriptionInternalProperties/write"",
  ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
  ""metadataVersion"": """",
  ""dataVersion"": ""v1"",
  ""data"": {
    ""resourcesContainer"": ""Inline"",
    ""resourceLocation"": """",
    ""publisherInfo"": ""Microsoft.Inventory"",
    ""resources"": [
      {
        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
        ""resourceId"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Inventory/subscriptionInternalProperties/default"",
        ""armResource"": {
          ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Inventory/subscriptionInternalProperties/default"",
          ""name"": ""default"",
          ""type"": ""Microsoft.Inventory/subscriptionInternalProperties"",
          ""properties"": {
              ""subscriptionId"": ""eaab1166-1e13-4370-a951-6ed345a48c15"",
              ""displayName"": ""Free Trial"",
              ""state"": ""Deleted""
          }
        },
        ""apiVersion"": ""v1"",
        ""resourceEventTime"": ""2023-03-21T18:19:00.5550107+00:00"",
        ""statusCode"": ""OK""
      }
    ],
    ""routingType"": ""Unknown"",
    ""additionalBatchProperties"": {
      ""system"": {
        ""contract"": {
          ""version"": ""0.0.3"",
          ""correlationId"": ""00000000-0000-0000-0000-000000000000""
        }
      }
    }
  }
}";

    }
}
