namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;

    public interface IEventWriterCallBack<TOutput, TEvent>
    {
        public void EventDataCreationCallBack(IEventOutputContext<TOutput> eventOutputContext, TEvent eventData);

        public Task EventBatchWriteSuccessCallBackAsync(List<IEventOutputContext<TOutput>> eventOutputContexts, long writeDurationInMilli);

        public Task EventBatchWriteFailCallBackAsync(List<IEventOutputContext<TOutput>> eventOutputContexts, Exception ex, long writeDurationInMilli);

        public Task EventTooLargeMessageCallBackAsync(IEventOutputContext<TOutput> eventOutputContext, int messageSize);
    }
}
