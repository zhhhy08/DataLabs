## Unrecognized Loations

ARN has location based partitioning. The locations in the scheme are preset. The locations that are not recognized by the scheme is reported by https://portal.microsoftgeneva.com/s/9747658E. Once there are unrecognized locations, please report to ARN oncall. ARN oncall will consider adding the locations to the scheme. Besides, also inform subscribers that use location based partitioning:

- ABCC: abccservice@service.microsoft.com

Add new locations is basically adding a new partitioning config. Below are the steps:
1. merge a PR that adds the new locations to the partitioning scheme. Example PR [Pull Request 9704253: expand arm locations.](https://msazure.visualstudio.com/One/_git/Mgmt-Governance-Notifications/pullrequest/9704253)
2. update partner subscriber onboarding configs by replacing old location partitioning configs with the new one
3. after the new onboarding configs are deployed, the sev3 shall not be triggered anymore. Close the incident.