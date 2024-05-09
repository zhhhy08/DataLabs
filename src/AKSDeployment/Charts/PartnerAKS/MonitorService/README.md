# MonitorService in Data Labs
Author: @dylanhartono

## Introduction
MonitorService is the deployment that contains the monitoring agents to send logs/traces/metrics to Geneva in order to be used for dgrep, dashboards, and other livesite tools. This README.md will include how to run the monitoring agents, debug, and provide general information about the flow for those to review and comment. Since this is a new document, please reach out to author to provide more succinct or more detailed explanations toward the product. 

> Note that Data Labs uses a different framework to ARG's monitoring due to Microsoft's new investment in industry standard technologies: (1) OpenTelemetry and (2) AKS. The approach towards diagnostics will be familiar but will have some nuances due to the Geneva team having a different approach with the OpenTelemetry library and the unix environment of AKS.

There are 3 parts to this file:
1. Running Diagnostics/MonitoringService in AKS and Locally
2. Debugging
3. Details

Please jump to the relevant section that you would wish to look and review.

## Running Diagnostics/MonitoringService in AKS and Locally
### Local Deployment or Unit Tests
UT's and Local Deployment automatically print OpenTelemetry logs/traces/metrics to STDOUT instead of being routed to Geneva accounts. Although some of the logs are extensive and hard to find, you are able to search through them from the output of the local deployment.

### AKS Deployments
Follow the commands in the `C:\Mgmt-Governance-DataLabs\src\AKSDeployment\Charts\Private_AKS_Setup_{ENV}.md` file for deploying the monitor service. MonitorService supports 2 outputs for OpenTelemetry diagnostics from application that is done at configuration level (values file):
1. Printing Diagnostics to your application's STDOUT (not often used, more for debugging quickly): Change the `monitorInfo.exporterType` field in your values file to a random string (i.e. DUMMY)
2. Sending Diagnostics to Geneva: Change the `monitorInfo.exporterType` field in your values file to `GENEVA`.

## Debugging 
### Failures when configured to send to Geneva
Authentication failures:
- **If this is a new AKS instance, please wait half a day first before investigating.** It takes time for the managed identity authentication to bake into the DSMS authentication that Geneva uses.
- You will be able to recognize a failure if MDSD fails to read the config file and MDM has error messages about not being able to send. Look into the STDOUT of the mdm, mdsd-partner, and mdsd-datalabs containers in the monitor-namespace to check for any failures.
- Please ensure that the kubelet identity from the AKS instance, the identity (objectId for MDM and resourceId for MDSD) in the values files, and the identity on the geneva account information (objectId and tenantId) are all the same (and present).
- `C:\Mgmt-Governance-DataLabs\src\AKSDeployment\Charts\Private_AKS_Setup_{ENV}.md` to set a kubelet identity 

Unreflected changes:
- The Geneva Account's configuration version and the one in the values file is incorrect (`genevaAccounts.${datalabs/partner}.gcs_config_version`). 
- This problem arises when updates made to the Geneva account configuration are made but are not reflected in the geneva logs, but it has not been updated in the values file. You can find the account version from viewing the geneva logging account under configurations and align it with the version determined in the values file.

Application Failure
- If MonitorService was created less than a minute before the application, please wait another minute to deploy the application. Applications take a dependency on the MonitorService to create the UDS (unix domain socket) before being created.
- This may be due to MDSD and MDM not working as expected. Please review authentication failures and review that first.
- Please reach out to Data Labs team about failure as this may be a configuration issue with socat. This should not happen.

### No results from OpenTelemetry
If you are a dev, please review internal documentation from Geneva team about OpenTelemetry to make sure you are sending values correctly. Currently, there are instances when `LoggerFactory.Log()` does not create any output due to wrong arguments.

On the Data Labs side, we are using Common code for ActivityTracing and ServiceMonitoring. This has been used already and should be stable, but if not please reach out to the Data Labs team. 

On the partner side, we are configuring the endpoints of OTLP, so please work with partner team on what meterNames, tracerNames, and loggerTableNames are expected and what are not showing up.

## Details
Diagnostics for Data Labs happens at 3 different levels:
1. AKS Deployment Layer: MDSD (logs/traces monitoring agent), MDM (metric monitoring agent), socat (network communication stream), AKS volumes
2. Library Layer: dotnet OpenTelemetry
3. Application Formatting Layer: Activity Tracing Library
 
