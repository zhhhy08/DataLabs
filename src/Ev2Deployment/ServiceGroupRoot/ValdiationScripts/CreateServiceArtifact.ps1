# [DEPRECATED DO NOT USE] UNLESS EV2 PROVIDES A WAY TO GIVE A STAGE MAP FOR THE DEPLOYMENT PROGRAMATICALLY
# script can be used to validate only in INT. **NO** Prod deployments should happen through this script.
# .\CreateServiceArtifact.ps1 -rolloutInfra Test -cloudName Int -rolloutSpecName Infra -dryRun $true
# Docs: https://ev2docs.azure.net/features/buildout/gettingStarted.html#step-4-register-service-artifacts-and-subscription
param
(
    [string]
    [ValidateSet('Test')] # Can expand later to other regions
    $rolloutInfra, #Ev2 rollout infra

    [string]
    [ValidateSet("Int")]
    $cloudName,

    [string]
    $rolloutSpecName, #Rollout Type name (folder\model\spec name formatted for this)

    [bool]
    $dryRun,

    [bool]
    $useForceFlag
)
Import-Module Microsoft.Azure.Deployment.Express.Client | Out-Null

if ($dryRun) {
    Write-Host  "Dry run is ENABLED." -BackgroundColor DarkYellow -ForegroundColor Black
    Write-Host  "1. Ev2 artifacts will be validated and registered." -BackgroundColor DarkYellow -ForegroundColor Black
    Write-Host  "2. Configuration specification files will be uploaded." -BackgroundColor DarkYellow -ForegroundColor Black
    Write-Host  "3. Since dry run is enabled, the validation rollout is triggered to check for common errors." -BackgroundColor DarkYellow -ForegroundColor Black
    Write-Host  "Note: Errors in step 3 does not mean dryRun = false will fail. Validation rollouts have some limitations." -BackgroundColor DarkYellow -ForegroundColor Black
}
else {
    Write-Host  "1. Ev2 artifacts will be registered." -BackgroundColor Green -ForegroundColor Black
    Write-Host  "2. Configuration specification files will be uploaded." -BackgroundColor Green -ForegroundColor Black
    Write-Host  "3. Rollout is run to deploy resources in the environment that this script is run in." -BackgroundColor Green -ForegroundColor Black
}

try {
    
    $serviceIdentifier = "00df9fbf-c722-42e4-9acd-bc7125483b22" # 00 ID for Control Plane Data Labs from service tree
    $rolloutSpecFilename = "Rollouts\{0}\RolloutSpec.{0}.{1}.copied.json" -f $rolloutSpecName, $cloudName  # use format to force consistency on rolloutspec to model naming
    $serviceGroupRoot = "C:\Users\ponaraya\Mgmt-Governance-DataLabs\src\Ev2Deployment\ServiceGroupRoot"#(Resolve-Path -Path ".\src\Ev2Deployment\ServiceGroupRoot").path  # Resolve absolute path to the ServiceGroupRoot directory)
    $rolloutSpecLocation = "$serviceGroupRoot\$rolloutSpecFilename"
    $scopedFileLocation = "$serviceGroupRoot\ScopedFile\scoped-file.json"
    $serviceModelPath = $serviceGroupRoot + "\" + (Get-Content -Raw $rolloutSpecLocation | ConvertFrom-Json ).rolloutMetadata.serviceModelPath
    $serviceGroup = (Get-Content -Raw $serviceModelPath | ConvertFrom-Json ).serviceMetadata.serviceGroup

    $configSpecFile = $serviceGroupRoot + "\" + (Get-Content -Raw $rolloutSpecLocation | ConvertFrom-Json ).rolloutMetadata.Configuration.serviceGroupscope.specpath
    $location = "eastus1" #(Get-Content -Raw $configSpecFile | ConvertFrom-Json).geographies.regions.name
    
    # Unregistering the ServiceArtifact for a new version. We will unregister and register in dryRun
    Write-Host "Executing`n Unregister-AzureServiceArtifacts -ServiceIdentifier $serviceIdentifier -ServiceGroup $serviceGroup -RolloutInfra $rolloutInfra -ConfirmDelete `n"
    Unregister-AzureServiceArtifacts -ServiceIdentifier $serviceIdentifier -ServiceGroup $serviceGroup -RolloutInfra $rolloutInfra -ConfirmDelete

    # Register service artifacts into Ev2.
    # https://ev2docs.azure.net/references/cmdlets/register-artifacts.html?q=register-azureserviceartifacts
    if ($useForceFlag) {
        Register-AzureServiceArtifacts -ServiceGroupRoot $serviceGroupRoot -RolloutSpec $rolloutSpecLocation -RolloutInfra $rolloutInfra -Force
    }
    else {
        Register-AzureServiceArtifacts -ServiceGroupRoot $serviceGroupRoot -RolloutSpec $rolloutSpecLocation -RolloutInfra $rolloutInfra
    }

    # Testing Service artifacts
    if ($dryRun) {
        Write-Host "Testing service artifacts: Test-AzureServiceArtifacts -ServiceGroupRoot $serviceGroupRoot -RolloutSpec $rolloutSpecLocation" -BackgroundColor DarkYellow -ForegroundColor Black
        Test-AzureServiceArtifacts -ServiceGroupRoot $serviceGroupRoot -RolloutSpec $rolloutSpecLocation
        Write-Host "Testing service build out: Test-AzureServiceBuildout -Location $location -RolloutInfra $rolloutInfra -ScopedServiceListPath  
        $scopedFileLocation -WaitToComplete" -BackgroundColor DarkYellow -ForegroundColor Black
        Test-AzureServiceBuildout -Location $location -RolloutInfra $rolloutInfra -ScopedServiceListPath  $scopedFileLocation -WaitToComplete
    } 
    else {
      
        Write-Host "Executing `n New-AzureServiceBuildout  -Location $location -RolloutInfra $rolloutInfra -ScopedServiceListPath $scopedFileLocation `n" 
        new-AzureServiceBuildout -Location $location -RolloutInfra $rolloutInfra -ScopedServiceListPath  $scopedFileLocation -WaitToComplete
        Write-Host "`Successfully Started buildout"
        Write-Host "`Use buildout ID to track the progress in EV2 Portal or " -BackgroundColor Green -ForegroundColor Black
        Write-Host "Run: Get-AzureServiceBuildout  -Location $location -RolloutInfra $rolloutInfra -BuildoutId [BuildoutId] "  -BackgroundColor Green -ForegroundColor Black
        Write-Host "The above command gives the Ev2 Portal dashboard link, copy & paste to a browser to check the status"  -BackgroundColor Green -ForegroundColor Black
    }
}
catch {
    Write-Host "Error executing one of the commands, refer to error output for more details"
    throw
}

# See final reuslts
# Get-AzureServiceBuildout -Location centralus -BuildoutId [BuildoutId] -RolloutInfra [Infra]

#https://ev2docs.azure.net/references/cmdlets/Intro.html