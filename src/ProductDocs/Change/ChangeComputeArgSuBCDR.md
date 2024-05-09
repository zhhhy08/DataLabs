# BCDR : ARG Scale Unit
This TSG refers to BCDR steps for Change Compute in ARG SU that is processing *tracked, container and Geneva health datasests*.  
**NOTE**: For any BCDR in general, please involve the Incident manager, respective team oncall (ARB in this case) and if needed, component team as well (change compute here)

## Actions
1. Obtain ACIS JIT for all prod regions
2. Download the PowerShell script to failover Change Compute traffic either from [here](https://msazure.visualstudio.com/One/_git/Mgmt-Governance-ResourcesCache?path=/src/Deployment/ScriptUtils/ChangeComputeTrafficFailover.ps1&version=GBmain) or from [DevOps Artifacts](https://msazure.visualstudio.com/One/_build?definitionId=180893) path: `drop_build_release_x64\release-x64\Deployment\ScriptUtils\ChangeComputeTrafficFailover.ps1`
3. Execute the script for the affected region as follows `.\ChangeComputeTrafficFailover.ps1 {region_shorthand} {health_status}`. For example, if you need to shift traffic out of `NEU`, you would run `.\ChangeComputeTrafficFailover.ps1 neu uneahlthy`
4. To accomodate the increase in traffic in the region failed over to, [scale out](https://msazure.visualstudio.com/One/_git/Mgmt-Governance-ResourcesCache?path=/src/TSGs/Change/Monitors/ChangeComputeSLA.md&version=GBmain&_a=preview) the VMSS in that region by 10 nodes (add 10 to the existing amount)

   | Unhealthy region | Region to scale out |
   |------------------|---------------------|
   | EUS              | WUS2                |
   | WUS2             | EUS                 |
   | SEA              | NEU                 |
   | NEU              | WEU                 |
   | WEU              | NEU                 |

   So if you ran `.\ChangeComputeTrafficFailover.ps1 neu uneahlthy`, scale out traffic in WEU

5. Set the `SharedSection/MaximumNumberOfBlobsInList` config to 100
6. **Once the issue in the affected region has been mitigated**
   - Restore traffic back to its original state by running `.\ChangeComputeTrafficFailover.ps1 {region_shorthand} healthy`. The region shorthand in this case does not matter (`.\ChangeComputeTrafficFailover.ps1 eus healthy` and `.\ChangeComputeTrafficFailover.ps1 neu healthy` will do the same thing)
   - Set the `SharedSection/MaximumNumberOfBlobsInList` config to 10

**WARNING**: In the rare event that **multiple** regions need traffic shifting (for example, if both WUS2 and SEA are problematic at the same time), contact a Change Compute member ASAP and ask them to handle the failover instead.  
7. Please involve Change Compute team for this step. It's possible that even after all the above steps, the region failed over to will **not** be able to handle the increased load (we can confirm this is the case if SLA sev 2's start firing in the region failed over to). For example, EUS takes on far more load (and correspondingly has more nodes in its VMSS) than WUS2 (same with WEU and NEU)
. For example, if we failover all of EUS's traffic to WUS2, WUS2 may not be able to process all messages within SLO. In this case, we may need 
   - A manual redistribution of traffic that shifts some traffic else where. Please note that it is **Multiplexer Partner Routing One** that sends traffic to various CC regions (traffic is partitioned based on subscription or tenant Id).
   - So, pull **Multiplexer Partner Routing One**'s most recent `EventHubSet` config value from ACIS actions and modify it to reditribute traffic more evenly among the afffected regions 
      - **IMPORTANT**: Modify **all** the regions of' Multiplexer Partner Routing''s `EventHubSet` accordingly). We have it so that any region of Change Compute can process notifications from any other region of Change Compute.
      - For example, if the EventHubSet value is `CC-Tracked-sea:changecompute:`**0:3**`;CC-Tracked-neu:changecompute:`**4:11**`;CC-Tracked-weu:changecompute:12:31;CC-Tracked-wus2:changecompute:32:43;CC-Tracked-eus:changecompute:44:63;CC-Container-sea:changecomputeset2:`**0:3**`;CC-Container-neu:changecomputeset2:`**4:11**`;CC-Container-weu:changecomputeset2:12:31;CC-Container-wus2:changecomputeset2:32:43;CC-Container-eus:changecomputeset2:44:63`, you can reduce the amount of traffic going into SEA and redirect to NEU  by changing it to `CC-Tracked-sea:changecompute:`**0:1**`;CC-Tracked-neu:changecompute:`**2:11**`;CC-Tracked-weu:changecompute:12:31;CC-Tracked-wus2:changecompute:32:43;CC-Tracked-eus:changecompute:44:63;CC-Container-sea:changecomputeset2:`**0:1**`;CC-Container-neu:changecomputeset2:`**2:11**`;CC-Container-weu:changecomputeset2:12:31;CC-Container-wus2:changecomputeset2:32:43;CC-Container-eus:changecomputeset2:44:63`
   OR   
   - scaling out the VMSS in the affected region