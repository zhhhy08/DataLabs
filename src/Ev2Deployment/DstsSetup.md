# Creating dSTS regional apps

## Prequisites

[dSCM Powershell Module](https://dev.azure.com/msazure/AzureCoreSecurityServices/_wiki/wikis/AzureCoreSecurityServices.wiki/20653/Azure-Security-Configuration-Management?anchor=powershell-module)

[Load and connect the module](https://msazure.visualstudio.com/AzureCoreSecurityServices/_wiki/wikis/AzureCoreSecurityServices.wiki/20658/dSCM-PowerShell-Samples?anchor=loading-module-and-connecting)

## Steps

### Step 1: Create an application group

> [!NOTE]
> Execution of these cmdlets may require approval from someone who has the right permissions in your team.

```powershell
$SawDeviceEnforcement = "SAWFull"
$serviceTreeId = "[Put you service tree Id here]"
$applicationGroupName = "[Your App group name]"
$group = New-DscmDstsApplicationGroup -ServiceTreeId $serviceTreeId -Name $applicationGroupName -CloudId "PROD"  -SawDeviceEnforcement $SawDeviceEnforcement -IsMfaEnabled $true
$configRequestId = Add-DscmDstsApplicationGroup -Object $group
$partnerAcronym = "[Your acryonym here]"
```

### Step 2: Create regional dSTS apps linked to above group

> [!NOTE]
> While Key Vault and certificates are not necessary to create the apps, it is better to have them provisioned and verify the subject name before proceeding.

```ps
$applicationName = "[Your app name]"
$groupId = (Get-DscmDstsApplicationGroup -ServiceTreeId $serviceTreeId | Where-Object { $_.Name -eq $applicationGroupName}).Id
$clientIdentity = New-DscmSubjectNameBasedApplicationClientIdentity -SubjectNames @("datalabs-$partnerAcronym-dsts-client.prod.gov-rp-art")
```

Run below commands for as many regions the partner is deployed in and replace dsts instance with one of the following values. You can choose any other regions as well, but these are the regions Datalabs is in.

- uswest3-dsts.dsts.core.windows.net
- useast-dsts.dsts.core.windows.net
- europenorth-dsts.dsts.core.windows.net
- swedenc-dsts.dsts.core.windows.net
- asiasoutheast-dsts.dsts.core.windows.net
- asiaeast-dsts.dsts.core.windows.net

```ps
$request = New-DscmCreateDstsApplicationRequest -Name $applicationName -ServiceTreeId $serviceTreeId -DstsInstance "uswest3-dsts.dsts.core.windows.net" -SawDeviceEnforcement $sawDeviceEnforcement -IsMfaEnabled $true -IsProduction $true -ClientIdentity $clientIdentity -GroupId $groupId

$configRequestId = Add-DscmDstsApplication -Object $request
```

### Step 3. Validate

Run these commands for every dsts instance in which app was previously created in.

```powershell
$app = Get-DscmDstsApplication -DstsInstance "useast-dsts.dsts.core.windows.net" -Name $applicationName
$app.ClientIdentity.SubjectNameBasedIdentity 
```

shows `{datalabs-$partnerAcronym-dsts-client.prod.gov-rp-art}`
