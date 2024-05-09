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

    [bool]
    $IgnoreBuild=$false
)

# Obtaining info from ServiceInfo.json
$INFO = (Get-Content "ServiceInfo.json" -Raw | ConvertFrom-Json)
$PARTNER_VALUES_FILE_MAPPING = $INFO.PARTNER_VALUES_FILE_MAPPING
$SERVICE_TO_FOLDER_INFO = $INFO.SERVICE_TO_FOLDER_INFO
$SERVICE_TO_APPINFO = $INFO.SERVICE_TO_APPINFO
$ACRONYM_TO_REGIONS = $INFO.ACRONYM_TO_REGIONS

$CloudLowercase = $Cloud.ToLower()
if ( $Cloud -eq 'local' )
{
    $CloudLowercase = 'int' # Local is in Int environment
}

# Project doesn't need building (MonitorService)
if ((-Not $IgnoreBuild) -And 
    ($ProjectNames = ($SERVICE_TO_FOLDER_INFO | Where-Object { $_.ServiceName -eq $ServiceName }).ProjectNames)) 
{
    if ($ServiceName -eq 'PartnerService')
    {
        $ProjectNames = ($ProjectNames | Where-Object { $_.PartnerAcronym -eq $Partner }).ProjectNames
    }
    Write-Output "Project Names are: $ProjectNames"
    
    foreach ($ProjectName in $ProjectNames) {
        if ( -not (Test-Path "..\..\..\out\Release-x64\$ProjectName") ) {
            Write-Output "..\..\..\out\Release-x64\$ProjectName does not exist. Building Project..."
            
            # Push-Location
            $FolderName = "..\..\DataLabs\$ProjectName"
            if ($ServiceName -eq 'PartnerService')  # Handling PartnerService edge case (file structure different)
            {
                $FolderName = 'PartnerSolutionServices\$ProjectName'
            }

            Write-Output "Set-Location $FolderName"
            Set-Location $FolderName
            Write-Output "dotnet build"
            dotnet build
            
            Write-Output "Returning to _Scripts Folder"
            Pop-Location
        }
        
        if( (docker ps 2>&1 ) -Match '^error' ) 
        {
            Write-Host "Docker is not running. Please start up Docker Desktop"
            exit
        }
        Write-Output "az acr login -n datalabs$($CloudLowercase)acr"
        az acr login -n datalabs$($CloudLowercase)acr
    
        Write-Output "docker build -t datalabs$($CloudLowercase)acr.azurecr.io/$($ProjectName.ToLower()):latest -f ..\..\..\..\out\Release-x64\$ProjectName\Dockerfile ..\..\..\..\out\Release-x64\$ProjectName"
        docker build -t datalabs$($CloudLowercase)acr.azurecr.io/$($ProjectName.ToLower()):latest -f ..\..\..\..\out\Release-x64\$ProjectName\Dockerfile ..\..\..\..\out\Release-x64\$ProjectName
        
        Write-Output "docker push datalabs$($CloudLowercase)acr.azurecr.io/$($ProjectName.ToLower()):latest"
        docker push datalabs$($CloudLowercase)acr.azurecr.io/$($ProjectName.ToLower()):latest
    }
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

$ValuesFileAcronym = ($PARTNER_VALUES_FILE_MAPPING | Where-Object { $_.acronym -eq $Partner }).ValuesFileAcronym
$ValuesFile = "$($ValuesFileAcronym)Values_$($Cloud).yaml"
if ( $Partner -ne 'rf' -And $Cloud -eq 'Prod' )
{
    $ValuesFile = "$($ValuesFileAcronym)Values_$($Cloud)_$Region.yaml"
}
elseif ( $Partner -eq 'rf' -And $Cloud -ne 'Prod' ) # ResourceFetcherAKS, non-Prod values
{
    $ValuesFile = "values_$($Cloud).yaml"
}
elseif ( $Partner -eq 'rf' -And $Cloud -eq 'Prod' ) #ResourceFetcherAKS, Prod values
{
    $ValuesFile = "values_$($Cloud)_$Region.yaml"
}

Write-Output "Values File: $ValuesFile"

$APPINFO = $SERVICE_TO_APPINFO | Where-Object { $_.ServiceName -eq $ServiceName }
$AppName = $APPINFO.AppName
$Namespace = $APPINFO.Namespace
Write-Output "AppName: $AppName"
Write-Output "Namespace: $Namespace"

Push-Location # Save _Scripts Directory
Write-Output "Set-Location .."
Set-Location ..

if ( $Partner -eq 'rf' ) 
{
    $AKSFolder = "ResourceFetcherAKS"
}
else 
{
    $AKSFolder = "PartnerAKS"
}

if ( $CommandType -eq 'azurecli' ) {
    Write-Output "az aks command invoke --resource-group $ResourceGroup --name $Name --command ""chmod +x _KubectlScripts/*.sh; _KubectlScripts/reinstall_service_helper.sh $Cloud $ValuesFile $ServiceName $Namespace $AppName $AKSFolder"" --file ." 
    az aks command invoke --resource-group $ResourceGroup --name $Name --command "chmod +x _KubectlScripts/*.sh; _KubectlScripts/reinstall_service_helper.sh $Cloud $ValuesFile $ServiceName $Namespace $AppName $AKSFolder" --file .
} 
else 
{
    Write-Output "az aks command invoke --resource-group $ResourceGroup --name $Name --command ""chmod +x _KubectlScripts/*.sh; _KubectlScripts/reinstall_service_helper.sh $Cloud $ValuesFile $ServiceName $Namespace"" --file ." 
    Invoke-AzAksRunCommand -ResourceGroupName $ResourceGroup -Name $Name -Command "chmod +x _KubectlScripts/*.sh; _KubectlScripts/reinstall_service_helper.sh $Cloud $ValuesFile $ServiceName $Namespace $AppName $AKSFolder" -Force -CommandContextAttachment "."
}
Pop-Location # Go back to _Scripts Directory 
