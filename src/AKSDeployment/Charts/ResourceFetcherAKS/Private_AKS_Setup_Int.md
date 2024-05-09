#### Resources to be Created Before Setup
- Created resource group.
- Created private AKS via deployement.

Set Variables
```sh
set RF_REGIONAL_RESOURCE_GROUP "ResourceFetcher-eastus"
set RF_GLOBAL_RESOURCE_GROUP "ResourceFetcher"
set RF_SUBSCRIPTION "02d59989-f8a9-4b69-9919-1ef51df4eff6"
set LOCATION "eastus"
set RF_MI_NAME "resourcefetcher-test"
set RF_ACR_MI_NAME "resourcefetcher-test-acrpush"
set RF_MONITORING_MI_NAME "resourcefetcher-test-monitoring"
set RF_AKS_NAME "resourcefetcher-test-eastus"
set RF_KV_NAME "gov-rp-int-art-gbl-kv"
set ACR_RESOURCE_ID "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/ResourceFetcher-eastus/providers/Microsoft.ContainerRegistry/registries/resourcefetchertesteastus"

set ABC_REGIONAL_RESOURCE_GROUP "ABC-eastus"
set ABC_SUBSCRIPTION "02d59989-f8a9-4b69-9919-1ef51df4eff6"
set ABC_AKS_NAME "abc-test-eastus"
set ABC_PE_NAME "abc-test-eastus-resourcefetcher-test-eastus-pe"
set ABC_PE_NIC_NAME "abc-test-eastus-resourcefetcher-test-eastus-pe-nic"
set ABC_PE_CONNECTION_NAME "abc-test-eastus-resourcefetcher-test-eastus-connection"
set ABC_PRIVATE_DNS_NAME "abc.test.eastus"
set ABC_PRIVATE_DNS_VNET_LINK_NAME "abc-test-eastus-resourcefetcher-test-eastus-link"
set ABC_PRIVATE_DNS_RECORD_SET_NAME "resourcefetcher-test-eastus"
```

Azure Login and Set Subscription
```sh
az login
az account set --subscription "${RF_SUBSCRIPTION}"
```
```sh
Powershell
Connect-AzAccount
Select-AzSubscription -SubscriptionName "${RF_SUBSCRIPTION}"
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

Create Private AKS
```sh
az aks create -g "${RF_REGIONAL_RESOURCE_GROUP}" -n "${RF_AKS_NAME}" --enable-cluster-autoscaler --min-count 3 --max-count 10 --enable-private-cluster --enable-oidc-issuer --enable-workload-identity --generate-ssh-keys --network-plugin azure --network-policy azure --auto-upgrade-channel patch --os-sku AzureLinux --enable-managed-identity
az aks enable-addons --addons azure-keyvault-secrets-provider --name "${RF_AKS_NAME}" --resource-group "${RF_REGIONAL_RESOURCE_GROUP}"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pods -n kube-system -l 'app in (secrets-store-csi-driver,secrets-store-provider-azure)'"
set SECRET_CLIENT_ID "$(az aks show -g "${RF_REGIONAL_RESOURCE_GROUP}" -n "${RF_AKS_NAME}" --query addonProfiles.azureKeyvaultSecretsProvider.identity.clientId -o tsv)"
-> Note this client Id, we need this for secret provider

# set policy to access secrets in your key vault
az keyvault set-policy -n "${RF_KV_NAME}" --secret-permissions get --spn "${SECRET_CLIENT_ID}"
# set policy to access certs in your key vault
az keyvault set-policy -n "${RF_KV_NAME}" --certificate-permissions get --spn "${SECRET_CLIENT_ID}"

