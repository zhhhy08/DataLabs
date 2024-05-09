namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using System;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;

    public interface IIOEventTaskContext
    {
        public DataSourceType DataSourceType { get; }
        public string DataSourceName { get; }
        public DateTimeOffset FirstEnqueuedTime { get; }
        public DateTimeOffset FirstPickedUpTime { get; }
        public DateTimeOffset DataEnqueuedTime { get; }
        public IInputMessage BaseInputMessage { get; }
        public OutputMessage OutputMessage { get; }
        public string InputCorrelationId { get; }

        // Task Cancellation Token And Source
        public CancellationToken TaskCancellationToken { get; }
        public IEventTaskCallBack EventTaskCallBack { get; }

        public OpenTelemetryActivityWrapper EventTaskActivity { get; }
        public IOEventTaskFlag IOEventTaskFlags { get; set; }
        public string PartnerChannelName { get; set; }
        public EventTaskFinalStage EventFinalStage { get; }

        public IRetryStrategy RetryStrategy { get; }
        public TimeSpan RetryDelay { get; }
        public int RetryCount { get; }

        public string FailedReason { get; }
        public string FailedDescription { get; }

        public RegionConfig RegionConfigData { get; }

        public void AddOutputMessage(OutputMessage outputMessage);
        public void DecreaseGlobalConcurrency();

        public void TaskMovedToEventHub(long writeDurationInMilli, int batchCount, long stopWatchTimestamp);
        public void TaskFailedToMoveToEventHub(Exception ex, long writeDurationInMilli, int batchCount, int retryDelay);

        public void TaskMovingToRetry(string retryReason, string reasonDetails, int retryDelayMs, string fromComponent, Exception taskException);
        public void TaskMovedToRetry(long writeDurationInMilli, int batchCount, long stopWatchTimestamp);
        public void TaskFailedToMoveToRetry(string poisonReason, Exception retryException, long writeDurationInMilli, int batchCount);

        public void TaskMovingToPoison(string poisonReason, string reasonDetails, string fromComponent, Exception taskException);
        public void TaskMovedToPoison(long writeDurationInMilli, int batchCount, long stopWatchTimestamp);
        public void TaskFailedToMoveToPoison(string failedReason, Exception poisonException, long writeDurationInMilli, int batchCount);

        public void TaskError(Exception taskException, string failedComponent, int retryDelayMs);
        public void TaskDrop(string dropReason, string reasonDetails, string fromComponent);
        public void TaskSuccess(long stopWatchTimestamp);

        // Partner Spent Time
        public long GetPartnerMaxTotalSpentTimeIncludingChildTasks();
    }
}
