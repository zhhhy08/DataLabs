#Create configMap from file and save it into yaml file
kubectl create configmap partner-manifest -n partner-namespace --from-file=manifest.json -o yaml --dry-run=client > manifest-config.yaml

#Create ConfigMap
kubectl apply -f manifest-config.yaml