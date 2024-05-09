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
    [ValidateSet('IOService', 'PartnerService', 'ResourceProxy', 'ResourceFetcherService', IgnoreCase = $false)]
    $ServiceName,

    [string]
    [Parameter(Mandatory=$true)]
    $ConfigFileName
)

# Obtaining info from ServiceInfo.json
$INFO = (Get-Content "ServiceInfo.json" -Raw | ConvertFrom-Json)
$ACRONYM_TO_REGIONS = $INFO.ACRONYM_TO_REGIONS
$SERVICE_TO_APPINFO = $INFO.SERVICE_TO_APPINFO

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
$Namespace = $APPINFO.Namespace
$ConfigMap = $APPINFO.ConfigMap
Write-Output "Namespace: $Namespace"
Write-Output "ConfigMap: $ConfigMap"

Push-Location # Save _Scripts Directory
if ( $Partner -eq 'rf' ) 
{
    Write-Output "Set-Location ..\ResourceFetcherAKS\_HotConfig"
    Set-Location ..\ResourceFetcherAKS\_HotConfig
}
else 
{
    Write-Output "Set-Location ..\PartnerAKS\_HotConfig"
    Set-Location ..\PartnerAKS\_HotConfig
}

if ( $CommandType -eq 'azurecli' ) {
    Write-Output "az aks command invoke --resource-group $ResourceGroup --name $Name --command ""kubectl patch configmap $ConfigMap -n $Namespace --type merge --patch-file $ConfigFileName"" --file ." 
    az aks command invoke --resource-group $ResourceGroup --name $Name --command "kubectl patch configmap $ConfigMap -n $Namespace --type merge --patch-file $ConfigFileName" --file .
} 
else 
{
    Write-Output "Invoke-AzAksRunCommand -ResourceGroupName $ResourceGroup -Name $Name -Force -Command ""kubectl patch configmap $ConfigMap -n $Namespace --type merge --patch-file $ConfigFileName"" -CommandContextAttachment ""ActivateBackupChannel.yaml""" 
    Invoke-AzAksRunCommand -ResourceGroupName $ResourceGroup -Name $Name -Force -Command "kubectl patch configmap $ConfigMap -n $Namespace --type merge --patch-file $ConfigFileName" -CommandContextAttachment "$ConfigFileName"
}
Pop-Location # Go back to _Scripts Directory 
