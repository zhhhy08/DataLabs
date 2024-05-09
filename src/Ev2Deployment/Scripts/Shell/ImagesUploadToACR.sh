#!/bin/bash
set -e # stop the script at the first error

echo "Folder Contents"
ls

if [ -z ${DESTINATION_ACR_NAME+x} ]; then
    echo "DESTINATION_ACR_NAME is unset, unable to continue"
    exit 1
fi

if [ -z ${TAG_NAME+x} ]; then
    echo "TAG_NAME is unset, unable to continue"
    exit 1
fi

## These image names and repo names should be consistent with the imagebuildinfo@1 tasks of the official yml.
## also the relative order of these arrays must match image-wise.
declare -a zipTarFiles=(
    "garnetserver.tar.gz"
    "inputoutputservice.tar.gz"
    "resourcefetcherproxyservice.tar.gz"
    "resourcefetcherservice.tar.gz"
    "socat.tar.gz"
    "abcpartnersolution.tar.gz"
    "azorespartnersolution.tar.gz"
    "idmpartnersolution.tar.gz"
    "raspartnersolution.tar.gz"
    "skupartialsyncpartnersolution.tar.gz"
    "skufullsyncpartnersolution.tar.gz"
    "armdatacachepartnersolution.tar.gz" 
    "cappartnersolution.tar.gz"
    "adminservice.tar.gz"
)
declare -a tarFiles=(
    "garnetserver.tar"
    "inputoutputservice.tar"
    "resourcefetcherproxyservice.tar"
    "resourcefetcherservice.tar"
    "socat.tar"
    "abcpartnersolution.tar"
    "azorespartnersolution.tar"
    "idmpartnersolution.tar"
    "raspartnersolution.tar"
    "skupartialsyncpartnersolution.tar"
    "skufullsyncpartnersolution.tar"
    "armdatacachepartnersolution.tar"
    "cappartnersolution.tar"
    "adminservice.tar"
)
declare -a repos=(
    "garnetserver"
    "inputoutputservice"
    "resourcefetcherproxyservice"
    "resourcefetcherservice"
    "socat"
    "abcpartnersolution"
    "azorespartnersolution"
    "idmpartnersolution"
    "raspartnersolution"
    "skupartialsyncpartnersolution"
    "skufullsyncpartnersolution"
    "armdatacachepartnersolution"
    "cappartnersolution"
    "adminservice"
)
declare -a sas=(
    ${TARBALL_IMAGE_FILE_SAS_GARNETSERVER}
    ${TARBALL_IMAGE_FILE_SAS_INPUTOUTPUTSERVICE}
    ${TARBALL_IMAGE_FILE_SAS_RESOURCEFETCHERPROXYSERVICE}
    ${TARBALL_IMAGE_FILE_SAS_RESOURCEFETCHERSERVICE}
    ${TARBALL_IMAGE_FILE_SAS_SOCAT}
    ${TARBALL_IMAGE_FILE_SAS_ABCPARTNERSOLUTION}
    ${TARBALL_IMAGE_FILE_SAS_AZRPARTNERSOLUTION}
    ${TARBALL_IMAGE_FILE_SAS_IDMPARTNERSOLUTION}
    ${TARBALL_IMAGE_FILE_SAS_RASPARTNERSOLUTION}
    ${TARBALL_IMAGE_FILE_SAS_SKUPARTIALSYNCPARTNERSOLUTION}
    ${TARBALL_IMAGE_FILE_SAS_SKUFULLSYNCPARTNERSOLUTION}
    ${TARBALL_IMAGE_FILE_SAS_ARMDATACACHEPARTNERSOLUTION}
    ${TARBALL_IMAGE_FILE_SAS_CAPPARTNERSOLUTION}
    ${TARBALL_IMAGE_FILE_SAS_ADMINSERVICE}
)

len=${#zipTarFiles[@]}

#apt update
#apt-get install -y unzip wget gzip

echo "Login cli using managed identity"
az login --identity

TMP_FOLDER=$(mktemp -d)
cd "$TMP_FOLDER"

echo "Downloading docker tarball images..."
# now loop through the above array
for ((i = 0; i < len; i++)); do
    echo "Downloading docker tarball images... from ${sas[$i]}"
    FILE_TO_DOWNLOAD="${sas[$i]}"
    wget -O "${zipTarFiles[$i]}" "$FILE_TO_DOWNLOAD"
done

echo "Folder Contents after zipped images download"
ls

echo "Getting acr credentials"
TOKEN_QUERY_RES=$(az acr login -n "$DESTINATION_ACR_NAME" -t)
TOKEN=$(echo "$TOKEN_QUERY_RES" | jq -r '.accessToken')
DESTINATION_ACR=$(echo "$TOKEN_QUERY_RES" | jq -r '.loginServer')
/package/unarchive/crane auth login "$DESTINATION_ACR" -u "00000000-0000-0000-0000-000000000000" -p "$TOKEN"

for ((i = 0; i < len; i++)); do
    DEST_IMAGE_FULL_NAME="$DESTINATION_ACR_NAME.azurecr.io/${repos[$i]}:$TAG_NAME"

    if [[ "${zipTarFiles[$i]}" == *"tar.gz"* ]]; then
        gunzip "${zipTarFiles[$i]}"
    fi

    echo "Pushing file ${tarFiles[$i]} to $DEST_IMAGE_FULL_NAME"
    /package/unarchive/crane push "${tarFiles[$i]}" "$DEST_IMAGE_FULL_NAME"
done

echo "Folder Contents after gunzipped images which got pushed to ACR"
ls
