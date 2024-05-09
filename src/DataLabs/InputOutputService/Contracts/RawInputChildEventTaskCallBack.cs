namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using System.Diagnostics;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;

    public class RawInputChildEventTaskCallBack : AbstractChildEventTaskCallBack<ARNRawInputMessage>
    {
        public RawInputChildEventTaskCallBack(IOEventTaskContext<ARNRawInputMessage> parentRawInputEventTaskContext)
            : base(parentRawInputEventTaskContext)
        {
        }

        protected override void SetParentTaskToNextChannel()
        {
            if (_parentEventTaskContext.IsAlreadyTaskCancelled())
            {
                _parentEventTaskContext.TaskDrop(DropReason.TaskCancelCalled.FastEnumToString(), "Task is cancelled", IOComponent.RawInputChannel.FastEnumToString());
            }
            else
            {
                // RawInput is always set to TaskSucess regardless of child tasks' result
                // Each Child Task will be retried individually
                _parentEventTaskContext.TaskSuccess(Stopwatch.GetTimestamp());
            }

        }
    }
}
