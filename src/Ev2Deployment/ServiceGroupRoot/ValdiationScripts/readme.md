**These scripts are for validation only**

These scripts are used to deploy region agnostic model rollout specs via powershell.
    
Powershell to be used : https://ev2docspreview.azure.net/references/cmdlets/Intro.html#get-latest-cmdlets
  
The DeployRegionAgnosticRolloutSpec.ps1 has all the necessary Ev2 commands. 
Use -dryRun $true to get the commands that would be executed.
  
   
For Test rollout infra : Use dev box/ SAW
For Prod rollout infra : Use SAW . 
**If using powershell** For Prod, the subscription requires access for the Ev2 Service Buildout app to create resources. 
Refer below to create the role assignment - 
https://ev2docs.azure.net/features/buildout/gettingStarted.html#subscription