```

Attach Azure Container Registry
If the below command does not work, you need to create an ACRPull role on the ACR for the AKS agent pool user identity.
```sh
az aks update -g "${RF_REGIONAL_RESOURCE_GROUP}" -n "${RF_AKS_NAME}" --attach-acr "${ACR_RESOURCE_ID}"
```
```sh
Powershell
Set-AzAksCluster -Name "${RF_AKS_NAME}" -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -AcrNameToAttach "${ACR_RESOURCE_ID}"
```

Create Managed Identity
```sh
az identity create --name "${RF_MI_NAME}" --resource-group "${RF_GLOBAL_RESOURCE_GROUP}" --location "${LOCATION}" --subscription "${RF_SUBSCRIPTION}"
set USER_ASSIGNED_CLIENT_ID "$(az identity show --resource-group "${RF_GLOBAL_RESOURCE_GROUP}" --name "${RF_MI_NAME}" --query 'clientId' -otsv)"
```
Create role assignment on ARG key vault to access certificate which will be used with AAD.

Create Monitoring Managed Identity
```sh
az identity create --name "${RF_MONITORING_MI_NAME}" --resource-group "${RF_GLOBAL_RESOURCE_GROUP}" --location "${LOCATION}" --subscription "${RF_SUBSCRIPTION}"
```
- Create role assignment on the resource group level for GenevaWarmPathResourceContributor and GenevaWarmPathStorageBlobContributor to send diagnostics to Geneva

Create ACR Managed Identity
```sh
az identity create --name "${RF_ACR_MI_NAME}" --resource-group "${RF_GLOBAL_RESOURCE_GROUP}" --location "${LOCATION}" --subscription "${RF_SUBSCRIPTION}"
```
Assign ACR Push role assginment

## Steps to be Performed
- Please upgrade Azure CLI because some AKS commands rquire latest Azure CLI version

Azure Login and Set Subscription
```sh
az login
az account set --subscription "${RF_SUBSCRIPTION}"
```
```sh
Powershell
Connect-AzAccount
Select-AzSubscription -SubscriptionName "${RF_SUBSCRIPTION}"
```

Set OIDC_ISSUE url to an env variable which will be used later
```sh
set AKS_OIDC_ISSUER "$(az aks show -n "${RF_AKS_NAME}" -g "${RF_REGIONAL_RESOURCE_GROUP}" --query "oidcIssuerProfile.issuerUrl" -otsv)"
```
```sh
Powershell
$AKS_OIDC_ISSUER = (Get-AzAksCluster -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}").OidcIssuerProfile.IssuerURL
```

Connect to AKS
```sh
az aks get-credentials --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}"
```
```sh
Powershell
Import-AzAksCredential -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Force
```

Change local repo directory to Mgmt-Governance-ResourcesCacheSolution\ARGSolution\Deployment\Charts\ResourceFetcherAKS

Create and Get namespace
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl apply -f namespace.yaml" --file .
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get namespace"
```
```sh
Powershell
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Force -Command "kubectl apply -f namespace.yaml" -CommandContextAttachment "namespace.yaml"
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Command "kubectl get namespace" -Force
```

Internal Load Balancer and Private Link Service
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl apply -f service.yaml" --file .
```
```sh
Powershell
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Force -Command "kubectl apply -f service.yaml" -CommandContextAttachment "service.yaml"
```

Wait for sometime, EXTERNAL-IP column will get a value
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get service resource-fetcher -n resource-fetcher-namespace"
```
```sh
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Command "kubectl get service resource-fetcher -n resource-fetcher-namespace" -Force
```

Create a variable for the resource group, list the private link service and save in variable
```sh
$AKS_MC_RG = $(az aks show -g "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --query nodeResourceGroup -o tsv)
az network private-link-service list -g $AKS_MC_RG --query "[].{Name:name,Alias:alias}" -o table
$AKS_PLS_ID = $(az network private-link-service list -g $AKS_MC_RG --query "[].id" -o tsv)
```
```sh
Powershell
$AKS_MC_RG = (Get-AzAksCluster -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}").NodeResourceGroup
Get-AzPrivateLinkService -ResourceGroupName "${AKS_MC_RG}"
$AKS_PLS_ID = (Get-AzPrivateLinkService -ResourceGroupName "${AKS_MC_RG}").Id
```

Switch to ABC partner subscription
```sh
az account set --subscription "${ABC_SUBSCRIPTION}"
```
```sh
Powershell
Select-AzSubscription -SubscriptionName "${ABC_SUBSCRIPTION}"
```

