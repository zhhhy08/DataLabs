param (
    $partner_name
)
 
$PARTNER_INFORMATION = @{
    bcdr=@{
        partner_shortname="abc"
        cloud="prod"
        subscription="caf615cd-215f-4706-8a49-600ecdfc59dc"
        regions=@("wu3", "eus", "neu", "sdc")
    }
    idMapping=@{
        partner_shortname="idm"
        cloud="prod"
        subscription="9b776e32-83f7-4e98-b234-f43612dea78d"
        regions=@("sea", "ea", "wu3", "eus", "neu", "sdc")
    }
    sku=@{
        partner_shortname="sku"
        cloud="prod"
        subscription="78e5e697-0cb0-4da8-9f0d-b36400fe6bce"
        regions=@("sea", "ea", "wu3", "eus", "neu", "sdc")
    }
    capabilities=@{
        partner_shortname="cap"
        cloud="prod"
        subscription="75c6bdbd-d177-465c-bd3c-b340c1333167"
        regions=@("sea", "ea", "wu3", "eus", "neu", "sdc")
    }
    resourceAlias=@{
        partner_shortname="ras"
        cloud="prod"
        subscription="8956daf3-20ca-419f-bb01-aab21c6a63f4"
        regions=@("sea", "ea", "wu3", "eus", "neu", "sdc")
    }
}
 
$PRIMARY_BACKUP_REGIONS = @{
    sea="ea"
    ea="sea"
    wu3="eus"
    eus="wu3"
    neu="sdc"
    sdc="neu"
}
 
$REGION_MAPPING = @{
    sea="southeastasia"
    ea="eastasia"
    wu3="westus3"
    eus="eastus"
    neu="northeurope"
    sdc="swedencentral"
}
 
try {
    echo $PARTNER_INFORMATION[$partner_name]
}
catch {
    echo "Error: Partner Information not recognized. Please use the short name of the partner in all capital letters (i.e. IDM)"
}
 
$partnerValuesFilePrefix=$partner_name
 
$partner_info=$PARTNER_INFORMATION[$partner_name]
$partner=$partner_info["partner_shortname"]
$subscription=$partner_info["subscription"]
$cloud=$partner_info["cloud"]
 
echo "partner = $partner\nsubscription = $subscription\n cloud = $cloud\n"
 
foreach($region in $partner_info["regions"]) {
    $backup_region=$PRIMARY_BACKUP_REGIONS[$region]
    $region_longname=$REGION_MAPPING[$region]
    $backup_region_longname=$REGION_MAPPING[$backup_region]
 
    echo "$region region"
 
    Select-AzSubscription $subscription
 
    $AKS_ResourceGroupName = "DataLabs${partner}RG-$region_longname"
    $AKS_Name = "${partner}${cloud}${region}aks"
    # Cluster info
    $cluster = Get-AzAksCluster -ResourceGroupName $AKS_ResourceGroupName -Name $AKS_Name
    $aks_objectid = $cluster.IdentityProfile.kubeletidentity.objectId
    $aks_id = $cluster.IdentityProfile.kubeletidentity.ResourceId
    $agentpool = $cluster.AgentPoolProfiles[0].Name
 
    $MI_ResourceGroupName = "DataLabs${partner}RG"
    $MI_Name = "${partner}${cloud}ioconnectorid"
    $mi_service_account_clientid = (Get-AzUserAssignedIdentity -Name $MI_Name -ResourceGroup $MI_ResourceGroupName).clientId
 
    $uppercase_cloud = $cloud.Substring(0, 1).ToUpper() + $cloud.Substring(1)
 
    $newvars = @{
        '\$\{subscription\}' = $subscription
        '\$\{region\}' = $region
        '\$\{region_longname\}' = $region_longname
        '\$\{cloud\}' = $cloud
        '\$\{mi_service_account_clientid\}' = $mi_service_account_clientid
        '\$\{aks_objectid\}' = $aks_objectid
        '\$\{uppercase_cloud\}' = $uppercase_cloud
        '\$\{aks_id\}' = $aks_id
        '\$\{backup_region\}' = $backup_region
        '\$\{backup_region_longname\}' = $backup_region_longname
        '\$\{agentpoolName\}' = $agentpool
    }
 
    $template = "${partnerValuesFilePrefix}Values_${uppercase_cloud}_template.yaml"
    $destinationFile = "${partnerValuesFilePrefix}Values_${uppercase_cloud}_${region}.yaml"
 
    $data = @()
    foreach($line in Get-Content $template) {
        foreach($key in $newvars.Keys) {
            if ($line -match $key) {
                $line = $line -replace $key, $newvars[$key]
            }
        }
        $data += $line
    }
 
    $data | Out-File "../$destinationFile"
}
 