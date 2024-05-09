1. In order to run InputOutputService and PartnerSolutionServiceBase locally
   Refer to README.txt in src/DataLabs/InputOutputService

Below steps are to setup AKS and build docker image and deploy it in AKS

2. In order to setup new AKS and deploy
   Refer to AKS_Setup.md (which will install public AKS and no monitoring service)
   Refer to Private_AKS_Setup_Int.md (which will install Private AKS with Geneva monitoring service)

3. How to build docker image locally
   1) Install docker daemon. Download docker from https://www.docker.com/
   2) Start Docker daemon
   3) Build all projects using "Build -> Rebuild Solution" in command bar
   4) If you select "Release" for build, go to Mgmt-Governance-DataLabs/out/Release-x64
      If you select "Debug" for build, go to Mgmt-Governance-DataLabs/out/Debug-x64
   5) For each service (InputOutputService, PartnerSolutionServiceBase, ResourceFetcherProxyService, ResourceFetcherService), go to each directory and run below command
      e.g)
      docker build -t jaeacr.azurecr.io/solution/io:latest -f c:/Repos/Mgmt-Governance-DataLabs/out/Release-x64/InputOutputService/Dockerfile c:/Repos/Mgmt-Governance-DataLabs/out/Release-x64/InputOutputService/ 

4. go to src/AKSDeployment/Charts/PartnerAKS
   Copy testValues.yaml to yourValues.yaml
5. Modify Resource Names(like EventHub Name, Storage Account, Service Bus) in the copied value file. You can see "#Replace" lines
