#First time deploy requires Ev2 shell script run for one time operations on the AKS cluster
#Shell script success depends on the role assignments that are assigned below
#Create the Arg Aks Deploy Script Role Definition before continuing. TODO: add https://learn.microsoft.com/en-us/azure/role-based-access-control/custom-roles-template#review-the-template

param (
    [ValidateSet("Int", "Canary", "Prod")]
	[Parameter(Mandatory=$true)]
    [string]$environmentName,

    [bool]$ForceNewVersion = $false
)

# login
if ([string]::IsNullOrEmpty($(Get-AzContext).Account)) {
    Connect-AzAccount
}

if($environmentName -eq "Int")
{
    . ..\..\..\Inputs\PartnerDetails\IntPartners.ps1
    $cloudName = "int"
    $argScaleSetMIPrincipcals = @("749f5d32-5d91-4ad8-8f8d-189cf506d8d2")

} 
elseif($environmentName -eq "Canary")
{
    . ..\..\..\Inputs\PartnerDetails\CanaryPartners.ps1
    $cloudName = "canary"
    $argScaleSetMIPrincipcals = @("71c47f70-f1fa-4a88-99e7-e444c5dc53d0")

} 
elseif($environmentName -eq "Prod")
{
    . ..\..\..\Inputs\PartnerDetails\ProdPartners.ps1
    $cloudName = "prod"
    $argScaleSetMIPrincipcals = @(
        "fa287782-6e15-4f23-b10e-dcd245adcec0",
        "5ba7b8b2-025e-41c5-b94f-09c96e76aedb",
        "3efbfa8a-a186-4e10-93a6-e4714fdbaf20",
        "7423de02-95a1-4670-b771-5167581c81e7",
        "07042582-5bbd-423d-8e41-ccd06f562bea"
    )
} 

$Params = @{
    datalabs = $datalabs
    partners = $partners
    cloudName = $cloudName
    argScaleSetMIPrincipals = $argScaleSetMIPrincipcals
}

.\Utils\OpsUtils @Params

if($environmentName -eq "Canary")
{
    # Create role assignments for MxRedis-idm to access output Eventhub
    # https://ms.portal.azure.com/#@MSAzureCloud.onmicrosoft.com/resource/subscriptions/bb596f76-3c15-4e59-af1f-7b0b7ff25f4b/resourcegroups/azureresourcestopologyeus2euapidmidentity/providers/microsoft.managedidentity/userassignedidentities/eus2euapidmscaleset/overview
    $IdmScaleSetRoleAssignmentParams = @{
        Partners = @($idmPartner)
        ArgScaleSetMIPrincipalIds = @("ff6ecaea-f351-4bc8-a52e-51ced6b01892")
    }

    $IdmScaleSetRoleAssignmentParams | ConvertTo-Json | Write-Output
    .\Utils\CreateRoleAssignmentsForArgScaleSet.ps1 @IdmScaleSetRoleAssignmentParams
}