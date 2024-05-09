$ModuleInfos = Import-PowerShellDataFile -Path "$PsScriptRoot/Geneva.GitOps.PSModule.psd1"
$version = $ModuleInfos.ModuleVersion

# payload properties that provide the folder names for the various configuration types
# these will exist in the Rollout Parameters files for the various service calls
# sample JSON snippet
# "accountConfigFolderName": {
#     "Value": "AccountConfig"
#  },
$MonitorV1ConfigFolderPropertyName = "monitorV1ConfigurationPath"
$MonitorV2ConfigFolderPropertyName = "monitorV2ConfigurationPath"
$MetricConfigFolderPropertyName = "metricConfigFolderName"
$AccountConfigFolderPropertyName = "accountConfigFolderName"
$TopologyConfigFolderPropertyName = "topologyConfigurationPath"
$HealthMonitorConfigFolderPropertyName = "healthMonitorConfigurationPath"

# sample JSON snippet for this property
# "configurationPackage": {
#     "Reference": {
#       "Path": "Package\\MetricConfigs.zip"
#     }
# },
$ConfigurationPackagePropertyName = "configurationPackage"

<#
.SYNOPSIS
    Validate the path exists and create it if it does not.

.PARAMETER Path
    The folder to test for/create.
#>
Function Test-OrCreatePath {
    param([string] $path)

    if (!(Test-Path ${path})) {
        Write-HostWithInfo -Section "Test-OrCreatePath" -message "Target folder does not exist, creating: ${path}"
        New-Item -ItemType Directory -Force -Path ${path} -ErrorAction Stop | out-null
    }
}

# a function GetFileAsJson that takes a path string as a parameter and returns JSON object
# NOTE: IF YOU UPDATE, update the GetAsJson method in the tests file
Function GetFileAsJson ($path) {
    $content = Get-Content -Raw -LiteralPath $path -ErrorAction Stop

    # powershell versions 6 and above support comments in json files
    if (6 -ile $PSVersionTable.PSVersion.Major) {
        return $content | ConvertFrom-Json
    }

    # remove the comments from the json file
    return $content -replace '(?m)(?<=^([^"]|"[^"]*")*)//.*' -replace '(?ms)/\*.*?\*/' | ConvertFrom-Json
}

Function Write-HostWithInfo {
    param(
        [string] $section,
        [string] $message,
        [bool] $Throw = $false
    )

    Write-Host "[Geneva GitOps][$section] $message"
    if ($Throw) {
        throw $message
    }
}

Function Copy-FoldersWithoutDuplicates {
    param(
        [string] $source,
        [string] $destination
    )

    if (!(Test-Path ${packagesRoot})) {
        New-Item -ItemType Directory -Force -Path ${packagesRoot} | out-null
    }
}

Function Get-FilesWithoutDuplicates {
    param(
        [string] $method,
        [string] $folder,
        [string[]] $files
    )

    if (Test-Path $folder) {
        $_files = Get-ChildItem $folder -Recurse -File
        foreach ($file in $_files) {
            # match from $files where the file name is on the end of the string
            $duplicate = $files | Where-Object { $_ -match "(\\|/)$($file.Name)$" }
            if ($null -ne $duplicate) {
                $full = $file.FullName
                Write-HostWithInfo -Throw $true -Section $method -message "ERROR: Duplicate file found in source folders: ${file} in ${full} and ${duplicate}"
            }
            else {
                $files += $file.FullName
            }
        }
    }
    else {
        Write-HostWithInfo -Section $method -message "Check your source folder list. Folder not found: $folder"
    }

    return $files
}


<#
.SYNOPSIS
    Used to pre-process your pipeline's copy of your repo. It will take your Logs account
    configurations and automatically update the Ev2 artifacts to include the needed
    RolloutSpec, ServiceModel, and ScopeBindings entries.

    This method will need to be called once per unique Service Resource Group in your Service Model.
.PARAMETER sourceRoot
    The root folder for all the geneva configuration files. Normally the GenevaSrc folder.
.PARAMETER serviceGroupRoot
    The root folder for the Ev2 artifacts. Normally the ServiceGroupRoot folder.
.PARAMETER logsRoot
    The root folder for the logs configurations. Normally the GenevaSrc/Logs folder.
.PARAMETER rolloutSpecJsonPath
    The filename (path optional, but will be relative to ServiceGroupRoot) for the RolloutSpec.json file. Normally RolloutSpec.json. It must exist in your ServiceGroupRoot folder.
.PARAMETER scopeBindingsJsonPath
    The filename (path optional, but will be relative to ServiceGroupRoot) for the ScopeBindings.json file. Normally ScopeBindings.json. It must exist in your ServiceGroupRoot folder.
.PARAMETER serviceModelJsonPath
    The filename (path optional, but will be relative to ServiceGroupRoot) for the ServiceModel.json file. Normally ServiceModel.json. It must exist in your ServiceGroupRoot folder.
.PARAMETER serviceResourceDefinitionName
    The name of the instance for the Logs account from your service model. Normally ServiceWebAppLogs.
