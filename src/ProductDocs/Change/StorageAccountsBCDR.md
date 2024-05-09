### **Note**:
- For any BCDR in general, please involve the Incident manager, respective team oncall (ARB in this case) and if needed, component team as well (change compute here)
- Failing over storage accounts is not a reversible operation. It will take multiple days to revert if run so please ensure that this is a non-transient issue before proceeding
    - Per [docs](https://learn.microsoft.com/en-us/azure/storage/common/storage-disaster-recovery-guidance#anticipate-data-loss-and-inconsistencies), *When a failover occurs, all data in the primary region is lost as the secondary region becomes the new primary. All data already copied to the secondary is maintained when the failover happens. However, any data written to the primary that hasn't also been copied to the secondary region is lost permanently.  
    The new primary region is configured to be locally redundant (LRS) after the failover.*

## Steps
1. Obtain Portal JIT access to the region that the incident was thrown from
2. Download the PowerShell script to failover Change Compute storages either from [here](https://msazure.visualstudio.com/One/_git/Mgmt-Governance-ResourcesCache?path=/src/Deployment/ScriptUtils/ChangeComputeStorageFailover.ps1) or from from [DevOps Artifacts](https://msazure.visualstudio.com/One/_build?definitionId=180893) path: `drop_build_release_x64\release-x64\Deployment\ScriptUtils\ChangeComputeStorageFailover.ps1`
3. Stop all service processing in the region by doing the following:
      1. Note down the active stamp of the affected region (A or B) ([Azure Resource Graph > Common > ArgAdminConfig_GetConfig -> Deployment/DeploymentState](https://portal.microsoftgeneva.com/17D5F92B?genevatraceguid=515fe9c0-2ec4-4e59-a3d7-a514723ddc5f)). 
      2. Use ACIS actions to stop processing of the service in the region by setting the active stamp to `C` ([Prepopulated Link](https://portal.microsoftgeneva.com/F54E1A32?genevatraceguid=207bbd6f-9a82-42e0-b560-4c90db22bfcc)), but please make sure to update the endpoint and value accordingly)  
      **Azure Resource Graph > Common > ArgAdminEv2_GetExecuteExtensionAdmin**  
      **Endpoint**: `{region shorthand}`_tm_ChangeCompute\
      **stampName**: gov-art-`{region shorthand}`-rp-c\
      **stampState**: On\
      **extensionName**: SetDeploymentState  
      (**please use a GetConfig to double check the formatting of the stampName**)
      <br/><br/>
      In case the above does not work, we need another way to stop service processing
          1. Navigate to Azure portal
          2. Navigate to the `nt6art` VMSS (ensure that the resource group indicates the correct region/active stamp)
          3. Navigate to `instances` on the left hand side
          4. Put all instances in a `stopped` state
4. Validating that the service has stopped processing traffic through these dashboards ([1](https://portal.microsoftgeneva.com/dashboard/share/128D4341) and [2](https://portal.microsoftgeneva.com/dashboard/share/71FECAD5))
5. Execute the PowerShell script in a Windows PowerShell window as follows `.\ChangeComputeStorageFailover.ps1 {region_shorthand} {storage_type}` (for example, if the EastUS blobs were affected, you would execute `.\ChangeComputeStorageFailover.ps1 eus blobs` or if the North Europe queues were affected, you would execute `.\ChangeComputeStorageFailover.ps1 neu queues`)
6. The script should take anywhere from 15-45 minutes to run. Once the script has finished running, it should output which storage accounts (if any) **failed** to migrate/failover. If this occurs, please rerun the script and ensure that no storages failed migration. 
7. Once all storages have successfully failed over, re-enable processing in the service by restoring the original active stamp of the service (Follow step 3.2 but change the **value** to what you found in 3.1 OR start all the stopped instances via portal depending on how you stopped the service) and use the same dashboards in step 4. to ensure that traffic is now flowing normally through the service again. 
