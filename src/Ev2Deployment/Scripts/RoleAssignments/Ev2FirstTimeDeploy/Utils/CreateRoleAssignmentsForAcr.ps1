
param
(
    [object]$datalabs,
    [array]$partners,
    [string]$CloudName
)

. .\Utils\CommonFunctions.ps1
$RegionMap = Import-Csv ..\..\..\Inputs\data\RegionMap.csv

$datalabsAcrResourceId = "/subscriptions/" + $datalabs.subscriptionId + "/resourceGroups/DataLabsRG/providers/Microsoft.ContainerRegistry/registries/datalabs" + $CloudName + "acr"
$acrPullRole = "AcrPull"


foreach ($location in  $datalabs.locations) {

    $regionAcronym = ($RegionMap | Where-Object { $_.region -eq $location }).Acronym

    #DataLabsRFRG-$location()
    $regionalResourceFetcherRG = "DataLabsRFRG-" + $location
    $rfAksName = "rf" + $CloudName + $regionAcronym + "aks" 
   
    Write-Host "Resource Fetcher RG: $regionalResourceFetcherRG aks name: $rfAksName"
    
    Select-AzSubscription -Subscription $datalabs.subscriptionId
    Import-AzAksCredential -ResourceGroupName $regionalResourceFetcherRG -Name $rfAksName   -SubscriptionId $datalabs.subscriptionId -Force
    $rf = Get-AzAksCluster -ResourceGroupName $regionalResourceFetcherRG -Name $rfAksName -SubscriptionId $datalabs.subscriptionId
    $raRf = @{
        ObjectId           = $rf.IdentityProfile.kubeletidentity.ObjectId
        RoleDefinitionName = $acrPullRole
        Scope              = $datalabsAcrResourceId
    }
    New-ArgAZRoleAssignment @raRf
}


foreach ($eachpartner in $partners) {
    $subid = $eachpartner.subscriptionId
    $partner = $eachpartner.partnerAcronym
    foreach ($location in  $eachpartner.locations) {

        $regionAcronym = ($RegionMap | Where-Object { $_.region -eq $location }).Acronym

        #DataLabs$config(stamp_$stamp().partner.partnerAcronym)RG-$location()
        $partnerRegionalRG = "DataLabs" + $partner + "RG-" + $location
        $partnerAksName = $partner + $CloudName + $regionAcronym + "aks" 

        Write-Host "$partner RG: $partnerRegionalRG  aks name : $partnerAksName"
        
        Select-AzSubscription -Subscription $subId
        Import-AzAksCredential -ResourceGroupName $partnerRegionalRG -Name $partnerAksName -SubscriptionId $subId  -Force
        $p = Get-AzAksCluster -ResourceGroupName $partnerRegionalRG -Name $partnerAksName -SubscriptionId  $subId
        $raPartner = @{
            ObjectId           = $p.IdentityProfile.kubeletidentity.ObjectId
            RoleDefinitionName = $acrPullRole
            Scope              = $datalabsAcrResourceId
        }
        New-ArgAZRoleAssignment @raPartner
    }
}

