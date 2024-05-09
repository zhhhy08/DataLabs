# High Retry and/or Poison Queue

### Causes

High Poison Queue is caused by the accumulation of failed messages to be processed. They are split into 3 reasons:
1. Partner: Dropping the message in their business logic.
2. Source of Truth: Etag conflicts, particularly in stateful services.
3. Internal: Low latency, high CPU/Memory, Service level bugs.

Retry Queue are put in similar issues

### Mitigation
For Partner Messages:
1. Transfer the IcM to the partner with the exceptions passed. Here is [Partner Information TSG](https://eng.ms/docs/cloud-ai-platform/azure-core/azure-management-and-platforms/control-plane-bburns/fleet-inventory/arb-product-docs/overview/arb-domains)

For Source of Truth Messages:
1. These messages often are due to ETag conflicts. Please review this [ETags Conflict TSG](./ETagConflicts.md)
2. Transfer the IcM to the partner with the exceptions passed. [Partner Information TSG](https://eng.ms/docs/cloud-ai-platform/azure-core/azure-management-and-platforms/control-plane-bburns/fleet-inventory/arb-product-docs/overview/arb-domains)

For service errors, there needs to be further investigation. Please take the following actions to investigate the error and then work with the Data Labs team to help mitigate:
- Regression in bits. Please rollback in involve Data Labs team.
- Check for any failures in the Span table of [ARGDataLabs] DGrep table.
- Check the servicebus for any failures in the Azure resource. The naming scheme of the service bus is "{PartnerAcronym}{Cloud}{RegionAcronym}sb{number}", like "abcprodeussb1". We need to also involve ServiceBus team to help find the root cause (we do not have dedicated infra for our SBs due to expenses, so noisy neighbors may cause throttling).
    - Follow [Scaling Out Service Bus Queue TSG](../operations/ScalingOutServiceBusQueue.md)
    - Here is a sample incident: [Incident 485574253](https://icmcdn.akamaized.net/imp/v3/incidents/details/485574253/home) : [ServiceBus] [HighCPU] 100% Memory in "abcprodwu3sb1" namespace

### Follow up
Not Required now as deadletter queue is so large:
- Clear the deadletter queue messages.
- There is a future TBD for retrying deadletter queue in the future (currently it is being dropped).

# Assessing Impact
Currently, messages cannot be retried from the deadletter queue and can be considered dropped.
1. Review the [Retry Reasons] dgrep logs under monitor for examples of failures.
2. Check for ActivityFailures within other components in [ARGDataLabs]
