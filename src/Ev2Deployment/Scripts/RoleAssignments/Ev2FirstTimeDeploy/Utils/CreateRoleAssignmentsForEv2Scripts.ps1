param
(
    [object]$datalabs,
    [array]$partners,
    [string]$CloudName
)
. .\Utils\CommonFunctions.ps1

$argAksDeployScriptRoleDefName = "Arg Aks Deploy Script"
$argAksDeployScriptRoleDefDescripion = "Roles for deploying aks one time ops and apps not currently possible via Ev2 for our configuration"
$aksClusterAdminRoleDefinitionName = "Azure Kubernetes Service RBAC Cluster Admin"

$subPath = "/subscriptions/"
$DataLabsSubPath = $subPath + $datalabs.subscriptionId

if ($CloudName -eq "canary") {
    $fileName = "RoleDefinitionArgAksDeployScript.prod.json"
}
else {
    $fileName = "RoleDefinitionArgAksDeployScript." + $cloudName + ".json"
}

Select-AzSubscription $datalabs.subscriptionId

# if does not exist, create
if ( Get-AzRoleDefinition -Name $argAksDeployScriptRoleDefName) {
    "$argAksDeployScriptRoleDefName Exists, Updating role definition"
    Set-AzRoleDefinition -InputFile  ..\$fileName
}
else {
    "$argAksDeployScriptRoleDefName Does not exist, create the role definition"
    #https://learn.microsoft.com/en-us/azure/private-link/rbac-permissions#approval-rbac-for-private-endpoint
    $role = New-Object Microsoft.Azure.Commands.Resources.Models.Authorization.PSRoleDefinition -Property @{
        Name        = $argAksDeployScriptRoleDefName
        Description = $argAksDeployScriptRoleDefDescripion
        IsCustom    = $true
        Actions     = @(
            "Microsoft.ContainerService/managedClusters/runCommand/action",
            "Microsoft.ContainerService/managedClusters/commandResults/read",
            "Microsoft.ContainerService/managedClusters/read",
            "Microsoft.ContainerService/managedClusters/listClusterUserCredential/action",
            "Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials/write",
            "Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials/read",
            "Microsoft.Network/privateDnsZones/*",
            "Microsoft.Network/virtualNetworks/*",
            "Microsoft.Network/virtualNetworks/subnets/*",
            "Microsoft.Network/privateEndpoints/*",
            "Microsoft.Network/networkinterfaces/*",
            "Microsoft.Network/locations/availablePrivateEndpointTypes/read",
            "Microsoft.ApiManagement/service/*",
            "Microsoft.ApiManagement/service/privateEndpointConnections/*",
            "Microsoft.Network/privateLinkServices/*"
        )
    }
    
    $role.AssignableScopes += ($DataLabsSubPath)
    foreach ($partner in $partners) {
        $role.AssignableScopes += ($subPath + $partner.subscriptionId)
    }
    $role.AssignableScopes = $role.AssignableScopes | Select-Object -Unique | Sort-Object
    New-AzRoleDefinition -Role $role -Debug

    "Save the following file to the repo under 'src\Ev2Deployment\Scripts\RoleAssignments' for future updates"

    Get-AzRoleDefinition -Name $argAksDeployScriptRoleDefName | ConvertTo-Json  -Depth 50 | Out-File -FilePath $fileName -Encoding utf8NoBOM
}

# get managed identity
# using one managed identity in one cloud across different partner subscriptions and resource fetcher subscriptions
# because all deployments and one time operations are controlled from ARG team
$DatalabsRG_ev2Id = "/resourceGroups/DataLabsRG/providers/Microsoft.ManagedIdentity/userAssignedIdentities/datalabs"
$objectIdOfEv2MI = @((Get-AzResource -ResourceId ($DataLabsSubPath + $DatalabsRG_ev2Id + $cloudName + "ev2id")).Properties.principalId)

# assign role assignments to 
# assign Arg Aks Deploy Script role assignment to subscription level 

$raRf = @{
    ObjectId           = $objectIdOfEv2MI
    RoleDefinitionName = $argAksDeployScriptRoleDefName
    Scope              = $DataLabsSubPath
}

New-ArgAZRoleAssignment @rarf

foreach ($partner in $partners) {

    $raPartner = @{
        ObjectId           = $objectIdOfEv2MI
        RoleDefinitionName = $argAksDeployScriptRoleDefName
        Scope              = $subPath + $partner.subscriptionId
    }
    New-ArgAZRoleAssignment @rapartner
}

# assign Azure Kubernetes Service RBAC Cluster Admin role  to  regional resource fetcher and all partner resource groups
foreach ($location in $datalabs.locations) {
    #DataLabsRFRG-$location()
    $scope = $DataLabsSubPath + "/resourceGroups/DataLabsRFRG-" + $location
    Write-Host "Resource Fetcher scope: $scope"

    $raRfaks = @{
        ObjectId           = $objectIdOfEv2MI
        RoleDefinitionName = $aksClusterAdminRoleDefinitionName
        Scope              = $scope
    }

    New-ArgAZRoleAssignment @rarfaks
}

foreach ($eachpartner in $partners) {
    $subid = $eachpartner.subscriptionId
    $partner = $eachpartner.partnerAcronym
    foreach ( $location in $eachpartner.locations) {
           
        #DataLabs$config(stamp_$stamp().partner.partnerAcronym)RG-$location()
        $scope = $subPath + $subid + "/resourceGroups/DataLabs" + $partner + "RG-" + $location

        Write-Host "$partner scope: $scope"

        $raPartneraks = @{
            ObjectId           = $objectIdOfEv2MI
            RoleDefinitionName = $aksClusterAdminRoleDefinitionName
            Scope              = $scope
        }

        New-ArgAZRoleAssignment @rapartneraks
    }
}