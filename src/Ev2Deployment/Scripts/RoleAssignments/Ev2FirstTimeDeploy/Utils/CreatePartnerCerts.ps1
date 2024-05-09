
param (
    [string]$PartnerAcronym,
    [string]$UserEmail,
    [string]$CloudName,
    [string]$Location,
    [string]$RegionAcronym,
    [string]$SubscriptionId,
    [bool]$ForceNewVersion
)

. .\Utils\CommonFunctions.ps1

function GetPartnerDstsCertDomainSuffix {
    # reusing the same subject name used by ARG services, SHOULD NOT be changed
    if ($CloudName -eq "int") {
        return "gov-rp-art-int"
    }
    #prod, canary
    return "gov-rp-art"
    # TODO add for national clouds, LX clouds
}

function GetPartnerSslCertDomainSuffix {
    if ($CloudName -eq "int") {
        return "int.datalabs.core.windows.net"
    }
    return "prod.datalabs.core.windows.net"
}

function CreateCertificate {
    param (
        [string]$oneCertIssuerName,
        [string]$providerName,
        [string]$certName,
        [string]$subjectName
    )

    $SetupKeyVaultCertConfigParams = @{
        vaultName    = $VaultName
        issuerName   = $oneCertIssuerName
        providerName = $providerName
        environment  = $CloudName
    }

    SetupKeyVaultCertConfig @SetupKeyVaultCertConfigParams

    $CreateManagedCertParams = @{
        vaultName       = $VaultName
        issuerName      = $oneCertIssuerName
        certName        = $certName 
        subjectName     = $subjectName
        forceNewVersion = $ForceNewVersion
    }
    
    CreateManagedCert @CreateManagedCertParams
}

$VaultName = $PartnerAcronym + $CloudName + $RegionAcronym + "kv".ToLower()
$ResourceGroup = "DataLabs" + $PartnerAcronym + "RG-" + $Location
$PartnerAksName= $PartnerAcronym + $CloudName + $RegionAcronym + "aks".ToLower()
Select-AzSubscription $SubscriptionId
Write-Host "Creating partner: $PartnerAcronym certificate in Keyvault: $VaultName"

Write-Host "Adding role assignment to create the cert"

New-AzRoleAssignment -RoleDefinitionName 'Key Vault Administrator' -SignInName $UserEmail -Scope "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$VaultName"

Write-Host "wait 2 minutes for role assignment to propagate"
Start-Sleep -Seconds 120

Write-Host "Creating/updating Datalabs partner app cert in regional keyvault $VaultName"

$certDomainSuffix = GetPartnerDstsCertDomainSuffix
$partnerDstsCertName = "datalabs-$PartnerAcronym-dsts-client-$CloudName-$Location-$certDomainSuffix"
$partnerDstsCertSubjectName = "CN=datalabs-$PartnerAcronym-dsts-client.$CloudName.$certDomainSuffix"
CreateCertificate "OneCertBasedPrivateIssuer" "OneCertV2-PrivateCA" $partnerDstsCertName $partnerDstsCertSubjectName

Write-Host "Creating/updating Datalabs ssl cert in regional keyvault $VaultName"
$certDomainSuffix = GetPartnerSslCertDomainSuffix
$partnerSslCertName = "$PartnerAksName-ssl"
$partnerSslCertSubjectName = "CN=$PartnerAksName.$certDomainSuffix"
CreateCertificate "OneCertBasedPublicIssuer" "OneCertV2-PublicCA" $partnerSslCertName $partnerSslCertSubjectName

Write-Host "Removing role assignment to create the cert"
Remove-AzRoleAssignment -RoleDefinitionName 'Key Vault Administrator' -SignInName $UserEmail -Scope "/subscriptions/$SubscriptionId/resourcegroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$VaultName"
