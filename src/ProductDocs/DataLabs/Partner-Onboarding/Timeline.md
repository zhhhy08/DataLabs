# DataLabs Onboarding Timeline

This table gives an estimate of how long certain tasks can take in the DataLabs onboarding process. 

|Step No | Main Task          | Sub Task           | Time Estimate      | Recommendations/Pre-requisites |
|--------|--------------------|--------------------|--------------------|-------------------------|
| 1 |**Start Onboarding**| Follow below steps | see below | |
| 1.1 | Request Approval | Talk to the DataLabs team about your scenario and review 1 pager. | 1 - 2 days | A 1-pager with scenario explained and business justification is required |
| 1.2 | | Get familiar with process and documentation | 1 - 2 days | |
| 2 |**Setup Infrastructure**| Follow below steps | see below | |
| 2.1 || Create a PR | 1 - 2 days | |
| 2.2 || Register stage maps | < 1 days | Run powershell script for [RegisterStagemap.ps1](https://msazure.visualstudio.com/One/_git/Mgmt-Governance-DataLabs?path=/src/Ev2Deployment/Scripts/Ev2Scripts/RegisterStagemap.ps1&version=GBmain) |
| 2.3 || Register subscription key  | < 1 days | For first time onboarding in INT only: run powershell command. |
| 2.4 || Create new infra release  | 1 - 3 days | Contingent on correct values in PR (step 2.1) |
| 2.5 || Create partner files | 1 - 2 days | Understand all the data/properties you want to expose. |
| 2.6 || Run RaoSimple Browser: Showps.ps1 script  | 1 day | Need to get subscription owner JIT for Canary/Prod. |
| 2.7 || Deploy second Infra release (internalAKSSetup)  | 1 - 4 days | Contingent on correct values in PR (step 2.4) and Raops.ps1 run correctly (step 2.5).  |
| 3 | **Set up monitoring account** | If you do not have monitoring account, please set it up | 1 - 3 days | |
| 4 | **Configure data flows and deployments** | These steps can be done in parallel | see below | |
| 4.1 | Deploy Application | Create a new release and deploy services in mentioned order  | 3 - 7 days | Contingent on correct values in PR (step 2.5) |
| 4.2 | Set up data flow from ARN (Subscriber configuration) | Create a PR | 5 - 12 days |Understand where to make changes, merge, deploy/redeploy, test, debug |
| 4.3 | Set up publisher data flow (ARN/ARG Onboarding) | Create an onboarding ICM | 5 - 12 days |Please continue to followup as sometimes it can get stuck in the queue |