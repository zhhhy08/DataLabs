# Scaling Out ServiceBusQueue

## Context
The naming scheme of the service bus namespace is "{PartnerAcronym}{Cloud}{RegionAcronym}sb{number}", like "abcprodeussb1".

Scaling out Service Bus may be required when our queues in the past are:
1. Throttling: Try first increasing the Messaging units (MUs) of the Service Bus Namespace through portal. If throttling still exists, please follow servicebus scale out instructions.
2. Size of the queues are getting too large: please follow service bus scale out instructions

## Service Bus Scale Out Instructions
Prerequisites:
- Involve Data Labs team member.
- Obtain JiT access to the subscription of the service bus queue.

Instructions:
1. Create a new service bus namespace through portal under the resource group of the service bus.
    - Namespace Name: utilize a different prefix to know what was added and avoid adding to the same underlying service bus infra (we do not have dedicated service bus queues)
    - Location: Same as the AKS cluster
    - Pricing Tier: Premium
    - All other details keep the same
2. Create 2 queues under this service bus namespace through portal
    - Names:
        - subjob
        - random name is ok for the second one (keep track of the name though)
    - Settings:
        - Max queue size: 80GB
        - Select: Enable dead lettering on message expiration
        - All other details keep the same
3. Create a config change for ServiceBusNameSpaceAndName to include your new service bus queues (follow naming scheme). Please follow the example [HotConfig file](https://dev.azure.com/msazure/One/_git/Mgmt-Governance-DataLabs?path=/src/AKSDeployment/Charts/PartnerAKS/_HotConfig/AddServiceBusQueue.yaml) and follow [Config Change TSG](./ConfigChange.md)
4. Restart the IOService by following the [Restarting Pods TSG](./RestartingPods.md).
5. Reach out to the Data Labs infra champion for this update.


## Follow up after Mitigation
Once the issue has been resolved, take the following action:
1. Follow [Config Change TSG](./ConfigChange.md) to revert to the old service bus queues.
2. If needed, reach out to Data Labs infra champion for scaling out service buses (modify infra for more service bus queues).
