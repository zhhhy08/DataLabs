# ARG data processing context
ARG Data Processing requires reconciliation (snapshots) for initial load, eventual consistency and also to handle compliance requirements for data retention. This is apart from the change notifications which is required for keeping the data consistent.  

## For Tracked resources
Data Platform in ARG automatically reconcile the data every day using ARM contracts for LIST/GET calls.  
And depends upon ARM to send the notifications  

## For Proxy resources
Notifications
1. All changes are going through ARM (ARM notification suffice) NO WORK by partner
2. Otherwise partner needs to send notifications on each change with payload

Reconciliation/Snapshots  
1. Partner can send periodic snapshots to ARG
2. Partner provide alternate way as described below.

# Required information from partner:
ARG has a way to do periodic reconciliation on ARG side for proxy partners.  
For this partner needs to provide ARM aligned LIST API (which returns list of [GenericResource](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.management.resourcemanager.models.genericresource?view=azure-dotnet))  
## List REST API
Example: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ServiceFabric/clusters/{clusterName}/applications?api-version=2019-11-01-preview

1. ARG will call this List REST API to do periodic sync in staggered way. (ARG syncs every subscription once a day and every tenant twice a day.)  

2. RP needs to handle load from ARG to List REST API for every scope on which List call needs to be made.  
List REST API can be on different scopes (e.g. subscription/resource/resource group/management group/tenant levels). Provided List REST API needs to allow sync all resources of the proxy type.  
(e.g. for provided List REST API example, we will do list call on every resource of type "Microsoft.ServiceFabric/clusters").  
### Available call reduction optimizations:
1. [Subscription level List REST API] Call only subscriptions where resource provider (e.g. Microsoft.Compute) is registered.


## Api version
Recommended way: partner should have proxy type in both ARM manifest and @[Swagger Spec](https://github.com/Azure/azure-rest-api-specs/tree/main/specification).  
This way ARG will auto-upgrade api-version for reconciliation if there were no breaking changes.  
  
Otherwise, please share exact api-version to be used.

## Contract details
### Properties are case-sensitive  
**What it means:** List resources Rest Api, Get resource Rest Api (if present), notification payload (if sent by partner) need to return same casing for properties/system data and other fields.

**Why:** If keys “Value” and “value” are both there in the properties bag, and when a user queries properties.value, and if we handle keys case-insensitively, there is no way for us to tell what’s the intention of the user - whether he is looking for the lower-cased key, or any one of the two is fine. 
If the casing matters, we expect RPs to unify the casing before ingesting to ARG.  
