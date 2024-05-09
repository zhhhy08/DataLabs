namespace Tests.ResourceAliasService.Helpers
{
    class Datasets
    {
        public const string notificationWithSubjectAsAliasSuccessMapping = @"{
            ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
            ""topic"": ""System"",
            ""subject"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/4cabbc43-cf76-4eb7-b016-0e5a5317630f/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44"",
            ""eventType"": ""microsoft.maintenance/scheduledevents/write"",
            ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
            ""metadataVersion"": """",
            ""dataVersion"": ""v1"",
            ""data"": {
                ""resourcesContainer"": ""Inline"",
                ""resourceLocation"": ""centraluseuap"",
                ""publisherInfo"": ""microsoft.maintenance"",
                ""resources"": [
                    {
                        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
                        ""resourceId"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                        ""armResource"": {
                            ""id"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                            ""name"": ""cec1277b-758b-475f-af1f-a2da44fad695"",
                            ""type"": ""microsoft.maintenance/scheduledevents"",
                            ""properties"": {
                            }
                        },
                        ""additionalResourceProperties"": {
                            ""system"": {
                                ""aliases"": {
                                    ""resourceId"": {
                                        ""state"": ""original""
                                    }
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
                }
            }
        }";

        public const string notificationWithSubjectNotAliasSuccessMapping = @"{
            ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
            ""topic"": ""System"",
            ""subject"": ""/providers/someotherservice/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/4cabbc43-cf76-4eb7-b016-0e5a5317630f/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44"",
            ""eventType"": ""microsoft.maintenance/scheduledevents/write"",
            ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
            ""metadataVersion"": """",
            ""dataVersion"": ""v1"",
            ""data"": {
                ""resourcesContainer"": ""Inline"",
                ""resourceLocation"": ""centraluseuap"",
                ""publisherInfo"": ""microsoft.maintenance"",
                ""resources"": [
                    {
                        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
                        ""resourceId"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                        ""armResource"": {
                            ""id"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                            ""name"": ""cec1277b-758b-475f-af1f-a2da44fad695"",
                            ""type"": ""microsoft.maintenance/scheduledevents"",
                            ""properties"": {
                            }
                        },
                        ""additionalResourceProperties"": {
                            ""system"": {
                                ""aliases"": {
                                    ""resourceId"": {
                                        ""state"": ""original""
                                    }
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
                }
            }
        }";

        public const string notificationWithSameSubjectAndResourceIdSuccessMapping = @"{
            ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
            ""topic"": ""System"",
            ""subject"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
            ""eventType"": ""microsoft.maintenance/scheduledevents/write"",
            ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
            ""metadataVersion"": """",
            ""dataVersion"": ""v1"",
            ""data"": {
                ""resourcesContainer"": ""Inline"",
                ""resourceLocation"": ""centraluseuap"",
                ""publisherInfo"": ""microsoft.maintenance"",
                ""resources"": [
                    {
                        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
                        ""resourceId"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                        ""armResource"": {
                            ""id"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                            ""name"": ""cec1277b-758b-475f-af1f-a2da44fad695"",
                            ""type"": ""microsoft.maintenance/scheduledevents"",
                            ""properties"": {
                            }
                        },
                        ""additionalResourceProperties"": {
                            ""system"": {
                                ""aliases"": {
                                    ""resourceId"": {
                                        ""state"": ""original""
                                    }
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
                }
            }
        }";


        public const string notificationWithSuccessMappingAndResolutionStateNotInPayload = @"{
            ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
            ""topic"": ""System"",
            ""subject"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/4cabbc43-cf76-4eb7-b016-0e5a5317630f/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44"",
            ""eventType"": ""microsoft.maintenance/scheduledevents/write"",
            ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
            ""metadataVersion"": """",
            ""dataVersion"": ""v1"",
            ""data"": {
                ""resourcesContainer"": ""Inline"",
                ""resourceLocation"": ""centraluseuap"",
                ""publisherInfo"": ""microsoft.maintenance"",
                ""resources"": [
                    {
                        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
                        ""resourceId"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                        ""armResource"": {
                            ""id"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                            ""name"": ""cec1277b-758b-475f-af1f-a2da44fad695"",
                            ""type"": ""microsoft.maintenance/scheduledevents"",
                            ""properties"": {
                            }
                        },
                        ""additionalResourceProperties"": {
                            ""system"": {
                            }
                        },
                        ""apiVersion"": ""v1"",
                        ""resourceEventTime"": ""2023-03-21T18:19:00.5550107+00:00"",
                        ""statusCode"": ""OK""
                    }
                ],
                ""routingType"": ""Unknown"",
                ""additionalBatchProperties"": {
                }
            }
        }";

        public const string notificationWithAliasAlreadyResolved = @"{
            ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
            ""topic"": ""System"",
            ""subject"": ""/providers/someotherservice/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/4cabbc43-cf76-4eb7-b016-0e5a5317630f/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44"",
            ""eventType"": ""microsoft.maintenance/scheduledevents/write"",
            ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
            ""metadataVersion"": """",
            ""dataVersion"": ""v1"",
            ""data"": {
                ""resourcesContainer"": ""Inline"",
                ""resourceLocation"": ""centraluseuap"",
                ""publisherInfo"": ""microsoft.maintenance"",
                ""resources"": [
                    {
                        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
                        ""resourceId"": ""/subscriptions/ece49473-f326-458c-80f6-ca724b874651/resourceGroups/wilful_turquoise_magpie/providers/Microsoft.Compute/virtualMachines/only_purple_moose"",
                        ""armResource"": {
                            ""id"": ""/subscriptions/ece49473-f326-458c-80f6-ca724b874651/resourceGroups/wilful_turquoise_magpie/providers/Microsoft.Compute/virtualMachines/only_purple_moose"",
                            ""name"": ""cec1277b-758b-475f-af1f-a2da44fad695"",
                            ""type"": ""microsoft.maintenance/scheduledevents"",
                            ""properties"": {
                            }
                        },
                        ""additionalResourceProperties"": {
                            ""system"": {
                                ""aliases"": {
                                    ""resourceId"": {
                                        ""state"": ""resolved"",
                                        ""id"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf""
                                    }
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
                }
            }
        }";

        public const string notificationWithSubjectAsAliasButNoSuffixSuccessMapping = @"{
            ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
            ""topic"": ""System"",
            ""subject"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/4cabbc43-cf76-4eb7-b016-0e5a5317630f"",
            ""eventType"": ""microsoft.maintenance/scheduledevents/write"",
            ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
            ""metadataVersion"": """",
            ""dataVersion"": ""v1"",
            ""data"": {
                ""resourcesContainer"": ""Inline"",
                ""resourceLocation"": ""centraluseuap"",
                ""publisherInfo"": ""microsoft.maintenance"",
                ""resources"": [
                    {
                        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
                        ""resourceId"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                        ""armResource"": {
                            ""id"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                            ""name"": ""cec1277b-758b-475f-af1f-a2da44fad695"",
                            ""type"": ""microsoft.maintenance/scheduledevents"",
                            ""properties"": {
                            }
                        },
                        ""additionalResourceProperties"": {
                            ""system"": {
                                ""aliases"": {
                                    ""resourceId"": {
                                        ""state"": ""original""
                                    }
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
                }
            }
        }";

        public const string notificationWithFailureMappingForSubject = @"{
            ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
            ""topic"": ""System"",
            ""subject"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ea9ce855-f457-442a-9d44-0de59bc45ae3/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44"",
            ""eventType"": ""microsoft.maintenance/scheduledevents/write"",
            ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
            ""metadataVersion"": """",
            ""dataVersion"": ""v1"",
            ""data"": {
                ""resourcesContainer"": ""Inline"",
                ""resourceLocation"": ""centraluseuap"",
                ""publisherInfo"": ""microsoft.maintenance"",
                ""resources"": [
                    {
                        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
                        ""resourceId"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                        ""armResource"": {
                            ""id"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                            ""name"": ""cec1277b-758b-475f-af1f-a2da44fad695"",
                            ""type"": ""microsoft.maintenance/scheduledevents"",
                            ""properties"": {
                            }
                        },
                        ""additionalResourceProperties"": {
                            ""system"": {
                                ""aliases"": {
                                    ""resourceId"": {
                                        ""state"": ""original""
                                    }
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
                }
            }
        }";

        public const string notificationWithFailureMappingForResourceId = @"{
            ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
            ""topic"": ""System"",
            ""subject"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/4cabbc43-cf76-4eb7-b016-0e5a5317630f/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44"",
            ""eventType"": ""microsoft.maintenance/scheduledevents/write"",
            ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
            ""metadataVersion"": """",
            ""dataVersion"": ""v1"",
            ""data"": {
                ""resourcesContainer"": ""Inline"",
                ""resourceLocation"": ""centraluseuap"",
                ""publisherInfo"": ""microsoft.maintenance"",
                ""resources"": [
                    {
                        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
                        ""resourceId"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/959c5346-46a9-45b9-b300-97e85175c615/providers/microsoft.maintenance/scheduledevents/cec1277b-758b-475f-af1f-a2da44fad695"",
                        ""armResource"": {
                            ""id"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/959c5346-46a9-45b9-b300-97e85175c615/providers/microsoft.maintenance/scheduledevents/cec1277b-758b-475f-af1f-a2da44fad695"",
                            ""name"": ""cec1277b-758b-475f-af1f-a2da44fad695"",
                            ""type"": ""microsoft.maintenance/scheduledevents"",
                            ""properties"": {
                            }
                        },
                        ""additionalResourceProperties"": {
                            ""system"": {
                                ""aliases"": {
                                    ""resourceId"": {
                                        ""state"": ""original""
                                    }
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
                }
            }
        }";

        public const string notificationWithAliasNotCorrectlyFormatted = @"{
            ""id"": ""/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc"",
            ""topic"": ""System"",
            ""subject"": ""/providers/microsoft.idmapping/aliases/default/namespaces/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/4cabbc43-cf76-4eb7-b016-0e5a5317630f/providers/microsoft.maintenance/scheduledevents/e333153b-9eac-4be2-b074-eefbf0a03d44"",
            ""eventType"": ""microsoft.maintenance/scheduledevents/write"",
            ""eventTime"": ""2023-03-21T18:19:00.58629+00:00"",
            ""metadataVersion"": """",
            ""dataVersion"": ""v1"",
            ""data"": {
                ""resourcesContainer"": ""Inline"",
                ""resourceLocation"": ""centraluseuap"",
                ""publisherInfo"": ""microsoft.maintenance"",
                ""resources"": [
                    {
                        ""correlationId"": ""b8cbc4e6-3e72-4b35-a72e-6546eba5f31f"",
                        ""resourceId"": ""/providers/microsoft.idmapping/aliases/default/namespaces/abc/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                        ""armResource"": {
                            ""id"": ""/providers/microsoft.idmapping/aliases/default/namespaces/abc/microsoft.compute/types/virtualmachines/identifiers/vmssidorvmid/values/ee58dda9-47fb-48bb-b895-4eb615bbd29d/providers/microsoft.maintenance/scheduledevents/66a2b578-bc99-43b0-9648-cab890f2ceaf"",
                            ""name"": ""cec1277b-758b-475f-af1f-a2da44fad695"",
                            ""type"": ""microsoft.maintenance/scheduledevents"",
                            ""properties"": {
                            }
                        },
                        ""additionalResourceProperties"": {
                            ""system"": {
                                ""aliases"": {
                                    ""resourceId"": {
                                        ""state"": ""original""
                                    }
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
                }
            }
        }";
    }
}
