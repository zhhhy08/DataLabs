# Scripts for AKS Deployment

**NOTE**: These powershell scripts will only work if you run them in the `_Scripts` folder.

Below are examples of how `describe_pods.ps1` and `reinstall_services.ps1` are used. Please refer to `ServiceInfo.json` to also see the relations between services, partners, regions, and folder structure. All the `helm` and `kubectl` commands will be found in `PartnerAKS\_KubectlScripts` and `ResourceFetcherAKS\_KubectlScripts`.

Example commands
```
.\describe_pods.ps1 -Cloud Local -Region eus -Partner RF -ServiceName MonitorService

PS C:\Mgmt-Governance-DataLabs\src\AKSDeployment\Charts\_Scripts> .\describe_pods.ps1 -CommandType azurecli -Cloud Local -Region eus -Partner abc -ServiceName MonitorService -LogLength -1
Resource Group: abc-eastus
Name: abc-test-eastus
AppName: geneva-services
Namespace: monitor-namespace
Set-Location ..\PartnerAKS
az aks command invoke --resource-group abc-eastus --name abc-test-eastus --command "chmod +x _KubectlScripts/*.sh; _KubectlScripts/describe_pods.sh Local MonitorService monitor-namespace geneva-services" --file .
```
```
.\configchange.ps1 -CommandType azurecli -Cloud Int -Region eus -Partner idm -ServiceName IOService -ConfigFileName IncreaseBatching.yaml                                                                              
```
```
.\reinstall_service.ps1 -CommandType azurecli -Cloud Int -Region eus -ServiceName ResourceProxy -Partner idm -ServiceName IOService

PS C:\Mgmt-Governance-DataLabs\src\AKSDeployment\Charts\_Scripts> .\reinstall_service.ps1 -CommandType azurecli -Cloud Int -Region eus -Partner idm -ServiceName IOService
Resource Group: DataLabsidmRG-eastus
Name: idminteusaks
Values File: idMappingValues_Int_eus.yaml
AppName: solution-io
Namespace: solution-namespace
Set-Location ..
az aks command invoke --resource-group DataLabsidmRG-eastus --name idminteusaks --command "chmod +x _KubectlScripts/*.sh; _Kub
```