#### Resources to be Created Before Setup

- Created resource group.
- Created input and output event hubs in above resource group.
- Created storage account in above resource group.
- Created service bus in above resource group.
- Created private AKS via deployement.

Set Variables

```sh
set REGIONAL_RESOURCE_GROUP "ABC-eastus"
set GLOBAL_RESOURCE_GROUP "ABC"
set SUBSCRIPTION "02d59989-f8a9-4b69-9919-1ef51df4eff6"
set LOCATION "eastus"
set IOCONNECTOR_MI_NAME "abc-test-ioconnector"
set AKS_NAME "abc-test-eastus"
set RF_PROXY_MI_NAME "abc-test-resourcefetcherproxy"
set MONITORING_MI_RESOURCE_ID "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourcegroups/ABC/providers/Microsoft.ManagedIdentity/userAssignedIdentities/abc-test-acrpush"
set ACR_RESOURCE_ID "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/ABC-eastus/providers/Microsoft.ContainerRegistry/registries/abctesteastus"
```

#### For Reference if Required

Below one time task if you already added aks-preview, you don't need to performe below tasks'

Azure Login and Set Subscription

```sh
az login
az account set --subscription "${SUBSCRIPTION}"
```

```sh
Powershell
Connect-AzAccount
Select-AzSubscription -SubscriptionName "${SUBSCRIPTION}"
```

Register for the provider - Microsoft.ContainerService

```sh
az provider register --namespace Microsoft.ContainerService
```

```sh
PowerShell
Register-AzResourceProvider -ProviderNamespace "Microsoft.ContainerService"
```

Please install the latest modules

```sh
Powershell
Install-Module -Name Az.Aks -AllowClobber -Force -SkipPublisherCheck
Install-Module -Name Az.Networking -AllowClobber -Force -SkipPublisherCheck
Install-Module -Name Az.ManagedServiceIdentity -AllowClobber -Force -SkipPublisherCheck
```

Create private AKS

```sh
az aks create -g "${REGIONAL_RESOURCE_GROUP}" -n "${AKS_NAME}" --enable-cluster-autoscaler --min-count 3 --max-count 10 --enable-private-cluster --enable-oidc-issuer --enable-workload-identity --generate-ssh-keys --network-plugin azure --network-policy azure --auto-upgrade-channel patch --os-sku AzureLinux --enable-managed-identity
```

Update AKS kubelet identity (monitoring authentication)

```sh
az aks update --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --assign-kubelet-identity "${MONITORING_MI_RESOURCE_ID}" --assign-identity "${MONITORING_MI_RESOURCE_ID}" --enable-managed-identity
```

Attach Azure Container Registry.
If the below command does not work, you need to create an ACRPull role on the ACR for the AKS agent pool user identity.

```sh
az aks update -g "${REGIONAL_RESOURCE_GROUP}" -n "${AKS_NAME}" --attach-acr "${ACR_RESOURCE_ID}"
```

```sh
Powershell
Set-AzAksCluster -Name "${AKS_NAME}" -ResourceGroupName "${REGIONAL_RESOURCE_GROUP}" -AcrNameToAttach "${ACR_RESOURCE_ID}"
```

Create Managed Identity

```sh
az identity create --name "${IOCONNECTOR_MI_NAME}" --resource-group "${GLOBAL_RESOURCE_GROUP}" --location "${LOCATION}" --subscription "${SUBSCRIPTION}"
set USER_ASSIGNED_CLIENT_ID "$(az identity show --resource-group "${GLOBAL_RESOURCE_GROUP}" --name "${IOCONNECTOR_MI_NAME}" --query 'clientId' -otsv)"
```

Assign necessary role-assignments "Azure Event Hubs Data Owner", "Storage Account Data Owner", "Azure Service Bus Data Owner", "GenevaWarmPathResourceContributor" and 
"GenevaWarmPathStorageBlobContributor" to above user managed identity

## Steps to be Performed