.PARAMETER extensionType
    The type of the Ev2 Logs extension. Normally Microsoft.Geneva.Logs/ConfigureLogsAccount.
.PARAMETER armParametersPath
    The service group root relative path to the ARM parameters file. Normally Parameters/ArmParams.json.
.PARAMETER targetArmResourceGroupName
    The name of the target Azure Resource Group for the Service Resource Group. "AzureResourceGroupName" 
    in the ServiceModel.json file ServiceResourceGroup section. Used to locate the appropriate ResourceGroup to add to.
.PARAMETER targetArmResourceGroupLocation
    The location of the target Azure Resource Group for the Service Resource Group. "Location" in the 
    ServiceModel.json file ServiceResourceGroup section. Used to locate the appropriate ResourceGroup to add to.
.PARAMETER targetArmSubscriptionId
    The subscription id of the target Azure Resource Group for the Service Resource Group. "AzureSubscriptionId" 
    in the ServiceModel.json file ServiceResourceGroup section. Used to locate the appropriate ResourceGroup to add to.
.PARAMETER targetServiceResourceGroupName
    The name of the target Service Resource Group. "InstanceOf" in the ServiceModel.json file ServiceResourceGroup section.
    Used to locate the appropriate ResourceGroup to add to.
#>
Function Update-Ev2ArtifactsForLogs {
    param(
        [string] $sourceRoot = "./GenevaSrc/",
        [string] $serviceGroupRoot = "./ServiceGroupRoot/",
        [string] $logsRoot = "${sourceRoot}/Logs/",
        [string] $rolloutSpecJsonPath = "RolloutSpec.json",
        [string] $scopeBindingsJsonPath = "ScopeBindings.json",
        [string] $serviceModelJsonPath = "ServiceModel.json",
        [string] $serviceResourceDefinitionName = "ServiceWebAppLogs",
        [string] $extensionType = "Microsoft.Geneva.Logs/ConfigureLogsAccount",
        [string] $armParametersPath = "Parameters/ArmParams.json",
        [string] $targetArmResourceGroupName,
        [string] $targetArmResourceGroupLocation,     
        [string] $targetArmSubscriptionId,       
        [string] $targetServiceResourceGroupName
    )

    $method = $MyInvocation.MyCommand

    Write-HostWithInfo -Section $method -message "Geneva GitOps Module Version: $version"
    $MyInvocation.MyCommand.Parameters.GetEnumerator() | ForEach-Object { Write-HostWithInfo -Section $method -message "$($_.Key) = '$(Get-Variable $_.Key -ValueOnly -EA SilentlyContinue)'" }

    # for this command we want to make sure we have the needed parameters
    # By default powershell will interactively prompt for Manadatory params
    # ADO tasks run in NonInteractive mode, but testing and other usages may not
    if ([string]::IsNullOrEmpty($targetArmResourceGroupName)) {
        Write-HostWithInfo -Throw $true -Section $method -message "targetArmResourceGroupName is a required parameter"
    }
    if ([string]::IsNullOrEmpty($targetArmResourceGroupLocation)) {
        Write-HostWithInfo -Throw $true -Section $method -message "targetArmResourceGroupLocation is a required parameter"
    }
    if ([string]::IsNullOrEmpty($targetArmSubscriptionId)) {
        Write-HostWithInfo -Throw $true -Section $method -message "targetArmSubscriptionId is a required parameter"
    }
    if ([string]::IsNullOrEmpty($targetServiceResourceGroupName)) {
        Write-HostWithInfo -Throw $true -Section $method -message "targetServiceResourceGroupName is a required parameter"
    }

    # validate passed paths actually exist
    if (!(Test-Path ${sourceRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "sourceRoot does not exist: ${sourceRoot}"
    }
    if (!(Test-Path ${serviceGroupRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "serviceGroupRoot does not exist: ${serviceGroupRoot}"
    }
    if (!(Test-Path ${logsRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "logsRoot does not exist: ${logsRoot}"
    }

    Write-HostWithInfo -Section $method -message "Searching for Log accounts in $logsRoot"
    $logsFolders = Get-ChildItem -LiteralPath "$logsRoot" -Attribute D -Depth 1 -ErrorAction SilentlyContinue

    if ($null -eq $logsFolders -or $logsFolders.Length -eq 0) {    
        Write-HostWithInfo -Section $method -message "No Logs accounts found [$Error]"
        return
    }

    # load the files, once, before processing to avoid excessive IO for those with many accounts
    $RolloutSpecJson = GetFileAsJson ${serviceGroupRoot}"/${rolloutSpecJsonPath}" 
    $ScopeBindingsJson = GetFileAsJson ${serviceGroupRoot}"/${scopeBindingsJsonPath}"
    $ServiceModelJson = GetFileAsJson ${ServiceGroupRoot}"/${serviceModelJsonPath}"

    # ensure we have the needed ServiceModel / ServiceResourceGroups
    # Composite key of AZ Resource Group Name, Location, and SubscriptionId
    $matchedResourceGroup = $ServiceModelJson.ServiceResourceGroups | Where-Object { ($_.AzureResourceGroupName -eq $targetArmResourceGroupName -and $_.Location -eq $targetArmResourceGroupLocation -and $_.AzureSubscriptionId -eq $targetArmSubscriptionId -and $_.InstanceOf -eq $targetServiceResourceGroupName) }

    if ($null -eq $matchedResourceGroup) {
        Write-HostWithInfo -Throw $true -Section $method -message "There is no matching ServiceResourceGroup in your ServiceModel. You provided AzureResourceGroupName: '${targetArmResourceGroupName}', Location: '${targetArmResourceGroupLocation}', InstanceOf '${targetServiceResourceGroupName}', and AzureSubscriptionId: '${targetArmSubscriptionId}'"
    }

    foreach ($folder in $logsFolders) {
        $name = $folder.Name;
        $split = $name.Split("_")
        # format is ${LogsEndpoint}_${LogsAccount}_${LogsNamespace}_${ConfigVer}
        $LogsEndpoint = $split[0];
        $LogsAccount = $split[1];
        $LogsNamespace = $split[2];
        $ConfigVer = $split[3];
  
        $StepName = "${LogsEndpoint}${LogsAccount}${LogsNamespace}${ConfigVer}"
        $TargetName = "${LogsEndpoint}-${LogsAccount}${LogsNamespace}-${ConfigVer}"
        $Action = "Extension/${StepName}"
        $OrchestratedStep = @"
    {    
        "Name": "$StepName",
        "TargetType": "ServiceResource",
        "TargetName": "$TargetName",
        "Actions": [ "$Action" ]   
    }
"@

        # if the steps do not exist create an empty container
        if ($null -eq $RolloutSpecJson.OrchestratedSteps) {
            $RolloutSpecJson.OrchestratedSteps = @()
        }

        # check if the desired step already exists
        $existingStep = $RolloutSpecJson.OrchestratedSteps | Where-Object { $_.Name -eq $StepName -or $_.TargetName -eq $TargetName -or $_.Actions -contains $Action }

        if ($null -ne $existingStep) {
            Write-HostWithInfo -Throw $true -Section $method -message "Logs Config '${StepName}' already has an orchestrated step in RolloutSpec. Please resolve before continuing."
        }

        Write-HostWithInfo -Section $method -message "Ensuring Logs Config '${StepName}' has an orchestrated step in RolloutSpec."
        $RolloutSpecJson.OrchestratedSteps += $OrchestratedStep | ConvertFrom-Json

        # update the ScopeBindings.json file scopeBindings array
        $ScopeBinding = @"
    {
        "scopeTagName": "${StepName}",
        "bindings": [
          {
            "find": "__EXTENSION_NAME__",
            "replaceWith": "${StepName}"
          },
          {
            "find": "__EXTENSION_TYPE__",
            "replaceWith": "${extensionType}"
          },
          {
            "find": "__ENDPOINT_NAME__",
            "replaceWith": "${LogsEndpoint}"
          },
          {
            "find": "__ACCOUNT__",
            "replaceWith": "${LogsAccount}"
          },
          {
            "find": "__NAMESPACE__",
            "replaceWith": "${LogsNamespace}"
          },
          {
            "find": "__CONFIGPATH__",
            "replaceWith": "Package/LogsConfig_${name}.zip"
          } 
        ]
    }
"@

        if ($null -eq $ScopeBindingsJson.scopeBindings) {
            $ScopeBindingsJson.scopeBindings = @()
            Write-HostWithInfo -Section $method -message "scopeBindings array is null"
        }

        Write-HostWithInfo -Section $method -message "Ensuring Logs Account '${StepName}' has a scoped tag name in ScopeBindings."
        $ScopeBindingsJson.scopeBindings += $ScopeBinding | ConvertFrom-Json

        # update the ServiceModel.json file serviceResourceGroups array
        $serviceResource = @"
    {
        "Name": "${TargetName}",
        "InstanceOf": "${serviceResourceDefinitionName}",
        "ArmParametersPath": "${armParametersPath}",
        "scopeTags": [
          {
            "name": "${StepName}"
          }
        ]
    }
"@

        if ($null -eq $matchedResourceGroup.ServiceResources) {
            # doesn't exist, create an empty array to store the resource in
            $matchedResourceGroup.ServiceResources = @();
        }

        $existingResource = $matchedResourceGroup.ServiceResources | Where-Object { $_.Name -eq "${TargetName}" }
        if ($null -ne $existingResource) {
            Write-HostWithInfo -Throw $true -Section $method -message "Logs Account '${StepName}' already has a Service Model Resource entry. Please resolve before continuing."
        }

        Write-HostWithInfo -Section $method -message "Creating Logs Account '${StepName}' Service Model Resource entry."
        $matchedResourceGroup.ServiceResources += $serviceResource | ConvertFrom-Json
    }

    # commit the files to the file system
    $RolloutSpecJson | ConvertTo-Json -Depth 100 | Out-File -FilePath $serviceGroupRoot"/${rolloutSpecJsonPath}" -Encoding utf8
    $ScopeBindingsJson | ConvertTo-Json -Depth 100 | Out-File -FilePath $ServiceGroupRoot"/${scopeBindingsJsonPath}" -Encoding utf8 
    $ServiceModelJson | ConvertTo-Json -Depth 100 | Out-File -FilePath $serviceGroupRoot"/${serviceModelJsonPath}" -Encoding utf8
}

# For backwards compatibility < 0.1.12
Function Write-LogsScopeBindings {
    param(
        [string] $sourceRoot,
        [string] $serviceGroupRoot,
        [string] $logsRoot,
        [string] $rolloutSpecJsonFilename,
        [string] $scopeBindingsJsonFilename,
        [string] $serviceModelJsonFilename,
        [string] $serviceResourceDefinitionName,
        [string] $extensionType,
        [string] $armParametersPath,
        [string] $targetArmResourceGroupName,
        [string] $targetArmResourceGroupLocation,     
        [string] $targetArmSubscriptionId,       
        [string] $targetServiceResourceGroupName
    )
    Update-Ev2ArtifactsForLogs -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -logsRoot $logsRoot -rolloutSpecJsonPath $rolloutSpecJsonFilename -scopeBindingsJsonPath $scopeBindingsJsonFilename -serviceModelJsonPath $serviceModelJsonFilename -serviceResourceDefinitionName $serviceResourceDefinitionName -extensionType $extensionType -armParametersPath $armParametersPath -targetArmResourceGroupName $targetArmResourceGroupName -targetArmResourceGroupLocation $targetArmResourceGroupLocation -targetArmSubscriptionId $targetArmSubscriptionId -targetServiceResourceGroupName $targetServiceResourceGroupName
}

<#
.SYNOPSIS
    Writes Geneva Logs account configuration to artifacts for consumption by the Ev2 extension.
.PARAMETER sourceRoot
    The root folder for all the geneva configuration files. Normally the GenevaSrc folder.
.PARAMETER serviceGroupRoot
    The root folder for the Ev2 artifacts. Normally the ServiceGroupRoot folder.
.PARAMETER logsRoot
    The root folder for the logs configurations. Normally the GenevaSrc/Logs folder.
.PARAMETER packagesRoot
    The root folder for the script output. Normally the ServiceGroupRoot/Package folder.
.PARAMETER logsZipFolder
    The folder for the logs zip file. Normally the ServiceGroupRoot/Package/Logs folder.
.PARAMETER importsFolder
    The folder for the logs account configuration imports. Normally the imports folder.
#>
Function Write-LogsArtifacts {
    param(
        [string] $sourceRoot = "./GenevaSrc/",
        [string] $serviceGroupRoot = "./ServiceGroupRoot/",
        [string] $logsRoot = "${sourceRoot}/Logs/",
        [string] $packagesRoot = "${serviceGroupRoot}/Package/",
        [string] $logsZipFolder = "${packagesRoot}/Logs/",
        [string] $importsFolder = "imports"
    )

    $method = $MyInvocation.MyCommand

    Write-HostWithInfo -Section $method -message "Geneva GitOps Module Version: $version"
    $MyInvocation.MyCommand.Parameters.GetEnumerator() | ForEach-Object { Write-HostWithInfo -Section $method -message "$($_.Key) = '$(Get-Variable $_.Key -ValueOnly -EA SilentlyContinue)'" }

    # validate passed paths actually exist
    if (!(Test-Path ${sourceRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "sourceRoot does not exist: ${sourceRoot}"
    }
    if (!(Test-Path ${serviceGroupRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "serviceGroupRoot does not exist: ${serviceGroupRoot}"
    }
    if (!(Test-Path ${logsRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "logsRoot does not exist: ${logsRoot}"
    }

    Test-OrCreatePath -Path $packagesRoot

    Write-HostWithInfo -Section $method -message "Searching for Log accounts in $logsRoot"
    $logsFolders = Get-ChildItem -LiteralPath "$logsRoot" -Attribute D -Depth 1 -ErrorAction SilentlyContinue

    if ($null -eq $logsFolders -or $logsFolders.Length -eq 0) {    
        Write-HostWithInfo -Section $method -message "No Log accounts found [$Error]"
        return
    }

    # Logs MA Configuration example
    Write-HostWithInfo -Section $method -message "Setup config files destination folder [$logsZipFolder]"
    # in case it already exists from Get-ChildItem or other call, clear any errors
    $Error.clear()
    New-Item -Path $logsZipFolder -ItemType "directory" -ErrorAction SilentlyContinue | out-null
    if ($Error) {
        Write-HostWithInfo -Throw $true -Section $method -message "Error creating folder [$Error]"
    }

    foreach ($folder in $logsFolders) {
        $name = $folder.Name;
        $split = $name.Split("_")
        # format is ${LogsEndpoint}_${LogsAccount}_${LogsNamespace}_${ConfigVer}
        $LogsNamespace = $split[2];
        $ConfigVer = $split[3];
    
        New-Item -Path $logsZipFolder -Name "${name}/${importsFolder}" -ItemType "directory" | out-null

        Write-HostWithInfo -Section $method -message "Copying other configuration XML with Main [${name}]"
        Copy-Item -Path "${logsRoot}/${name}/" -Filter *.* -Destination "${logsZipFolder}/${name}/${importsFolder}/" -Recurse -Container:$false

        Write-HostWithInfo -Section $method -message "Building main.xml for ${name}"
        Move-Item "${logsZipFolder}/${name}/${importsFolder}/${LogsNamespace}${ConfigVer}.xml" -Destination "${logsZipFolder}/${name}/main.xml"            

        Write-HostWithInfo -Section $method -message "Compressing for artifact creation"
        Get-ChildItem "${logsZipFolder}/${name}" | Compress-Archive -Force -DestinationPath "${packagesRoot}/LogsConfig_${name}.zip"
    }
}

<#
.SYNOPSIS
    Writes Geneva metrics account configuration to an artifact for consumption by the Ev2 extension.
.PARAMETER SourceRoot
    The root folder for all the geneva configuration files. Normally the GenevaSrc folder.
.PARAMETER ServiceGroupRoot
    The root folder for the Ev2 artifacts. Normally the ServiceGroupRoot folder.
.PARAMETER PackagesRoot
    The root folder for the script output. Normally the ServiceGroupRoot/Package folder.
.PARAMETER MetricsSourceFolders
    The list of folders to search for metrics configurations. Normally the folders: GenevaSrc/Metrics and GenevaSrc/AccountConfig.
.PARAMETER ConfigPackagePath
    The path to the output zip file. Normally the ServiceGroupRoot/Package/MetricsConfigs.zip file.
#>
Function Write-MetricsArtifacts {
    param(
        [string] $sourceRoot = "./GenevaSrc/",
        [string] $serviceGroupRoot = "./ServiceGroupRoot/",
        [string] $packagesRoot = "${serviceGroupRoot}/Package/",
        [string[]] $metricsSourceFolders = @("${sourceRoot}/Metrics", 
            "${sourceRoot}/AccountConfig"
        ),
        [string] $configPackagePath = "${packagesRoot}/MetricConfigs.zip"
    )

    $method = $MyInvocation.MyCommand

    Write-HostWithInfo -Section $method -message "Geneva GitOps Module Version: $version"
    $MyInvocation.MyCommand.Parameters.GetEnumerator() | ForEach-Object { Write-HostWithInfo -Section $method -message "$($_.Key) = '$(Get-Variable $_.Key -ValueOnly -EA SilentlyContinue)'" }

    # validate passed paths actually exist
    if (!(Test-Path ${sourceRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "sourceRoot does not exist: ${sourceRoot}"
    }
    if (!(Test-Path ${serviceGroupRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "serviceGroupRoot does not exist: ${serviceGroupRoot}"
    }

    Test-OrCreatePath -Path $packagesRoot

    $metricFolders = @();
    foreach ($folder in $metricsSourceFolders) {
        if (Test-Path $folder) {
            $metricFolders += $folder

            # first, we will make sure there are not subfolders if there are also files present in the root folder
            # We wouldn't know if folder by namespace is enabled or disabled, we can't allow both
            $items = Get-ChildItem $folder
            # force both into an Array, PS will return a single item as a single object
            $files = @( $items | Where-Object -FilterScript { $_.PSIsContainer -eq $false } )
            $folders = @( $items | Where-Object -FilterScript { $_.PSIsContainer -eq $true } )
            # if both the files and folders have a count abort
            if ($files.Count -gt 0 -and $folders.Count -gt 0) {
                Write-HostWithInfo -Throw $true -Section $method -message "ERROR: Both files and folders found in $folder. If you are using folder per namespace, REMOVE the files in the root of the folder. If you are not using folder per namespace, REMOVE the subfolders."
            }
        }
        else {
            Write-HostWithInfo -Section $method -message "Check your source folder list. Folder not found: $folder"
        }
    }

    if ($metricFolders.Length -ne 0) {
        # second, we will walk the folder to make sure there are no duplicate files
        # we don't need to use the return value here, this method should throw in the event of duplicate file names
        # relative to the passed path, for example /Monitors/file1.json and /MonitorsV2/file1.json is OK
        # /Monitors/file1.json and /Monitors/subfolder/file1.json is NOT OK
        foreach ($folder in $metricFolders) {
            Get-FilesWithoutDuplicates -method $method -folder $folder -files @()
        }

        Write-HostWithInfo -Section $method -message "Bundling Metrics artifacts into ${configPackagePath}"
        Compress-Archive -Path $metricFolders -Force -DestinationPath $configPackagePath
    }
    else {
        Write-HostWithInfo -Section $method -message "No Metrics configurations found, you may pass an array of folder names using the MetricsSourceFolders parameter"
    }
}

<#
.SYNOPSIS
    Writes Geneva Monitor and Health configurations to an artifact for consumption by the Ev2 extension.
.PARAMETER SourceRoot
    The root folder for all the geneva configuration files. Normally the GenevaSrc folder.
.PARAMETER ServiceGroupRoot
    The root folder for the Ev2 artifacts. Normally the ServiceGroupRoot folder.
.PARAMETER PackagesRoot
    The root folder for the script output. Normally the ServiceGroupRoot/Package folder.
.PARAMETER HealthSourceFolders
    The list of folders to search for health/monitor configurations. Normally the folders: GenevaSrc/Monitors, GenevaSrc/MonitorsV2, GenevaSrc/HealthMonitors, and GenevaSrc/TopologyConfig.
.PARAMETER ConfigPackagePath
    The path to the output zip file. Normally the ServiceGroupRoot/Package/MonitorConfigs.zip file.
#>
Function Write-HealthArtifacts {
    param(
        [string] $sourceRoot = "./GenevaSrc/",
        [string] $serviceGroupRoot = "./ServiceGroupRoot/",
        [string] $packagesRoot = "${serviceGroupRoot}/Package/",
        [string[]] $healthSourceFolders = @("${sourceRoot}/Monitors", 
            "${sourceRoot}/MonitorsV2",
            "${sourceRoot}/HealthMonitors", 
            "${sourceRoot}/TopologyConfig"
        ),
        [string] $configPackagePath = "${packagesRoot}/MonitorConfigs.zip"
    )

    try {
        $method = $MyInvocation.MyCommand

        Write-HostWithInfo -Section $method -message "Geneva GitOps Module Version: $version"
        $MyInvocation.MyCommand.Parameters.GetEnumerator() | ForEach-Object { Write-HostWithInfo -Section $method -message "$($_.Key) = '$(Get-Variable $_.Key -ValueOnly -EA SilentlyContinue)'" }

        # validate passed paths actually exist
        if (!(Test-Path ${sourceRoot})) {
            Write-HostWithInfo  -Throw $true -Section $method -message "sourceRoot does not exist: ${sourceRoot}"
        }
        if (!(Test-Path ${serviceGroupRoot})) {
            Write-HostWithInfo  -Throw $true -Section $method -message "serviceGroupRoot does not exist: ${serviceGroupRoot}"
        }

        Test-OrCreatePath -Path $packagesRoot

        $Error.clear()

        # make sure we have the directory or PS has a quirky behavior on the first copy to non-existant folders
        # where it will copy the contents and not the folder to the destination
        Set-Variable -Name tempFolder -Value "_GenevaGitOpsEv2BuildScript_Process";
        New-Item -ItemType Directory -Path $tempFolder -Force -ErrorAction SilentlyContinue | Out-Null 
        # we have a seperate temp folder where we will extract only the JSON files 
        # and not folder structure if they're using folder per namespace
        Set-Variable -Name tempFolderBundle -Value "_GenevaGitOpsEv2BuildScript_Bundle";
        New-Item -ItemType Directory -Path $tempFolderBundle -Force -ErrorAction SilentlyContinue | Out-Null 
    
        if (!(Test-Path $tempFolder) -or !(Test-Path $tempFolderBundle)) {
            Write-HostWithInfo -Throw $true -Section $method -message "ERROR creating temp folders used to process health/monitor configurations. [$Error]"
        }

        $healthFolders = @();
    
        foreach ($folder in $healthSourceFolders) {
            if (Test-Path $folder) {
                # first, we will make sure there are not subfolders if there are also files present in the root folder
                # We wouldn't know if folder by namespace is enabled or disabled, we can't allow both
                $items = Get-ChildItem $folder
                # force both into an Array, PS will return a single item as a single object
                $files = @( $items | Where-Object -FilterScript { $_.PSIsContainer -eq $false } )
                $folders = @( $items | Where-Object -FilterScript { $_.PSIsContainer -eq $true } )
                # if both the files and folders have a count abort
                if ($files.Count -gt 0 -and $folders.Count -gt 0) {
                    Write-HostWithInfo -Throw $true -Section $method -message "ERROR: Both files and folders found in $folder. If you are using folder per namespace, REMOVE the files in the root of the folder. If you are not using folder per namespace, REMOVE the subfolders."
                }

                # second, we will walk the folder to make sure there are no duplicate files
                # we don't need to use the return value here, this method should throw in the event of duplicate file names
                # we do this check PER folder here instead of the whole package, as Monitors/Health
                # includes files that are very likely to have the same name in TopologyConfig, etc
                # in the form of __ACCOUNT_NAME__.json
                Get-FilesWithoutDuplicates -method $method -folder $folder -files @()

                $healthFolders += $folder
                # Clone the information we need to post process
                Write-HostWithInfo -Section $method -message "$folder will be processed"
                Copy-Item -Path $folder -Recurse -Destination $tempFolder -Container -ErrorAction Stop
            }
            else {
                Write-HostWithInfo -Section $method -message "Folder not found: $folder. Check your source folder list. This isn't blocking, just a warning."
            }
        }

        if ($healthFolders.Length -eq 0) {
            Write-HostWithInfo -Section $method -message "None of the specified Health Source Folders exist"
            return
        }

        # get the child folders only
        $jsonFolders = Get-ChildItem -Path $tempFolder -Attribute D -Depth 0
        foreach ($folder in $jsonFolders) {
            # make sure the folder is present in our bundle folder
            New-Item -ItemType Directory -Path "${tempFolderBundle}/$($folder.Name)" -Force -ErrorAction SilentlyContinue | Out-Null
            if (!(Test-Path "${tempFolderBundle}/$($folder.Name)")) {
                Write-HostWithInfo -Throw $true -Section $method -message "ERROR creating bundle folder '${tempFolderBundle}/$($folder.Name)'. [$Error]"
            } 
            # peek at only JSON files
            $jsonFiles = Get-ChildItem $folder.FullName -Filter *.json -Recurse
            foreach ($jsonFile in $jsonFiles) {
                $json = $jsonFile | Get-Content -Raw | ConvertFrom-Json
                # monitor v1 check
                if ($json.monitors.length -gt 0) {
                    # 1 is custom type
                    foreach ($monitor in $json.monitors | Where-Object -FilterScript { ($_.templateType -EQ 1) -and ($_.templateSpecificParameters.jsSnippet -like "JsFileRef:*.js") }) {
                        $split = $monitor.templateSpecificParameters.jsSnippet -split ":"
                        $jsFileRef = $split[1]
                        $jsFilePath = "$($jsonFile.Directory.FullName)/$($jsFileRef)"
                        Write-HostWithInfo -Section $method -message "Monitor V1 has JsFileRef embedded $($jsFilePath)"
                        # Clear the snippet
                        [string] $jsContent = Get-Content -Raw -Path "$($jsFilePath)"
                        $monitor.templateSpecificParameters.jsSnippet = $jsContent
                        # write modified content back to the file
                        [string] ($json | ConvertTo-Json -Depth 100) | Set-Content -Path "$($jsonFile.FullName)"
                        # remove JS file, don't include it in our bundle
                        Remove-Item -Path "$($jsFilePath)"
                    }
                }
                # monitor v2 check
                if ($json.alertConditions.length -gt 0) {
                    # 1 is custom type
                    foreach ($alertCondition in $json.alertConditions) {
                        foreach ($condition in $alertCondition.conditions | Where-Object -FilterScript { ($_.snippet -like "JsFileRef:*.js") }) {
                            $split = $condition.snippet -split ":"
                            $jsFileRef = $split[1]
                            $jsFilePath = "$($jsonFile.Directory.FullName)/$($jsFileRef)"
                            Write-HostWithInfo -Section $method -message "Monitor V2 has JsFileRef embedded $($jsFilePath)"
                            # Clear the snippet
                            [string] $jsContent = Get-Content -Raw -Path "$($jsFilePath)"
                            $condition.snippet = $jsContent
                            # write modified content back to the file
                            [string] ($json | ConvertTo-Json -Depth 100) | Set-Content -Path "$($jsonFile.FullName)"
                            # remove JS file, don't include it in our bundle
                            Remove-Item -Path "$($jsFilePath)"
                        }
                    }
                }

                # we only want to copy files, not structure
                # so, Monitors/*.json, not Monitors/Namespace/*.json
                Copy-Item -Path $jsonFile.FullName -Destination "${tempFolderBundle}/$($folder.Name)/$($jsonFile.Name)" -ErrorAction Stop
            }

        }

        Write-HostWithInfo -Section $method -message "Bundling Health artifacts into package $configPackagePath"
        # bundle monitor configs from the Process temp folder
        Compress-Archive -Path $tempFolderBundle/* -Force -DestinationPath $configPackagePath
    }
    finally {
        # clean up, even if we fail
        Remove-Item $tempFolder -Recurse
        Remove-Item $tempFolderBundle -Recurse
    }
}

<#
.SYNOPSIS
    Writes Geneva SLO/SLI configurations to an artifact for consumption by the Ev2 extension.
.PARAMETER SourceRoot
    The root folder for all the geneva configuration files. Normally the GenevaSrc folder.
.PARAMETER ServiceGroupRoot
    The root folder for the Ev2 artifacts. Normally the ServiceGroupRoot folder.
.PARAMETER PackagesRoot
    Where to place the generated SLO/SLI artifacts. Normally the ServiceGroupRoot/Package folder.
.PARAMETER SloSourceFolders
    The list of folders to search for SLO/SLI configurations. Normally the folders: GenevaSrc/SLO_SLI.
.PARAMETER PackageDestinationFolder
    The folder to place the generated SLO/SLI artifacts. Normally the ServiceGroupRoot/Package/SLOConfigs folder.
#>
Function Write-SloArtifacts {
    param(
        [string] $sourceRoot = "./GenevaSrc/",
        [string] $serviceGroupRoot = "./ServiceGroupRoot/",
        [string] $packagesRoot = "${serviceGroupRoot}/Package/",
        [string[]] $sloSourceFolders = @("${sourceRoot}/SLO_SLI/"),
        [string] $packageDestinationFolder = "${packagesRoot}/SLOConfigs/"
    )

    $method = $MyInvocation.MyCommand

    Write-HostWithInfo -Section $method -message "Geneva GitOps Module Version: $version"
    $MyInvocation.MyCommand.Parameters.GetEnumerator() | ForEach-Object { Write-HostWithInfo -Section $method -message "$($_.Key) = '$(Get-Variable $_.Key -ValueOnly -EA SilentlyContinue)'" }

    # validate passed paths actually exist
    if (!(Test-Path ${sourceRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "sourceRoot does not exist: ${sourceRoot}"
    }
    if (!(Test-Path ${serviceGroupRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "serviceGroupRoot does not exist: ${serviceGroupRoot}"
    }

    Test-OrCreatePath -Path $packagesRoot
    Test-OrCreatePath -Path $packageDestinationFolder

    $sloFiles = @()
    # for each folder in sloSourceFolders, check for duplicate files
    # we'll build up the sloFiles array as we go
    ForEach ($folder in $sloSourceFolders) {
        $sloFiles = Get-FilesWithoutDuplicates -method $method -folder $folder -files $sloFiles
    }

    if ($sloFiles.Length -ne 0) {
        Write-HostWithInfo -Section $method -message "Consolidating SLO/SLI artifacts"
        # we just want to copy files, not structure, duplicate files will get flagged by not using -Force
        Copy-Item -Path $sloFiles -Destination $packageDestinationFolder -Container -ErrorAction Stop
        Write-HostWithInfo -Section $method -message "SLO/SLI artifacts copied to ${packageDestinationFolder}"
    }
    else {
        Write-HostWithInfo -Section $method -message "No SLO/SLI configurations found, you may pass an array of folder names using the sloSourceFolders parameter"
    }
}

<#
.SYNOPSIS
    Writes Geneva Dashboards to an artifact for consumption by the Ev2 extension.
.PARAMETER SourceRoot
    The root folder for all the geneva configuration files. Normally the GenevaSrc folder.
.PARAMETER ServiceGroupRoot
    The root folder for the Ev2 artifacts. Normally the ServiceGroupRoot folder.
.PARAMETER PackagesRoot
    The root folder for the script output. Normally the ServiceGroupRoot/Package folder.
.PARAMETER DashboardSourceFolders
    The list of folders to search for dashboard configurations. Normally the folders GenevaSrc/Dashboards.
.PARAMETER ConfigPackagePath
    The path to the output zip file. Normally the ServiceGroupRoot/Package/Dashboard.zip file.
#>
Function Write-DashboardArtifacts {
    param(
        [string] $sourceRoot = "./GenevaSrc/",
        [string] $serviceGroupRoot = "./ServiceGroupRoot/",
        [string] $packagesRoot = "${serviceGroupRoot}/Package/",
        [string[]] $dashboardSourceFolders = @("${sourceRoot}/Dashboards"),
        [string] $configPackagePath = "${packagesRoot}/Dashboards.zip"
    )

    $method = $MyInvocation.MyCommand

    Write-HostWithInfo -Section $method -message "Geneva GitOps Module Version: $version"
    $MyInvocation.MyCommand.Parameters.GetEnumerator() | ForEach-Object { Write-HostWithInfo -Section $method -message "$($_.Key) = '$(Get-Variable $_.Key -ValueOnly -EA SilentlyContinue)'" }

    # validate passed paths actually exist
    if (!(Test-Path ${sourceRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "sourceRoot does not exist: ${sourceRoot}"
    }
    if (!(Test-Path ${serviceGroupRoot})) {
        Write-HostWithInfo -Throw $true -Section $method -message "serviceGroupRoot does not exist: ${serviceGroupRoot}"
    }

    Test-OrCreatePath -Path $packagesRoot

    $folders = @();
    foreach ($folder in $dashboardSourceFolders) {
        if (Test-Path $folder) {
            $folders += $folder
        }
        else {
            Write-HostWithInfo -Section $method -message "Check your source folder list. Folder not found: $folder"
        }
    }

    if ($folders.Length -ne 0) {
        Write-HostWithInfo -Section $method -message "Bundling Dashboard artifacts into ${configPackagePath}"
        Compress-Archive -Path $folders -Force -DestinationPath $configPackagePath
    }
    else {
        Write-HostWithInfo -Section $method -message "No Dashboard configurations found, you may pass an array of folder names using the DashboardSourceFolders parameter"
    }
}

Export-ModuleMember -Function Write-LogsArtifacts, Write-MetricsArtifacts, Write-HealthArtifacts, Write-LogsScopeBindings, Write-SloArtifacts, Update-Ev2ArtifactsForLogs, Write-DashboardArtifacts