param (
    $subscription,
    $region,
    $region_longname,
    $cloud,
    $idm_subscription,
    $abc_subscription,
    $cap_subscription
)

Select-AzSubscription $subscription

$AKS_ResourceGroupName = "DataLabsRFRG-$region_longname"
$AKS_Name = "rf${cloud}${region}aks"
$aks_objectid = (Get-AzAksCluster -ResourceGroupName $AKS_ResourceGroupName -Name $AKS_Name).IdentityProfile.kubeletidentity.objectId
$azurekeyvaultsecretsprovider_clientId = (Get-AzResource -ResourceGroupName $AKS_ResourceGroupName -Name $AKS_Name -ExpandProperties).Properties.addonProfiles.azureKeyvaultSecretsProvider.identity.clientId

$MI_Name = "rf${cloud}id"
$MI_ResourceGroupName = "DataLabsRFRG"
$rf_serviceAccount_clientId = (Get-AzUserAssignedIdentity -Subscription $subscription -Name $MI_Name -ResourceGroup $MI_ResourceGroupName).clientId

# Get Partner Service ClientId
$ABC_MI_Name = "abc${cloud}ioconnectorid"
$ABC_MI_ResourceGroupName = "DataLabsabcRG"
Select-AzSubscription $abc_subscription
$abc_serviceAccount_clientId = (Get-AzUserAssignedIdentity -Subscription $abc_subscription -Name $ABC_MI_Name -ResourceGroup $ABC_MI_ResourceGroupName).clientId

$IDM_MI_Name = "idm${cloud}ioconnectorid"
$IDM_MI_ResourceGroupName = "DataLabsidmRG"
Select-AzSubscription $idm_subscription
$idm_serviceAccount_clientId = (Get-AzUserAssignedIdentity -Subscription $idm_subscription -Name $IDM_MI_Name -ResourceGroup $IDM_MI_ResourceGroupName).clientId

$CAP_MI_Name = "cap${cloud}ioconnectorid"
$CAP_MI_ResourceGroupName = "DataLabscapRG"
Select-AzSubscription $cap_subscription
$cap_serviceAccount_clientId = (Get-AzUserAssignedIdentity -Subscription $cap_subscription -Name $CAP_MI_Name -ResourceGroup $CAP_MI_ResourceGroupName).clientId

Select-AzSubscription $subscription
$uppercaseCloud = $cloud.Substring(0, 1).ToUpper() + $cloud.Substring(1)

$newvars = @{
    '\$\{subscription\}' = $subscription
    '\$\{region\}' = $region
    '\$\{region_longname\}' = $region_longname
    '\$\{cloud\}' = $cloud
    '\$\{idm_serviceAccount_clientId\}' = $idm_serviceAccount_clientId
    '\$\{abc_serviceAccount_clientId\}' = $abc_serviceAccount_clientId
    '\$\{cap_serviceAccount_clientId\}' = $cap_serviceAccount_clientId
    '\$\{rf_serviceAccount_clientId\}' = $rf_serviceAccount_clientId
    '\$\{azurekeyvaultsecretsprovider_clientId\}' = $azurekeyvaultsecretsprovider_clientId
    '\$\{aks_objectid\}' = $aks_objectid
    '\$\{uppercaseCloud\}' = $uppercaseCloud
}

$template = "values_${uppercaseCloud}_template.yaml"
$destinationFile = "values_${uppercaseCloud}_${region}.yaml"

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