namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;

    [ExcludeFromCodeCoverage]
    public class ServiceBusWriter : IEventWriter<ServiceBusMessage, ServiceBusMessageBatch>
    {
        public const string ServiceBusBatchWriteDurationName = "ServiceBusBatchWriteDuration";
        public const string ServiceBusBatchSizeMetricName = "ServiceBusCreateBatchDuration";

        public static readonly Histogram<int> BatchWriteDuration = MetricLogger.CommonMeter.CreateHistogram<int>(ServiceBusBatchWriteDurationName);
        public static readonly Histogram<int> BatchSizeMetric = MetricLogger.CommonMeter.CreateHistogram<int>(ServiceBusBatchSizeMetricName);

        public string Name { get; }
        public string QueueName { get; }
        public string NameSpace { get; }

        public ServiceBusSender ServiceBusSender { get; }

        public ServiceBusWriter(ServiceBusSender serviceBusSender)
        {
            QueueName = serviceBusSender.EntityPath;
            NameSpace = serviceBusSender.FullyQualifiedNamespace.FastSplitAndReturnFirst('.');
            Name = NameSpace + "/" + QueueName;
            ServiceBusSender = serviceBusSender;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task SendAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            // ServiceBus SDK has a bug where it receives ServiceBusy(throttling) and if we pass cancellationToken, it doesn't reset internal serviceBusy flag
            // https://github.com/Azure/azure-sdk-for-net/issues/42952
            // Until this is fixed, we will not pass cancellationToken
            return ServiceBusSender.SendMessageAsync(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task SendAsync(IEnumerable<ServiceBusMessage> messages, CancellationToken cancellationToken = default)
        {
            // ServiceBus SDK has a bug where it receives ServiceBusy(throttling) and if we pass cancellationToken, it doesn't reset internal serviceBusy flag
            // https://github.com/Azure/azure-sdk-for-net/issues/42952
            // Until this is fixed, we will not pass cancellationToken
            return ServiceBusSender.SendMessagesAsync(messages);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<ServiceBusMessageBatch> CreateEventDataBatch(CancellationToken cancellationToken = default)
        {
            // ServiceBus SDK has a bug where it receives ServiceBusy(throttling) and if we pass cancellationToken, it doesn't reset internal serviceBusy flag
            // https://github.com/Azure/azure-sdk-for-net/issues/42952
            // Until this is fixed, we will not pass cancellationToken
            return ServiceBusSender.CreateMessageBatchAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddToBatch(ServiceBusMessageBatch eventDataBatch, ServiceBusMessage eventData)
        {
            return eventDataBatch.TryAddMessage(eventData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBatchCount(ServiceBusMessageBatch eventDataBatch)
        {
            return eventDataBatch.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetMaxSizeInBytes(ServiceBusMessageBatch eventDataBatch)
        {
            return eventDataBatch.MaxSizeInBytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetSizeInBytes(ServiceBusMessageBatch eventDataBatch)
        {
            return eventDataBatch.SizeInBytes;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetSizeInBytes(ServiceBusMessage eventData)
        {
            return eventData.Body == null ? 0 : eventData.Body.ToMemory().Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ServiceBusMessage CreateEventData(BinaryData binaryData)
        {
            return new ServiceBusMessage(binaryData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddProperty(ServiceBusMessage eventData, string key, object value)
        {
            if (value != null)
            {
                eventData.ApplicationProperties[key] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddDelayMessageTime(ServiceBusMessage eventData, TimeSpan delay)
        {
            if (delay.Ticks > 0)
            {
                eventData.ScheduledEnqueueTime = DateTime.UtcNow.Add(delay);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task SendBatchAsync(ServiceBusMessageBatch eventDataBatch, CancellationToken cancellationToken = default)
        {
            int batchSize = eventDataBatch.Count;

            var startTimeStamp = Stopwatch.GetTimestamp();

            Exception? exception = null;

            try
            {
                // ServiceBus SDK has a bug where it receives ServiceBusy(throttling) and if we pass cancellationToken, it doesn't reset internal serviceBusy flag
                // https://github.com/Azure/azure-sdk-for-net/issues/42952
                // Until this is fixed, we will not pass cancellationToken

                await ServiceBusSender.SendMessagesAsync(eventDataBatch).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            long endTimestamp = Stopwatch.GetTimestamp();

            var writeDurationInMilli = (int)Stopwatch.GetElapsedTime(startTimeStamp, endTimestamp).TotalMilliseconds;

            bool success = exception == null;
            
            var namePair = new KeyValuePair<string, object?>(MonitoringConstants.QueueNameDimension, Name);
            BatchWriteDuration.Record(writeDurationInMilli, namePair, MonitoringConstants.GetSuccessDimension(success));
            BatchSizeMetric.Record(batchSize, namePair, MonitoringConstants.GetSuccessDimension(success));

            if (exception != null)
            {
                throw exception;
            }
        }

        public void Dispose()
        {
            // Do not dispose serviceBusAdminManager and senders
        }
    }
}
