param
(
    [string]
    [Parameter(Mandatory=$true)]
    [ValidateSet('azurecli', 'powershell')]
    $CommandType,

    [string]
    [Parameter(Mandatory=$true)]
    [ValidateSet('Local', 'Int', 'Canary', 'Prod', IgnoreCase = $false)]
    $Cloud,

    [string]
    [Parameter(Mandatory=$true)]
    [ValidateSet('eus', 'eu2', 'wu3', 'sdc', 'neu', 'sea', 'ea', IgnoreCase = $false)]
    $Region,

    [string]
    [Parameter(Mandatory=$true)]
    [ValidateSet('abc', 'idm', 'pol', 'cap', 'sku', 'RF', IgnoreCase = $false)]
    $Partner,

    [string]
    [Parameter(Mandatory=$true)]
    [ValidateSet('IOService', 'PartnerService', 'ResourceProxy', 'ResourceFetcherService', 'MonitorService', IgnoreCase = $false)]
    $ServiceName,

    [int]
    $LogLength = -1
)

# Obtaining info from ServiceInfo.json
$INFO = (Get-Content "ServiceInfo.json" -Raw | ConvertFrom-Json)
$SERVICE_TO_APPINFO = $INFO.SERVICE_TO_APPINFO
$ACRONYM_TO_REGIONS = $INFO.ACRONYM_TO_REGIONS

$CloudLowercase = $Cloud.ToLower()
if ( $Cloud -eq 'local' )
{
    $CloudLowercase = 'int' # Local is in Int environment
}

$RegionLong = ($ACRONYM_TO_REGIONS | Where-Object { $_.acronym -eq $Region }).region
$ResourceGroup = "DataLabs$($Partner)RG-$RegionLong"
$Name = "$($Partner)$($CloudLowercase)$($Region)aks"

# Overrides (not with Ev2)
if ( $Cloud -eq 'local' -And $Partner -eq 'abc' )
{
    $ResourceGroup = 'abc-eastus'
    $Name = "abc-test-eastus"
}
if ( $Cloud -eq 'local' -And $Partner -eq 'rf' )
{
    $ResourceGroup = 'resourcefetcher-eastus'
    $Name = "resourcefetcher-test-eastus"
}
Write-Output "Resource Group: $ResourceGroup"
Write-Output "Name: $Name"

$APPINFO = $SERVICE_TO_APPINFO | Where-Object { $_.ServiceName -eq $ServiceName }
$AppName = $APPINFO.AppName
$Namespace = $APPINFO.Namespace
Write-Output "AppName: $AppName"
Write-Output "Namespace: $Namespace"

Push-Location # Save _Scripts Directory
Write-Output "Set-Location .."
Set-Location ..

if ( $CommandType -eq 'azurecli' ) {
    Write-Output "az aks command invoke --resource-group $ResourceGroup --name $Name --command ""chmod +x _KubectlScripts/*.sh; _KubectlScripts/describe_pods.sh $Namespace $LogLength $AppName"" --file ." 
    az aks command invoke --resource-group $ResourceGroup --name $Name --command "chmod +x _KubectlScripts/*.sh; _KubectlScripts/describe_pods.sh $Namespace $LogLength $AppName" --file .
}
else 
{
    Write-Output "Invoke-AzAksRunCommand -ResourceGroupName $ResourceGroup -Name $Name -Command ""chmod +x _KubectlScripts/*.sh; _KubectlScripts/describe_pods.sh $Namespace $LogLength $AppName"" -Force -CommandContextAttachment ""."""
    Invoke-AzAksRunCommand -ResourceGroupName $ResourceGroup -Name $Name -Command "chmod +x _KubectlScripts/*.sh; _KubectlScripts/describe_pods.sh $Namespace $LogLength $AppName" -Force -CommandContextAttachment "."
}
Pop-Location # Go back to _Scripts Directory
