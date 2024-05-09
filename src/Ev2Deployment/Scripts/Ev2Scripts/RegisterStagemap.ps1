#Docs - https://ev2docs.azure.net/features/rollout-orchestration/stagemap.html#register-a-service-group-stagemap
# Download the stage map locally to SAW and run this script.
# This is a one time step needed only if stage map changes for some reason

$ServiceId = "00df9fbf-c722-42e4-9acd-bc7125483b22"
$StageMapFilePath = "<replace local path here>"
$rolloutInfra = "<should be Test/Prod>"
New-AzureServiceStageMap -ServiceIdentifier $ServiceId  -StageMapFilePath $StageMapFilePath -RolloutInfra $rolloutInfra
Get-AzureServiceStageMap -ServiceIdentifier $ServiceId  -RolloutInfra $rolloutInfra