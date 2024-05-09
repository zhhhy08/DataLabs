Function New-ArgAZRoleAssignment {
    param
    (
        [string]$ObjectId,
        [string]$RoleDefinitionName,
        [string]$Scope
    )

    
    Write-Host -BackgroundColor Cyan -ForegroundColor Black "Executing - Get-AzRoleAssignment -ObjectId  $ObjectId -RoleDefinitionName  $RoleDefinitionName  -Scope $Scope" 

    $existingRoleAssignment = Get-AzRoleAssignment -ObjectId $ObjectId -RoleDefinitionName $RoleDefinitionName -Scope $Scope

    if (!$existingRoleAssignment) {
        New-AzRoleAssignment -ObjectId $ObjectId -RoleDefinitionName $RoleDefinitionName -Scope $Scope 
    }
    else {
        Write-Host "Role assignment already exists" 
        Write-Host $existingRoleAssignment.RoleAssignmentId -BackgroundColor DarkGreen
    }
}

Function SetupKeyVaultCertConfig {
    param
    (
        [String]$vaultName,
        [String]$issuerName,
        [String]$providerName,
        [String]$environment
    )

    $existingIssuer = Get-AzKeyVaultCertificateIssuer -VaultName $vaultName
    if (!$existingIssuer -Or !$existingIssuer.Name.Contains($issuerName)) {
        Set-AzKeyVaultCertificateIssuer -VaultName $vaultName -IssuerProvider $providerName -Name $issuerName
    }
    $contacts = Get-AzKeyVaultCertificateContact -VaultName $vaultName
    # Lx regions do not support this function
    if (!($environment.Equals("usnat") -Or $environment.Equals("ussec")) -And (!$contacts -Or !$contacts.Email.Contains("earc@microsoft.com"))) {
        Add-AzKeyVaultCertificateContact -VaultName $vaultName -EmailAddress earc@microsoft.com
    }
}

Function CreateNewCert {
    param
    (
        [String]$issuerName,
        [String]$vaultName,
        [String]$certName,
        [String]$subjectName,
        [String[]]$dnsNames,
        [bool] $isSslAdminIssuer,
        [bool]$isEncryptionCert
    )

    if ($isEncryptionCert) {
        if ($isSslAdminIssuer) {
            Write-Error "Encryption certs are only intended to be created with OneCert issuer, this cert is likely misconfigured"
        }
        $policy = New-AzKeyVaultCertificatePolicy `
            -SecretContentType application/x-pkcs12 `
            -SubjectName $subjectName `
            -ValidityInMonths 6 `
            -IssuerName $issuerName `
            -RenewAtPercentageLifetime 49 `
            -KeyUsage DataEncipherment, KeyEncipherment, DigitalSignature
    }
    elseif ($dnsNames -and $isSslAdminIssuer) {
        # SSLAdmin doesn't work in AME, we are creating with Unknown issuer and print the CSR for creating in SSLAdmin
        $policy = New-AzKeyVaultCertificatePolicy `
            -SecretContentType application/x-pkcs12 `
            -SubjectName $subjectName `
            -ValidityInMonths 6 `
            -IssuerName "Unknown"`
            -DnsNames $dnsNames
    }
    elseif ($dnsNames) {
        $policy = New-AzKeyVaultCertificatePolicy `
            -SecretContentType application/x-pkcs12 `
            -SubjectName $subjectName `
            -ValidityInMonths 6 `
            -IssuerName $issuerName `
            -RenewAtPercentageLifetime 49 `
            -DnsNames $dnsNames
    }
    elseif ($isSslAdminIssuer) {
        # SSLAdmin doesn't work in AME, we are creating with Unknown issuer and print the CSR for creating in SSLAdmin
        $policy = New-AzKeyVaultCertificatePolicy `
            -SecretContentType application/x-pkcs12 `
            -SubjectName $subjectName `
            -ValidityInMonths 6 `
            -IssuerName "Unknown"
    }
    else {
        $policy = New-AzKeyVaultCertificatePolicy `
            -SecretContentType application/x-pkcs12 `
            -SubjectName $subjectName `
            -ValidityInMonths 6 `
            -IssuerName $issuerName `
            -RenewAtPercentageLifetime 49
    }

    if ($isSslAdminIssuer) {
        Write-Warning "The below certificate signing request must be created in SSLAdmin. Please select 6 month validity when creating in SSLAdmin. After approval this cert must be merged back, then you must then run this script again to update the certificate policy."
    }
    
    Add-AzKeyVaultCertificate -VaultName $vaultName -Name $certName -CertificatePolicy $policy
}

