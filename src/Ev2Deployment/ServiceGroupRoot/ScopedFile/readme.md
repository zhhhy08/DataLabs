Buildout Scope [scoped-file.json] - lists services to buildout using a test New-AzureServiceBuildout command cmdlet. You will need this file for testing only. Note, once your artifacts are registered with Ev2, for new regions that come up in the future, Ev2 will automatically use the registered artifacts.

reference : https://ev2docs.azure.net/features/buildout/gettingStarted.html#examine-buildout-service-artifacts

In this repo: if using the Scripts/Utils/DeployRegionAgnosticRolloutSpec.ps1 to deploy, then this file is needed.