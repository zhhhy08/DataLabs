# Rollback Services

### Reasoning
New bits are causing a regression in services. **Please involve the Data Labs team if you are restarting the cache service.**

Services that are possible to restart: ioservice, resourceproxy, partnerservice, resourcefetcherservice, monitorservice and cacheservice.

### Kubectl Actions
Please run these actions and share the results with the Data Labs team.

Here are some prerequisites:
1. Obtain a JiT with "Owner" role of the subscription for the partner
2. Give yourself the role of "Azure Kuberenetes Cluster RBAC Cluster Admin" for the AKS cluster you want to run actions on.
3. Open the AKS in Azure Portal, and on the lefthand menu under "Kubernetes resources" should be a page for "Run command". You can run the following commands below.
4. After completing these actions, remove the RBAC role that you created in step 2.

```
# Check for old version existing
kubectl get pods -A

# Get revision history for deployment
$service = "<Insert Service Name Here>"
helm history $service

# Note down the latest revision
helm rollback $service <revision>

# Validate
kubectl get pods -A # Verify new services are there and have the "Running" status
helm history $service # Verify history that rollback was successful
```
