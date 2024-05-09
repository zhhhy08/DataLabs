# Output Eventgrid Write Message Count Failure

### Cause
Output Eventgrids are failing to write messages to ARN and are sent to the retry queue. Many of these failures will be resolved when retried as there are 2 eventgrids per region, (next is TBD) but in the case of an outage from the EventGrid team, we are planning to have a BCDR scenario to route to eventgrids in a different region.

### Diagnosis
1. Check on the [ARGDataLabs > IOService > OutputEventGrid dashboards] to confirm the increase in traffic at the InputEventHub for a specific partner and region.
2. Look for failures in logs for [PublishToArn] dgrep logs in "[IOService] High EventGridMessageCountFaiures" to see if there are any failures given by EventGrid.

### Mitigation

1. TBD: Switching to backup EventGrids (unsupported).
2. Try restarting IOService with the "Restarting Pods" TSG under operations.
3. Resolve possible EventGrid failures with the EventGrid team.

### Assessment of Impact
1. Refer to the logs from ArnNotificationClient.PublishToArn in DGrep from earlier for subscriptions that were impacted.
