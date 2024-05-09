# Introduction

This project has all the Geneva configuration and EV2 files for build integration.

EV2 deployments currently supports updating
- Account Configurations
- Metrics Configurations (Pre-aggregates)
- Monitor V1 Configurations
- Monitor V2 Configurations
- Health Topology Configurations
- Logs (MA) Configuration 
- SLO Yaml Configurations 

Folders -

- .: This folder will have all your Geneva Control Configurations. This folder is customizable from Geneva side. Configurations will lie in the Root Folder, if Root folder option is set to empty in Geneva.
- ServiceGroupRoot: Sample Ev2 ServiceGroupRoot with Rollout params for deploying changes in Metrics Account Configuration, Metrics Preaggregates, Monitors, Logs Configuration Changes and SLO.
- .build : This folder has a sample build.ps1 script which may be called in your build pipeline to package the assets into the target extension's expected format within ServiceGroupRoot/Package. The script is used in the Build pipeline/stage. The **Package** folder will contain the appropriate artifacts after processing (as a build artifact). **It's recommended that you use the Geneva.GitOps.PSModule to package the assets.** The sample pipeline shows basic usage of the module.
- .pipelines: This folder contains a sample build and release pipeline which you may reference while creating your own ADO pipelines
- Sample [Pipeline details](https://msazure.visualstudio.com/One/_build?definitionId=297261) . 
- Ev2 release job using the Ev2 Extensions. See [Ev2 Release details](https://msazure.visualstudio.com/One/_releaseDefinition?definitionId=49142&_a=definition-pipeline)

## Getting Started

 - Build pipeline setup. See [Docs](https://eng.ms/docs/products/geneva/connectors/source_control/_genevaev2settings)

## Additional documentation

- [Geneva Source Control UX](https://portal.microsoftgeneva.com/account/configurations)
- [Geneva Docs For Source Control/Ev2](https://eng.ms/docs/products/geneva/connectors/source_control/gettingstartedv2)
- [Geneva Onboarding Docs](https://eng.ms/docs/products/geneva/getting_started/v2/landingpage)
- [Geneva UI for creating an account](https://portal.microsoftgeneva.com/settings/onboard)
- [EV2 Docs](https://ev2docs.azure.net/)
- [EV2 Metrics Extension with Jarvis](https://eng.ms/docs/products/geneva/connectors/ev2/metrics-release-pipelines)
- [EV2 Health Extension](https://eng.ms/docs/products/geneva/alerts/howdoi/managemonitorstopologywithev2)
- [EV2 Logs Extension](https://eng.ms/docs/products/geneva/logs/howtoguides/ev2/ev2)
- [EV2 SLO Extension](https://ev2docs.azure.net/features/service-artifacts/actions/http-extensions/shared-extensions/Microsoft.SLO.html)