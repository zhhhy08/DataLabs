# High Input EventHub Waiting Messages

Data Labs IOService utilizes a backpressure model where messages will not be read from the input eventhub until the other messages have continued processing. This means an increasing input eventhub may be related to failures within Data Labs processing

### Diagnosis

1. Check on the [ARGDataLabs > IOService > InputEventHub dashboards] to confirm the increase in traffic at the InputEventHub for a specific partner and region.
2. Look through [ARGDataLabs dashboard] to track any spikes in Activity Duration or Failed Activity Counts for main service methods. If this is the case, please dig into the exception messages in dgrep logs and work with Data Labs team to mitigate.
3. Review any failures in EventHubTaskManager with the [EventHubTaskManager Failures] dgrep logs in the monitor and EventHub Portal Page for any failures on the side of the EventHub. If there are significant issues, we will need to reach out to the EventHub team.

### Mitigation

1. If there was a recent deployment, follow the [Rollback Service TSG](../operations/RollbackServices.md).
2. If there was a recent config change, follow the [Config Change TSG](../operations/ConfigChange.md).
3. Restart Pods by following the [Restarting Pods TSG](../operations/RestartingPods.md).
4. Involve Data Labs members to assess activity failures and do an emergency rollout for bits.
5. File an IcM with the EventHub team for mitigation.

### Assessing Impact

Messages in premium eventhubs are retained for [90 days](https://learn.microsoft.com/en-us/azure/event-hubs/event-hubs-quotas), so there will be no data loss, regardless of restarting pods or fixing bits with a deployment. 

For documenting the delay for customers:
1. Delay: Indicate when InputEventHubs increased, the max size of the InputEventHub waiting messages, when messages started to decrease, and when the waiting messages dropped to 0.
2. Which Customers impacted: N/A, track inputResourceId's in logs during the time EventHubWaitingMessages are being burned down. View the logs [Assess Impact: Review Subscriptions Processed after EventHubManager Processes Again], for the last 15min to see what messages are being processed.