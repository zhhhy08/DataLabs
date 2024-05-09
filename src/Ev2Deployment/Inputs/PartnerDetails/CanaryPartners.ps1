
$locations = @("eastus2euap")

$idmPartner = @{
    locations                   = $locations
    partnerAcronym              = "idm"
    subscriptionId              = "bb596f76-3c15-4e59-af1f-7b0b7ff25f4b"
}

$capPartner = @{
    locations                   = $locations
    partnerAcronym              = "cap"
    subscriptionId              = "67593859-7d8e-4115-858d-e371e0461a57"
}

$datalabs = @{
    locations      = $locations
    subscriptionId = "c66bb4b1-b928-4268-b925-cc62eff17dad"
}

$partners = @( 
    @{
        locations                   = $locations
        partnerAcronym              = "abc"
        subscriptionId              = "75e7e676-7873-4432-98bd-01a68cc5bca1"
    },
    $idmPartner,
    $capPartner
)