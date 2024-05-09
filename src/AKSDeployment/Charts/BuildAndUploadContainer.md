#################################################################################
#Local Build (requires local docker daemon running)
#################################################################################
cd C:\Repos\Mgmt-Governance-ResourcesCacheSolution\ARGSolution\SolutionPlatform\InputOutputService

#0.1 Download and start Docker Desktop
#0.2 Set up publishing to folder specified by Dockerfile.
	#0.2.1 Click on "Build > Publish Selection" for the service you want to build and a new window will ask to publish to a target.
	#0.2.2 Set "Target" and "Specific Target" to be a folder
	#0.2.3 Set folder location to be in the "out\publish" folder (need to add these folders if they do not exist). This location is specified in Dockerfile (where Dockerfile will read the .dll files)
	#0.2.4 Press Finish!

#1. Build the service that you want in Visual Studio
#2. Publish that service (can right click service or on menu bar on top).
#3. Right click on DockerFile in the project you want to build in Visual studio
#4. Select "Build Docker Image"
#5. Tag built image to Azure Container Registry repository name

docker tag inputoutputservice jaeacr.azurecr.io/solution/io

#4. Login acr where docker image will be uploaded
az acr login --name jaeacr

#5. Upload the image to Azure Container Registry
docker push jaeacr.azurecr.io/solution/io


#Debugging

failed to compute cache key: "/SolutionPlatform/InputOutputService/out/publish" not found: not found    InputOutputService
C:\Mgmt-Governance-ResourcesCacheSolution\ARGSolution\SolutionPlatform\InputOutputService\Dockerfile    1    

#Docker in current iteration is not building for us, Visual Studio is doing instead and Docker is just copying .dll files from a folder. Please follow step 0.2 to resolve this issue.