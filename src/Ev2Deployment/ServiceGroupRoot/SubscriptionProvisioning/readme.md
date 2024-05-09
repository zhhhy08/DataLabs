**Subscription provisioning INT file**

The SubscriptionProvisioning.RolloutParameters.Int.json file was deleted from this repository when we added another subscription in INT.
the INT specific subscription provisioning file had a hardcoded value for "backfilledSubscriptionId" config as previously there was just one subscription in INT. This stopped working when we added a new subscription as it tried to backfill the new subscription key with the old sunscription id. 

Now, the rolloutParameterspath in Service Model is pointed to the Prod subscriptionProvisioning parameters file as subscription key is already registered for each partner in INT. 

Every time a new partner is onboarded, register a new subscription key using the following command:

The subscriptionKey should be in a format: DataLabs$config(stamp_$stamp().partner.partnerAcronym)IntSubscriptionLibrarySub

https://ev2docs.azure.net/getting-started/tutorial/orchestration/subscription.html?q=backfill%20subscription&tabs=tabid-1


$ServiceId = "<replace this with the Guid of your service, retrieved from the Service Tree portal in the prepare step of this tutorial>"
$SubscriptionIdForBackfill = "<replace this with the subscription id your want to backfill>"

Register-AzureServiceSubscription -ServiceIdentifier $ServiceId -SubscriptionKey "<new subscription Key>" -SubscriptionId $SubscriptionIdForBackfill -RolloutInfra Test


To know more about subscription Provisioning, read this:
https://ev2docs.azure.net/features/service-artifacts/subscriptionProvisioningParameters.html#declarative-backfill

