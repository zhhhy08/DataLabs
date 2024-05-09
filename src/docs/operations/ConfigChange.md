# Config Changes

**Disclaimer**: Currently, there is no support for livesite support utlizing Geneva Actions for Data Labs. Instead, we utilize powershell commands to override the configuration file for the AKS cluster (named configmap in kubernetes).

### Running Config Change

#### Prerequisites
- Azure Powershell. You can download on the software center
- Azure AKS Powershell. Run the following command to install

```
Install-Module -Name Az.Aks -AllowClobber -Force -SkipPublisherCheck
```

#### Prequisites for Canary and PROD regions
- Obtain owner access to the subscription through JiT on SAW.
- Go to the AKS instance > Access control (IAM) and assign your AME account the "Azure Kubernetes Service RBAC Cluster Admin" Role. Please remember to remove this role assignment once you finish working on the AKS instance.

#### Steps
1. Identify the config file that you want to modify.
    - **Identify the value that you want to override**: The values that are in the config map are found in the `configmap.yaml` file in a respective application template, and we will be overriding that value. For instance, if the traffic tuner needs to be updated, we should change the IO Service. Please keep that file open for the next step where you will prepare the values to change.
    - **Identify the config name**: The configmap name should be in the `PartnerAKS/BaseValueFiles/dataLabsServices.yaml` and `ResourceFetcherAKS/BaseValuesFiles/rfServices.yaml`.  The name is found in `ioConfigMap.name`, which is `solution-io-config` (as of 9/6/2023).
2. **If this change is permanent, please raise a PR** and let respective area champions know about the change. Changes to the configmap will be overriden if there is a new deployment.
    - If this is for livesite, please ignore and note to Data Labs team.
    - In order to prevent reverting config changes, please check in the code changes before the next deployment.
3. Create a new file (any name is ok) and modify the values that need to be changed in the `configmap.yaml`. "data" will always be at the top and any values replaced will be indented afterwards in order to follow the format for configmap. Example is seen below:
    - **Note**: The values of the configmap are from `configmap.yaml`, not from the values files.
```
data:
    # Input values in ConfigMap that you want to override. Examples are seen here.
    RawInputChannelConcurrency: "100"
    InputChannelConcurrency: "300"
    PartnerSolutionGrpcUseMultiConnections: "false"
```
4. Assign the respective values to the AKS instance that you want to perform an config change on (please modify example as needed).
```
set SUBSCRIPTION "75e7e676-7873-4432-98bd-01a68cc5bca1"
set RESOURCE_GROUP "DataLabsABCRG-eastus2euap"
set AKS_NAME "abccanaryecyaks"
set CONFIG_MAP "solution-io-config"
set HOT_CONFIG_FILE_NAME "bcdrValues_Canary_HotConfig.yaml" # name of file created in step 3
set CONFIG_MAP_NAMESPACE "solution-namespace" # namespace of config map updated
```

5. Sign in under the respective account (MSFT for non-prod and AME for prod/canary) with the following powershell commands.
```
# Sign into Azure Account
Connect-AzAccount
Select-AzSubscription -SubscriptionName "${SUBSCRIPTION}"

# Obtain the AKS Context
Import-AzAksCredential -ResourceGroupName "${RESOURCE_GROUP}" -Name "${AKS_NAME}" -Force
```

6. Patch the config map with the following powershell command.
```
Invoke-AzAksRunCommand -ResourceGroupName ${RESOURCE_GROUP} -Name ${AKS_NAME} -Force -Command "kubectl patch configmap ${CONFIG_MAP} -n ${CONFIG_MAP_NAMESPACE} --type merge --patch-file ${HOT_CONFIG_FILE_NAME}" -CommandContextAttachment "${HOT_CONFIG_FILE_NAME}"

# Expected output should look like this
# Id                : 24fdcf16b05645178fd71abbe9c9b946
# ProvisioningState : Succeeded
# ExitCode          : 0
# StartedAt         : 8/15/2023 7:08:46 PM
# FinishedAt        : 8/15/2023 7:08:47 PM
# Logs              : configmap/solution-io-config patched
# 
# Reason            :
```


7. Verify Results by describing the values of the configmap.
```
Invoke-AzAksRunCommand -ResourceGroupName ${RESOURCE_GROUP} -Name ${AKS_NAME} -Force -Command "kubectl describe configmap ${CONFIG_MAP} -n ${CONFIG_MAP_NAMESPACE}"
```