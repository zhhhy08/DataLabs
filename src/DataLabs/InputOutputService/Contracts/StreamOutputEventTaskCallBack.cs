namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using System.Diagnostics;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;

    public class StreamOutputEventTaskCallBack : AbstractChildEventTaskCallBack<ARNSingleInputMessage>
    {
        private readonly int _retryDelayMs;
        private readonly int _maxChildForRetry;

        public StreamOutputEventTaskCallBack(
            IOEventTaskContext<ARNSingleInputMessage> parentInputEventTaskContext, 
            int retryDelayMs, 
            int maxChildForRetry) 
            : base(parentInputEventTaskContext)
        {
            _retryDelayMs = retryDelayMs;
            _maxChildForRetry = maxChildForRetry;
        }

        public void HandleStreamChildError()
        {
            // some of child tasks are dropped or poisoned
            // When total number of childs is less than maxChildForRetry
            //  => Retry Parent
            // Otherwise, parent task will be poison
            if (TotalChild <= _maxChildForRetry)
            {
                var retryReason = HasDropPoisonedETagConflict ?
                    RetryReason.SourceOfTruthEtagConflict : RetryReason.StreamChildTaskError;

                _parentEventTaskContext.TaskMovingToRetry(
                    retryReason.FastEnumToString(),
                    "Stream Child Task Error. TotalChild: " + TotalChild,
                    _retryDelayMs,
                    IOComponent.StreamOutputEventTaskCallBack.FastEnumToString(),
                    null);
            }
            else
            {
                var poisonReason = HasDropPoisonedETagConflict ?
                    PoisonReason.EtagConflictAndManyResponses : PoisonReason.StreamChildErrorAndManyResponses;

                _parentEventTaskContext.TaskMovingToPoison(
                    poisonReason.FastEnumToString(),
                    "Stream Child Task Error. TotalChild: " + TotalChild,
                    IOComponent.StreamOutputEventTaskCallBack.FastEnumToString(),
                    null);
            }
        }

        protected override void SetParentTaskToNextChannel()
        {
            if (TotalChildDropped > 0 || TotalChildMovedToPoison > 0)
            {
                HandleStreamChildError();
            }
            else
            {
                _parentEventTaskContext.TaskSuccess(Stopwatch.GetTimestamp());
            }
        }
    }
}
