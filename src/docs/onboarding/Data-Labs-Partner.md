# Data Labs Partner Onboarding Guide

## Submit Onboarding Request

Partner send onboarding request email to Data Labs’s onboarding approval DL CPSP@microsoft.com.

Process for onboarding non-RP partner is the same as RPs. Partner should work with the Data Labs team to decide on a unique namespace that they want to use for their team.

__Subject__: Data Labs Onboarding {date}: Onboarding {NameSpace} in {INT/DF/PROD/FF/MC/etc…}

__Content__ (do not include the quoted notes):

1. List of types that their partner service is expected to receive:

    | Input Type | Action |
    |---|---|
    | e.g., microsoft.compute/virtualmachines | e.g., Delete, Write |
    | e.g., microsoft.recoveryservices/vaults/backupfabrics | e.g., All |

    > 1. The types and actions are case insensitive.
    > 2. User needs to specify which actions they want to receive for each type. Use “all” if all actions are needed.
    > 3. For tracked resources, MOVE action is separated with two actions, DELETE + WRITE. The source resource is deleted, and the destination resource is considered a write. // TODO: update after supporting abc center milestone#2.
    > 4. For all deleted resources, since the ARM Get call returns NOT FOUND for these resources, the API-version is Undefined.
    > 5. Partner confirms that these types are already onboarded to [ARN](https://portal.microsoftgeneva.com/dashboard/AzureResourceNotifications/ARN%2520Partners/Data%2520Flowing%2520through%2520ARN?overrides=%5b%7b%22query%22:%22//*%5bid=%27NotificationPublisherInfo%27%5d%22,%22key%22:%22value%22,%22replacement%22:%22%22%7d,%7b%22query%22:%22//*%5bid=%27NotificationResourceProviderNamespace%27%5d%22,%22key%22:%22value%22,%22replacement%22:%22%22%7d,%7b%22query%22:%22//*%5bid=%27ResourceType%27%5d%22,%22key%22:%22value%22,%22replacement%22:%22%22%7d,%7b%22query%22:%22//*%5bid=%27Action%27%5d%22,%22key%22:%22value%22,%22replacement%22:%22%22%7d%5d%20) or [ARG](https://portal.microsoftgeneva.com/s/E68E18E0). If there are types not onboarded, follow [publisher onboarding](https://eng.ms/docs/cloud-ai-platform/azure-core/azure-management-and-platforms/control-plane-bburns/azure-resource-notifications/azure-resource-notifications-documentation/partners/publisher/onboarding) to onboard them.

2. List of output types from partner service and provide traffic estimation:

    | Output Namespace | Output Type/Action | Traffic Estimation |
    | --- | -- | -- |
    | e.g., Microsoft.ABCDE | e.g., UnifiedProtectedItem/Write | 1000/min |
    | e.g., Microsoft.ABCDE | e.g., UnifiedProtectedItem/Delete | 500/min |
    | e.g., Microsoft.XYZ | e.g., TypeXYZ/Write | 100 / min |

    > 1. Regarding the action:
    >    1. for the normal update or create, suggest using an action that ends with write. e.g., Microsoft.DataLabs/UnifiedProtectedItem/Write
    >    2. for a move, suggest using an action that ends with move or move/action. e.g., Microsoft.DataLabs/UnifiedProtectedItem/Move
    >    3. for a delete, use delete action. e.g., Microsoft.DataLabs/UnifiedProtectedItem/Delete
    >    4. for a snapshot, use snapshot action. e.g., Microsoft.DataLabs/UnifiedProtectedItem/Snapshot
    >    5. If your action cannot be covered by above four scenarios, pls call it out in the onboarding request. ARG needs to add support for your action.

3. Determine target query store.
   1. Currently ARG supports Pacific (Redis based), Sailfish and Galaxy (CosmosDb) stores.
   2. By default, the table name (used in [ARG query](https://ms.portal.azure.com/#view/HubsExtension/ArgQueryBlade)) is {Namespace}Resources. For example: ABCDEResources.

4. If the partner requests other APIs than the ARG query, please provide the [OpenAPI (swagger) specification](https://armwiki.azurewebsites.net/rpaas/swaggeronboarding.html) that is signed off by ARM and SDK teams. Also provide the SLA requirements for each API.

    |API Paths |Method |Latency |Availability|
    |--|--|--|--|
    |e.g., /providers/Microsoft.ABCDE/resources |Get |1s |99%|
    |e.g., /providers/Microsoft. ABCDE/history |Get | | |

5. List of types from input or output that need to be cached.

    | Cached Types |
    | --- |
    | e.g., microsoft.recoveryservices/vaults/backupfabrics |

    > 1. The cached types should be the input types or the output types.

6. List of extra dependency types which does not belong to the input types or output types.

    | Extra Dependency Types | API version |
    | --- | -- |
    | e.g., Microsoft.Compute/virtualMachines | e.g., 2020-01-01 |

7. Provides the [PCCode and service Id](https://microsoftservicetree.com/services/00df9fbf-c722-42e4-9acd-bc7125483b22/overview). ARG will create a subscription and set billing to the partner team. If the partner already has a scale unit set up in ARG, Data Labss will use the existing subscription.

    | PCCode | Service Id |
    | --- | --- |
    | e.g., P12345678 | e.g., 00000000-0000-0000-0000-000000000000 |

## Create Onboarding ICM Ticket

Once the onboarding request is approved, create an [onboarding ticket](https://portal.microsofticm.com/imp/v3/incidents/create?tmpl=b3I3uP) so Data Labs team can start the onboarding process.

## Publish NuGet Package

Data labs provides a Nuget SDK [Microsoft.WindowsAzure.Governance.DataLabs.Partner](https://msazure.visualstudio.com/One/_artifacts/feed/OneBranch-Consumption-Extended/NuGet/Microsoft.WindowsAzure.Governance.DataLabs.Partner/overview/2023.10.21.2). The SDK includes APIs that partner service can use to interact with data labs // TODO (include API TSG).

When adding the data labs nuget package, make sure you added [OneBranch-Consumption-Extended](https://msazure.pkgs.visualstudio.com/One/_packaging/OneBranch-Consumption-Extended/nuget/v3/index.json) as package source.

After adding processing logic, partner team shall create a NuGet package for the project and publish it. Partner team provides the NuGet package link to Data Labs team. Data Labs team will include the Nuget package in our repo and deploy the code to the cloud.


## Work with Azure Resources

Partner service receives notifications in [ARN Schema v3](https://eng.ms/docs/cloud-ai-platform/azure-core/azure-management-and-platforms/control-plane-bburns/azure-resource-notifications/azure-resource-notifications-documentation/partners/arn-schema/arn-schema-v3), which is an EventGridEvent. Resources are always inline in the notifications. Each notification contains one resource.
To work with ARN Schema, include DataLab Common Nuget for Partner and implement Partner interface: [Microsoft.WindowsAzure.Governance.Notifications.ARNContracts](https://msazure.visualstudio.com/One/_artifacts/feed/OneBranch-Consumption-Extended/NuGet/Microsoft.WindowsAzure.Governance.Notifications.ARNContracts/overview/0.2.0).

After curating the input, partner service should model the output in ARN Schema v3 as well. Some important fields are:

- EventGridEvent.subject:
  - If the notification contains only one resource, use “/subscriptions/{subId}/resourceGroups/{rgName}/resourproviders/{providerNamespace}/{resourceType}/{resourceName}”.
  - Otherwise, if all the resources in the notification belong to one subscription, use “/subscriptions/<subscription_guid>”
  - Otherwise, if all the resources in the notification belong to one tenant, use “/tenants/<tenand_guid>”
  - Otherwise, use “/”
- EventGridEvent.eventType:
  - The resources in one notification should be of the same type. "{providerNamespace}/{resourceType}/{action: write | delete | move\action | start\action … | snapshot}"
