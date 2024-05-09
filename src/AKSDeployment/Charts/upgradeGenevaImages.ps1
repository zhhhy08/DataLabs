
$mdsdManifest = docker manifest inspect linuxgeneva-microsoft.azurecr.io/distroless/genevamdsd:recommended -v | ConvertFrom-Json
$mdmManifest = docker manifest inspect linuxgeneva-microsoft.azurecr.io/distroless/genevamdm:recommended -v | ConvertFrom-Json
$mdsdSHA = $mdsdManifest.Descriptor.digest
$mdmSHA = $mdmManifest.Descriptor.digest

$filePaths = (".\PartnerAKS\BaseValueFiles\dataLabsServices.yaml", ".\ResourceFetcherAKS\BaseValueFiles\rfServices.yaml")

function UpdateMonitoringAppImageTags(
    [Parameter(Mandatory = $true)]
    [string]
    $filePath,
    [Parameter(Mandatory = $true)]
    [string]
    $mdsdSHA,
    [Parameter(Mandatory = $true)]
    [string]
    $mdmSHA
) {
    if ((Test-Path $filePath) -eq $false) {
        Write-Host "Invalid file $filePath."
        return
    }

    if ([string]::IsNullOrEmpty($mdsdSHA) -or [string]::IsNullOrEmpty($mdmSHA)) {
        Write-Host "Invalid image tags: mdsd - $mdsdSHA, mdm - $mdmSHA"
        return
    }

    $patternMdsd = '(?<=mdsd:[\s\S]*?recommended@)sha256:[A-Fa-f0-9]{64}'
    $patternMdm = '(?<=mdm:[\s\S]*?recommended@)sha256:[A-Fa-f0-9]{64}'

    $content = Get-Content -Path $filePath -Raw
    $modifiedMdsd = $content -replace $patternMdsd, $mdsdSHA
    $modifiedMdm = $modifiedMdsd -replace $patternMdm, $mdmSHA

    Set-Content -Path $filePath -Value $modifiedMdm
}



$filePaths | %  {
	UpdateMonitoringAppImageTags -filePath $_ -mdsdSHA $mdsdSHA -mdmSHA $mdmSHA
}