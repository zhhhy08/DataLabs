#!/bin/bash
set -e # stop the script at the first error
source ./Common.sh

loginAndUnzip ${CHARTS_TAR}

echo "---------------------------------------"

RESOURCE_FETCHER_CHARTS_DIR="ResourceFetcherAKS"

RF_SUBSCRIPTION=${RfSubscription}
RF_SUB_RG="DataLabsRFRG"
RF_REGIONAL_RESOURCE_GROUP="DataLabsRFRG-${Location}"
RF_AKS_NAME="rf${CloudName}${RegionAcronym}aks"
RF_MI_NAME="rf${CloudName}id"
VALUES_FILENAME=${ValuesFilename}
DNS_LABEL_PLACEHOLDER_VALUE="\[\[<DNS_LABEL_NAME>\]\]"
ENABLE_ADMIN_SERVICE=${EnableAdminService}

echo "RF_SUBSCRIPTION ${RF_SUBSCRIPTION}"
echo "RF_SUB_RG ${RF_SUB_RG}"
echo "RF_REGIONAL_RESOURCE_GROUP ${RF_REGIONAL_RESOURCE_GROUP}"
echo "RF_AKS_NAME ${RF_AKS_NAME}"
echo "RF_MI_NAME ${RF_MI_NAME}"
echo "VALUES_FILENAME ${VALUES_FILENAME}"

#--------------------------------------

echo "Set subscription ${RF_SUBSCRIPTION}"
az account set --subscription "${RF_SUBSCRIPTION}"
echo "---------------------------------------"

#  connect to AKS get-credentials
echo "connecting to ${RF_AKS_NAME}"
echo "az aks get-credentials --resource-group ${RF_REGIONAL_RESOURCE_GROUP} --name ${RF_AKS_NAME}"
az aks get-credentials --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}"
echo "---------------------------------------"

#  move to resourcefetcheraks directory
echo "moving to ${RESOURCE_FETCHER_CHARTS_DIR} and printing contents..."
cd ${RESOURCE_FETCHER_CHARTS_DIR}
ls

echo "---------------------------------------"
echo Resource Fetcher operations # need once for datalabs setup in a region
echo "---------------------------------------"

echo "running : az extension add --name aks-preview . This can affect the output of oidcIssuerProfile in az aks show if at a lower version or not present"
az extension add --name aks-preview

echo "Set OIDC_ISSUE url to an env variable which will be used later"

echo "AKS_OIDC_ISSUER=\"\$(az aks show -n \"${RF_AKS_NAME}\" -g \"${RF_REGIONAL_RESOURCE_GROUP}\" --query \"oidcIssuerProfile.issuerUrl\" -otsv)\""
AKS_OIDC_ISSUER="$(az aks show -n "${RF_AKS_NAME}" -g "${RF_REGIONAL_RESOURCE_GROUP}" --query "oidcIssuerProfile.issuerUrl" -otsv)"

echo "Retrieved AKS_OIDC_ISSUER ${AKS_OIDC_ISSUER}"
echo "---------------------------------------"

# Create Temporary file
TMPFILE=$(mktemp)

echo "Create and Get namespace"

echo "az aks command invoke --resource-group \"${RF_REGIONAL_RESOURCE_GROUP}\" --name \"${RF_AKS_NAME}\" --command \"kubectl apply -f namespace.yaml\" --file ."
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl apply -f namespace.yaml" --file . 2>&1 >$TMPFILE
checkExitCodeAndExit $TMPFILE

echo "az aks command invoke --resource-group \"${RF_REGIONAL_RESOURCE_GROUP}\" --name \"${RF_AKS_NAME}\" --command \"kubectl get namespace\""
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl get namespace" 2>&1 >$TMPFILE
checkExitCodeAndExit $TMPFILE
echo "---------------------------------------"

if [ "$ENABLE_ADMIN_SERVICE" == "true" ]; then
  echo "Create Admin service Load Balancer"
  # update the DNS name placeholder with the value for partner
  sed -i "s/$DNS_LABEL_PLACEHOLDER_VALUE/$RF_AKS_NAME/" "./AdminService/service.yaml"
  echo "az aks command invoke --resource-group \"${RF_REGIONAL_RESOURCE_GROUP}\" --name \"${RF_AKS_NAME}\" --command \"kubectl apply -f ./AdminService/service.yaml\" --file ."
  az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl apply -f ./AdminService/service.yaml" --file . 2>&1 >$TMPFILE

  checkExitCodeAndExit $TMPFILE
  echo "---------------------------------------"
fi

echo "Create ServiceAccount"

echo "az aks command invoke --resource-group \"${RF_REGIONAL_RESOURCE_GROUP}\" --name \"${RF_AKS_NAME}\" --command \"helm upgrade --install --atomic --timeout 1200s --force -f ./BaseValueFiles/rfServices.yaml -f ${VALUES_FILENAME} serviceaccount ServiceAccount\" --file ."
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "helm upgrade --install --atomic --timeout 1200s --force -f ./BaseValueFiles/rfServices.yaml -f ${VALUES_FILENAME} serviceaccount ServiceAccount" --file . 2>&1 >$TMPFILE
checkExitCodeAndExit $TMPFILE
echo "---------------------------------------"

echo "Internal Load Balancer and Private Link Service"

echo "az aks command invoke --resource-group \"${RF_REGIONAL_RESOURCE_GROUP}\" --name \"${RF_AKS_NAME}\" --command \"kubectl apply -f service.yaml\" --file ."
az aks command invoke --resource-group "${RF_REGIONAL_RESOURCE_GROUP}" --name "${RF_AKS_NAME}" --command "kubectl apply -f service.yaml" --file . 2>&1 >$TMPFILE
checkExitCodeAndExit $TMPFILE
echo "---------------------------------------"

echo "Create Federated Credential"

echo "az identity federated-credential create --name resource-fetcher-service-federated-${RegionAcronym} --identity-name \"${RF_MI_NAME}\" --resource-group \"${RF_SUB_RG}\" --issuer ${AKS_OIDC_ISSUER} --subject system:serviceaccount:resource-fetcher-namespace:resource-fetcher-identity"
az identity federated-credential create --name resource-fetcher-service-federated-${RegionAcronym} --identity-name "${RF_MI_NAME}" --resource-group "${RF_SUB_RG}" --issuer ${AKS_OIDC_ISSUER} --subject system:serviceaccount:resource-fetcher-namespace:resource-fetcher-identity

echo "---------------------------------------"
