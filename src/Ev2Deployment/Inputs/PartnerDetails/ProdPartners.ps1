
$locations = @("westus3", "eastus", "swedencentral", "northeurope")

$asiaLocations = @("southeastasia", "eastasia")

$datalabs = @{
    locations      = $locations + $asiaLocations
    subscriptionId = "68d38d95-0964-447c-8840-f381378f9253"
}


$partners = @( 
    @{
        locations                   = $locations
        partnerAcronym              = "abc"
        subscriptionId              = "caf615cd-215f-4706-8a49-600ecdfc59dc"
    },
    @{
        locations                   = $locations + $asiaLocations
        partnerAcronym              = "idm"
        subscriptionId              = "9b776e32-83f7-4e98-b234-f43612dea78d"
    },
    @{
        locations                   = $locations + $asiaLocations
        partnerAcronym              = "sku"
        subscriptionId              = "78e5e697-0cb0-4da8-9f0d-b36400fe6bce"
    },
    @{
        locations                   = $locations + $asiaLocations
        partnerAcronym              = "cap"
        subscriptionId              = "75c6bdbd-d177-465c-bd3c-b340c1333167"
    },
    @{
        locations                   = $locations + $asiaLocations
        partnerAcronym              = "ras"
        subscriptionId              = "8956daf3-20ca-419f-bb01-aab21c6a63f4"
    }
)