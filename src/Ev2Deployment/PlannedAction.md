In order to deploy Ev2 and create subscription for Canary, I will be running CreateServiceArtifact with the following fields:

This will do diagnostic resource subscription creation and infra subscription creation + infra for

.\CreateServiceArtifact.ps1 --rolloutInfra Prod --location eastus2euap --rolloutSpecName DiagResources --scopedFileName scoped-file.json
.\CreateServiceArtifact.ps1 --rolloutInfra Prod --location eastus2euap --rolloutSpecName Infra --scopedFileName scoped-file.json
