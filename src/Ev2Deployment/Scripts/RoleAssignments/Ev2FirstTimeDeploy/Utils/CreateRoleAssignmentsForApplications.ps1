
param
(
    [object]$datalabs,
    [array]$partners,
    [string]$CloudName
)


Function New-ArgAZRoleAssignmentForApps {
    param
    (
        [string]$ObjectId,
        [string]$RoleDefinitionName,
        [string]$Scope
    )

    
    Write-Host -BackgroundColor Cyan "Executing - Get-AzRoleAssignment -ObjectId  $ObjectId -RoleDefinitionName  $RoleDefinitionName  -Scope $Scope" 

    $existingRoleAssignment = Get-AzRoleAssignment -ObjectId $ObjectId -RoleDefinitionName $RoleDefinitionName -Scope $Scope

    Write-Host -BackgroundColor DarkYellow "Listing all current role assignments scope for this ObjectId  $ObjectId RoleDefinitionName  $RoleDefinitionName " 

    $existingRoleAssignment | ForEach-Object { Write-Host $_.Scope -BackgroundColor DarkYellow }

    $createRA = $true;

    if ($existingRoleAssignment.count -gt 0) {
        # if the existing role assignment already has a RA with the exact scope of resource group level, do not try to create another one.
        $existingRoleAssignment | ForEach-Object { if ($_.Scope -eq $Scope) { $createRA = $false } }
        
    }

    "Create new role assignment? " + $createRA

    if ($createRA) {
        Write-Host "New-AzRoleAssignment -ObjectId $ObjectId -RoleDefinitionName $RoleDefinitionName -Scope $Scope "
        New-AzRoleAssignment -ObjectId $ObjectId -RoleDefinitionName $RoleDefinitionName -Scope $Scope 
    }
    else {
        Write-Host "Role assignment already exists" 
        $existingRoleAssignment | ForEach-Object { Write-Host $_.RoleAssignmentId -BackgroundColor DarkGreen }
    }
}


$acrPushRole = "AcrPush"
$eventHubDataOwnerRole = "Azure Event Hubs Data Owner"
$serviceBusDataOwnerRole = "Azure Service Bus Data Owner"
$storageBlobDataOwnerRole = "Storage Blob Data Owner"
$keyVaultSecretsUserRole = "Key Vault Secrets User"
$eventGridDataSenderRole = "EventGrid Data Sender"


#"acrPushRoleId"--- ev2 --- subdatalabs rg

Select-AzSubscription -Subscription $datalabs.subscriptionId
$subPath = "/subscriptions/"
$subscriptionRGName = $subPath + $datalabs.subscriptionId + "/resourceGroups/DataLabsRG"
$datalabsRG_ev2Id = $subscriptionRGName + "/providers/Microsoft.ManagedIdentity/userAssignedIdentities/datalabs" + $cloudName + "ev2id"
$objectIdOfEv2MI = @((Get-AzResource -ResourceId ($DatalabsRG_ev2Id)).Properties.principalId)

Write-Host "Adding $acrPushRole for $subscriptionRGName "

$raRf = @{
    ObjectId           = $objectIdOfEv2MI
    RoleDefinitionName = $acrPushRole
    Scope              = $subscriptionRGName
}

New-ArgAZRoleAssignmentForApps  @rarf
$RegionMap = Import-Csv ..\..\..\Inputs\data\RegionMap.csv

foreach ($location in  $datalabs.locations) {
    $regionAcronym = ($RegionMap | Where-Object { $_.region -eq $location }).Acronym

    # "keyVaultSecretsUserRole" --- rf aks secret client id --- subdatalabs rg
    
    #DataLabsRFRG-$location()
    $regionalResourceFetcherRG = "DataLabsRFRG-" + $location
    $regionalResourceFetcherRGScope = $subPath + $datalabs.subscriptionId + "/resourceGroups/" + $regionalResourceFetcherRG
    $rfAksName = "rf" + $CloudName + $regionAcronym + "aks" 
   
    Write-Host "Resource Fetcher RG: $regionalResourceFetcherRG aks name: $rfAksName"

    Select-AzSubscription -Subscription $datalabs.subscriptionId
    # get object id of azureKeyvaultSecretsProvider of AKS of resource fetcher service
    $r = Get-AzResource -ResourceGroupName $regionalResourceFetcherRG -Name $rfAksName -ExpandProperties
    $secretObjectId = $r.Properties.addonProfiles.azureKeyvaultSecretsProvider.identity.objectId

    $raKV = @{
        ObjectId           = $secretObjectId
        RoleDefinitionName = $keyVaultSecretsUserRole
        Scope              = $subscriptionRGName
    }
    
    New-ArgAZRoleAssignmentForApps  @raKV

    $raKV.Scope = $regionalResourceFetcherRGScope
    New-ArgAZRoleAssignmentForApps  @raKV
}

