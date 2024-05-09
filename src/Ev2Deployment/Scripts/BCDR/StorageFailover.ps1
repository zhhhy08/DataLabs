# This is for disaster discovery when source of truth storage accounts are completely unreachable in a region due to an outage causing Datalabs to be affected.
# IMPORTANT: must be executed as a last resort ONLY
# This script is to failover the  source of truth storage accounts in Datalabs
# TODO: add TSG link

param
(
    [ValidateSet("Int", "Canary", "Prod")]
	[Parameter(Mandatory=$true)]
    [string]$partnerOnboardedToEnv,

    [Parameter(Mandatory=$true)]
    [string]$partnerAcronym,
    
    [ValidateSet("int","eus","neu", "sdc", "sea", "ea", "wu3", "ecy")]
	[Parameter(Mandatory=$true)]
    [string]$regionAcronym,

    [bool] $dryRun = $true
)

function FetchStorages
{
    param
    (
	    [Parameter(Mandatory=$true)]
        [string]$rgName
    )
    
    return Get-AzResource -ResourceGroupName  $rgName | Where-Object {$_.ResourceName.Contains("sotsa")}
}

function ValidateStorageFailoverComplete
{
    param
    (
	    [Parameter(Mandatory=$true)]
        [string]$rgName
    )

    Write-Host "All storage accounts are now in the process of failing over."
    $global:jobs | Wait-Job

    $storages = FetchStorages $rgName
    foreach($storage in $storages)
    {
        $canFailover = (Get-AzStorageAccount -ResourceGroupName  $rgName -Name $storage.ResourceName -IncludeGeoReplicationStats).GeoReplicationStats.CanFailover 

        if($null -ne $canFailover)
        {
            Write-Host "`r`n$($storage.Id) has failed migration.`r`n"  
        }
    }
}

function FailoverStorages
{
    param
    (
        [Parameter(Mandatory=$true)]
        [string]$rgName
    ) 

    $storages = @(FetchStorages $rgName)
    $storagesReadyForFailover = @()
    $storagesAlreadyFailedOver = @()
    $maxWaitIterations = 30

    while($storages.Count -gt 0 -and $maxWaitIterations -gt 0)
    {
        Write-Host "Waiting 60 seconds.`r`n"
        Start-Sleep -Seconds 60 
        $maxWaitIterations--

        Write-Host "Checking which storage accounts are ready for failover. $($storages.Count) storage accounts still need failover."
        foreach($storage in $storages)
        {
        
            $lastSyncTime = (Get-AzStorageAccount -ResourceGroupName  $rgName -Name $storage.ResourceName -IncludeGeoReplicationStats).GeoReplicationStats.LastSyncTime.DateTime 2>$null

            # Storages that have already failed over become LRS and therefore don't have a last sync time
            if($null -eq $lastSyncTime)
            {
                Write-Host "$($storage.Id) has already failed over. Removing from list."
                $storagesAlreadyFailedOver += $storage         
            }

            if($scriptStartTime -lt $lastSyncTime)
            {    
                $storagesReadyForFailover += $storage 
            }
       
        }
        
        Write-Host "$($storagesReadyForFailover.Count) storage accounts ready for failover on this iteration."
        foreach($storageReadyForFailover in $storagesReadyForFailover)
        {
            Write-Host "Starting storage failover for $($storageReadyForFailover.Id)."
            $storages = $storages -ne $storageReadyForFailover
            $job = Invoke-AzStorageAccountFailover -ResourceGroupName $rgName -Name $storageReadyForFailover.ResourceName -Force -AsJob
            $global:jobs += $job
        }
        foreach($storageFailedOver in $storagesAlreadyFailedOver)
        {
            $storages = $storages -ne $storageFailedOver
        }
        $storagesAlreadyFailedOver = @()
        $storagesReadyForFailover = @()
    }

    if($maxWaitIterations -le 0)
    {
        Write-Host "Maximum wait time reached."
    }
}

if($partnerOnboardedToEnv -eq "Int")
{
    . ..\..\Inputs\PartnerDetails\IntPartners.ps1
    $regionAcronymList = @("int")
} 
elseif($partnerOnboardedToEnv -eq "Canary")
{
    . ..\..\Inputs\PartnerDetails\CanaryPartners.ps1
    $regionAcronymList = $regionAcronyms
} 
elseif($partnerOnboardedToEnv -eq "Prod")
{
    . ..\..\Inputs\PartnerDetails\ProdPartners.ps1
    $regionAcronymList = $regionAcronyms + $asiaRegionAcronyms
} 

if($regionAcronymList -notcontains $regionAcronym)
{
    Write-Error "Region is not accepted in this environment, double check the partnerOnboardedToEnv and regionAcronym"
    return
}

$regionToLocationName = @{
  int="eastus"
  eus="eastus"
  neu="northeurope"
  sdc="swedencentral"
  sea="southeastasia"
  ea="eastasia"
  wu3="westus3"
  ecy="eastus2euap"
}

$scriptStartTime = (Get-Date).ToUniversalTime()
Write-Host "Script start time: $($scriptStartTime) UTC"

$regionAcronym = $regionAcronym.ToLower()
$subId = ""
Write-Host "Partners onboarded in this environment" $partners.Count 

foreach ($partner in $partners) {
    if($partner.partnerAcronym -eq $partnerAcronym)
    {
        $subId = $partner.subscriptionId
    }
}

$storageResourceGroup = "DataLabs"+$partnerAcronym+"RG-"+$regionToLocationName[$regionAcronym]

if($subId -eq "")
{
    Write-Error "Subscription Id is empty, please check the partner acronym"
    return;
}

Write-Host "Starting failover for resource group - " $storageResourceGroup " for partner " $partnerAcronym " in subscription " $subId

Connect-AzAccount -Subscription $subId

$global:jobs = @()

if($dryRun)
{
    Write-Host "Dry run is enabled, no operations are done for failover"
    $storages = @(FetchStorages $storageResourceGroup)
    Write-Host "Storages count:" $storages.Count
    foreach($storage in $storages)
    {
        Write-Host "Storage name :" $storage.Name
    }
} 
else 
{
    Write-Host "THIS IS NOT A DRY RUN, press yes to continue, no to exit." -BackgroundColor DarkRed
    	
    $title    = 'Confirm'
	$question = "Do you want to failover source of truth storage accounts in "+ $storageResourceGroup +" for partner "+ $partnerAcronym +" in subscription "+ $subId +"?"
	$choices  = '&Yes', '&No'

	$decision = $Host.UI.PromptForChoice($title, $question, $choices, 1)

	if ($decision -eq 0) {
	    Write-Host "Failng over storages"	
        FailoverStorages $storageResourceGroup
        ValidateStorageFailoverComplete $storageResourceGroup
	} 
}

Write-Host "Script end time: $((Get-Date).ToUniversalTime()) UTC"