function  UpdateCert {
    param
    (
        [String]$issuerName,
        [String]$vaultName,
        [String]$certName,
        [String]$subjectName,
        [String[]]$dnsNames ,
        [object] $existingCert,
        [bool] $isSslAdminIssuer,
        [bool] $forceNewVersion,
        [bool]$displayThumbprint,
        [bool]$exportPublicKey
    )

    if ($forceNewVersion) {
        Write-Host "Creating new version of certificate" $certName
        $certPolicy = Get-AzKeyVaultCertificatePolicy -VaultName $vaultName -Name $certName
        if ($isSslAdminIssuer) {
            # SSLAdmin creation doesn't work in AME, we are setting the cert to renew early and after renewal will reset the cert policy
            $certPolicy.RenewAtNumberOfDaysBeforeExpiry = $null
            $certPolicy.RenewAtPercentageLifetime = 5
            Set-AzKeyVaultCertificatePolicy -VaultName $vaultName -Name $certName -InputObject $certPolicy
            
        }
        else {
            Add-AzKeyVaultCertificate -VaultName $vaultName -Name $certName -CertificatePolicy $certPolicy
        }
    }
    else {
        $certOperation = Get-AzKeyVaultCertificateOperation -VaultName $vaultName -Name $certName
        switch ($certOperation.Status) {
            { $_ -eq "failed" } {  
                Write-Error "Certificate $certName in vault $vaultName creation failed with error code $($certOperation.ErrorCode)"
            }
            { $_ -ne "completed" } { 
                Write-Host "Certificate $certName  is still creating with status $($certOperation.Status)"
            }
        }
        if ($existingCert.Enabled) {
            Write-Host "Certificate" $certName "created on" $existingCert.Created ":" $existingCert.SecretId
            if ($displayThumbprint) {
                Write-Host "Thumbprint:" $existingCert.Thumbprint
            }
            if ($exportPublicKey) {
                $file = ($certName + ".cer")
                Write-Host "Writing public key file" $file
                $export = Export-Certificate -Cert $existingCert.Certificate -FilePath $file
            }
            $certPolicy = Get-AzKeyVaultCertificatePolicy -VaultName $vaultName -Name $certName
            $updatePolicy = $false
            if ($certPolicy.SubjectName -ne $subjectName) {
                Write-Warning "The subject name is being changed from: $($certPolicy.SubjectName) to: $subjectName "
                $certPolicy.SubjectName = $subjectName
                $updatePolicy = $true
            }
            if ($certPolicy.IssuerName -ne $issuerName) {
                Write-Warning "Expected issuer: $issuerName but found: $($certPolicy.IssuerName)"
                $certPolicy.IssuerName = $issuerName
                $updatePolicy = $true
            }
            if ($certPolicy.RenewAtPercentageLifetime -ne 49) { 
                Write-Warning "Cert was not configured to renew at 49 percent before expiration, instead it was set to: $($certPolicy.RenewAtPercentageLifetime) percentage."
                $certPolicy.RenewAtNumberOfDaysBeforeExpiry = $null
                $certPolicy.RenewAtPercentageLifetime = 49
                $certPolicy.EmailAtNumberOfDaysBeforeExpiry = $null	
                $certPolicy.EmailAtPercentageLifetime = $null
                $updatePolicy = $true
            }
            if ($certPolicy.ValidityInMonths -ne 6) {
                Write-Warning "Cert was not configured with a 6 month validity, instead it was set to: $($certPolicy.ValidityInMonths) months."
                $certPolicy.ValidityInMonths = 6
                $updatePolicy = $true
            }
            if ($certPolicy.DnsNames.Count -ne $dnsNames.Count) {
                Write-Warning "Cert was not configured with same number of dns names."
                $certPolicy.DnsNames = $dnsNames
                $updatePolicy = $true
            }
            else {
                $mismatch = $false
                foreach ($policyDnsName in $certPolicy.DnsNames) {
                    if (!($dnsNames -contains $policyDnsName)) {
                        $mismatch = $true
                        break
                    }
                }
                foreach ($dnsName in $dnsNames) {
                    if (!($certPolicy.DnsNames -contains $dnsName)) {
                        $mismatch = $true
                        break
                    }
                }
                if ($mismatch) {
                    Write-Warning "The policy dnsNames did not match the expected list."
                    $certPolicy.DnsNames = $dnsNames
                    $updatePolicy = $true
                }
            }
            
            if ($updatePolicy) {
                Write-Host "Updating policy of certificate" $certName
                Set-AzKeyVaultCertificatePolicy -VaultName $vaultName -Name $certName -InputObject $certPolicy
            }
        }
    }

}

Function CreateManagedCert {
    param
    (
        [String]$issuerName,
        [String]$vaultName,
        [String]$certName,
        [String]$subjectName,
        [String[]]$dnsNames = @(),
        [bool]$forceNewVersion,
        [bool]$displayThumbprint = $false,
        [bool]$exportPublicKey = $false,
        [bool]$isEncryptionCert = $false
    )

    $isSslAdminIssuer = $issuerName -eq "SSLAdminBasedIssuer"
    $existingCert = Get-AzKeyVaultCertificate -VaultName $vaultName -Name $certName

    if (!$existingCert) {
        $CreateNewCertParams = @{
            vaultName        = $VaultName
            issuerName       = $issuerName
            certName         = $certName 
            subjectName      = $subjectName
            dnsNames         = $dnsNames
            isSslAdminIssuer = $isSslAdminIssuer
            isEncryptionCert = $isEncryptionCert
        }  
        CreateNewCert @CreateNewCertParams
    }
    else {
        $UpdateCertParams = @{
            vaultName         = $VaultName
            issuerName        = $issuerName
            certName          = $certName 
            subjectName       = $subjectName
            dnsNames          = $dnsNames
            isSslAdminIssuer  = $isSslAdminIssuer
            existingCert      = $existingCert
            forceNewVersion   = $ForceNewVersion
            displayThumbprint = $displayThumbprint
            exportPublicKey   = $exportPublicKey
        }  
        UpdateCert @UpdateCertParams
    }
}