Connect to ABC AKS to get details of vNet and subnet
```sh
az aks get-credentials --resource-group "${ABC_REGIONAL_RESOURCE_GROUP}" --name "${ABC_AKS_NAME}"
$ABC_AKS_MC_RG = $(az aks show -g "${ABC_REGIONAL_RESOURCE_GROUP}" --name "${ABC_AKS_NAME}" --query nodeResourceGroup -o tsv)
$ABC_AKS_VNET = $(az network vnet list -g "${ABC_AKS_MC_RG}" --query "[].name" -o tsv)
$ABC_AKS_VNET_ID = $(az network vnet list -g "${ABC_AKS_MC_RG}" --query "[].id" -o tsv)
$ABC_AKS_VNET_SUBNET = $(az network vnet subnet list -g "${ABC_AKS_MC_RG}" --vnet-name "${ABC_AKS_VNET}" --query "[].id" -o tsv)
```
```sh
Powershell
Import-AzAksCredential -ResourceGroupName "${ABC_REGIONAL_RESOURCE_GROUP}" -Name "${ABC_AKS_NAME}" -Force
$ABC_AKS_MC_RG = (Get-AzAksCluster -ResourceGroupName "${ABC_REGIONAL_RESOURCE_GROUP}" -Name "${ABC_AKS_NAME}").NodeResourceGroup
$ABC_AKS_VNET = (Get-AzVirtualNetwork -ResourceGroupName "${ABC_AKS_MC_RG}")
$ABC_AKS_VNET_ID = (Get-AzVirtualNetwork -ResourceGroupName "${ABC_AKS_MC_RG}").Id
$ABC_AKS_VNET_SUBNET = Get-AzVirtualNetworkSubnetConfig -VirtualNetwork ${ABC_AKS_VNET}
```

Create Private Endpoint for Private link service
```sh
az network private-endpoint create -g "${ABC_REGIONAL_RESOURCE_GROUP}" --name "${ABC_PE_NAME}" --subnet "${ABC_AKS_VNET_SUBNET}" --private-connection-resource-id "${AKS_PLS_ID}" --connection-name "${ABC_PE_CONNECTION_NAME}" --nic-name "${ABC_PE_NIC_NAME}"
$ABC_PE_NIC_IP = $(az network nic show -g "${ABC_REGIONAL_RESOURCE_GROUP}" -n "${ABC_PE_NIC_NAME}" --query "ipConfigurations[].privateIpAddress" -o tsv)
```
```sh
Powershell
$AKS_PLS= New-AzPrivateLinkServiceConnection -Name "${ABC_PE_CONNECTION_NAME}" -PrivateLinkServiceId "${AKS_PLS_ID}"
New-AzPrivateEndpoint -ResourceGroupName "${ABC_REGIONAL_RESOURCE_GROUP}" -Name "${ABC_PE_NAME}" -Subnet $ABC_AKS_VNET_SUBNET -PrivateLinkServiceConnection $AKS_PLS -CustomNetworkInterfaceName "${ABC_PE_NIC_NAME}" -Location "${LOCATION}"
$ABC_PE_NIC_IP = (Get-AzNetworkInterface -ResourceGroupName "${ABC_REGIONAL_RESOURCE_GROUP}" -Name "${ABC_PE_NIC_NAME}").IpConfigurations[0].PrivateIpAddress
```
You may need to approve the above connection

Create Private DNS Zone
```sh
az network private-dns zone create -g "${ABC_REGIONAL_RESOURCE_GROUP}" -n "${ABC_PRIVATE_DNS_NAME}"
az network private-dns link vnet create -g "${ABC_REGIONAL_RESOURCE_GROUP}" -n "${ABC_PRIVATE_DNS_VNET_LINK_NAME}" -z "${ABC_PRIVATE_DNS_NAME}" -v "${ABC_AKS_VNET_ID}" -e False
az network private-dns record-set a add-record -g "${ABC_REGIONAL_RESOURCE_GROUP}" -z "${ABC_PRIVATE_DNS_NAME}" -n "${ABC_PRIVATE_DNS_RECORD_SET_NAME}" -a "${ABC_PE_NIC_IP}"
```
```sh
Powershell
New-AzPrivateDnsZone -ResourceGroupName "${ABC_REGIONAL_RESOURCE_GROUP}" -Name "${ABC_PRIVATE_DNS_NAME}"
New-AzPrivateDnsVirtualNetworkLink -ResourceGroupName "${ABC_REGIONAL_RESOURCE_GROUP}" -ZoneName "${ABC_PRIVATE_DNS_NAME}" -Name "${ABC_PRIVATE_DNS_VNET_LINK_NAME}" -VirtualNetworkId "${ABC_AKS_VNET_ID}"
New-AzPrivateDnsRecordSet -ResourceGroupName "${ABC_REGIONAL_RESOURCE_GROUP}" -ZoneName "${ABC_PRIVATE_DNS_NAME}" -Name "${ABC_PRIVATE_DNS_RECORD_SET_NAME}" -RecordType A -TTL 3600 -PrivateDnsRecords (New-AzPrivateDnsRecordConfig -IPv4Address "${ABC_PE_NIC_IP}") 
```

