namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;

    public class TestEventTaskContext : AbstractEventTaskContext<TestEventTaskContext>
    {
        public readonly CancellationTokenSource TaskTimeOutCancellationTokenSource;
        public override CancellationToken TaskCancellationToken { get; }
        public override TestEventTaskContext TaskContext => this;
        public override string Scenario { get; set; }
        public override EventTaskFinalStage EventFinalStage { get; set; }

        public int MovingToPoisonCalledCount = 0;
        public int MovingToRetryCalledCount = 0;
        public bool Disposed = false;
        public bool TimeOutExpired = false;

        public TestNextChannel RetryChannel;
        public TestNextChannel PoisonChannel;

        public TestEventTaskContext(ActivityContext parentContext, bool createNewTraceId, int timeOutMilliSecond) :
            base(TestTaskChannel.TestActivitySource, "TestEventTask", parentContext, createNewTraceId, retryCount: 0, topActivityStartTime: default)
        {
            TaskTimeOutCancellationTokenSource = new CancellationTokenSource();
            TaskCancellationToken = TaskTimeOutCancellationTokenSource.Token;

            if (timeOutMilliSecond > 0)
            {
                SetTaskTimeout(TimeSpan.FromMilliseconds(timeOutMilliSecond));
            }

            RetryChannel = new TestNextChannel("RetryChannel");
            PoisonChannel = new TestNextChannel("PoisonChannel");
        }

        protected override void TaskTimeoutHandler()
        {
            TimeOutExpired = true;
        }

        protected override void Dispose(bool disposing)
        {
            TaskTimeOutCancellationTokenSource.Dispose();
            EventFinalStage = EventTaskFinalStage.SUCCESS;
            Disposed = true;
        }

        public override void TaskMovingToPoison(string poisonReason, string reasonDetails, string component, Exception ex)
        {
            Interlocked.Increment(ref MovingToPoisonCalledCount);
            SetNextChannel(PoisonChannel);
        }

        public override void TaskMovingToRetry(string retryReason, string reasonDetails, int retryDelayMs, string component, Exception ex)
        {
            Interlocked.Increment(ref MovingToRetryCalledCount);
            SetNextChannel(RetryChannel);
        }

        public override void CancelTask()
        {
        }

        public override bool IsAlreadyTaskCancelled()
        {
            return false;
        }
    }
}
