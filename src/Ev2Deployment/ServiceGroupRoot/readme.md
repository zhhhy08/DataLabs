This folder contains the folders for Ev2 deployment as per the required structure.
This project uses the region agnostic model.
Reference: 
https://ev2docs.azure.net/features/buildout/genericServiceModel.html
https://ev2docs.azure.net/features/buildout/rolloutSpec.html
https://ev2docs.azure.net/features/buildout/subscriptionProvisioningParameters.html#subscription-key-variables
https://ev2docs.azure.net/features/parameterization/variables.html
https://ev2docs.azure.net/features/parameterization/scopetags.html#scope-binding-for-non-string-replacement

Subscription name
ARG DataLabs $config(stamp_$stamp().partner.partnerAcronym) defined per stamp as one stamp in one region is for one partner
Subscription name for abc partner - ARG DataLabs ABC Int/ Canary/ Public
Subscription name for resource fetcher - ARG DataLabs Int/ Canary/ Public

Subscription key
"MyStampSub-$stamp()" - recommendation -  as per stamp is per partner, so we will be creating one sub per partner in this case.
Subscription key for abc partner - datalabsabcsub-1
Subscription key for resource fetcher - datalabsresourcefetchersub

Resource group name allows 90 characters
Global resource group (contains subscription resources) name for abc partner - datalabsabcrg
Global resource group (contains subscription resources) name for abc partner - datalabsresourcefetcherrg
Regional resource group name for abc partner - datalabsabcrg-eastus etc.
Regional resource group name for resource fetcher - datalabsresourcefetcherrg-eastus

ARG specifies service specification per cloud.

Version file - BuildVer.txt is automatically generated as a part of the official build process.
Any failure in the rollout will trigger a Sev4 to the "Notification" tag information of RolloutSpec.Infra.json file.
