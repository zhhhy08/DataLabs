# Create Values file for New Partner

1. Copy an existing values file for a partner into a new file (name it `{team}_{cloud}.yaml` in `Charts/PartnerAKS`).
    - Copy the values file that matches your cloud. There are differences between configurations in INT and Canary's.
2. Rename the storage accounts, eventhubs, and service bus's with the new partner's acronym (i.e. transferring from abc values file to id mapping values file will be `abccanaryecyipehns0/0abccanaryecyipeh` with `abc` prefix to `idmcanaryecyipehns0/0idmcanaryecyipeh` with `idm` prefix). Please also track the count of expected SA's, EH's, and SB's as there may be more resources (differing by prefix/suffix of 0->1->2->3)
3. Steps below require infra to be complete and obtain JiT access.
4. Update the subscription in `genevaAccounts.partner.miResourceId` and `genevaAccounts.datalabs.miResourceId`.
5. Update the clientId's after infrastructure deployment completes: 
    - Replace `ioServiceAccount.clientId` with `{prefix}canaryioconnectorid`'s `clientId`
    - Replace `resourceProxyServiceAccount.clientId` with `{prefix}canaryrfproxyid`'s `clientId`
    - Replace `mdm.configData`'s objectId with `{prefix}canaryecyaks-agentpool`'s `clientId`. Also notify DataLabs team to update Geneva account for authentication of this objectId.
6. Resolve Monitoring
    - If the partner is an internal partner:
        1. Comment out `genevaAccounts.partner` and `socat.diagnosticEndpoints.mdsdPartner` to only use a single MDSD and MDM for diagnostics.
    - If the partner is an external partner:
        1. Input your Geneva GCS settings into `genevaAccounts.partner`.
        2. For partners, update the security for the logs and metrics geneva account to allow the objectId of `{prefix}canaryecyaks-agentpool`. **This will cause failures in the MonitorService if this is not added!**