### 1. AKS Deployment Layer
Relevant links:
- Repo Example of Geneva Images: https://dev.azure.com/msazure/One/_git/Compute-Runtime-Tux-GenevaContainers?path=%2Fdocker_geneva_samples%2FAKSGenevaSample%2FDeployment%2FGeneva%2Fchart%2Ftemplates%2Fgeneva-services.yaml&version=GBmariner
- Manage Identity Authentication: https://eng.ms/docs/products/geneva/collect/authentication/managedidentityoverview
- MDM Configurations: https://eng.ms/docs/products/geneva/collect/metrics/metricsextclireference
- MDSD Configurations: https://eng.ms/docs/products/geneva/collect/instrument/opentelemetrydotnet/configurationoptions

Code Pointers:
- `C:\Mgmt-Governance-DataLabs\src\AKSDeployment\Charts\PartnerAKS\MonitorService\templates\geneva-services.yaml`
- `C:\Mgmt-Governance-DataLabs\src\AKSDeployment\Charts\PartnerAKS\bcdrValues_Int.yaml` under Monitoring configurations
- Each service with an application has an endpoint in their environment variables they send diagnostics through, and either they are sent to a UDS (unix domain socket) or a socat image (directing traffic through network to MonitorService. This is only with PartnerService)

Summary: 

For linux systems, Geneva team supports providing mariner images of the monitoring agents (MDSD for dgrep logs and MDM for dashboards), and we use them as black boxes to configure and send diagnostics to the respective geneva accounts (authentication, configuration version, region, etc). Currently, we configure MDSD and MDM to open a Unix Domain Socket (UDS) that applications are able to write to. The Geneva team has provided an internal exporter for OTLP that works specifically for MDSD and MDM. Note that the UDS is only able to be exposed through a hostPath directory, which violates security specifically for the PartnerService (exposing node directories). 

For PartnerService, we utilize socat images to send anything exported by the PartnerService through the network, and it is written by the MonitorService socat images to the respective UDS instead. Therefore, we can bypass needing to expose a hostPath to the partner.

### 2. Endpoint Layer
Relevant links:
- Instrumentation Guidance: https://eng.ms/docs/products/geneva/collect/instrument/opentelemetrydotnet/otel-dotnet-guidance
- Configuring OTLP Exporter: https://eng.ms/docs/products/geneva/collect/instrument/opentelemetrydotnet/intro
- LogTableMappings: https://eng.ms/docs/products/geneva/collect/instrument/opentelemetrydotnet/configurationoptions#tablenamemappings-optional

Code Pointers:
- Common Logger: `C:\Mgmt-Governance-DataLabs\src\DataLabs\Common\ServiceMonitoring\LoggerExtensions.cs`
- Common Tracer: `C:\Mgmt-Governance-DataLabs\src\DataLabs\Common\ServiceMonitoring\Tracer.cs`
- Common Metrics: `C:\Mgmt-Governance-DataLabs\src\DataLabs\Common\ServiceMonitoring\MetricLogger.cs`
- Startup: `C:\Mgmt-Governance-DataLabs\src\DataLabs\InputOutputService\Program.cs`
- Partner: `C:\Mgmt-Governance-DataLabs\src\DataLabs\PartnerCommon\DataLabsInterface\IDataLabsInterface.cs`

Summary:

At the application level, we utilize the OpenTelemetry exporter that Geneva Team created to write from the application to a specific UDS location, which is configured in the deployment files. One thing to note that this requires that the UDS is already created, so if there are no instances of the UDS at startup, exceptions will happen. We wrapped this capability in the common library for all Data Labs components to utilize.

For partners, we are taking the meters, traces, and logging table names, and we are doing the configurations for exporting.

### 3. Application Formatting Layer
Relevant links:
- Activity Tracing Library in ARG Repo
- https://github.com/open-telemetry/opentelemetry-dotnet

Code Pointers:
- `C:\Mgmt-Governance-DataLabs\src\DataLabs\Common\ActivityTracing\`
- `C:\Mgmt-Governance-DataLabs\src\DataLabs\InputOutputService\OpenTelemetry\IOServiceOpenTelemetry.cs`

Summary:

We utilize a similar version to ActivityTracing in ARG, except we configured it to utilize OpenTelemetry instead of ETW and IFx for ServiceFabric. For metrics and traces, we are using the ones defined in OpenTelemetry. These are used throughout the DataLabs code base, so please refer to those for more information.