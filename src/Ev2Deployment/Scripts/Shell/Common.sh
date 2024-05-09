loginAndUnzip() {
    chartsTar=$1
	
    echo "Initial Folder Contents"
    ls

    #  login into azure, set subscription
    echo "---------------------------------------"
    echo "Login cli using managed identity"
    az login --identity
    echo "---------------------------------------"

    echo "creating and moving to temp folder, needed for unzip rights"
    TMP_FOLDER=$(mktemp -d)
    cd $TMP_FOLDER

    #  copy the files - charts folder
   wget -O "charts.tar" ${CHARTS_TAR}

    #unzip tar folder
    tar -xvf charts.tar #Folder structure of PartnerAKS and ResourceFetcherAKS should be created

}

checkAge() {
    outFile="$1"
    # namespace="$2"
    # appNameLowerCase="$3"
	
    check=false
    ageFile=$(mktemp)
 
    #setting max age to 15 mins considering retrying thrice with 5 mins break.
    podMaxAge="120"  

    #To check age of a pod, only consider pods related to the app which is being deployed.
    #Two apps get deployed to solution-namespace, hence they have to be filtered accordingly.
    
    grep -v -e NAME $outFile > $ageFile
  

    #This will read each column from kubectl results which is stored in $outFile
    while read name ready status restarts age; do

        #If Pod age is in days or hours, the pod is too old.
        if [[ $age == *"d"* ]]; then
            age=${age%%d*}    
            age=$((age * 24 * 60))
            check=true
            break
        elif [[ $age == *"h"* ]]; then
            age=${age%%h*}
            age=$((age * 60))
        fi

        #If pod age contains value like 5m14s, then it will extract 5 out of it as we want to compare age in minutes.
        if [[ $age == *"s"* ]]; then
            age="${age%%[!0-9]*}"
        else
            #age=12m, this command will remove "m"
            age=${age%%m*}
        fi
        #Checking if Pod is older than 60 minutes.
        if [[ "$age" -gt "$podMaxAge" ]]; then
            check=true
            break
        fi
    done < $ageFile

    echo $check
}

