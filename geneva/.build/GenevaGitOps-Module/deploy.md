# steps to deploy

nuget pack .\Geneva.GitOps.PSModule.nuspec

nuget push .\Geneva.GitOps.PSModule.**VERSION**.nupkg -Source "https://msazure.pkgs.visualstudio.com/_packaging/GenevaGitOps/nuget/v3/index.json" -ApiKey "ADO"

# steps to test

Make sure you're in the folder .build/GenevaGitOps-Module/

# one time only
Install-Module -Name Pester -Force -SkipPublisherCheck -Scope CurrentUser

# execute the test(s)
Invoke-Pester -Path .\Geneva.GitOps.PSModule.Tests.ps1  (optional -Tag "Tag")