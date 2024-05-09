https://ev2docs.azure.net/features/buildout/gettingStarted.html#examine-servicespec


Folder content from [ARG Repo](https://msazure.visualstudio.com/One/_git/Mgmt-Governance-ResourcesCache?path=/src/Ev2Deployment/Ev2Deployment/ServiceGroupRoot/ServiceSpec&version=GBmain&_a=contents) specify "ownerGroupObjectId" as well but it is removed in this repo as it is optional. Ev2 should be able to get this value from service tree by itself. Re add "ownerGroupObjectId" in this file if its causing issues.

