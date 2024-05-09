cloud=$1
values_file=$2
service=$3
namespace=$4
appname=$5
aksfolder=$6

echo "Cloud: "$cloud
echo "Values File: "$values_file
echo "Service: "$service
echo "Namespace: "$namespace
echo "AppName: "$appname
echo "AKSFolder: "$aksfolder

echo "cd $aksfolder"
curr_dir=$(pwd)
cd $aksfolder

echo ""

lower_service=$(echo "$service" | tr '[:upper:]' '[:lower:]') # transformation: to lower

# DRY RUN - Uncomment for testing
# echo "helm template -f BaseValueFiles/dataLabsServices.yaml -f BaseValueFiles/dataLabsImages_$cloud.yaml -f $values_file $lower_service $service"
# helm template -f BaseValueFiles/dataLabsServices.yaml -f BaseValueFiles/dataLabsImages_$cloud.yaml -f $values_file $lower_service $service

echo "helm uninstall $lower_service"
helm uninstall $lower_service

echo "helm install -f BaseValueFiles/dataLabsServices.yaml -f BaseValueFiles/dataLabsImages_$cloud.yaml -f $values_file $lower_service $service"
helm install -f BaseValueFiles/dataLabsServices.yaml -f BaseValueFiles/dataLabsImages_$cloud.yaml -f $values_file $lower_service $service

sleep 5
cd $curr_dir
./_KubectlScripts/describe_pods.sh $namespace 10 $appname