- Replace input event hub, output event hub, storage account, service bus in bcdrValues_Int.yaml
- Replace clientId of ioServiceAccount in bcdrValues_Int.yaml with above USER_ASSIGNED_CLIENT_ID
- Please upgrade Azure CLI because some AKS commands rquire latest Azure CLI version

Azure Login and Set Subscription

```sh
az login
az account set --subscription "${SUBSCRIPTION}"
```

```sh
Powershell
Connect-AzAccount
Select-AzSubscription -SubscriptionName "${SUBSCRIPTION}"
```

Set OIDC_ISSUE url to an env variable which will be used later

```sh
set AKS_OIDC_ISSUER "$(az aks show -n "${AKS_NAME}" -g "${REGIONAL_RESOURCE_GROUP}" --query "oidcIssuerProfile.issuerUrl" -otsv)"
```

```sh
Powershell
$AKS_OIDC_ISSUER = (Get-AzAksCluster -ResourceGroupName "${REGIONAL_RESOURCE_GROUP}" -Name "${AKS_NAME}").OidcIssuerProfile.IssuerURL
```

Connect to AKS

```sh
az aks get-credentials --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}"
```

```sh
Powershell
Import-AzAksCredential -ResourceGroupName "${REGIONAL_RESOURCE_GROUP}" -Name "${AKS_NAME}" -Force
```

Change local repo directory to Mgmt-Governance-Datalabs\src\AKSDeployment\Charts\PartnerAKS\

The user should have "Azure Kubernetes Service Cluster Admin" role on the AKS.

Create and Get namespace

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl apply -f namespace.yaml" --file .
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get namespace"
```

```sh
Powershell
Invoke-AzAksRunCommand -ResourceGroupName "${REGIONAL_RESOURCE_GROUP}" -Name "${AKS_NAME}" -Force -Command "kubectl apply -f namespace.yaml" -CommandContextAttachment "namespace.yaml"
Invoke-AzAksRunCommand -ResourceGroupName "${REGIONAL_RESOURCE_GROUP}" -Name "${AKS_NAME}" -Command "kubectl get namespace" -Force
```

If you don't have helm installed, Refer to the file InstallHelm.md in the local repo directory Mgmt-Governance-ResourcesCacheSolution\ARGSolution\Deployment\Charts

Create ServiceAccount

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml serviceaccount ServiceAccount" --file .
```

```sh
Powershell
$itemssa = "bcdrValues_Int.yaml", "ServiceAccount\templates\serviceaccount.yaml", "ServiceAccount\Chart.yaml"
Invoke-AzAksRunCommand -ResourceGroupName "${REGIONAL_RESOURCE_GROUP}" -Name "${AKS_NAME}" -Force -Command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml serviceaccount ServiceAccount" -CommandContextAttachment $itemssa
```

Create Federated Credential for IO connector

```sh
az identity federated-credential create --name solution-io-federated --identity-name "${IOCONNECTOR_MI_NAME}" --resource-group "${GLOBAL_RESOURCE_GROUP}" --issuer ${AKS_OIDC_ISSUER} --subject system:serviceaccount:solution-namespace:solution-io-identity
```

```sh
Powershell
New-AzFederatedIdentityCredentials -Name solution-io-federated -IdentityName  "${IOCONNECTOR_MI_NAME}" -ResourceGroupName "${GLOBAL_RESOURCE_GROUP}" -Issuer ${AKS_OIDC_ISSUER} -Subject system:serviceaccount:solution-namespace:solution-io-identity
```

Create Federated Credential for Resource Fetcher Proxy

```sh
az identity federated-credential create --name resource-fetcher-federated --identity-name "${RF_PROXY_MI_NAME}" --resource-group "${GLOBAL_RESOURCE_GROUP}" --issuer ${AKS_OIDC_ISSUER} --subject system:serviceaccount:solution-namespace:resourcefetcherproxy-identity
```

