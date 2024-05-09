### Local build steps
- Open DataLabs.sln in VS 2022.
- Select Release & x64 flavor of build.
- Rebuild or (Clean/ Build) the solution.

### Docker image build steps
- Open a command prompt in admin mode.
- Move to "<SourcePathFromMachine>\Mgmt-Governance-DataLabs\out\Release-x64" folder.

Build image - Admin service
```sh
docker build -t datalabsintacr.azurecr.io/adminservice:latest -f <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/AdminService/Dockerfile <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/AdminService/
```

Build image - Resource fetcher proxy service
```sh
docker build -t datalabsintacr.azurecr.io/resourcefetcherproxyservice:latest -f <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/ResourceFetcherProxyService/Dockerfile <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/ResourceFetcherProxyService/
```

Build image - IO service
```sh
docker build -t datalabsintacr.azurecr.io/inputoutputservice:latest -f <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/InputOutputService/Dockerfile <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/InputOutputService/
```

Build image - Cache service
```sh
docker build -t datalabsintacr.azurecr.io/garnetserver:latest -f <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/GarnetServer/Dockerfile <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/GarnetServer/
```

Build image - ABC partner solution service
```sh
docker build -t datalabsintacr.azurecr.io/abcpartnersolution:latest -f <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/ABCPartnerSolutionService/Dockerfile <SourcePathFromMachine>/Mgmt-Governance-DataLabs/out/Release-x64/ABCPartnerSolutionService/
```

### Open PowerShell 7 or Windows PowerShell window and do below
- Move to "<SourcePathFromMachine>\Mgmt-Governance-DataLabs\src\AKSDeployment\Charts\PartnerAKS" folder.

### Login & select INT subscription

Set Variables
```sh
set SUBSCRIPTION "02d59989-f8a9-4b69-9919-1ef51df4eff6"
set DATALABS_GLOBAL_RESOURCE_GROUP "DataLabsRG"
set REGIONAL_RESOURCE_GROUP "ABC-eastus"
set AKS_NAME "abc-test-eastus"
```

Azure Login and Set Subscription
```sh
az login
az account set --subscription "${SUBSCRIPTION}"
```

### Docker image push steps

Push admin service image
```sh
az acr login -n datalabsintacr
docker push datalabsintacr.azurecr.io/adminservice:latest
```

Push resource fetcher proxy image
```sh
az acr login -n datalabsintacr
docker push datalabsintacr.azurecr.io/resourcefetcherproxyservice:latest
```

Push IO service image
```sh
az acr login -n datalabsintacr
docker push datalabsintacr.azurecr.io/inputoutputservice:latest
```

Push cache image
```sh
az acr login -n datalabsintacr
docker push datalabsintacr.azurecr.io/garnetserver:latest
```

Push ABC partner solution image
```sh
az acr login -n datalabsintacr
docker push datalabsintacr.azurecr.io/abcpartnersolution:latest
```

### Apps deployment steps

Uninstall, install & health check - Monitor service
```sh
az aks get-credentials --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall monitorservice"

az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Local.yaml monitorservice MonitorService" --file .
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n monitor-namespace -o wide"

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
az aks get-credentials --resource-group "${GA_REGIONAL_RESOURCE_GROUP}" --name "${GA_AKS_NAME}"
az aks command invoke --resource-group "${GA_REGIONAL_RESOURCE_GROUP}" --name "${GA_AKS_NAME}" --command "helm uninstall adminservice"

az aks command invoke --resource-group "${GA_REGIONAL_RESOURCE_GROUP}" --name "${GA_AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f values_Local.yaml adminservice AdminService" --file .
az aks command invoke --resource-group "${GA_REGIONAL_RESOURCE_GROUP}" --name "${GA_AKS_NAME}" --command "kubectl get pods -n admin-namespace -o wide"
```

Uninstall, install & health check - Resource fetcher proxy service
```sh
az aks get-credentials --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall resourceproxy"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Local.yaml resourceproxy ResourceProxy" --file .
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n solution-namespace -o wide"
```

Uninstall, install & health check - IO service
```sh
az aks get-credentials --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall ioservice"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Local.yaml ioservice IOService" --file .
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n solution-namespace -o wide"
```

Uninstall, install & health check - Cache service
```sh
az aks get-credentials --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall cacheservice1"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Local.yaml cacheservice1 CacheService --set setArrayIndexVar=0" --file .

az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall cacheservice2"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Local.yaml cacheservice2 CacheService --set setArrayIndexVar=1" --file .

az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall cacheservice3"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Local.yaml cacheservice3 CacheService --set setArrayIndexVar=2" --file .
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n cache-namespace -o wide"
```

Uninstall, install & health check - ABC partner solution service
```sh
az aks get-credentials --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall partnerservice"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Local.yaml partnerservice PartnerService" --file .
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n partner-namespace -o wide"
```

### Debug commands if pods are not running

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n solution-namespace -o wide"
-> Say Name came as "solution-io-b7cf4ddf9-4qvlm"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl describe pods solution-io-b7cf4ddf9-4qvlm -n solution-namespace"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl logs solution-io-b7cf4ddf9-4qvlm  -n solution-namespace"
```

### Some more useful commands

```sh
docker image ls
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -A"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get deployment -A"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get deployment -n solution-namespace"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl describe deployment -A"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl describe deployment resource-proxy -n solution-namespace"
```

If helm install succeeds but we don't see any pods
```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get deployment -A"
```

If we don't see any deployment even after helm install success. It means deployment file itself has some typo or syntax error.
In such case, we can run
- with "--dry-run --debug" it will print actual final deployment files after replacing values.
- Then we can review the final generated deployment files (after replacing with values file)
```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install --dry-run --debug -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Local.yaml partnerservice ./PartnerService"
```