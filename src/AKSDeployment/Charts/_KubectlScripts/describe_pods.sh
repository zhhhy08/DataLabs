# Usage: az aks command invoke -n abcinteusaks -g DataLabsabcRG-eastus --command "chmod +x testing.sh; ./testing.sh bcdr-solution partner-namespace 10" --file .

namespace=$1
tail=${2:-"5"}
appname=${3:-""}

# Describe Failing Pods
echo "Getting Failing Pods"
if [ -z $appname ] # AppName is empty
then
    echo "kubectl get pods -n $namespace --field-selector=status.phase!=Running -o name"
    failing_pods=$(kubectl get pods -n $namespace --field-selector=status.phase!=Running -o name)
    pods=$(kubectl get pods -n $namespace -o name)
else
    echo "kubectl get pods -n $namespace --selector=app==$appname --field-selector=status.phase!=Running -o name"
    failing_pods=$(kubectl get pods -n $namespace --selector=app==$appname --field-selector=status.phase!=Running -o name)
    pods=$(kubectl get pods -n $namespace --selector=app==$appname -o name)
fi
echo $failing_pods

if [ -z "$pods" ]
then
    echo "There are no pods with the specified namespace (and appname). Please check your input values"
    exit
fi

if [ -z "$failing_pods" ]
then
    echo "There are no failed pods... Describing random pod"
    echo "kubectl describe $pods[0] -n $namespace"
    kubectl describe $pods[0] -n $namespace
    echo ""
else
    echo "Describing failing pods"
    for pod in $failing_pods
    do
        echo $pod
        echo "kubectl describe $pod -n $namespace" 
        kubectl describe $pod -n $namespace
        echo ""
    done
fi


echo ""

# Getting Logs
echo "Getting Pod Logs"
if [ -z "$appname" ] # AppName is empty
then
    echo "kubectl get pods -o=name -n $namespace"
    pods=$(kubectl get pods -o=name -n $namespace)
    echo $pods

    echo "kubectl get pods -o=jsonpath='{range .items[*]}{.spec.containers[*].name}{\"\n\"}{end}' -n $namespace | uniq"
    containers=$(kubectl get pods -o=jsonpath='{range .items[*]}{.spec.containers[*].name}{"\n"}{end}' -n $namespace | uniq)
    echo $containers
else
    echo "kubectl get pods -o=name --selector=app==$appname -n $namespace"
    pods=$(kubectl get pods -o=name --selector=app==$appname -n $namespace)
    echo $pods

    echo "kubectl get pods -o=jsonpath='{range .items[?(@.metadata.labels.app==\"$appname\")]}{.spec.containers[*].name}{\"\n\"}{end}' -n $namespace | uniq"
    containers=$(kubectl get pods -o=jsonpath='{range .items[?(@.metadata.labels.app=="'$appname'")]}{.spec.containers[*].name}{"\n"}{end}' -n $namespace | uniq)
    echo $containers
fi

for pod in $pods
do
    echo $pod
    if [ $tail -lt 0 ]
    then
        for container in $containers
        do
            echo $container
            kubectl logs $pod -n $namespace -c $container
            echo ""
        done
    else
        for container in $containers
        do
            echo $container
            kubectl logs $pod -n $namespace -c $container --tail $tail
            echo ""
        done
    fi
    echo ""
done

echo "kubectl get pods -n $namespace"
kubectl get pods -n $namespace