```sh
Powershell
New-AzFederatedIdentityCredentials -Name resource-fetcher-federated -IdentityName  "${RF_PROXY_MI_NAME}" -ResourceGroupName "${GLOBAL_RESOURCE_GROUP}" -Issuer ${AKS_OIDC_ISSUER} -Subject system:serviceaccount:solution-namespace:resourcefetcherproxy-identity
```

Create PartnerService

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml partnerservice PartnerService" --file .
```

Check if expected Pods are running in partner-namespace

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n partner-namespace -o wide"
```

For overriding you can use additional config file as below
```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml -f image_tag_override_example.yaml partnerservice PartnerService" --file .
```

Create IOService

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml ioservice IOService" --file .
```

Check if exptected Pods are running in solution-namespace

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n solution-namespace -o wide"
```

Create CacheSerivce

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml cacheservice1 CacheService --set setArrayIndexVar=0" --file .
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml cacheservice2 CacheService --set setArrayIndexVar=1" --file .
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml cacheservice3 CacheService --set setArrayIndexVar=2" --file .
```

Check if exptected Pods are running in cache-namespace

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n cache-namespace -o wide"
```

Create ResourceProxy

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml resourceproxy ResourceProxy" --file .
```

Check if exptected Pods are running in solution-namespace

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n solution-namespace -o wide"
```

Create MonitorService
- Reminder: Monitoring managed identity requires GenevaWarmPathResourceContributor and GenevaWarmPathStorageBlobContributor role assignments to send diagnostics to Geneva.

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml monitorservice MonitorService" --file .
```

Check if exptected Pods are running in solution-namespace

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n monitor-namespace -o wide"
```

## End

## Some other useful commands

Uninstall

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall partnerservice"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall ioservice"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall resourceproxy"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm uninstall monitorservice"
```

Logs to Investigate if the Pod is not in Running status

```sh
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -n solution-namespace -o wide"
-> Say Name came as "solution-io-b7cf4ddf9-4qvlm"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl describe pods solution-io-b7cf4ddf9-4qvlm -n solution-namespace"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl logs solution-io-b7cf4ddf9-4qvlm  -n solution-namespace"
```

Docker useful commands

```sh
-> The container registry is abctesteastus
az acr login -n abctesteastus
-> Built docker image is testpartnersolutionservice
docker tag testpartnersolutionservice abctesteastus.azurecr.io/partnersolutioncontainer:latest
docker push abctesteastus.azurecr.io/partnersolutioncontainer:latest
```

IO Service

```sh
-> The container registry is abctesteastus
az acr login -n abctesteastus
-> Built docker image is inputoutputservice
docker tag inputoutputservice abctesteastus.azurecr.io/solution/io:latest
docker push abctesteastus.azurecr.io/solution/io:latest
```

ABC Partner Service

```sh
-> The container registry is abctesteastus
az acr login -n abctesteastus
-> Built docker image is abcpartner
docker tag abcpartner abctesteastus.azurecr.io/azurebusinesscontinuitypartner:latest
docker push abctesteastus.azurecr.io/azurebusinesscontinuitypartner:latest
```

Resource Fetcher Proxy Service

```sh
-> The container registry is abctesteastus
az acr login -n abctesteastus
-> Built docker image is resourcefetcherproxyservice
docker tag resourcefetcherproxyservice abctesteastus.azurecr.io/resourcefetcherproxy:latest
docker push abctesteastus.azurecr.io/resourcefetcherproxy:latest
```

Some more useful commands

```sh
docker image ls

az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get pods -A"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get deployment -A"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get deployment -n solution-namespace"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl describe deployment -A"
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl describe deployment resource-proxy -n solution-namespace"

-> If helm install succeeds but we don't see any pods
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "kubectl get deployment -A"
-> If we don't see any deployment even after helm install success. It means deployment file itself has some typo or syntax error.
-> In such case, we can run
az aks command invoke --resource-group "${REGIONAL_RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install --dry-run --debug -f bcdrValues_Int.yaml partnerservice ./PartnerService"
-> with "--dry-run --debug" it will print actual final deployment files after replacing values.�
-> Then we can review the final generated deployment files (after replacing with values file)
```
