# ABC Center TSG

### Guidelines for INT environment

 we all understand the INT can get volatile as we currently only have
 one environment. 

 If INT has broken and anyone has reported/asked we should finish the
 root causing of the issue in 24 hours and fixing the issue within 48
 hours. If it is taking longer to fix, we should inform proactively and
 consider reverting any change causing the break. 

 Guidelines for Canary environment- We all are working hard towards the
 Canary deployment and we should have it by end of May (possibly
 earlier) - the expectation would be that we will be deploying first in
 INT to make sure things don't break and only then deploy to Canary. 

 Something could still break in Canary, which is ok as we all
 understand that canary would be fixed asap (same day) or we will
 revert the changes causing the issue to make Canary healthy again. 

 **Troubleshooting for ABC + data labs components.** 

 **[Ownership areas for diagnostics]** 

-   ASR data sync - Pratul 

-   Backup data sync - Khalid 

-   ARN + Input event hub + Output + Sailfish + Kusto - Jing 

-   IO Connector/ Cache/ Blob - Jae 

-   ABC partner solution - Ayesha, Pratul, Sarita 

-   Resource fetcher proxy - KG 

-   Resource fetcher - Arpita 

-   Portal - Shriladha 

-   Monitoring - Dylan 

-   Data labs Infra - KG, Pooja 

 To figure out the potentially root cause easily (self-serve steps)  

 Data for ABC center moves in the following sequence - 

 ASR/ Backup data sync -> ARN -> Input event hub -> IO connector ->
 ABC partner -> Resource fetcher proxy -> Resource fetcher -> IO
 connector -> Blob/ Cache/ Output event hub -> Sailfish -> Portal 

 For INT environment, here is a sample Geneva log to figure out the
 culprit service -  <https://jarvis-west.dc.ad.msft.net/A56A7F3C from
 the logs and then can engage with the owner using list provided above
 and pinned in this group 

 **Self-service steps** 