Change local repo directory to Mgmt-Governance-ResourcesCacheSolution\ARGSolution\Deployment\Charts\ResourceFetcherAKS

Switch to resource fetcher subscription
```sh
az account set --subscription "${RF_SUBSCRIPTION}"
```
```sh
Powershell
Select-AzSubscription -SubscriptionName "${RF_SUBSCRIPTION}"
```

Connect back (though not needed)
```sh
az aks get-credentials --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}"
```
```sh
Powershell
Import-AzAksCredential -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Force
```

If you don't have helm installed, Refer to the file InstallHelm.md in the local repo directory Mgmt-Governance-ResourcesCacheSolution\ARGSolution\Deployment\Charts

Create ServiceAccount
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f BaseValueFiles/rfServices.yaml -f values_int.yaml serviceaccount ServiceAccount" --file .
```
```sh
Powershell
$itemssa = "values_Int.yaml", "ServiceAccount\templates\serviceaccount.yaml", "ServiceAccount\Chart.yaml", "BaseValueFiles\rfServices.yaml"
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Force -Command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f BaseValueFiles/rfServices.yaml -f values_int.yaml serviceaccount ServiceAccount" -CommandContextAttachment $itemssa
```

Create Federated Credential
```sh
az identity federated-credential create --name resource-fetcher-service-federated --identity-name "${RF_MI_NAME}" --resource-group "${RF_GLOBAL_RESOURCE_GROUP}" --issuer ${AKS_OIDC_ISSUER} --subject system:serviceaccount:resource-fetcher-namespace:resource-fetcher-identity
```
```sh
Powershell
New-AzFederatedIdentityCredentials -Name resource-fetcher-service-federated -IdentityName  "${RF_MI_NAME}" -ResourceGroupName "${RF_GLOBAL_RESOURCE_GROUP}" -Issuer ${AKS_OIDC_ISSUER} -Subject system:serviceaccount:resource-fetcher-namespace:resource-fetcher-identity
```

Create ResourceFetcherService
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f BaseValueFiles/rfServices.yaml -f values_int.yaml resourcefetcherservice ResourceFetcherService" --file .
```
```sh
Powershell
$items = "values_Int.yaml", "ResourceFetcherService\templates\deployment.yaml","ResourceFetcherService\templates\configmap.yaml", "ResourceFetcherService\Chart.yaml"
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Force -Command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f BaseValueFiles/rfServices.yaml -f values_int.yaml resourcefetcherservice ResourceFetcherService" -CommandContextAttachment $items
```

Check if expected Pods are running in resource-fetcher-namespace
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pods -n resource-fetcher-namespace -o wide"
```
```sh
Powershell
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Command "kubectl get pods -n resource-fetcher-namespace -o wide" -Force
```

Create MonitorService
- Reminder: Monitoring managed identity requires GenevaWarmPathResourceContributor and GenevaWarmPathStorageBlobContributor role assignments to send diagnostics to Geneva.
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm install -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_Int.yaml -f bcdrValues_Int.yaml monitorservice MonitorService" --file .
```

Check if exptected Pods are running in solution-namespace

```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pods -n monitor-namespace -o wide"
```

## End

## Some other useful commands
Uninstall 
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm uninstall resourcefetcherservice"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm uninstall monitorservice"
```
```sh
Powershell
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Command "helm uninstall resourcefetcherservice" -Force
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Command "helm uninstall serviceaccount" -Force
```

Delete Service
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl delete service resource-fetcher -n resource-fetcher-namespace"
```
```sh
Powershell
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Command "kubectl delete service resource-fetcher -n resource-fetcher-namespace" -Force
```

