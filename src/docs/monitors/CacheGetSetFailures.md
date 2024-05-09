# Cache Get and Set Failures

### Causes
Cache Set and Get Failures are relatively unknown and cannot be restarted like other services because it is stateful. Please reach out to Data Labs team for this failure.

### Mitigation
N/A

TBD work is cache replication in different zones. Future action will be denylisting a cachepool or cachenode that may be down.

### Assessing Impact
Cache Failures does not have an immediate impact to customers, but since there is a cache failure, partners have to rely on making outgoing calls to ResourceFetcher and ResourceFetcherProxy. This can impact SLO and throttling for customer subscriptions.

1. Review the [ARGDataLabs > DataFlow E2E Dashboards] to see if E2E SLO is impacted.
2. Look at [ARGDataLabs > ResourceFetcher Dashboard] to assess the increase of calls.

Pass this information to the Data Labs team to take a closer look regarding impact.
