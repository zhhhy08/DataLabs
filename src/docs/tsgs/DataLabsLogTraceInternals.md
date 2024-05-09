# DataLabs Logs/Trace Internals

## Overview

DataLabs reads input notifications from EventHub and send them to Partner's Nuget through interface contract. Then DataLabs receives Partner's responses and publish them to ARN.

You can refer High Level Design pages

DataLabs High Level Design  TODO add link to DataLabsHLD.md

## Contract between DataLabs and Partners

DataLabs and Partner communicates based on interface contract. 

You can refer the interface below    
https://msazure.visualstudio.com/One/_git/Mgmt-Governance-DataLabs?path=/src/DataLabs/PartnerCommon/DataLabsInterface/IDataLabsInterface.cs&version=GBmain

DataLabs built nuget for above interface and published the nuget in offical nuget repo.    
https://msazure.visualstudio.com/One/_artifacts/feed/Official/NuGet/Microsoft.WindowsAzure.Governance.DataLabs.Partner/overview/

Partner has to download the interface nuget and has to implement the interface with their business logic and need to publish the Partner Nuget to official nuget repo. 
For example. ABC Partner publishes their nuget here    
https://msazure.visualstudio.com/One/_artifacts/feed/Official/NuGet/Microsoft.AzureBusinessContinuity.PartnerSolution/overview

Then DataLabs dowloads the Partner's Nuget and run the partner nuget inside AKS.
For each Partner, we deploy dedicated AKS per Partner per region. For now, we will deploy 2 US, 2 EU, 2 ASIA region.

Currently we are supporting two types of interface.

1. One input and one output. Partner can send one output for each individual resource notification
 
`public Task<DataLabsARNV3Response> GetResponseAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)`

2. One input and streaming outputs. Partner can send streaming of outputs for one input. Number of responses in a streaming is up to Maximum(Default value is set to 50)
 
`public IAsyncEnumerable<DataLabsARNV3Response> GetResponsesAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)`


## Micro Services in DataLabs

When an input ARN Notifications reach to DataLabs's Input EventHub, 
DataLabs can trace individual resource from Input (EventHub) -> Partner Logic -> Partner Output -> ARN Publish.

In other words, when input notification have batch resources. DataLabs break the batch resources into individual input resource(a.k.a individual resource notification) and send individual resource notification concurrently to Partner Business Logic. 
And DataLabs can have logs / metrics per individual resource notification level and individual resource notification can be traced across all logs based on OpenTelemetry TraceId across all micro services including Retry Flow

In DataLabs. there are 5 micro services. (each Service runs in one POD in AKS)
1. *MonitorService*: Run Geneva Monitor Agents and send collected logs/metrics/traces to Geneva Acount
2. *IOSerivce*: IOService reads notification from input EventHub, send to PartnerService, recevies Partner Service's Output and publish the output to Source of Truth and ARN (currently OutputEventHub)
3. *PartnerSerivce*: PartnerService is asp.net web server which runs PartnerNuget. IOService and PartnerService communicates with Grpc
4. *ResourceProxyService*: when Partner Nuget need ARM GetCall, PartnerNuget send request to the ResourceProxyService through given IResourceProxyClient in above interface contract
5. *ResourceFetcherService*: ResourceFetcherService is separate stanalone service. We deploy one ResourceFetcherSeriver per each region. all Partner AKS will send ARM Get call request through resourceProxyService -> resourceFetcherSerivce.


## Data Flow inside DataLabs
Here is steps which DataLabs does when a notification comes to EventHub.

1. IOService reads an eventHub message from EventHub
2. IOService check if it is single resource inline notification or batch notification or blob based notification
3. If it is blob based notification, it downloads blob.
4. If a notification has batch resources (either inline batch or batch after blob download), IOService breaks the batch resources into individual resource notification
5. IOService concurrently flow those individual resource notification to PartnerService

6. Based on resource Types in configuration, IOService and PartnerService communicates either one input to one output flow OR one input to streaming output flow.
7. PartnerService receives the individual resource notification from IOService and send it to PartnerNuget based on above flow (one input to one output VS one input to streaming output)
8. PartnerService receives responses(output) from PartnerNuget and send it IOService
9. IOService send the Partner Responses to Source Of Truth and Publish it to ARN


## How to trace individual resource notification inside DataLabs

DataLabs is utilizing both OpenTelemetry Activity(Trace) and ARG ActivityMonitor(Logs). 
OpenTelemetry Activity consists of 32 chars(bytes) TraceId and 16 chars(bytes) SpanId. Every OpenTelemetry Activity has TraceId and SpanId. 
As for OpenTelemetry, you can refer 
https://opentelemetry.io/docs/instrumentation/net/

The way DataLabs utilize OpenTelemetry Activity and ARG ActivityMonitor is when we recevie notification, we create internal task which uses OpenTelemetry Activity. So it has traceId. 
Then TraceId is automatically populated for all ARG ActivityMontior's column (env_dt_traceId). So we can trace all ARG style ActivityMonitor based on TraceId for individual resource notification

In DataLabs, we also added extra columns in addition to columns ARG service is using. 

### Extra Columns in ActivityStarted/Completed/Failed table

Extra columns added in DataLabs ActivityStarted/Completed/Failed table
1. *correlationId* (input correlationId)
2. *inputResourceId*
3. *outputCorrelationId*
4. *outputResourceId*
5. *startTime*

Those extra columns are very useful to filter logs based on the column. 
The extra columns are autopopulated for all ActivityMonitors inside DataLabs Framework. 
You can refer More details to this Page about how it can be autopopulated. (TODO. add new TSG page)

