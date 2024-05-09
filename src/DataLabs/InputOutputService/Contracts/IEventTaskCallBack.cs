namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using System;
    using System.Diagnostics;

    public interface IEventTaskCallBack
    {
        public void TaskStarted(IIOEventTaskContext eventTaskContext, ref TagList tagList);
        public void TaskCancelCalled(IIOEventTaskContext eventTaskContext);
        public void TaskTimeoutCalled(IIOEventTaskContext eventTaskContext);
        public void TaskErrorCalled(IIOEventTaskContext eventTaskContext, Exception ex);

        // Final Stage Callbacks
        public void TaskSuccess(IIOEventTaskContext eventTaskContext);
        public void TaskMovedToRetry(IIOEventTaskContext eventTaskContext);
        public void TaskMovedToPoison(IIOEventTaskContext eventTaskContext);
        public void TaskDropped(IIOEventTaskContext eventTaskContext);
        public void FinalCleanup(); // This will be called after all Task Dispose()

        public bool IsTaskCancelled { get; }
        public bool HasParentTask { get; }
        public string TaskCancelledReason { get; }
        public long PartnerTotalSpentTime { get; set; }
    }
}
