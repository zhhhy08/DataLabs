namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public class EventHubAsyncTaskInfo : IEventTaskCallBack
    {
        public bool IsCompleted => _taskCompleted == 1 || 
            (_maxDurationExpireTaskInfo != null && _maxDurationExpireTaskInfo.IsCompleted);

        public bool IsTaskSuccess => _taskSuccess == 1;
        public bool IsTaskFiltered => _taskFiltered == 1;

        public bool IsTaskCancelled => _isTaskCancelled;
        public bool HasParentTask => false;
        public string TaskCancelledReason => IsTaskCancelled ? SolutionConstants.IsCancelled : null;
        public long PartnerTotalSpentTime { get; set; }

        public string DataSourceName { get; }
        public string RegionName { get; }
        public string MessageId { get; }
        public DateTimeOffset EnqueuedTime { get; }
        public string PartitionId { get; }
        public long SequenceNumber { get; }
        public long Offset { get; }
        public ActivityContext ActivityContext { get; }


        internal readonly DateTimeOffset CreationTime;
        internal volatile ICancellableTask CancellableTask;
        internal volatile BinaryData MessageData;
        internal readonly bool HasCompressed;

        private EventHubAsyncTaskInfo _maxDurationExpireTaskInfo;
        private int _taskSuccess;
        private int _taskCompleted;
        private int _taskFiltered;
        private volatile bool _isTaskCancelled;
        
        public int numCleanupCalled; // for UnitTest

        public EventHubAsyncTaskInfo(
            string dataSourceName, 
            string messageId,
            DateTimeOffset enqueuedTime,
            string partitionId, 
            long sequenceNumber, 
            long offset, 
            BinaryData messageData, 
            bool hasCompressed,
            ActivityContext activityContext,
            string regionName)
        {
            DataSourceName = dataSourceName;
            MessageId = messageId;
            EnqueuedTime = enqueuedTime;
            PartitionId = partitionId;
            SequenceNumber = sequenceNumber;
            Offset = offset;
            MessageData = messageData;
            HasCompressed = hasCompressed;
            ActivityContext = activityContext;
            CreationTime = DateTimeOffset.UtcNow;
            RegionName = regionName;
        }

        public void SetCancellableTask(ICancellableTask cancellableTask)
        {
            CancellableTask = cancellableTask;
        }

        internal void CleanupResources()
        {
            // Need to release all referrences
            CancellableTask = null;
            MessageData = null;
        }

        public EventHubAsyncTaskInfo CreateAndSetMaxDurationExpireTaskInfo()
        {
            if (_maxDurationExpireTaskInfo != null)
            {
                return null;
            }

            // Create callback for new Task
            var newEventHubAsyncTaskInfo = new EventHubAsyncTaskInfo(
                dataSourceName: DataSourceName,
                messageId: MessageId,
                enqueuedTime: EnqueuedTime,
                partitionId: PartitionId,
                sequenceNumber: SequenceNumber,
                offset: Offset,
                messageData: null,
                hasCompressed: false,
                activityContext: ActivityContext,
                regionName: RegionName);

            if (Interlocked.CompareExchange(ref _maxDurationExpireTaskInfo, newEventHubAsyncTaskInfo, null) == null)
            {
                return newEventHubAsyncTaskInfo;
            }
            return null;
        }

        public void TaskFiltered()
        {
            Interlocked.Exchange(ref _taskFiltered, 1);
            SetTaskCompleted();
        }

        public void TaskStarted(IIOEventTaskContext eventTaskContext, ref TagList tagList)
        {
            tagList.Add(SolutionConstants.EventHubMessageId, MessageId);
            tagList.Add(SolutionConstants.PartitionId, PartitionId);
            tagList.Add(SolutionConstants.EventHubSequenceNumber, SequenceNumber);
            tagList.Add(SolutionConstants.EventHubOffset, Offset);
        }

        public void TaskCancelCalled(IIOEventTaskContext eventTaskContext)
        {
            _isTaskCancelled = true;
        }

        public void TaskTimeoutCalled(IIOEventTaskContext eventTaskContext)
        {
        }

        public void TaskErrorCalled(IIOEventTaskContext eventTaskContext, Exception ex)
        {
        }

        public void TaskSuccess(IIOEventTaskContext eventTaskContext)
        {
            Interlocked.Exchange(ref _taskSuccess, 1);
        }

        public void TaskMovedToRetry(IIOEventTaskContext eventTaskContext)
        {
        }

        public void TaskMovedToPoison(IIOEventTaskContext eventTaskContext)
        {
        }

        public void TaskDropped(IIOEventTaskContext eventTaskContext)
        {
        }

        public void FinalCleanup()
        {
            numCleanupCalled++;
            SetTaskCompleted();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetTaskCompleted()
        {
            Interlocked.Exchange(ref _taskCompleted, 1);
            MessageData = null;
        }
    }
}
