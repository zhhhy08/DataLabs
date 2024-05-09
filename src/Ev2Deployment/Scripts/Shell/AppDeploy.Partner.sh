#!/bin/bash
set -e # stop the script at the first error

source ./Common.sh

loginAndUnzip ${CHARTS_TAR}

echo "---------------------------------------"
COMPONENT="Partner"
PARTNER_CHARTS_DIR="PartnerAKS"

PARTNER_SUBSCRIPTION=${SubscriptionId}
PARTNER_REGIONAL_RESOURCE_GROUP="DataLabs${PartnerAcronym}RG-${DataLabsLocation}"
PARTNER_AKS_NAME="${PartnerAcronym}${CloudName}${RegionAcronym}aks"

echo "Set subscription ${PARTNER_SUBSCRIPTION}"
az account set --subscription "${PARTNER_SUBSCRIPTION}"
echo "---------------------------------------"

#  move to partneraks directory
echo "moving to ${PARTNER_CHARTS_DIR} and printing contents..."
cd ${PARTNER_CHARTS_DIR}
ls
echo "---------------------------------------"

#  connect to AKS get-credentials
echo "connecting to ${PARTNER_AKS_NAME}"
echo "az aks get-credentials --resource-group ${PARTNER_REGIONAL_RESOURCE_GROUP} --name ${PARTNER_AKS_NAME}"
az aks get-credentials --resource-group "${PARTNER_REGIONAL_RESOURCE_GROUP}" --name "${PARTNER_AKS_NAME}"
echo "---------------------------------------"

case ${AppName} in

MonitorService)

    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "monitor-namespace" $AppName $ValuesFilename

    ;;

PartnerService)

    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "partner-namespace" $AppName $ValuesFilename

    ;;

IOService)

    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "solution-namespace" $AppName $ValuesFilename

    ;;

CacheService)

    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "cache-namespace" $AppName $ValuesFilename 0
    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "cache-namespace" $AppName $ValuesFilename 1
    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "cache-namespace" $AppName $ValuesFilename 2

    ;;

PartnerCacheService)

    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "cache-namespace" $AppName $ValuesFilename 0
    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "cache-namespace" $AppName $ValuesFilename 1
    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "cache-namespace" $AppName $ValuesFilename 2

    ;;

ResourceProxy)

    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "solution-namespace" $AppName $ValuesFilename

    ;;

AdminService)

    deployApp $CloudName $PARTNER_REGIONAL_RESOURCE_GROUP $PARTNER_AKS_NAME $COMPONENT "admin-namespace" $AppName $ValuesFilename

    ;;

esac
