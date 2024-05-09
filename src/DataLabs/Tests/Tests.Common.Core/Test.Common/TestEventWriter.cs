namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;

    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TestEmulator;

    internal class TestEventWriterCallBack : IEventWriterCallBack<TestEventOutputContext, TestEventData>
    {
        internal int FailCalled;
        internal int SuccessCalled;
        internal int CreationCalled;
        internal int TooLargeCalled;

        public Task EventBatchWriteFailCallBackAsync(List<IEventOutputContext<TestEventOutputContext>> eventOutputContexts, Exception ex, long writeDurationInMilli)
        {
            FailCalled++;
            return Task.CompletedTask;
        }

        public Task EventBatchWriteSuccessCallBackAsync(List<IEventOutputContext<TestEventOutputContext>> eventOutputContexts, long writeDurationInMilli)
        {
            SuccessCalled++;
            return Task.CompletedTask;
        }

        public void EventDataCreationCallBack(IEventOutputContext<TestEventOutputContext> eventOutputContext, TestEventData eventData)
        {
            CreationCalled++;
            return;
        }

        public Task EventTooLargeMessageCallBackAsync(IEventOutputContext<TestEventOutputContext> eventOutputContext, int messageSize)
        {
            TooLargeCalled++;
            return Task.CompletedTask;
        }
    }
}

