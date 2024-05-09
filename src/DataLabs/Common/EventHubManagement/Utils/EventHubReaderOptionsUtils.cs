namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Processor;
    using System;

    public static class EventHubReaderOptionsUtils
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
        */

        internal static TimeSpan DefaultConnectionIdleTimeout = TimeSpan.FromMinutes(1); // same as default value in EH SDK

        /// <summary>The desired amount of time to allow between load balancing verification attempts.</summary>
        internal static TimeSpan DefaultLoadBalancingUpdateInterval = TimeSpan.FromSeconds(15); // ARG TimeSpan.FromSeconds(8);

        /// <summary>The desired amount of time to consider a partition owned by a specific event processor.</summary>
        internal static TimeSpan DefaultPartitionOwnershipExpirationInterval = TimeSpan.FromMinutes(1); // ARG TimeSpan.FromSeconds(20)

        internal static bool DefaultEnableReceiverRuntimeMetric = true;
        internal static int DefaultBatchSize = 100;
        internal static int DefaultPrefetchCount = 300;
        internal static TimeSpan? DefaultMaximumWaitTime = TimeSpan.FromSeconds(10);

        // TODO
        // Need to verify Balanced and Greedy with newer EventHub SDK.
        // SDK ARG is using seems to be default Balanced while newer SKD default seems to be GREEDY
        internal static LoadBalancingStrategy DefaultLoadBalancingStrategy = LoadBalancingStrategy.Balanced; 

        public static EventHubProcessorOptions CreateDefaultOptions(string consumerGroup, TimeSpan? initialOffsetFromCurrent = null)
        {
            return PrepareEventHubProcessorOptions(
                consumerGroup,
                leaseRenewInterval: DefaultLoadBalancingUpdateInterval,
                leaseDuration: DefaultPartitionOwnershipExpirationInterval,
                connectionIdleTimeout: DefaultConnectionIdleTimeout,
                initialOffsetFromCurrent: initialOffsetFromCurrent,
                startTime: null,
                startSequenceNumberNoCheckpointsStr: null,
                enableReceiverRuntimeMetric: DefaultEnableReceiverRuntimeMetric,
                prefetchCount: DefaultPrefetchCount,
                batchSize: DefaultBatchSize,
                maximumWaitTime: DefaultMaximumWaitTime,
                loadBalancingStrategy: DefaultLoadBalancingStrategy);
        }

        public static EventHubProcessorOptions PrepareEventHubProcessorOptions(
            string consumerGroup, 
            TimeSpan leaseRenewInterval,
            TimeSpan leaseDuration, 
            TimeSpan connectionIdleTimeout,
            TimeSpan? initialOffsetFromCurrent = null, 
            DateTimeOffset? startTime = null,
            string? startSequenceNumberNoCheckpointsStr = null,
            bool enableReceiverRuntimeMetric = true,
            int prefetchCount = 300,
            int batchSize = 100,
            TimeSpan? maximumWaitTime = null,
            LoadBalancingStrategy loadBalancingStrategy = LoadBalancingStrategy.Balanced)
        {
            var options = new EventHubProcessorOptions(
                new EventProcessorClientOptions()
                {
                    PrefetchCount = prefetchCount,
                    ConnectionOptions = new EventHubConnectionOptions()
                    {
                        ConnectionIdleTimeout = connectionIdleTimeout,
                        // TODO test performance
                        ReceiveBufferSizeInBytes = 64*1024, //Default is 8K
                        //SendBufferSizeInBytes = 8192 //Default is 8K
                    },
                    LoadBalancingUpdateInterval = leaseRenewInterval,
                    PartitionOwnershipExpirationInterval = leaseDuration,
                    CacheEventCount = batchSize,
                    TrackLastEnqueuedEventProperties = enableReceiverRuntimeMetric,
                    LoadBalancingStrategy = loadBalancingStrategy,
                    MaximumWaitTime = maximumWaitTime
                },
                consumerGroup);

            options.SetStartPositionWhenNoCheckpoint(initialOffsetFromCurrent, startTime, startSequenceNumberNoCheckpointsStr);

            return options;
        }
    }
}