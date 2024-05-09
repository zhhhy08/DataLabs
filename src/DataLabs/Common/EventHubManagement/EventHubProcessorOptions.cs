namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Consumer;
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;

    public class EventHubProcessorOptions
    {
        private static readonly ActivityMonitorFactory EventHubProcessorOptionsGetStartPositionWhenNoCheckpoint =
            new ActivityMonitorFactory("EventHubProcessorOptions.GetStartPositionWhenNoCheckpoint");

        #region Constants

        private const char Separator = ';';

        private const char DictionaryValueSeparator = ':';

        #endregion

        #region Properties

        public EventProcessorClientOptions ProcessorClientOptions { get; set; }

        public string ConsumerGroup { get; set; }

        public int MaxRetryCountForCheckpoint { get; set; } = 3;

        private Dictionary<string, EventPosition>? _startSequenceNumberWhenNoCheckpoint;
        private EventPosition? _startPositionWhenNoCheckpoint;

        #endregion

        #region Constructors

        public EventHubProcessorOptions(EventProcessorClientOptions options, string consumerGroup)
        {
            this.ProcessorClientOptions = options;
            this.ConsumerGroup = consumerGroup;
        }

        public EventHubProcessorOptions(EventHubProcessorOptions options)
        {
            this.ProcessorClientOptions = options.ProcessorClientOptions;
            this.ConsumerGroup = options.ConsumerGroup;
            this._startPositionWhenNoCheckpoint = options._startPositionWhenNoCheckpoint;
            this._startSequenceNumberWhenNoCheckpoint = options._startSequenceNumberWhenNoCheckpoint;
        }

        #endregion

        #region Public methods

        public void SetStartPositionWhenNoCheckpoint(TimeSpan? initialOffsetFromCurrent, DateTimeOffset? startDateTime, string? startSequenceNumberWhenNoCheckpoint)
        {
            if (startDateTime != null)
            {
                this._startPositionWhenNoCheckpoint = EventPosition.FromEnqueuedTime(startDateTime.Value.UtcDateTime);
            }
            else if (initialOffsetFromCurrent != null)
            {
                this._startPositionWhenNoCheckpoint = EventPosition.FromEnqueuedTime(DateTime.UtcNow.Subtract(initialOffsetFromCurrent.Value));
            }

            this._startSequenceNumberWhenNoCheckpoint =
                string.IsNullOrWhiteSpace(startSequenceNumberWhenNoCheckpoint)
                    ? null
                    : startSequenceNumberWhenNoCheckpoint.Split(Separator)
                        .ToDictionary(
                            pair => pair.Split(DictionaryValueSeparator)[0],
                            pair => EventPosition.FromSequenceNumber(
                                long.Parse(pair.Split(DictionaryValueSeparator)[1])),
                            StringComparer.OrdinalIgnoreCase);
        }

        public EventPosition? GetStartPositionWhenNoCheckpoint(string eventHubNamespace, string eventHubName,
            string partitionId, IActivity? parentActivity)
        {
            if (_startSequenceNumberWhenNoCheckpoint == null && _startPositionWhenNoCheckpoint == null)
            {
                return null;
            }

            GuardHelper.ArgumentNotNullOrEmpty(eventHubNamespace);
            GuardHelper.ArgumentNotNullOrEmpty(eventHubName);
            GuardHelper.ArgumentNotNullOrEmpty(this.ConsumerGroup);
            GuardHelper.ArgumentNotNullOrEmpty(partitionId);

            using var monitor = EventHubProcessorOptionsGetStartPositionWhenNoCheckpoint.ToMonitor(parentActivity);
            monitor.Activity["EventHubNamespace"] = eventHubNamespace;
            monitor.Activity["EventHubName"] = eventHubName;
            monitor.Activity["ConsumerGroup"] = this.ConsumerGroup;
            monitor.Activity["PartitionId"] = partitionId;
            monitor.OnStart();

            try
            {
                var key = $"{eventHubNamespace}_{eventHubName}_{this.ConsumerGroup}_{partitionId}";
                monitor.Activity["Key"] = key;
                monitor.Activity.LogCollectionAndCount("StartSequenceNumberWhenNoCheckpoint", _startSequenceNumberWhenNoCheckpoint, ignoreCountForSingleValueProperty: true);

                if (this._startSequenceNumberWhenNoCheckpoint != null &&
                    this._startSequenceNumberWhenNoCheckpoint.TryGetValue(key, out var sequenceNumberEventPosition))
                {
                    monitor.Activity["EventPositionSource"] = "SequenceNumberConfig";
                    monitor.Activity["EventPosition"] = sequenceNumberEventPosition.ToString();
                    monitor.OnCompleted();

                    return sequenceNumberEventPosition;
                }

                monitor.Activity["EventPositionSource"] = "DateTimeConfig";
                monitor.Activity["EventPosition"] = _startPositionWhenNoCheckpoint?.ToString();
                monitor.OnCompleted();

                return this._startPositionWhenNoCheckpoint;
            }
            catch (Exception exception)
            {
                monitor.OnError(exception);
                throw;
            }
        }

        #endregion
    }
}