#requires -Version 7.1
#run this from the root of the repo is powershell 7.1 installed in vscode powershell extension terminal.
#Use $psversiontable to determine your powershell version.
#change the default vs code debug to "powershell launch current file"
function Format-Json {
    Param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true)]
        [string]$Json,
  
        [ValidateRange(1, 4)]
        [int]$Indentation = 4
  
    )
  
    if ($PSCmdlet.ParameterSetName -eq 'Minify') {
        return ($Json | ConvertFrom-Json) | ConvertTo-Json -Depth 100 -Compress
    }
  
    # If the input JSON text has been created with ConvertTo-Json -Compress
    # then we first need to reconvert it without compression
    if ($Json -notmatch '\r?\n') {
        $Json = ($Json | ConvertFrom-Json) | ConvertTo-Json -Depth 100
    }
  
    $indent = 0
    $regexUnlessQuoted = '(?=([^"]*"[^"]*")*[^"]*$)'
  
    $result = $Json -split '\r?\n' |
    ForEach-Object {
        # If the line contains a ] or } character, 
        # we need to decrement the indentation level unless it is inside quotes.
        if ($_ -match "[}\]]$regexUnlessQuoted") {
            $indent = [Math]::Max($indent - $Indentation, 0)
        }
  
        # Replace all colon-space combinations by ": " unless it is inside quotes.
        $line = (' ' * $indent) + ($_.TrimStart() -replace ":\s+$regexUnlessQuoted", ': ')
  
        # If the line contains a [ or { character, 
        # we need to increment the indentation level unless it is inside quotes.
        if ($_ -match "[\{\[]$regexUnlessQuoted") {
            $indent += $Indentation
        }
  
        $line | Where-Object { [system.string]::IsNullOrWhiteSpace($_.trimend("\s+")) -ne $true }
    }
    return $result -Join [Environment]::NewLine
}

function ReadRawFileContent {
    Param(
        [Parameter(Mandatory = $true)]
        [string] $baseFileFullName,
        [Parameter(Mandatory = $true)]
        [string] $newFileName
    )
    Copy-Item $baseFileFullName -Destination $newFileName -Force 
    return Get-Content -Raw $newFileName 
}

function GenerateRolloutSpec {
    param (
        [Parameter(Mandatory = $true)]
        [string] $baseFileFullName,
        [Parameter(Mandatory = $true)]
        [string] $newFileName,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$appName,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$componentName,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$appCloudName
    )
    $specBaseFileRawContent = ReadRawFileContent -baseFileFullName $baseFileFullName -newFileName $newFileName
    $specBaseFileRawContent = $specBaseFileRawContent.Replace("[[APPNAME]]", $appName)
    $specBaseFileRawContent = $specBaseFileRawContent.Replace("[[COMPONENT]]", $componentName)
    $specBaseFileRawContent = $specBaseFileRawContent.Replace("[[CLOUDNAME]]", $appCloudName)

    $result = ConvertFrom-Json $specBaseFileRawContent -Depth 50
    $result.rolloutMetadata | Add-Member -MemberType NoteProperty -Name Configuration -Force -Value ([ordered]@{
            serviceGroupScope = [ordered]@{
                specPath = "ConfigurationSpecification\Configuration.$appCloudName.json"
            }
        })
    $smName = $result.rolloutMetadata.serviceModelPath
    $replaceString = ""
    if ($appName) {
        $replaceString += ".$componentName.$appName"
    }
    if ($appCloudName -eq "Int") {   
        $replaceString += ".Int"
    }
    if ($appName) {
        $replaceString += ".generated"
    }
    $smName = $smName.Replace('.json', "$replaceString.json")
    $result.rolloutMetadata.serviceModelPath = $smName

    $result | ConvertTo-Json -Depth 50 | Format-Json | Set-Content -Path $newFileName
}