Logs to Investigate if the Pod is not in Running status
```sh
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pods -n resource-fetcher-namespace -o wide"
-> Say Name came as "resource-fetcher-b7cf4ddf9-4qvlm"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl describe pods resource-fetcher-b7cf4ddf9-4qvlm -n resource-fetcher-namespace"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl logs resource-fetcher-b7cf4ddf9-4qvlm -n resource-fetcher-namespace"
```
```sh
Powershell
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Command "kubectl get pods -n resource-fetcher-namespace -o wide" -Force
-> Say Name came as "resource-fetcher-b7cf4ddf9-4qvlm"
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Command "kubectl describe pods resource-fetcher-b7cf4ddf9-4qvlm -n resource-fetcher-namespace" -Force
Invoke-AzAksRunCommand -ResourceGroupName "${RF_REGIONAL_RESOURCE_GROUP}" -Name "${RF_AKS_NAME}" -Command "kubectl logs resource-fetcher-b7cf4ddf9-4qvlm -n resource-fetcher-namespace" -Force
```

Docker useful commands
```sh
-> The container registry is resourcefetchertesteastus
az acr login -n resourcefetchertesteastus
Connect-AzContainerRegistry -Name resourcefetchertesteastus
-> Built docker image is resourcefetcherservice
docker tag resourcefetcherservice resourcefetchertesteastus.azurecr.io/resourcefetchercontainer:latest
docker push resourcefetchertesteastus.azurecr.io/resourcefetchercontainer:latest
```

Keyvault useful commads
```sh
# set policy to access keys in your key vault
az keyvault set-policy -n "${RF_KV_NAME}" --key-permissions get --spn "${SECRET_CLIENT_ID}"

Example
az keyvault set-policy -n gov-rp-int-art-gbl-kv --certificate-permissions get --spn 26d422f5-0069-43c4-9351-1953e90656ee
az keyvault set-policy -n gov-rp-int-art-gbl-kv --secret-permissions get --spn 26d422f5-0069-43c4-9351-1953e90656ee
```
```sh
PS C:\Code\BCDR\Mgmt-Governance-ResourcesCacheSolution\ARGSolution\Deployment\Charts\ResourceFetcherAKS> az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pod -o 'custom-columns=PodName:.metadata.name,Containers:.spec.containers[*].name,Image:.spec.containers[*].image' -n resource-fetcher-namespace"
command started at 2023-03-14 12:29:53+00:00, finished at 2023-03-14 12:29:55+00:00 with exitcode=0
PodName                             Containers          Image
resource-fetcher-7fc49cf68c-dsjkj   fetcher-container   resourcefetchertesteastus.azurecr.io/resourcefetchercontainer:latest
resource-fetcher-7fc49cf68c-pvw48   fetcher-container   resourcefetchertesteastus.azurecr.io/resourcefetchercontainer:latest
resource-fetcher-7fc49cf68c-qj5l6   fetcher-container   resourcefetchertesteastus.azurecr.io/resourcefetchercontainer:latest

## show secrets held in secrets-store
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl exec resource-fetcher-7fc49cf68c-dsjkj -n resource-fetcher-namespace -c fetcher-container -- ls /secrets-store/"

## print a test secret 'ExampleSecret' held in secrets-store
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl exec resource-fetcher-7fc49cf68c-dsjkj -n resource-fetcher-namespace -c fetcher-container -- cat /secrets-store/ExampleSecret"
```

Some more useful commands
```sh
docker image ls

az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get pods -A"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get deployment -A"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get deployment -n resource-fetcher-namespace" 
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl describe deployment -A"
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl describe deployment resource-fetcher -n resource-fetcher-namespace"
 
-> If helm install succeeds but we don't see any pods
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get deployment -A"
-> If we don't see any deployment even after helm install success. It means deployment file itself has some typo or syntax error.
-> In such case, we can run 
az aks command invoke --resource-group "${RESOURCE_GROUP}" --name "${AKS_NAME}" --command "helm install --dry-run --debug -f BaseValueFiles/rfServices.yaml -f values_int.yaml resourcefetcherservice ./ResourceFetcherService"
-> with "--dry-run --debug" it will print actual final deployment files after replacing values.�
-> Then we can review the final generated deployment files (after replacing with values file)
```
