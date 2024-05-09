Refer
https://ev2docs.azure.net/features/parameterization/configuration.html#create-customized-configuration-settings

In ARG, we maintain one file per cloud for configurations. example file in ARG repo for Prod with 5 region specific settings is at [link](https://msazure.visualstudio.com/One/_git/Mgmt-Governance-ResourcesCache?path=/src/Ev2Deployment/Ev2Deployment/ServiceGroupRoot/ConfigurationSpecification/ConfigurationSpecification.RolloutInfra.Prod.json&version=GBmain&_a=contents)

This is linked to ARM parameter files via the ScopeBindings.json file and tags specified in the service model via the ["scopeTags"](https://ev2docs.azure.net/features/parameterization/scopetags.html) property.
ARM Parameter files will have the "find" portion in the ScopeBindings.json

All resource fetcher resource config are at the region level.

One Stamp for one partner - configs here are per partner for this region. https://ev2docs.azure.net/features/parameterization/configuration.html#add-stamp-level-configurations.

Execution constraints - https://ev2docs.azure.net/features/buildout/executionConstraint.html#deploy-to-regions-a-and-b-and-re-deploy

Security groups
Prod - 
8f63c3ac-a8dd-481e-8b16-2fe2c9497f31 : AME/AP-ResourceTopology
2214c35c-b4ac-431d-ad89-6a9fbbc6c8b3 : AME/TM-ResourceTopology
INT - c70a0001-4c4a-47b9-ae9b-358b11e9f43e : ARGMSFTDeployment
