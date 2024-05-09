# Azure Resource Configuration Changes Documentation

The below section provides the Azure Resource Configuration Changes public documentation links for different topics.

- [Portal UX](https://ms.portal.azure.com/#view/Microsoft_Azure_OneInventory/ResourceChangesOverview.ReactView)
- [Public Documentation](https://docs.microsoft.com/en-us/azure/governance/resource-graph/how-to/get-resource-changes)
- [ARG REST API Reference](https://docs.microsoft.com/en-us/azure/governance/resource-graph/first-query-rest-api)
- Internal documentation
    - [Docs](https://microsoft.sharepoint.com/:f:/t/GovernanceVteam/EnF_yvj3ONZDgdd2kQQADu4B5MbcxM5RLQun_wAqxhs1rQ?e=wuIV3o)
    - [Azure Resource Builder services (visio)](https://microsoft.sharepoint.com/:u:/t/GovernanceVteam/EbpWdbDGtIJFry-xCkke8ZsB0TRzIduWpeZ8a9bog2fdwg?e=tt2QWe)
    - [Change compuate (visio)](https://microsoft.sharepoint.com/:u:/t/GovernanceVteam/Ea9t6Qrm8hRIh8X4plYzsnoB61YX9zfU9O9L3LUnAm_1uw?e=z8tHrS)
- Dashboards
    - [Change Compute](https://portal.microsoftgeneva.com/dashboard/AzureResourcesTopology/Change%2520Compute?overrides=[{%22query%22:%22//dataSources%22,%22key%22:%22account%22,%22replacement%22:%22AzureResourcesTopology%22},{%22query%22:%22//*[id%3D%27Environment%27]%22,%22key%22:%22value%22,%22replacement%22:%22%22},{%22query%22:%22//*[id%3D%27ScaleUnit%27]%22,%22key%22:%22value%22,%22replacement%22:%22%22}]%20)
    - [Deployment Validation](https://portal.microsoftgeneva.com/dashboard/AzureResourcesTopology/Write%2520Path/Deployment%2520validation/Change)
    - [KPIs](https://portal.microsoftgeneva.com/dashboard/AzureResourcesTopology/Change%2520Compute/KPI)
- Support
    - [Azure Resource Builder On-Call](https://portal.microsofticm.com/imp/v3/oncall/current?serviceId=26994&teamIds=116815&scheduleType=current&shiftType=current&viewType=1&gridViewStartDate=2024-03-10T08:00:00.000Z&gridViewEndDate=2024-03-17T06:59:59.999Z&gridViewSelectedDateRangeType=9)
    - [Change Tracking Teams channel](https://teams.microsoft.com/l/channel/19%3affd712833fea498884c36bf67a74aa0e%40thread.skype/Change%2520Tracking?groupId=f068473a-3eec-44af-90e3-11c124b4d791&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47)


Sample queries to get Resource Changes for the supported datasets using [Resource Graph Explorer](https://portal.azure.com/#blade/HubsExtension/ArgQueryBlade) in Azure Portal.

## Tracked resources changes
```kusto
ResourceChanges
| extend resId = tostring(properties.targetResourceProperties.targetResourceId) 
| extend resType=tostring(properties.targetResourceProperties.targetResourceType)
| extend changeTime = todatetime(properties.changeAttributes.changedAt)
| extend changeType = tostring(properties.changeType)
| project name, resourceGroup, resId, resType, changeType, changeTime, properties
| limit 100
| order by changeTime desc
```

## Container resources changes (resource groups, subscriptions, management groups etc...)
```kusto
ResourceContainerChanges
| limit 100
| order by changeTime desc
```

## [Proxy Type] Health resources changes
```kusto
HealthResourceChanges
| limit 100
| order by changeTime desc
```