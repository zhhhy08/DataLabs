param
(
    [object]$datalabs,
    [array]$partners,
    [string]$cloudName,
    [array]$argScaleSetMIPrincipals
)

$CreateRoleAssignmentParams = @{
    datalabs  = $datalabs
    partner   = $partners
    cloudName = $cloudName
}
.\Utils\CreateRoleAssignmentsForEv2Scripts @CreateRoleAssignmentParams

.\Utils\CreateRoleAssignmentsForAcr @CreateRoleAssignmentParams

.\Utils\CreateRoleAssignmentsForApplications.ps1 @CreateRoleAssignmentParams 

#First party app is used in Resource Fetcher service, it will fail without this cert at startup

$userAlias = (Get-AzContext).Account.Id

$KVOpsParams = @{
    UserEmail       = $userAlias
    CloudName       = $cloudName
    Datalabs        = $datalabs
    ForceNewVersion = $ForceNewVersion
}

.\Utils\CreateDatalabsCerts.ps1 @KVOpsParams

$ArgScaleSetRoleAssignmentParams = @{
    Partners                  = $partners
    ArgScaleSetMIPrincipalIds = $argScaleSetMIPrincipcals
}

.\Utils\CreateRoleAssignmentsForArgScaleSet.ps1 @ArgScaleSetRoleAssignmentParams
$RegionMap = Import-Csv ..\..\..\Inputs\data\RegionMap.csv

#Create client certificates if required

foreach ($eachpartner in $partners) {
    foreach ($location in  $eachpartner.locations) {
        $acronym = ($RegionMap | Where-Object { $_.region -eq $location }).Acronym
        $PartnerCertsParams = [ordered]@{
            PartnerAcronym  = $eachpartner.partnerAcronym
            UserEmail       = $userAlias
            CloudName       = $cloudName
            Location        = $location
            RegionAcronym   = $acronym
            SubscriptionId  = $eachpartner.subscriptionId
            ForceNewVersion = $ForceNewVersion
        }
        .\Utils\CreatePartnerCerts.ps1 @PartnerCertsParams  
    }
}