checkExitCode(){
    outFile="$1"
    check=false

    if grep -q "exitcode" "$outFile"; then
        exitcode_line=$(grep "exitcode" "$outFile")
		
        #Fetching the exit code value if present
        exitcode_value=${exitcode_line#*=}
        if [[ "$exitcode_value" -ne 0 ]]; then
            check=true
        fi
    else 
        check=true
    fi

    echo $check
}

checkExitCodeAndExit() {
    outFile="$1"

    cat $outFile
    check=$(checkExitCode $outFile)
    if [[ "$check" = true ]];then
        exit 1;
    fi
}

runAKSCommand() {
   aks_command="$1"
   # Temporarily disable "exit immediately" behavior
   akscommand=$(cat $aks_command)
   set +e
   counter=1
   result_status=1
   while [[ $counter -le 4 ]]; do
       eval $akscommand
        if [ $? -eq 0 ]; then
             echo "Command successful: $akscommand"
             result_status=0
             break
        else
            echo "Command failed: $akscommand"
            sleep 5 # retry
         fi
   echo "Retry Counter is $counter"
   ((counter++))
   done
   # Re-enable "exit immediately" behavior
   set -e
   echo "Result status of Run AKSCOMMAND function: $result_status"
}

deployApp() {

    cloudName="$1"
    resourceGroup="$2"
    aksName="$3"
    component="$4"
    podNamespace="$5"
    appName="$6"
    valuesFilename="$7"
    arrayIndex="$8"

    if [ -n "$arrayIndex" ]; then
        setVarString="--set setArrayIndexVar=${arrayIndex}"
        ((appSuffixValue = arrayIndex + 1))
        appNameLowerCase="${appName,,}${appSuffixValue}"
    else
        setVarString=""
        appNameLowerCase=${appName,,}
    fi

    # Create Temporary file
    TMPFILE=$(mktemp)
    CHECKAGEFILE=$(mktemp)
    COMMAND_FILE=$(mktemp)

    if [ "$component" == "ResourceFetcher" ]; then
        valuesFileString="./BaseValueFiles/rfServices.yaml"
    else
        valuesFileString="./BaseValueFiles/dataLabsServices.yaml -f ./BaseValueFiles/dataLabsImages_${CloudName^}.yaml"
    fi
    echo "----------------------------------------"
    count=1
    revision=0 # first time helm release not found
    while [ "$count" -lt 4 ]; do
        echo "${appName} latest upgrade result"
        echo "az aks command invoke --resource-group \"${resourceGroup}\" --name \"${aksName}\" --command \"helm history ${appNameLowerCase} --max 1 -o yaml\""
        # Saving the command in to Temporary file as input to runAKSCommand function 
        echo 'az aks command invoke --resource-group "${resourceGroup}" --name "${aksName}" --command "helm history ${appNameLowerCase} --max 1 -o yaml" 2>&1 > $TMPFILE' > $COMMAND_FILE
        runAKSCommand $COMMAND_FILE
        if [ "$result_status" -eq 0 ]; then
            result_exit_code=$(checkExitCode $TMPFILE)
            cat $TMPFILE
            echo "Result exit code true or false: $result_exit_code"
            if [ "$result_exit_code" == "false" ]; then
                echo "az aks command helm history executed successfully"
                echo "checking helm status and revision..."
                #Check recent helm upgraded version
                revision=$(cat $TMPFILE | grep "revision" | sed 's/[^0-9]*\([0-9]*\).*/\1/')
                echo "Helm release ${appName} version: $revision"
                status=$(cat $TMPFILE | grep "status:" | sed 's/status: \([a-zA-Z]*\)/\1/')
                status=$(echo $status | tr -d ' ')
                echo "Status of latest deployment: $status"
                if [[ "$status" != *"pending"* ]]; then
                    echo "Helm Status: $status"
                    echo "Helm revision: $revision"
                    echo "Going to run helm upgrade.."
                    break
                else
                    echo "Helm upgrade is pending.. Status: $status"
                    echo "Exiting scirpt because previous upgrade was not successfull"
                    exit 1  # exit if status is not deployed
                fi
            else
                echo "az aks Command helm history failed with exit code 1"
                echo "checking if helm ${appNameLowerCase} is new install.."
                error=$(cat $TMPFILE | grep "Error:")
                error=${error,,}
                echo "error: $error"
                if [[ "$error" == *"release: not found"* ]]; then
                    echo "Helm Release not found So ${appNameLowerCase} New Install.... Going to run helm Upgrade "
                    break
                fi
            fi
        else
            echo "Error executing command.. Retrying again.."
        fi
        sleep 10
        echo "Retry Counter is $count"
        ((count++))
    done
    echo "----------------------------------------"

    # # Helm Upgrade
    count=1
    helmupgradesuccess=false
    while [ "$count" -lt 10 ]; do
        if [ "$helmupgradesuccess" == "false" ]; then
            if [ "$count" == 1 ]; then
                echo "az aks command invoke --resource-group ${resourceGroup} --name ${aksName} --command \"helm upgrade --install --atomic --wait --timeout 3600s --force -f ${valuesFileString} -f ${valuesFilename} ${appNameLowerCase} ${appName} ${setVarString}\" --file ."
                echo 'az aks command invoke --resource-group "${resourceGroup}" --name "${aksName}" --command "helm upgrade --install --atomic --wait --timeout 3600s --force  -f ${valuesFileString} -f ${valuesFilename} ${appNameLowerCase} ${appName} ${setVarString}" --file . 2>&1 >$TMPFILE' > $COMMAND_FILE
                runAKSCommand $COMMAND_FILE
                if [ "$result_status" -eq 0 ]; then
                    result_exit_code=$(checkExitCode $TMPFILE)
                    echo "Result exit code true or false: $result_exit_code"
                    cat $TMPFILE
                    if [ "$result_exit_code" == "false" ]; then
                        echo "az aks command helm upgrade executed successfully"
                        revision_line=$(cat $TMPFILE | grep "REVISION:")
                        upgradedrevision=$(echo "$revision_line" | cut -d ':' -f2 | xargs)
                        echo "upgraded revision is: $upgradedrevision"
                        status_line=$(cat $TMPFILE | grep "STATUS:")
                        status=$(echo "$status_line" | cut -d ':' -f2 | xargs)
                        echo "Status of upgrade deployment: $status"
                        if [ "$status" == "deployed" ] && [ "$revision" != "$upgradedrevision" ]; then
                            echo "Helm Status: $status"
                            helmupgradesuccess=true
                        else
                            echo "Helm upgrade is not successful. Status: $status"    
                        fi
                    else
                        echo "az aks Command helm upgrade failed with exit code $result_exit_code"
                        echo "checking the helm status....."
                        sleep 300 # sleep 5 mins to give more time for the command
                        echo "az aks command invoke --resource-group \"${resourceGroup}\" --name \"${aksName}\" --command \"helm status ${appNameLowerCase} --show-resources\""
                        echo 'az aks command invoke --resource-group "${resourceGroup}" --name "${aksName}" --command "helm status ${appNameLowerCase} --show-resources" 2>&1 > $TMPFILE' > $COMMAND_FILE
                        runAKSCommand $COMMAND_FILE
                        if [ "$result_status" -eq 0 ]; then
                            result_exit_code=$(checkExitCode $TMPFILE)
                            echo "Result exit code true or false: $result_exit_code"
                            cat $TMPFILE
                            if [ "$result_exit_code" == "false" ]; then
                                echo "az aks command helm status --show-resources executed successfully"
                                revision_line=$(cat $TMPFILE | grep "REVISION:")
                                upgradedrevision=$(echo "$revision_line" | cut -d ':' -f2 | xargs)
                                echo "helm upgrade revision: $upgradedrevision"
                                status_line=$(cat $TMPFILE | grep "STATUS:")
                                status=$(echo "$status_line" | cut -d ':' -f2 | xargs)
                                echo "helm status : $status"
                                if [ "$status" == "deployed" ] && [ "$revision" != "$upgradedrevision" ]; then
                                    echo "Helm Status: $status"
                                    helmupgradesuccess=true
                                else
                                    echo "Helm upgrade is not successful. Status: $status and revision is not changed"    
                                fi
                            else
                                echo "az aks Command helm stauts failed with exit code $result_exit_code"
                            fi
                        else
                            echo "Run AKSCOMMAND function failed.. $result_status"
                        fi                        
                    fi
                else
                    echo "Run AKSCOMMAND function failed.. $result_status"
                fi
            else
                echo "checking the helm status....."
                echo "az aks command invoke --resource-group \"${resourceGroup}\" --name \"${aksName}\" --command \"helm status ${appNameLowerCase} --show-resources\""
                echo 'az aks command invoke --resource-group "${resourceGroup}" --name "${aksName}" --command "helm status ${appNameLowerCase} --show-resources" 2>&1 > $TMPFILE' > $COMMAND_FILE
                runAKSCommand $COMMAND_FILE
                if [ "$result_status" -eq 0 ]; then
                    result_exit_code=$(checkExitCode $TMPFILE)
                    echo "Result exit code true or false: $result_exit_code"
                    cat $TMPFILE
                    if [ "$result_exit_code" == "false" ]; then
                        echo "az aks command helm status --show-resources executed successfully"
                        revision_line=$(cat $TMPFILE | grep "REVISION:")
                        upgradedrevision=$(echo "$revision_line" | cut -d ':' -f2 | xargs)
                        echo "helm upgrade revision: $upgradedrevision"
                        status_line=$(cat $TMPFILE | grep "STATUS:")
                        status=$(echo "$status_line" | cut -d ':' -f2 | xargs)
                        echo "helm status : $status"
                        if [ "$status" == "deployed" ] && [ "$revision" != "$upgradedrevision" ]; then
                            echo "Helm Status: $status"
                            helmupgradesuccess=true
                        else
                            echo "Helm upgrade is not successful. Status: $status and revision is not changed"    
                        fi
                    else
                        echo "az aks Command helm stauts failed with exit code $result_exit_code"
                    fi
                else
                    echo "Run AKSCOMMAND function failed.. $result_status"
                fi
            fi
        elif [ "$helmupgradesuccess" == "true" ]; then
                echo "az aks command invoke --resource-group \"${resourceGroup}\" --name \"${aksName}\" --command \"helm status ${appNameLowerCase} --show-resources\""
                echo 'az aks command invoke --resource-group "${resourceGroup}" --name "${aksName}" --command "helm status ${appNameLowerCase} --show-resources" 2>&1 > $TMPFILE' > $COMMAND_FILE
                runAKSCommand $COMMAND_FILE
                if [ "$result_status" -eq 0 ]; then
                    result_exit_code=$(checkExitCode $TMPFILE)
                    echo "Result exit code true or false: $result_exit_code"
                    cat $TMPFILE
                    if [ "$result_exit_code" == "false" ]; then
                        echo "az aks command helm status --show-resources executed successfully"
                        status_line=$(cat $TMPFILE | grep "STATUS:")
                        status=$(echo "$status_line" | cut -d ':' -f2 | xargs)
                        if [ "$status" == "deployed" ]; then
                            echo "Helm upgrade is successful. Status: $status"
                        else
                            echo "Helm upgrade is not successful. Status: $status"
                        fi
                        #checking pod related info
                        echo "----------------------------------------"
                        echo "Helm status pod related info.. "
                        #pod_string="==> v1/Pod(related)"
                        cat $TMPFILE | sed -n '/==> v1\/Pod(related)/,/^$/p' | grep -v -e '==>' > $CHECKAGEFILE
                        cat $CHECKAGEFILE
                        echo "----------------------------------------"
                        pod_running=$(cat $TMPFILE | grep "Running" | wc -l)
                        echo "Pods running lines: $pod_running"
                        pod_creating=$(cat $TMPFILE | grep "ContainerCreating" | wc -l)
                        echo "Pods creating lines: $pod_creating"
                        pod_failed=$(cat $TMPFILE | grep "Failed" | wc -l)
                        echo "Pods failed lines: $pod_failed"
                        pod_pending=$(cat $TMPFILE | grep "Pending" | wc -l)
                        echo "Pods pending lines: $pod_pending"
                        pod_terminating=$(cat $TMPFILE | grep "Terminating" | wc -l)
                        echo "Pods terminating lines: $pod_terminating"
                        pod_imagepullback=$(cat $TMPFILE | grep "ImagePullBackOff" | wc -l)
                        echo "Pods ImagePullBackOff lines: $pod_imagepullback"
                        pod_crash=$(cat $TMPFILE | grep "CrashLoopBackOff" | wc -l)
                        echo "Pods CrashLoopBackOff lines: $pod_crash"
                        # check PODAGE
                        checkPodAge=$(checkAge $CHECKAGEFILE)
                        echo "Are any of the pods older than 120 mins : $checkPodAge"
                        if [ "$checkPodAge" != false ] || [ "$pod_creating" != 0 ] || [ "$pod_failed" != 0 ] || [ "$pod_pending" != 0 ] || [ "$pod_terminating" != 0 ] || [ "$pod_imagepullback" != 0 ] || [ "$pod_crash" != 0 ]; then
                            echo "Age is greater than 120 minutes or Not all pods are running.."
                        elif [ "$status" == "deployed" ]; then
                            echo "pods are running and Status is: $status"
                            break
                        fi
                    else
                        echo "az aks Command helm status failed with exit code $result_exit_code"
                    fi
                else
                    echo "Run AKSCOMMAND function failed.. $result_status"
                fi
        fi 
        sleep 300
        echo "Retry Counter is $count"
        ((count++))
        echo "----------------------------------------"
    done
    echo "----------------------------------------"
    
    # Check the most recent upgrade result of all services
    count=1
    while [ "$count" -lt 4 ]; do
        echo "${appName} latest upgrade result"
        echo "az aks command invoke --resource-group \"${resourceGroup}\" --name \"${aksName}\" --command \"helm history ${appNameLowerCase} --max 1 -o yaml\""
        echo 'az aks command invoke --resource-group "${resourceGroup}" --name "${aksName}" --command "helm history ${appNameLowerCase} --max 1 -o yaml" > $TMPFILE 2>&1' > $COMMAND_FILE
        runAKSCommand $COMMAND_FILE
        if [ "$result_status" -eq 0 ]; then
            result_exit_code=$(checkExitCode $TMPFILE)
            cat $TMPFILE
            echo "Result exit code true or false: $result_exit_code"
            if [ "$result_exit_code" == "false" ]; then
                echo "az aks command helm history after upgrade executed successfully"
                #Check new helm upgraded version
                latestrevision=$(cat $TMPFILE | grep "revision" | sed 's/[^0-9]*\([0-9]*\).*/\1/')
                echo "Helm release ${appName} latest revision: $latestrevision"
                status=$(cat $TMPFILE | grep "status:" | sed 's/status: \([a-zA-Z]*\)/\1/')
                status=$(echo $status | tr -d ' ')
                echo "Status of latest deployment: $status"
                if [ "$status" == "deployed" ] && [ "$revision" != "$latestrevision" ]; then
                    echo "Helm Upgrade Status is $status and Latest Revision is $latestrevision"
                    break
                fi
            else
                echo "az aks Command helm history after upgrade failed with exit code 1"
                sleep 10 # sleep 10 seconds to retry the command
            fi
        else
            echo "Run AKSCOMMAND function failed..  $result_status"
        fi
        echo "Retry Counter is $count"
        ((count++))
        echo "----------------------------------------"
    done

    echo "----------------------------------------"
    #If any of the above checks fail..Exit
    if [ "$checkPodAge" != false ] || [ "$status" != "deployed" ] || [ "$revision" == "$latestrevision" ] || [ "$pod_creating" != 0 ] || [ "$pod_failed" != 0 ] || [ "$pod_pending" != 0 ] || [ "$pod_terminating" != 0 ] || [ "$pod_imagepullback" != 0 ] || [ "$pod_crash" != 0 ]; then
        echo "Exiting due to failure in the script..."
        exit 1;
    else
        echo "Helm Upgrade successfull..."
    fi
    echo "---------------------------------------"
}