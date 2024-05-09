# ETag Conflicts

### Cause

ETag Conflicts occur in stateful services (like ABCC team) when there is a conflict on uploading two different revisions of a blob concurrently to the storage account. IOService will raise a failure in uploading source of truth and will send the second message to the retry queue.

This issue would be caused the logic of the partner code and will need to be passed to the partner. ETag conflicts will be a consistent issue for stateful services due to concurrency, but if there are too many, it can overload the retry and poison queue for messages. Please take the following steps.

### Analysis and Mitigation Steps

1. View the [ARGDataLabs > IOService > SourceOfTruth dashboard] under the "Blob SourceOfTruth Upload ETag Conflict Counter" and note the number of ETag conflicts a resource would have.
2. View the [ARGDataLabs > IOService > RetryQueue] and [ARGDataLabs > IOService > PoisonQueue] to see if there is a significant increase in poison queue or retry queue. 
    
    1. If there are no issues, please go ahead to step 3.
    2. If there was a sudden increase due to a recent deployment, please revert bits with [Rollback Service TSG](../operations/RollbackServices.md).
    3. If an increase was not due to deployment, please check for any related failures to blobs with [OutputBlobClient.UploadContentAsync Activities] dgrep log in the "[Blob] ETag Conflict Count" monitor and viewing the corresponding storage account Azure Monitor metrics for failures.
3. For external partners, find the nuget version of the partner by looking at the .csproj files in [PartnerSolutionServices > {partnerName}PartnerSolutionService](https://msazure.visualstudio.com/One/_git/Mgmt-Governance-DataLabs?path=/src/DataLabs/PartnerSolutionServices). Internal partners just build from main branch.
4. Transfer IcM to the partner team with information from step 1 and step 3 (if external partner), so that the partner knows where there are heavy concurrent operations for their service.

### Assessing Impact
1. Review logs that are failing [OutputBlobClient.UploadContentAsync Activities] in monitor (same as step 2.3) and aggregate on exceptions to see common failures.
