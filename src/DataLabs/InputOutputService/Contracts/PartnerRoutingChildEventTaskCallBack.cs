namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using System.Diagnostics;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;

    public class PartnerRoutingChildEventTaskCallBack : AbstractChildEventTaskCallBack<ARNSingleInputMessage>
    {
        public PartnerRoutingChildEventTaskCallBack(IOEventTaskContext<ARNSingleInputMessage> parentARNSingleInputEventTaskContext)
            : base(parentARNSingleInputEventTaskContext)
        {
        }

        protected override void SetParentTaskToNextChannel()
        {
            if (_parentEventTaskContext.IsAlreadyTaskCancelled())
            {
                _parentEventTaskContext.TaskDrop(DropReason.TaskCancelCalled.FastEnumToString(), "Task is cancelled", IOComponent.PartnerRoutingManager.FastEnumToString());
            }
            else
            {
                _parentEventTaskContext.TaskSuccess(Stopwatch.GetTimestamp());
            }
        }
    }
}
