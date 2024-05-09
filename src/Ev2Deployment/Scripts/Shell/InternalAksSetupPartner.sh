#!/bin/bash
set -e # stop the script at the first error
source ./Common.sh

createPrivateLinkForResourceFetcherAks(){
    rfResourceGroup="$1"
    rfAksName="$2"

    echo "Getting the private link service id from resource fetcher resource group ${rfResourceGroup}/${rfAksName}"

    echo "Set subscription ${RF_SUBSCRIPTION}"
    az account set --subscription "${RF_SUBSCRIPTION}"

    echo "aksMcResourceGroupName=\$(az aks show -g \"${rfResourceGroup}\" --name \"${rfAksName}\" --query nodeResourceGroup -otsv)"
    aksMcResourceGroupName=$(az aks show -g "${rfResourceGroup}" --name "${rfAksName}" --query nodeResourceGroup -otsv)
    echo "Retrieved RF AKS MC resource group name ${aksMcResourceGroupName}"

    rfAksPlsResourceId="/subscriptions/${RF_SUBSCRIPTION}/resourceGroups/${aksMcResourceGroupName}/providers/Microsoft.Network/privateLinkServices/resource-fetcher-pls"
    echo "Retrieved RF PLS ID ${rfAksPlsResourceId}"

    echo "---------------------------------------"

    partnerPeName="${PARTNER_AKS_NAME}-${rfAksName}-pe"
    partnerPeNicName="${PARTNER_AKS_NAME}-${rfAksName}-pe-nic"
    partnerPeConnectionName="${PARTNER_AKS_NAME}-${rfAksName}-connection"
    partnerPrivateDnsVnetLinkName="${PARTNER_AKS_NAME}-${rfAksName}-link"
    partnerPrivateDnsRecordSetName="${rfAksName}"

    echo "Switch to partner subscription"
    az account set --subscription "${PARTNER_SUBSCRIPTION}"

    echo "Create Private Endpoint from partner aks to  Private link service of current region's resource fetcher aks"

    echo "az network private-endpoint create -g \"${PARTNER_REGIONAL_RESOURCE_GROUP}\" --name \"${partnerPeName}\" --subnet \"${PARTNER_AKS_VNET_SUBNET_ID}\" --private-connection-resource-id \"${rfAksPlsResourceId}\" --connection-name \"${partnerPeConnectionName}\" --nic-name \"${partnerPeNicName}"
    az network private-endpoint create -g "${PARTNER_REGIONAL_RESOURCE_GROUP}" --name "${partnerPeName}" --subnet "${PARTNER_AKS_VNET_SUBNET_ID}" --private-connection-resource-id "${rfAksPlsResourceId}" --connection-name "${partnerPeConnectionName}" --nic-name "${partnerPeNicName}"

    echo "---------------------------------------"

    echo "Get the NIC private IP value"

    echo "partnerPeNicIp=\$(az network nic show -g \"${PARTNER_REGIONAL_RESOURCE_GROUP}\" -n \"${partnerPeNicName}\" --query \"ipConfigurations[0].privateIPAddress\"  -o tsv)"
    partnerPeNicIp="$(az network nic show -g "${PARTNER_REGIONAL_RESOURCE_GROUP}" -n "${partnerPeNicName}" --query "ipConfigurations[0].privateIPAddress" -o tsv)"

    echo "Partner private endpoint NIC IP ${partnerPeNicIp}"

    echo "---------------------------------------"

    echo "Creating A record in private DNS zone"

    recordExists=$(az network private-dns record-set a list -g "${PARTNER_REGIONAL_RESOURCE_GROUP}" -z "${PARTNER_PRIVATE_DNS_NAME}" --query "[?contains(id, '${rfAksName}')] | length(@)")

    if ((${recordExists} <= 0)); then
        echo "az network private-dns record-set a add-record -g \"${PARTNER_REGIONAL_RESOURCE_GROUP}\" -z \"${PARTNER_PRIVATE_DNS_NAME}\" -n \"${partnerPrivateDnsRecordSetName}\" -a \"${partnerPeNicIp}\""
        az network private-dns record-set a add-record -g "${PARTNER_REGIONAL_RESOURCE_GROUP}" -z "${PARTNER_PRIVATE_DNS_NAME}" -n "${partnerPrivateDnsRecordSetName}" -a "${partnerPeNicIp}"
    else
        echo "Dns record exists"
    fi
    echo "---------------------------------------"
}


