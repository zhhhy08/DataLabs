
param
(
    [array]$Partners,
    [array]$ArgScaleSetMIPrincipalIds
)

. .\Utils\CommonFunctions.ps1

$rolesToAssign = @(
    "Azure Event Hubs Data Owner", 
    "Storage Account Contributor", 
    "Storage Blob Data Contributor", 
    "Storage Queue Data Contributor"
)

$subPath = "/subscriptions/"

foreach ($eachpartner in $partners) {
    $subid = $eachpartner.subscriptionId
    $partner = $eachpartner.partnerAcronym
    foreach( $location in $eachpartner.locations) {

        Select-AzSubscription -Subscription $subId

        $regionalPartnerRGName = $subPath + $subId + "/resourceGroups/DataLabs" + $partner + "RG-" + $location

        Write-Host "Adding $rolesToAssign for $regionalPartnerRGName "

        foreach ($argScaleSetMIPrincipalId in $ArgScaleSetMIPrincipalIds) {
            foreach ($role in $rolesToAssign) {
                $ra = @{
                    ObjectId = $argScaleSetMIPrincipalId
                    RoleDefinitionName = $role
                    Scope = $regionalPartnerRGName
                }

                New-ArgAZRoleAssignment  @ra
            }
        }
    }
}
