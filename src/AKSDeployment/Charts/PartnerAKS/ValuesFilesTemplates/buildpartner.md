# Build Partner

Utilizes a generic yaml file template in order to generate values files for different partners. Currently, this is a rudimentary approach, but in the future, we hope to make it even easier to build partner values files consistently and correctly.

Requirements:
- Needs access to PROD resources, so it must be run on SAW.
- Please download these for powershell
    - Install-Module -Name Az.Aks -AllowClobber -Force -SkipPublisherCheck
    - Install-Module -Name Az.Networking -AllowClobber -Force -SkipPublisherCheck
    - Install-Module -Name Az.ManagedServiceIdentity -AllowClobber -Force -SkipPublisherCheck
    - Please add more if they do not exist.

buildpartner.ps1 is used in:
- ABC:
    - usage: ./buildpartner.ps1 bcdr
    - note: Please do not run this command for bcdr value files as westus3 utilizes traffic tuner to control their private preview.
- IDM: 
    - usage: ./buildpartner.ps1 idMapping
- CAP:
    - usage: ./buildpartner.ps1 capabilities
    - note: Policy resource Capabilities
- RAS:
    - usage: ./buildpartner.ps1 resourceAlias


To create new values files, please refer to the templates that were made for ABC team (bcdrValues, external partner) and IDM team (idMapping, internal partner)
