### Local build steps
- Open DataLabs.sln in VS 2022.
- Select Release & x64 flavor of build.
- Rebuild or (Clean/ Build) the solution.

### Docker image build steps
- Open a command prompt in admin mode.
- Move to "<SourcePathFromMachine>\Mgmt-Governance-DataLabs\out\Release-x64" folder.

Build image - Admin Service
```sh
docker build -t datalabsintacr.azurecr.io/adminservice:latest -f <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/AdminService/Dockerfile <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/AdminService/
```

Build image - Resource fetcher service
```sh
docker build -t datalabsintacr.azurecr.io/resourcefetcherservice:latest -f <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/ResourceFetcherService/Dockerfile <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/ResourceFetcherService/
```

### Open PowerShell 7 or Windows PowerShell window and do below
- Move to "<SourcePathFromMachine>\Mgmt-Governance-DataLabs\src\AKSDeployment\Charts\ResourceFetcherAKS" folder.

### Login & select INT subscription

Set Variables
```sh
set SUBSCRIPTION "02d59989-f8a9-4b69-9919-1ef51df4eff6"
set DATALABS_GLOBAL_RESOURCE_GROUP "DataLabsRG"
set RF_REGIONAL_RESOURCE_GROUP "ResourceFetcher-eastus"
set RF_AKS_NAME "resourcefetcher-test-eastus"
```

Azure Login and Set Subscription
```sh
az login
az account set --subscription "${SUBSCRIPTION}"
```

### Docker image push steps

Push resource fetcher image
```sh
az acr login -n datalabsintacr
docker push datalabsintacr.azurecr.io/resourcefetcherservice:latest
```

Push admin service image
```sh
az acr login -n datalabsintacr
docker push datalabsintacr.azurecr.io/adminservice:latest
```

### Apps deployment steps

Uninstall, install & health check - Monitor service
```sh
az aks get-credentials --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm uninstall monitorservice"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm install -f BaseValueFiles/rfServices.yaml -f values_Local.yaml monitorservice MonitorService" --file .
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pods -n monitor-namespace -o wide"

- Please wait for 2 minutes after monitor service installation, before installing other services.
```

Uninstall, install & health check - AzureProfiler (NOTE: Image is not available on all clusters, installation may fail)
```sh
az aks get-credentials --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall azureprofiler"

az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Local.yaml azureprofiler AzureProfiler" --file .
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n monitor-namespace -o wide"

- Please wait for 2 minutes after monitor service installation, before installing other services.
```

Uninstall, give permissions, install & health check - Admin service
```sh
az aks get-credentials --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm uninstall adminservice"

az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm install -f BaseValueFiles/rfServices.yaml -f values_Int.yaml adminservice AdminService" --file .
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pods -n admin-namespace -o wide"
```

Uninstall, give permissions, install & health check - Resource fetcher service
```sh
az aks get-credentials --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm uninstall resourcefetcherservice"

az keyvault set-policy -n gov-rp-int-art-gbl-kv --certificate-permissions get --spn 26d422f5-0069-43c4-9351-1953e90656ee
az keyvault set-policy -n gov-rp-int-art-gbl-kv --secret-permissions get --spn 26d422f5-0069-43c4-9351-1953e90656ee

az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm install -f BaseValueFiles/rfServices.yaml -f values_Local.yaml resourcefetcherservice ResourceFetcherService" --file .
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pods -n resource-fetcher-namespace -o wide"
```

### Debug commands if pods are not running

```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pods -n resource-fetcher-namespace -o wide"
-> Say Name came as "resource-fetcher-b7cf4ddf9-4qvlm"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl describe pods resource-fetcher-b7cf4ddf9-4qvlm -n resource-fetcher-namespace"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl logs resource-fetcher-b7cf4ddf9-4qvlm -n resource-fetcher-namespace"
```

### Some more useful commands

```sh
docker image ls
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pods -A"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get deployment -A"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get deployment -n resource-fetcher-namespace" 
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl describe deployment -A"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl describe deployment resource-fetcher -n resource-fetcher-namespace"
```

If helm install succeeds but we don't see any pods
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get deployment -A"
```

If we don't see any deployment even after helm install success. It means deployment file itself has some typo or syntax error.
In such case, we can run
- with "--dry-run --debug" it will print actual final deployment files after replacing values.
- Then we can review the final generated deployment files (after replacing with values file)
```sh
az aks command invoke --resource-group "${RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install --dry-run --debug -f BaseValueFiles/rfServices.yaml -f values_Local.yaml resourcefetcherservice ./ResourceFetcherService"
```