## OpenTelemetry Activity in DataLabs

In addition to ARG ActivityMonitor tables (ActivityStarted/Completed/Failed), OpenTelemetry Activity we create for traceId/spanId appears in separate table called "Span".
Here are OpenTelemetry Activity We create in DataLabs

Inside IOService
1. **EventHubTaskManager**: This OpenTelemetry Activity is created when we read one EventHub message.
2. **EventHubSingleInputEventTask**: If EventHub Message is single inline notification, this activity is created
3. **EventHubRawInputEventTask**: If EventHub Message is batch or blob based notification, this activity is created
4. **RawInputChildEventTask**: For individual resource notification from batch resource notification, this activity is created
5. **StreamResponseChildEventTask**: For individual response in streaming response scenario for one input, this activity is created
6. **RetrySingleInputEventTask**: For retry flow of single resource, this activity is created
7. **RetryRawInputEventTask**: For retry flow of original eventHub message (like blob download fail), this activity is created

Inside PartnerSerivce
1. **PartnerSolutionServerResponse**: One input to One response flow, this activity is created
2. **PartnerSolutionServerStreamResponse**: One input to Multi Response flow. this activity is created

Inside ResourceFetcher Proxy Service
1. **ResourceFetcherProxy.GetResource**: For every resource fetcher proxy request, this activity is created

Inside ResourceFetcher Service
1. **ResourceFetcher.GetResource**: For every resource fetcher serivce request, this activity is created

## Span Table showing OpenTelemetry Activity

Span table shows actual summary of above task. Span table has properties column as ARG ActivityMonitor and it contains most of key/value pairs. 
In addition to propertie column, below seprate columns are added for showing quick summary about each Task.

It shows some useful column including below columns
1. correlationId (input correlationId)
2. inputResourceId
3. outputCorrelationId
4. outputResourceId
5. Success (True/False)
6. FinalStatus(More details status)
7. FinalStage
8. PartnerResponseFlags
9. EventList (this shows more detail code flow in IOService)
10. FailedReason/FailedDescription/Exception
11. TaskFailedComponent
12. RetryCount
13. startTime
	

## OpenTelemetry Activity Sequence in several cases

For example, here is actual OpenTelemetry Activity flow

Case 1) Single Inline EventHub Message and Single Partner Response Flow (IDMapping) without Resource Fetcher Proxy call. 
All below openTelemetry Activity has same TraceId. so we can trace all below tasks with same trace Id

**EventHubTaskManager -> EventHubSingleInputEventTask -> PartnerSolutionServerResponse**

Case 2) Single Inline EventHub Message and Single Partner Response Flow with Resource Fetcher Proxy call
All below openTelemetry Activity has same TraceId. so we can trace all below tasks with same trace Id

**EventHubTaskManager -> EventHubSingleInputEventTask -> PartnerSolutionServerResponse -> ResourceFetcherProxy.GetResource -> ResourceFetcher.GetResource**

Case 3) Batch Notification EventHub Message and Single Partner Response Flow
Because one EventHub Message has multiple resources, we break it into individual resource notification and assign a different trace Id per each individual resource

**EventHubTaskManager -> EventHubRawInputEventTask (until this, same traceId from EventHubTaskManager)**   
-> RawInputChildEventTask (new TraceId) -> PartnerSolutionServerResponse ->(Resource Proxy/Fetcher Serivce Acitvity if applicable)   
-> RawInputChildEventTask (new TraceId) -> PartnerSolutionServerResponse ->(Resource Proxy/Fetcher Serivce Acitvity if applicable)    
-> RawInputChildEventTask (new TraceId) -> PartnerSolutionServerResponse ->(Resource Proxy/Fetcher Serivce Acitvity if applicable)    

When we create new TraceId in RawInputChildEventTask, we add column *ParentTraceId* in each RawInputChildEventTask Activity and column *ChildTraceIds* in parent EventHubRawInputEventTask Activity. So that we can link between two traceIds.

Case 4) Single Inline EventHub Message and Partner Streaming Responses (ABC Partner)

**EventHubTaskManager -> EventHubSingleInputEventTask -> PartnerSolutionServerStreamResponse -> (Resource Proxy/Fetcher Serivce Acitvity if applicable)**   
-> StreamResponseChildEventTask (new TraceId) ->    
-> StreamResponseChildEventTask (new TraceId) ->    
-> StreamResponseChildEventTask (new TraceId) ->    

When we create new TraceId in StreamResponseChildEventTask, we add column *ParentTraceId* in each StreamResponseChildEventTask Activity and column *ChildTraceIds* in parent EventHubSingleInputEventTask Activity. So that we can link between two traceIds.

Case 5) Batch Notification EventHub Message and Partner Streaming Responses (ABC Partner)

**EventHubTaskManager -> EventHubRawInputEventTask (until this, same traceId from EventHubTaskManager)**   
-> RawInputChildEventTask (new TraceId) -> PartnerSolutionServerResponse ->(Resource Proxy/Fetcher Serivce Acitvity if applicable) -> StreamResponseChildEventTask (new TraceId) ->    

When we create new TraceId in RawInputChildEventTask, we add column *ParentTraceId* in child openTelemetry Activity and column *ChildTraceIds* in parent EventHubRawInputEventTask Activity. So that we can link between two traceIds.

When we create new TraceId in StreamResponseChildEventTask, we add column *ParentTraceId* in child openTelemetry Activity and column *ChildTraceIds* in parent RawInputChildEventTask Activity. So that we can link between two traceIds.

In summary, basically we assing one unique Trace Id to a combination of one input and one response(output). So we can trace a combination of input and output.