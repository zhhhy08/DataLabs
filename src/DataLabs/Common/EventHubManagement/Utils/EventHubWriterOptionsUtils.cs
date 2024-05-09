namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using global::Azure.Messaging.EventHubs;
    using System;

    public static class EventHubWriterOptionsUtils
    {
        /*
         * From ARG
         *     <!-- Event Hub Reader -->
            <Parameter Name="AzureEventHubReaderProviderEventHubPath" DefaultValue="changecompute" />
            <Parameter Name="AzureEventHubReaderProviderEventHubListenNames" DefaultValue="" />
            <Parameter Name="AzureEventHubReaderProviderEventHubStorageAccountNames" DefaultValue="" />
            <Parameter Name="AzureEventHubReaderProviderEventHubLeaseContainerNames" DefaultValue="" />
            <Parameter Name="AzureEventHubReaderProviderEventHubConsumerGroupName" DefaultValue="changetracking" />
            <Parameter Name="AzureEventHubReaderProviderEventHubEnableReceiverRuntimeMetric" DefaultValue="true" />
            <Parameter Name="AzureEventHubReaderProviderEventHubLeaseDuration" DefaultValue="20" />
            <Parameter Name="AzureEventHubReaderProviderEventHubLeaseRenewInterval" DefaultValue="8" />
            <Parameter Name="AzureEventHubReaderProviderEventHubAmqpConnectionIdleTimeout" DefaultValue="00:00:20" />
    
            <!-- Azure Event Hub Writer -->
            <Parameter Name="AzureEventHubWriterProviderEventHubPath" DefaultValue="" />
            <Parameter Name="AzureEventHubWriterProviderEventHubWriterNames" DefaultValue="" />
            <Parameter Name="AzureEventHubWriterProviderMaxMessageSizeInKB" DefaultValue="41" />
            <Parameter Name="AzureEventHubWriterProviderThrottleTimeout" DefaultValue="00:00:30" />
            <Parameter Name="AzureEventHubWriterProviderTimeoutPercentThreshold" DefaultValue="10" />
            <Parameter Name="AzureEventHubWriterProviderTimeoutTargetSampleSizePerMinute" DefaultValue="3000" />
            <Parameter Name="AzureEventHubWriterProviderHealthEvaluationPeriodInSecond" DefaultValue="10" />
            <Parameter Name="AzureEventHubWriterProviderMaxRetryCount" DefaultValue="2" />
            <!--ChangeComputeBlobContainer-->

             <!--- Partial Sync --->
            <Parameter Name="FastEventHubWriterTimeout" DefaultValue="00:00:02" />
            <Parameter Name="SlowEventHubWriterTimeout" DefaultValue="00:00:03" />
            <Parameter Name="RetryFastEventHubWriterTimeout" DefaultValue="00:02:00" />
            <Parameter Name="RetrySlowEventHubWriterTimeout" DefaultValue="00:03:00" />

             <!--- change compute --->
            FastEventHubWriterTimeout = TimeSpan.Parse(ServiceConfiguration.Current.Get(
                nameof(AzureEventHubWriterProvider),
                nameof(FastEventHubWriterTimeout),
                defaultValue: "00:00:10"));

            SlowEventHubWriterTimeout = TimeSpan.Parse(ServiceConfiguration.Current.Get(
                nameof(AzureEventHubWriterProvider),
                nameof(SlowEventHubWriterTimeout),
                defaultValue: "00:00:20"));
        */

        internal static EventHubConnectionOptions CreateEventHubWriterConnectionOptions(int sendbufferSizeInBytes = 32*1024)
        {
            return new EventHubConnectionOptions()
            {
                //ConnectionIdleTimeout = connectionIdleTimeout,
                SendBufferSizeInBytes = sendbufferSizeInBytes //Default is 8K
            };
        }

        internal static EventHubsRetryOptions CreateEventHubsRetryOptions(int maxRetry, int delayInMSec, int maxDelayInSec, int timeoutPerAttempInSec)
        {
            return new EventHubsRetryOptions()
            {
                MaximumRetries = maxRetry,
                Delay = TimeSpan.FromMilliseconds(delayInMSec),
                MaximumDelay = TimeSpan.FromSeconds(maxDelayInSec),
                TryTimeout = TimeSpan.FromSeconds(timeoutPerAttempInSec),
                Mode = EventHubsRetryMode.Exponential
            };
        }
    }
}