function GenerateServiceModel {
    param (
        [Parameter(Mandatory = $true)]
        [string] $baseFileFullName,
        [Parameter(Mandatory = $true)]
        [string] $newFileName,
        [Parameter(Mandatory = $true)]
        [object]$app,
        [Parameter(Mandatory = $true)]
        [string] $pcloudName,
        [Parameter(Mandatory = $true)]
        [string] $psubscriptionKey,
        [Parameter(Mandatory = $true)]
        [string] $pglobalSubscriptionKey
    )
    $smBaseFileRawContent = ReadRawFileContent -baseFileFullName $baseFileFullName -newFileName $newFileName
    $smBaseFileRawContent = $smBaseFileRawContent.Replace("[[NAME]]", $app.Name)
    $smBaseFileRawContent = $smBaseFileRawContent.Replace("[[SUBSCRIPTIONKEY]]", $psubscriptionKey)
    $smBaseFileRawContent = $smBaseFileRawContent.Replace("[[AZURERESOURCEGROUPNAME]]", $app.AzureResourceGroupName)
    $smBaseFileRawContent = $smBaseFileRawContent.Replace("[[APPCONFIGSCOPEBINDINGNAME]]", $app.ConfigsScopeBindingName)
    $smBaseFileRawContent = $smBaseFileRawContent.Replace("[[APPNAME]]", $app.AppName)
    $smBaseFileRawContent = $smBaseFileRawContent.Replace("[[COMPONENT]]", $app.Component)
    $smBaseFileRawContent = $smBaseFileRawContent.Replace("[[GLOBALSUBSCRIPTIONKEY]]", $pglobalSubscriptionKey)

    $result = ConvertFrom-Json $smBaseFileRawContent -Depth 50
    if ( $pcloudName -eq "int") {
        $smName = $result.serviceMetadata.serviceSpecificationPath
        $smName = $smName.Replace('.json', '.int.json')
        $result.serviceMetadata.serviceSpecificationPath = $smName
    }
    $result | ConvertTo-Json -Depth 50 | Format-Json | Set-Content -Path $newFileName
}

function GenerateRolloutParams {
    param (
        [Parameter(Mandatory = $true)]
        [string] $baseFileFullName,
        [Parameter(Mandatory = $true)]
        [string] $newFileName,
        [Parameter(Mandatory = $true)]
        [string]$pappName,
        [Parameter(Mandatory = $true)]
        [string]$pcomponentName
    )
    $rpBaseFileRawContent = ReadRawFileContent -baseFileFullName $baseFileFullName -newFileName $newFileName
    $rpBaseFileRawContent = $rpBaseFileRawContent.Replace("[[APPNAME]]", $pappName)
    $rpBaseFileRawContent = $rpBaseFileRawContent.Replace("[[COMPONENT]]", $pcomponentName)
    $result = ConvertFrom-Json $rpBaseFileRawContent -Depth 50
    $result | ConvertTo-Json -Depth 50 | Format-Json | Set-Content -Path $newFileName
}

$repo = "Mgmt-Governance-DataLabs"
$repoIndex = $pwd.path.IndexOf($repo) + $repo.length + 1
if ($pwd.path -notlike "*$repo") {
    if ($pwd.path -like "*$($repo)*") {
        Set-Location $pwd.path.substring(0, $repoIndex)
    }
}

$modifiedFiles = Get-ChildItem -Path .\src\Ev2Deployment -Recurse | Where-Object { $_.name -like "*.copied.json" -or $_.name -like "*.generated.json" }
$modifiedFiles | Remove-Item

$rolloutSpecBaseFiles = Get-ChildItem -Path .\src\Ev2Deployment\Inputs\RolloutBaseJson 
$serviceModelBaseFiles = Get-ChildItem -Path .\src\Ev2Deployment\Inputs\ServiceModelBaseJson
$rolloutParamsBaseFiles = Get-ChildItem -Path .\src\Ev2Deployment\Inputs\RolloutParametersBaseJson


$cloudDataFile = ".\src\Ev2Deployment\Inputs\Data\CloudData.csv"
$cloudData = Import-Csv $cloudDataFile
$cloudData | Sort-Object  CloudName, Component, SubscriptionKey | ConvertTo-Json | Format-Json | Out-File $cloudDataFile.replace(".csv", ".json")