loginAndUnzip ${CHARTS_TAR}

echo "running : az extension add --name aks-preview . This can affect the output of az aks show if at a lower version or not present"
az extension add --name aks-preview

echo "az cli version"
az version --query '"azure-cli"'

echo "---------------------------------------"

PARTNER_CHARTS_DIR="PartnerAKS"

#  move to partneraks directory
echo "moving to ${PARTNER_CHARTS_DIR} and printing contents..."
cd ${PARTNER_CHARTS_DIR}
ls
echo "---------------------------------------"

RF_SUBSCRIPTION=${RfSubscription}
RF_REGIONAL_RESOURCE_GROUP="DataLabsRFRG-${Location}"
RF_AKS_NAME="rf${CloudName}${RegionAcronym}aks"
if [ -n "$PairedRegionAcronym" ]; then
    RF_PAIRED_REGION_RESOURCE_GROUP="DataLabsRFRG-${PairedRegionLocation}"
    RF_PAIRED_REGION_AKS_NAME="rf${CloudName}${PairedRegionAcronym}aks"
fi 

PARTNER_SUBSCRIPTION=${SubscriptionId}
PARTNER_SUB_RG="DataLabs${PartnerAcronym}RG"
PARTNER_REGIONAL_RESOURCE_GROUP="DataLabs${PartnerAcronym}RG-${Location}"
PARTNER_AKS_NAME="${PartnerAcronym}${CloudName}${RegionAcronym}aks"
PARTNER_PRIVATE_DNS_NAME="${PartnerAcronym}.${CloudName}.${RegionAcronym}aks"
PARTNER_PRIVATE_DNS_VNET_LINK_NAME="${PARTNER_AKS_NAME}-${RF_AKS_NAME}-link"
DNS_LABEL_PLACEHOLDER_VALUE="\[\[<DNS_LABEL_NAME>\]\]"
ENABLE_ADMIN_SERVICE=${EnableAdminService}

VALUES_FILENAME=${ValuesFilename}

IOCONNECTOR_MI_NAME="${PartnerAcronym}${CloudName}ioconnectorid"

echo "RF_SUBSCRIPTION ${RF_SUBSCRIPTION}"
echo "RF_REGIONAL_RESOURCE_GROUP ${RF_REGIONAL_RESOURCE_GROUP}"
echo "RF_AKS_NAME ${RF_AKS_NAME}"
if [ -n "$PairedRegionAcronym" ]; then
    echo "RF_PAIRED_REGION_RESOURCE_GROUP ${RF_PAIRED_REGION_RESOURCE_GROUP}"
    echo "RF_PAIRED_REGION_AKS_NAME ${RF_PAIRED_REGION_AKS_NAME}"
fi
echo "PARTNER_SUB_RG ${PARTNER_SUB_RG}"
echo "PARTNER_REGIONAL_RESOURCE_GROUP ${PARTNER_REGIONAL_RESOURCE_GROUP}"
echo "PARTNER_AKS_NAME ${PARTNER_AKS_NAME}"
echo "PARTNER_SUBSCRIPTION ${PARTNER_SUBSCRIPTION}"
echo "PARTNER_PRIVATE_DNS_NAME ${PARTNER_PRIVATE_DNS_NAME}"
echo "VALUES_FILENAME ${VALUES_FILENAME}"
echo "IOCONNECTOR_MI_NAME ${IOCONNECTOR_MI_NAME}"

echo "---------------------------------------"
echo Partner operations # needed for every partner
echo "---------------------------------------"

echo "Switch to partner subscription"
az account set --subscription "${PARTNER_SUBSCRIPTION}"

#  connect to AKS get-credentials
echo "connecting to ${PARTNER_AKS_NAME}"
echo "az aks get-credentials --resource-group ${PARTNER_REGIONAL_RESOURCE_GROUP} --name ${PARTNER_AKS_NAME}"
az aks get-credentials --resource-group "${PARTNER_REGIONAL_RESOURCE_GROUP}" --name "${PARTNER_AKS_NAME}"
echo "---------------------------------------"

echo "Set OIDC_ISSUE url to an env variable which will be used later"

