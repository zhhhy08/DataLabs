# Monitoring Deployments

DataLabs monitoring (Metrics, Health/Monitors) deployments are moved to EV2.
We are also using release YAML to define the deployment flows.

*Note*: Automatic push of monitoring changes are risky and non compliant. All other teams will also move to EV2 based deployments including ARG at some time.


## Monitoring Change Flow
- Changes can be done through Geneva UX and reuse/create branch/PR or
- directly from code by changing Mgmt-Governance-DataLabs\geneva\GenevaSrc in your branch and pushing a PR
- PR requires all the code policy including reviews/work-items etc.
- Once the PR is merged to main branch, it triggers official build for Geneva files. Pipeline Definition [Mgmt-Governance-ResourcesCache-Official](https://msazure.visualstudio.com/One/_build?definitionId=351013)
- After the official build, release build (based on YAML) will be automatically triggered which runs the EV2 flow. Pipeline Definition [Mgmt-Governance-DataLabs-Geneva-Release-Official](https://msazure.visualstudio.com/One/_build?definitionId=353043)
Note: If you update the configs through Geneva UX it's triggering the release through Geneva account which doesn't have access to PROD deployments so the last step will fail. Please trigger the release yourself to publish the changes.

## Release Approval
Monitoring deployments now requires release approval as product deployments. 
Once you do the code changes and official build finished and release pipeline are triggered. It will be stopped at approval phase. 
Also as updated in above notes, config updates through Geneva UX will fail to trigger deployment, please trigger the release yourself. 
### *Note*: Make sure **you ask for deployment approval** after the above step.