foreach ($eachpartner in $partners) {
    $subid = $eachpartner.subscriptionId
    $partner = $eachpartner.partnerAcronym
    foreach ( $location in $eachpartner.locations) {
        #"EventHubDataOwnerRoleDefId"--- parter resources MI : ioconnector ---regional partner resources RG
        #"AzureServiceBusDataOwnerRoleDefId" ---parter resources MI : ioconnector---regional partner resources RG
        #"StorageBlobDataOwnerRoleDefId"--- parter resources MI : ioconnector---regional partner resources RG

        Select-AzSubscription -Subscription $subId

        $subPartnerRGName = $subPath + $subId + "/resourceGroups/DataLabs" + $partner + "RG"
        $regionalPartnerRGName = $subPath + $subId + "/resourceGroups/DataLabs" + $partner + "RG-" + $location
        $rgName = "DataLabs" + $partner + "RG-" + $location
        $ioconnectorId = $subPartnerRGName + "/providers/Microsoft.ManagedIdentity/userAssignedIdentities/" + $partner + $cloudName + "ioconnectorid"
        $objectIdOfIoconnectorMI = @((Get-AzResource -ResourceId ($ioconnectorId)).Properties.principalId)

        Write-Host "Adding $eventHubDataOwnerRole, $serviceBusDataOwnerRole, $storageBlobDataOwnerRole, $eventGridDataSenderRole for $regionalPartnerRGName "

        $raEventhub = @{
            ObjectId           = $objectIdOfIoconnectorMI
            RoleDefinitionName = $eventHubDataOwnerRole
            Scope              = $regionalPartnerRGName
        }
        New-ArgAZRoleAssignmentForApps  @raEventhub

        $raServiceBus = @{
            ObjectId           = $objectIdOfIoconnectorMI
            RoleDefinitionName = $serviceBusDataOwnerRole
            Scope              = $regionalPartnerRGName
        }
        New-ArgAZRoleAssignmentForApps  @raServiceBus

        $raStorageBlob = @{
            ObjectId           = $objectIdOfIoconnectorMI
            RoleDefinitionName = $storageBlobDataOwnerRole
            Scope              = $regionalPartnerRGName
        }
        New-ArgAZRoleAssignmentForApps  @raStorageBlob

        $raEventGrid = @{
            ObjectId           = $objectIdOfIoconnectorMI
            RoleDefinitionName = $eventGridDataSenderRole
            Scope              = $regionalPartnerRGName
        }
        New-ArgAZRoleAssignmentForApps  @raEventGrid

        #Create KV Secrets user for Partner AKS
        $regionAcronym = ($RegionMap | Where-Object { $_.region -eq $location }).Acronym
        $partnerAksName = $eachpartner.partnerAcronym + $CloudName + $regionAcronym + "aks"
        Write-Host "Partner Regional RG: $regionalPartnerRGName aks name: $partnerAksName"

        # get object id of azureKeyvaultSecretsProvider of AKS of partner service
        
        $aksResource = Get-AzResource -ResourceGroupName $rgName -Name $partnerAksName -ExpandProperties
        $secretObjectId = $aksResource.Properties.addonProfiles.azureKeyvaultSecretsProvider.identity.objectId

        $raKV = @{
            ObjectId           = $secretObjectId
            RoleDefinitionName = $keyVaultSecretsUserRole
            Scope              = $regionalPartnerRGName
        }
        
        New-ArgAZRoleAssignmentForApps  @raKV
    }
}