echo "set AKS_OIDC_ISSUER \"\$(az aks show -n \"${PARTNER_AKS_NAME}\" -g \"${PARTNER_REGIONAL_RESOURCE_GROUP}\" --query \"oidcIssuerProfile.issuerUrl\" -otsv)"
AKS_OIDC_ISSUER="$(az aks show -n "${PARTNER_AKS_NAME}" -g "${PARTNER_REGIONAL_RESOURCE_GROUP}" --query "oidcIssuerProfile.issuerUrl" -otsv)"

echo "Retrieved AKS_OIDC_ISSUER ${AKS_OIDC_ISSUER}"

echo "---------------------------------------"

# Create Temporary file
TMPFILE=$(mktemp)

echo "Create and Get namespace"

echo "az aks command invoke --resource-group \"${PARTNER_REGIONAL_RESOURCE_GROUP}\" --name \"${PARTNER_AKS_NAME}\" --command \"kubectl apply -f namespace.yaml\" --file ."
az aks command invoke --resource-group "${PARTNER_REGIONAL_RESOURCE_GROUP}" --name "${PARTNER_AKS_NAME}" --command "kubectl apply -f namespace.yaml" --file . 2>&1 >$TMPFILE
checkExitCodeAndExit $TMPFILE

echo "az aks command invoke --resource-group \"${PARTNER_REGIONAL_RESOURCE_GROUP}\" --name \"${PARTNER_AKS_NAME}\" --command \"kubectl get namespace"
az aks command invoke --resource-group "${PARTNER_REGIONAL_RESOURCE_GROUP}" --name "${PARTNER_AKS_NAME}" --command "kubectl get namespace" 2>&1 >$TMPFILE
checkExitCodeAndExit $TMPFILE

echo "---------------------------------------"

echo "Create ServiceAccount"

echo "az aks command invoke --resource-group \"${PARTNER_REGIONAL_RESOURCE_GROUP}\" --name \"${PARTNER_AKS_NAME}\" --command \"helm upgrade --install --atomic --timeout 1200s --force -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_${CloudName^}.yaml  -f ${VALUES_FILENAME} serviceaccount ServiceAccount\" --file ."
az aks command invoke --resource-group "${PARTNER_REGIONAL_RESOURCE_GROUP}" --name "${PARTNER_AKS_NAME}" --command "helm upgrade --install --atomic --timeout 1200s --force -f ./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_${CloudName^}.yaml -f ${VALUES_FILENAME} serviceaccount ServiceAccount" --file . 2>&1 >$TMPFILE
checkExitCodeAndExit $TMPFILE

echo "---------------------------------------"

if [ "$ENABLE_ADMIN_SERVICE" == "true" ]; then
  echo "Create Admin service Load Balancer"
  # update the DNS name placeholder with the value for partner
  sed -i "s/$DNS_LABEL_PLACEHOLDER_VALUE/$PARTNER_AKS_NAME/" "./AdminService/service.yaml"
  echo "az aks command invoke --resource-group \"${PARTNER_REGIONAL_RESOURCE_GROUP}\" --name \"${PARTNER_AKS_NAME}\" --command \"kubectl apply -f ./AdminService/service.yaml\" --file ."
  az aks command invoke --resource-group "${PARTNER_REGIONAL_RESOURCE_GROUP}" --name "${PARTNER_AKS_NAME}" --command "kubectl apply -f ./AdminService/service.yaml" --file . 2>&1 >$TMPFILE

  checkExitCodeAndExit $TMPFILE
  echo "---------------------------------------"
fi

echo "Create Federated Credential for IO connector"

echo "az identity federated-credential create --name solution-io-federated-${RegionAcronym} --identity-name \"${IOCONNECTOR_MI_NAME}\" --resource-group \"${PARTNER_SUB_RG}\" --issuer ${AKS_OIDC_ISSUER} --subject system:serviceaccount:solution-namespace:solution-io-identity"
az identity federated-credential create --name solution-io-federated-${RegionAcronym} --identity-name "${IOCONNECTOR_MI_NAME}" --resource-group "${PARTNER_SUB_RG}" --issuer ${AKS_OIDC_ISSUER} --subject system:serviceaccount:solution-namespace:solution-io-identity

echo "---------------------------------------"

