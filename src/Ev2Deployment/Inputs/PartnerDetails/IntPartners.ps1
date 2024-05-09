$locations = @("eastus")
$capLocations = @("eastus2")

$datalabs = @{
    locations      = $locations
    subscriptionId = "02d59989-f8a9-4b69-9919-1ef51df4eff6"
}

$partners = @(
    @{
        locations                   = $locations
        partnerAcronym              = "abc"
        subscriptionId              = "02d59989-f8a9-4b69-9919-1ef51df4eff6"
    },
    @{
        locations                   = $locations
        partnerAcronym              = "idm"
        subscriptionId              = "02d59989-f8a9-4b69-9919-1ef51df4eff6"
    },
    @{
        locations                   = $capLocations
        partnerAcronym              = "cap"
        subscriptionId              = "02d59989-f8a9-4b69-9919-1ef51df4eff6"
    },
    @{
        locations                   = $locations
        partnerAcronym              = "ras"
        subscriptionId              = "6d5b60f5-24e0-4722-acbb-ad6b9ee7675f"
        partnerCertificatesRequired = $true
    },
    @{
        locations                   = $locations
        partnerAcronym              = "azr"
        subscriptionId              = "6d5b60f5-24e0-4722-acbb-ad6b9ee7675f"
        partnerCertificatesRequired = $false
    }
)