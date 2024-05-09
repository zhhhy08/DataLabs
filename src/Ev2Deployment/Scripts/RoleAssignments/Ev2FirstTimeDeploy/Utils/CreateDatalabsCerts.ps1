
param (
    [string]$UserEmail,
    [string]$CloudName,
    [object]$Datalabs,
    [bool]$ForceNewVersion
)

. .\Utils\CommonFunctions.ps1

$subscriptionId = $Datalabs.subscriptionId
Select-AzSubscription $subscriptionId

# reusing the same subject name used by ARG services, SHOULD NOT be changed
if ($CloudName -eq "int") {
    $sslCertDomainSuffix = "int.datalabs.core.windows.net"
    $certDomainSuffix = "gov-rp-art-int"
    $firstpartyappcn = "int"
}
else {
    #prod, canary
    $sslCertDomainSuffix = "prod.datalabs.core.windows.net"
    $certDomainSuffix = "gov-rp-art"
    $firstpartyappcn = "prod"
} # TODO add for national clouds, LX clouds

$RegionMap = Import-Csv ..\..\..\Inputs\data\RegionMap.csv

foreach ($location in  $Datalabs.locations) {

    $acronym = ($RegionMap | Where-Object { $_.region -eq $location }).Acronym
    $VaultName = "datalabs" + $CloudName + $acronym + "rfkv"
    $RegionalResourceFetcherRG = "DataLabsRFRG-" + $location
    $ResourceFetcherAksName = "rf" + $CloudName + $acronym + "aks"

    Write-Host "Adding role assignment to create the cert"

    New-AzRoleAssignment -RoleDefinitionName 'Key Vault Administrator' -SignInName $UserEmail -Scope "/subscriptions/$subscriptionId/resourcegroups/$RegionalResourceFetcherRG/providers/Microsoft.KeyVault/vaults/$VaultName"

    Write-Host "wait 2 minutes for role assignment to propagate"
    Start-Sleep -Seconds 120

    Write-Host "Creating/updating ARG first party app cert in regional keyvault $VaultName"

    $oneCertPrivateIssuerName = "OneCertBasedPrivateIssuer"
    $oneCertPublicIssuerName = "OneCertBasedPublicIssuer"
    $privateProviderName = "OneCertV2-PrivateCA"
    $publicProviderName = "OneCertV2-PublicCA"

    $SetupKeyVaultCertConfigParams = @{
        vaultName    = $VaultName
        issuerName   = $oneCertPrivateIssuerName
        providerName = $privateProviderName
        environment  = $CloudName
    }

    SetupKeyVaultCertConfig @SetupKeyVaultCertConfigParams

    $CreateAADManagedCertParams = @{
        vaultName       = $VaultName
        issuerName      = $oneCertPrivateIssuerName
        certName        = "aad-rp-$CloudName-arg-first-party-app-cert" 
        subjectName     = "CN=aad-rp.$firstpartyappcn.$certDomainSuffix" # reusing the same subject name used by ARG services, SHOULD NOT be changed
        forceNewVersion = $ForceNewVersion
    }


    CreateManagedCert @CreateAADManagedCertParams

    $CreateArmAdminManagedCertParams = @{
        vaultName       = $VaultName
        issuerName      = $oneCertPrivateIssuerName
        certName        = "datalabs-arm-admin-$CloudName-$certDomainSuffix" 
        subjectName     = "CN=datalabs-arm-admin.$CloudName.$certDomainSuffix"
        forceNewVersion = $ForceNewVersion
    }

    CreateManagedCert @CreateArmAdminManagedCertParams

    Write-Host "Creating/updating SSL cert in regional keyvault $VaultName"

    $SetupKeyVaultCertConfigParams.issuerName = $oneCertPublicIssuerName
    $SetupKeyVaultCertConfigParams.providerName = $publicProviderName

    SetupKeyVaultCertConfig @SetupKeyVaultCertConfigParams

    $CreateSslManagedCertParams = @{
        vaultName       = $VaultName
        issuerName      = $oneCertPublicIssuerName
        certName        = "$ResourceFetcherAksName-ssl" 
        subjectName     = "CN=$ResourceFetcherAksName.$sslCertDomainSuffix"
        forceNewVersion = $ForceNewVersion
    }

    CreateManagedCert @CreateSslManagedCertParams

    Write-Host "Removing role assignment to create the cert"
    Remove-AzRoleAssignment -RoleDefinitionName 'Key Vault Administrator' -SignInName $UserEmail -Scope "/subscriptions/$subscriptionId/resourcegroups/$RegionalResourceFetcherRG/providers/Microsoft.KeyVault/vaults/$VaultName"
}