$stageMapDataFile = ".\src\Ev2Deployment\Inputs\Data\StageMapData.csv"
$stageMapData = Import-Csv $stageMapDataFile
$stageMapData | Sort-Object StageEnvironment, StageCloud, Sequence, StageName, Region, Stamps | ConvertTo-Json | Format-Json | Out-File $stageMapDataFile.replace(".csv", ".json")

$appDataFile = ".\src\Ev2Deployment\Inputs\Data\Apps.csv"
$appData = Import-Csv $appDataFile
$appData | Sort-Object  CloudName, Name, SubscriptionKey, AzureResourceGroupName, ConfigsScopeBindingName, AppName, Component | ConvertTo-Json | Format-Json | Out-File $appDataFile.replace(".csv", ".json")

$CloudNames = $stageMapData.StageCloud | Select-Object -Unique
foreach ($CloudName in $CloudNames) {
    Write-Host $CloudName

    $globalSubscriptionObject = $cloudData | Where-Object { $_.CloudName -eq $CloudName -and $_.Component -eq "Global" } 

    # rollout spec file generation
    foreach ($specBaseFile in $rolloutSpecBaseFiles) {

        $SpecBaseType = ($specBaseFile.name -split "\.")[1]
        Write-Host $SpecBaseType

        if ($SpecBaseType -eq "Applications") {
            foreach ($app in $appData) {
                $appname = $app.AppName
                $component = $app.Component
                $newname = $specBaseFile.fullname.replace("\Inputs\RolloutBaseJson\RolloutSpec.$SpecBaseType.json", "\ServiceGroupRoot\Rollouts\$SpecBaseType\$CloudName\RolloutSpec.$SpecBaseType.$CloudName.$component.$appname.copied.json")
                GenerateRolloutSpec -baseFileFullName $specBaseFile.FullName -newFileName $newname -appName $appname -componentName $component -appCloudName $CloudName
                Write-Host $newname
            }
        }
        else {
            $newname = $specBaseFile.fullname.replace("\Inputs\RolloutBaseJson\RolloutSpec.$SpecBaseType.json", "\ServiceGroupRoot\Rollouts\$SpecBaseType\RolloutSpec.$SpecBaseType.$CloudName.copied.json")
            GenerateRolloutSpec -baseFileFullName $specBaseFile.FullName -newFileName $newname -appName "" -componentName "" -appCloudName $CloudName
            Write-Host $newname
        }
    }

    # service model file generation
    foreach ($serviceModelBaseFile in $serviceModelBaseFiles ) {
        $serviceModelBaseType = ($serviceModelBaseFile.name -split "\.")[2]
        Write-Host $serviceModelBaseType

        if ($serviceModelBaseType -eq "Applications") {
            foreach ($app in $appData) {
                $newname = $serviceModelBaseFile.Fullname.replace("\Inputs\ServiceModelBaseJson\DataLabs.ServiceModel.$serviceModelBaseType.json", "\ServiceGroupRoot\Rollouts\$serviceModelBaseType\$CloudName\DataLabs.ServiceModel.$serviceModelBaseType.$($app.Component).$($app.AppName).generated.json")

                if ($CloudName -eq "int") {   
                    $newname = $newname.Replace(".generated.json", ".Int.generated.json")
                }

                $cloudObject = $cloudData | Where-Object { $_.CloudName -eq $CloudName -and $_.Component -eq $app.Component } 

                GenerateServiceModel -baseFileFullName $serviceModelBaseFile.FullName -newFileName $newname -app $app -psubscriptionKey $cloudObject.SubscriptionKey -pcloudName $CloudName -pglobalSubscriptionKey $globalSubscriptionObject.SubscriptionKey
                Write-Host $newname
            }
        }
    }

    #rollout parameters generation
    foreach ($rpBaseFile in $rolloutParamsBaseFiles) {
        $rpBaseFileType = ($rpBaseFile.name -split "\.")[0]
        Write-Host $rpBaseFileType

        foreach ($app in $appData) {

            $newname = $rpBaseFile.Fullname.replace("\Inputs\RolloutParametersBaseJson\$rpBaseFileType.Rollout.json", "\ServiceGroupRoot\Rollouts\$rpBaseFileType\RolloutParameters\AppDeploy.$($app.Component).$($app.AppName).Rollout.copied.json")

            GenerateRolloutParams -baseFileFullName $rpBaseFile.FullName -newFileName $newname -pcomponentName $app.Component -pappName $app.AppName
            Write-Host $newname
        }
    }

    # stage map file generation
    $CloudSequences = $stageMapData | Where-Object { $_.StageCloud -eq $cloudName }
    $version = $CloudSequences.version | Select-Object -First 1
    $CloudSequence = $CloudSequences.Sequence | Select-Object -Unique | Sort-Object
    $stages = @() 
    $defaultStages = @()
    foreach ($Sequence in $CloudSequence) {
        $substages = @()

        $stampsString = $($CloudSequences | Where-Object { $_.Sequence -eq $Sequence }).Stamps | Select-Object -First 1 
        $stamps = $stampsString -split "-"
        
        for ($i = 1; $i -le $stamps.Count; $i++) {
            $substage = [ordered]@{
                sequence = [int]$i
                name     = "StampSet$i"
                stamps   = @()
            }
            $stampsetstring = $stamps[$i - 1] -split ":"
            $substage.stamps += $stampsetstring
            $substages += $substage
        }

        $stage = [ordered]@{
            sequence = [int]$Sequence
            name     = $($CloudSequences | Where-Object { $_.Sequence -eq $Sequence }).StageName | Select-Object -First 1
            regions  = @()
            stages   = $substages
        }
        
        $defaultStage = [ordered]@{
            sequence = [int]$Sequence
            name     = $($CloudSequences | Where-Object { $_.Sequence -eq $Sequence }).StageName | Select-Object -First 1
            regions  = @()
        }

        $tempregions = ($CloudSequences | Where-Object { $_.Sequence -eq $Sequence }).region
        $stage.regions += $tempregions 
        $defaultStage.regions += $tempregions
        
        $stages += $stage
        $defaultStages += $defaultStage
    }
    $manual = $true
    if ($CloudName -eq "int") {   
        $manual = $false
    }
    $configuration = [ordered]@{
        promotion = [ordered]@{
            manual  = [bool]$manual
            timeout = "P3D"
        }
    }
    $map = [ordered]@{
        "`$schema"    = "https://ev2schema.azure.net/schemas/2020-04-01/StageMap.json"
        name          = "Microsoft.Azure.Datalabs.$CloudName"
        version       = $version
        configuration = $configuration
        stages        = $stages
    }
    $map | ConvertTo-Json -Depth 15 | Set-Content ".\src\Ev2Deployment\StageMaps\StageMap.$CloudName.generated.json"

    $defaultmap = [ordered]@{
        "`$schema"    = "https://ev2schema.azure.net/schemas/2020-04-01/StageMap.json"
        name          = "Datalabs.$CloudName.Default"
        version       = "1.0.0.0"
        configuration = $configuration
        stages        = $defaultStages
    }
    $defaultmap | ConvertTo-Json -Depth 15 | Set-Content ".\src\Ev2Deployment\StageMaps\StageMap.Default.$CloudName.generated.json"

}


$utf8 = New-Object System.Text.UTF8Encoding $false
$fx1 = Get-ChildItem *.json -Recurse -Path .\src\Ev2Deployment\ServiceGroupRoot
foreach ($fx in $fx1) {
    $c = (Get-Content $fx.fullname -Raw -Encoding utf8 | Format-Json) 
    If ($c.length -ge 1) {
        [Io.File]::WriteAllText($fx.fullname, ($c), $utf8)
    }
    Else {
        throw "Content Length is 0 for $($fx.fullname)"
    }
}
