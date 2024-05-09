# DOCS - https://ev2docs.azure.net/getting-started/production/presence.html#manually-register-your-service-presence
# This script is to be run from SAW 
# This is a one time script run needed when new regions are added or removed

$ServiceId = "00df9fbf-c722-42e4-9acd-bc7125483b22"
$Locations =  @(
    "southeastasia", 
    "eastasia",
    "eastus2euap", 
    "westus3",
    "eastus",
    "northeurope", 
    "swedencentral"
)
$ServiceGroupNames = @(
    "Microsoft.Azure.DataLabs.Applications",
    "Microsoft.Azure.DataLabs.ImagesUpload",
    "Microsoft.Azure.DataLabs.Infra",
    "Microsoft.Azure.DataLabs.InfraInternalAksSetup"
)


foreach ($ServiceGroupName in $ServiceGroupNames)
{
    Register-AzureServicePresence -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -Locations $Locations -RolloutInfra Prod
    Get-AzureServicePresence -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -RolloutInfra Prod
}