#### 1. Is Backup pushing data? - Khalid 

 Execute:
 \[[Web](https://dataexplorer.azure.com/clusters/https%3a%2f%2fmabprod1.kusto.windows.net/databases/MABKustoProd1?query=H4sIAAAAAAAEAD2NTU7DMBBG95V6BysrWIQQXBJl0UpVKVIXIGh6gbE9oRaJx7LHQZE4PASkbL%2bf94778%2bE9YcKWgeO%2b71ff4uuKAcVbQG0jXuwwd4MXOwEfdFOaWwHOiCf0PU0DOn6FAcV2KzLUU%2b7J3JfZAjk6tjxdJv%2b%2feLE6UKSO786oacQwtRhGqzEWI6SeY6FAfyb%2fDCpYHQsfiFGzJXcgx2AdhiVEc2IcYibWi61NKupg%2fXw4mT9jCQ%2byLE2TV1X3mG9qI3NoOpXXIGsDlWzkRmUzQVNy%2fMtar34AJ2ybOBMBAAA%3d)\]
 \[[Desktop](https://mabprod1.kusto.windows.net/MABKustoProd1?query=H4sIAAAAAAAEAD2NTU7DMBBG95V6BysrWIQQXBJl0UpVKVIXIGh6gbE9oRaJx7LHQZE4PASkbL%2bf94778%2bE9YcKWgeO%2b71ff4uuKAcVbQG0jXuwwd4MXOwEfdFOaWwHOiCf0PU0DOn6FAcV2KzLUU%2b7J3JfZAjk6tjxdJv%2b%2feLE6UKSO786oacQwtRhGqzEWI6SeY6FAfyb%2fDCpYHQsfiFGzJXcgx2AdhiVEc2IcYibWi61NKupg%2fXw4mT9jCQ%2byLE2TV1X3mG9qI3NoOpXXIGsDlWzkRmUzQVNy%2fMtar34AJ2ybOBMBAAA%3d&web=0)\]
 \[[Web
 (Lens)](https://lens.msftcloudes.com/v2/#/discover/query//results?datasource=(cluster:mabprod1.kusto.windows.net,database:MABKustoProd1,type:Kusto)&query=H4sIAAAAAAAEAD2NTU7DMBBG95V6BysrWIQQXBJl0UpVKVIXIGh6gbE9oRaJx7LHQZE4PASkbL%2bf94778%2bE9YcKWgeO%2b71ff4uuKAcVbQG0jXuwwd4MXOwEfdFOaWwHOiCf0PU0DOn6FAcV2KzLUU%2b7J3JfZAjk6tjxdJv%2b%2feLE6UKSO786oacQwtRhGqzEWI6SeY6FAfyb%2fDCpYHQsfiFGzJXcgx2AdhiVEc2IcYibWi61NKupg%2fXw4mT9jCQ%2byLE2TV1X3mG9qI3NoOpXXIGsDlWzkRmUzQVNy%2fMtar34AJ2ybOBMBAAA%3d&runquery=1)\]
 \[[Desktop
 (SAW)](https://mabprod1.kusto.windows.net/MABKustoProd1?query=H4sIAAAAAAAEAD2NTU7DMBBG95V6BysrWIQQXBJl0UpVKVIXIGh6gbE9oRaJx7LHQZE4PASkbL%2bf94778%2bE9YcKWgeO%2b71ff4uuKAcVbQG0jXuwwd4MXOwEfdFOaWwHOiCf0PU0DOn6FAcV2KzLUU%2b7J3JfZAjk6tjxdJv%2b%2feLE6UKSO786oacQwtRhGqzEWI6SeY6FAfyb%2fDCpYHQsfiFGzJXcgx2AdhiVEc2IcYibWi61NKupg%2fXw4mT9jCQ%2byLE2TV1X3mG9qI3NoOpXXIGsDlWzkRmUzQVNy%2fMtar34AJ2ybOBMBAAA%3d&saw=1)\] 
 <https://mabprod1.kusto.windows.net/MABKustoProd1 

 EARCQueueStatsAll 

 \| where PreciseTimeStamp \ ago(1d) and DeploymentName ==
 \"ecy-pod01\" 

 \| where EntityType ==
 \"Microsoft.RecoveryServices/vaults/backupFabrics/protectionContainers/protectedItems\" 

 \| where SubscriptionId == \"1a2311d9-66f5-47d3-a9fb-7a37da63934b\" 

 \| count 

 #### 2. Is ASR pushing data? - Pratul 

__TODO__
  
#### 3. Is input event hub receiving the or not? -Jing/ Jae 
 [abc-test-eastus-input-1 - Microsoft
 Azure](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/ABC-eastus/providers/Microsoft.EventHub/namespaces/abc-test-eastus-input-1/eventhubs/abc-test-eastus-input-1/overview) 

 #### 4. Is ABC receiving events or not? -Ayesha/ Sarita/ Pratul 

 ABC processed the events or not - from Span/ Log table - \"success
 field as true\" 

 ABC message details can be viewed from env_dt_traceId 

 <https://portal.microsoftgeneva.com/s/1C4C09E2 

#### 5. ARM client query to fetch UPI created above - Jing 

 armclient POST
 /providers/Microsoft.ResourceGraph.PPE/resources?api-version=2019-04-01
 \"{ \'subscriptions\':
 \[\'76fb41ba-5387-4dff-89f5-24ae457ade99\',\'1a2311d9-66f5-47d3-a9fb-7a37da63934b\'\],
 \'query\': \'TestProxyResources \| where type =\~
 \\\'Microsoft.AzureBusinessContinuity/UnifiedProtectedItems\\\' \|
 take 5\',\'options\':{\'maxRows\':100}}\" 

 Check from Portal 
 <https://aka.ms/AzureBusinessContinuity/develop 

 <https://aka.ms/BCDRcenter/develop 