echo "Create Federated Credential for Resource Fetcher Proxy"
echo "az identity federated-credential create --name resource-fetcher-federated-${RegionAcronym} --identity-name \"${IOCONNECTOR_MI_NAME}\" --resource-group \"${PARTNER_SUB_RG}\" --issuer ${AKS_OIDC_ISSUER} --subject system:serviceaccount:solution-namespace:resourcefetcherproxy-identity"
az identity federated-credential create --name resource-fetcher-federated-${RegionAcronym} --identity-name "${IOCONNECTOR_MI_NAME}" --resource-group "${PARTNER_SUB_RG}" --issuer ${AKS_OIDC_ISSUER} --subject system:serviceaccount:solution-namespace:resourcefetcherproxy-identity
echo "---------------------------------------"
echo "Connect to partner AKS to get details of vNet and subnet"

PARTNER_AKS_MC_RG=$(az aks show -g "${PARTNER_REGIONAL_RESOURCE_GROUP}" --name "${PARTNER_AKS_NAME}" --query nodeResourceGroup -o tsv)
PARTNER_AKS_VNET=$(az network vnet list -g "${PARTNER_AKS_MC_RG}" --query "[].name" -o tsv)
PARTNER_AKS_VNET_ID=$(az network vnet list -g "${PARTNER_AKS_MC_RG}" --query "[].id" -o tsv)
PARTNER_AKS_VNET_SUBNET_ID=$(az network vnet subnet list -g "${PARTNER_AKS_MC_RG}" --vnet-name "${PARTNER_AKS_VNET}" --query "[].id" -o tsv)

echo "Retrieved PARTNER_AKS_MC_RG ${PARTNER_AKS_MC_RG} "
echo "Retrieved PARTNER_AKS_VNET ${PARTNER_AKS_VNET} "
echo "Retrieved PARTNER_AKS_VNET_ID ${PARTNER_AKS_VNET_ID} "
echo "Retrieved PARTNER_AKS_VNET_SUBNET_ID ${PARTNER_AKS_VNET_SUBNET_ID} "

echo "---------------------------------------"

echo "Create Private DNS Zone"

dnsZoneExists=$(az network private-dns zone list -g "${PARTNER_REGIONAL_RESOURCE_GROUP}" --query "[].id | length(@)")

if ((${dnsZoneExists} <= 0)); then
  echo "az network private-dns zone create -g \"${PARTNER_REGIONAL_RESOURCE_GROUP}\" -n \"${PARTNER_PRIVATE_DNS_NAME}\""
  az network private-dns zone create -g "${PARTNER_REGIONAL_RESOURCE_GROUP}" -n "${PARTNER_PRIVATE_DNS_NAME}"
else
  echo "Dns Zone exists"
fi

echo "---------------------------------------"


echo "Create Private DNS Zone Vnet link"

vnetLinkExists=$(az network private-dns link vnet list -g "${PARTNER_REGIONAL_RESOURCE_GROUP}" -z "${PARTNER_PRIVATE_DNS_NAME}" --query "[].id | length(@)")

if ((${vnetLinkExists} <= 0)); then
  echo "az network private-dns link vnet create -g \"${PARTNER_REGIONAL_RESOURCE_GROUP}\" -n \"${PARTNER_PRIVATE_DNS_VNET_LINK_NAME}\" -z \"${PARTNER_PRIVATE_DNS_NAME}\" -v \"${PARTNER_AKS_VNET_ID}\" -e False"
  az network private-dns link vnet create -g "${PARTNER_REGIONAL_RESOURCE_GROUP}" -n "${PARTNER_PRIVATE_DNS_VNET_LINK_NAME}" -z "${PARTNER_PRIVATE_DNS_NAME}" -v "${PARTNER_AKS_VNET_ID}" -e False
else
  echo "vnet link exists"
fi

echo "---------------------------------------"

echo "Connecting ${RF_REGIONAL_RESOURCE_GROUP}/${RF_AKS_NAME} private link service to ${PARTNER_AKS_NAME}"
createPrivateLinkForResourceFetcherAks ${RF_REGIONAL_RESOURCE_GROUP} ${RF_AKS_NAME}

echo "---------------------------------------"


if [ -n "$PairedRegionAcronym" ]; then
    echo "Connecting ${RF_PAIRED_REGION_RESOURCE_GROUP}/${RF_PAIRED_REGION_AKS_NAME} private link service to ${PARTNER_AKS_NAME}"
    createPrivateLinkForResourceFetcherAks ${RF_PAIRED_REGION_RESOURCE_GROUP} ${RF_PAIRED_REGION_AKS_NAME}
fi