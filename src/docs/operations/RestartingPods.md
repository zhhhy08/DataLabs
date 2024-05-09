# Restarting Pods

### Reasoning
Restarting pods is the generic way for mitigation for unknown issues. If there was a recent deployment, please try to revert the deployment instead since the error is more likely due to bad bits.

### Selecting Pods to Restart
For restarting pods, please restart the pods that are causing the problem. You can look through dashboards to try to assess this.

If it requires all services to restart in PartnerAKS, please restart in the order of:
1. ResourceProxy
2. PartnerService
3. IOService

For ResourceFetcherAKS, please just restart ResourceFetcherService. Reminder that restarting Resource Fetcher will affect all services in that region!

### Kubectl Actions
**NOTE**: Plesae get an ACK from the Data Labs team before running actions. Once run, please share results with the Data Labs team.

Prerequisites: [Run Kubectl Commands](./RunKubectlCommands.md)

IOService:
```
$numReplicas=kubectl get deployments -n solution-namespace -o jsonpath='{.items[*].spec.replicas}'

# Scaling down (removing the pods)
kubectl scale deployment solution-io -n solution-namespace --replicas=0
# Wait ~10 seconds
kubectl get pods -n solution-namespace # Confirm all pods for IOService are gone 

# Scaling back up (reinitializing the pods)
kubectl scale deployment solution-io -n solution-namespace --replicas=$numReplicas
kubectl get pods -n solution-namespace # Check Pods Restarted
```

PartnerService:
```
$numReplicas=kubectl get deployments -n partner-namespace -o jsonpath='{.items[*].spec.replicas}'
$partnerAppName=kubectl get deployments -n partner-namespace -o jsonpath='{.items[].metadata.name}'

# Scaling down (removing the pods)
kubectl scale deployment $partnerAppName --replicas=0 -n partner-namespace
# Wait ~10 seconds
kubectl get pods -n partner-namespace # Confirm all pods for partner are gone 

# Scaling back up (reinitializing the pods)
kubectl scale deployment $partnerAppName --replicas=$numReplicas -n partner-namespace
kubectl get pods -n partner-namespace # Check Pods Restarted
```

ResourceProxy:
```
kubectl rollout restart daemonset resource-proxy -n solution-namespace

# Check Rollout Status of resource-proxy app
kubectl rollout status daemonset resource-proxy -n solution-namespace

# Check that pods are Running
kubectl get pods -n solution-namespace 
```

ResourceFetcherService
```
$numReplicas=kubectl get deployments -n resource-fetcher-namespace -o jsonpath='{.items[*].spec.replicas}'

# Scaling down (removing the pods)
kubectl scale deployment resource-fetcher -n resource-fetcher-namespace --replicas=0
# Wait ~10 seconds
kubectl get pods -n resource-fetcher-namespace # Confirm all pods for IOService are gone 

# Scaling back up (reinitializing the pods)
kubectl scale deployment resource-fetcher -n resource-fetcher-namespace --replicas=$numReplicas
kubectl get pods -n resource-fetcher-namespace # Check Pods Restarted 
```

### Extra Notes
FYI, scaling the deployments for restarting is more efficient than utilizing `kubectl restart` because the restart is for the entire deployment and will adhere to the upgradeRollout strategy, which is deploying one pod at a time. Since we just want the pods to restart their processing, we scale down the replicas to 0 and then scale up the replicas to its original size to get new pods.

ResourceProxy cannot do this though since it is a daemonset. We need to restart the daemonset completely.