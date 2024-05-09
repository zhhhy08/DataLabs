#!/bin/bash
set -e # stop the script at the first error

source ./Common.sh

loginAndUnzip ${CHARTS_TAR}

echo "---------------------------------------"
COMPONENT="ResourceFetcher"
RESOURCE_FETCHER_CHARTS_DIR="ResourceFetcherAKS"

RF_SUBSCRIPTION=${SubscriptionId}
RF_REGIONAL_RESOURCE_GROUP="DataLabsRFRG-${DataLabsLocation}"
RF_AKS_NAME="rf${CloudName}${RegionAcronym}aks"

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

case ${AppName} in

MonitorService)

    deployApp $CloudName $RF_REGIONAL_RESOURCE_GROUP $RF_AKS_NAME $COMPONENT "monitor-namespace" $AppName $ValuesFilename

    ;;

ResourceFetcherService)

    deployApp $CloudName $RF_REGIONAL_RESOURCE_GROUP $RF_AKS_NAME $COMPONENT "resource-fetcher-namespace" $AppName $ValuesFilename

    ;;

AdminService)
    deployApp $CloudName $RF_REGIONAL_RESOURCE_GROUP $RF_AKS_NAME $COMPONENT "admin-namespace" $AppName $ValuesFilename

    ;;

esac
