# Data Labs SLO Message Duration High

### Causes
Data processing on the service side is severely impacted that it takes more than 10sec to process, not including the partner side. 

### Mitigation
1. Restart IOService, PartnerService, and ResourceProxy with the "Restarting Pods" TSG.
2. Involve Data Labs team regarding the high SLO.

### Assessing Impact
1. Assess E2E impact for SLO with [ARGDataLabs > IOService > DataFlow]. Please check if it is impacting all nodes and messages, or just a few.
2. Please work with Data Labs team with handling this scenario as this is not expected to be hit.
