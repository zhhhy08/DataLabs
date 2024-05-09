namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TestEmulator
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;

    public class TestBufferedEventWriter<TOutput, TEvent> : IBufferedEventWriter<TOutput, TEvent>
        where TOutput : class
        where TEvent : class
    {
        public int Count => EventOutputs.Count;
        public List<BinaryData> EventOutputs = new List<BinaryData>();
        public bool UseAsyncAdd { get; set; }
        public bool ReturnException { get; set; }

        public IEventWriterCallBack<TOutput, TEvent>? EventWriterCallBack { get; set; }

        public async ValueTask<bool> AddEventMessageAsync(IEventOutputContext<TOutput> eventOutputContext)
        {
            if (UseAsyncAdd)
            {
                return await AddEventMessageToChannelAsync(eventOutputContext).ConfigureAwait(false);
            }

            if (ReturnException)
            {
                throw new Exception("Test exception");
            }

            EventOutputs.Add(eventOutputContext.GetOutputMessage());
            return true; // sync
        }

        public ValueTask<bool> AddEventMessageToChannelAsync(IEventOutputContext<TOutput> eventOutputContext)
        {
            var copiedContexts = new List<IEventOutputContext<TOutput>>();
            copiedContexts.Add(eventOutputContext);

            if (ReturnException)
            {
                var ex = new Exception("Test exception");

                if (EventWriterCallBack != null)
                {
                    _ = Task.Run(() => EventWriterCallBack.EventBatchWriteFailCallBackAsync(copiedContexts, ex, 100)); //background task
                }

                return ValueTask.FromResult(false); // async
            }

            EventOutputs.Add(eventOutputContext.GetOutputMessage());

            if (EventWriterCallBack != null)
            {
                _ = Task.Run(() => EventWriterCallBack.EventBatchWriteSuccessCallBackAsync(copiedContexts, 100)); //background task
            }

            return ValueTask.FromResult(false); // async
        }

        public void Dispose()
        {
        }

        public void Clear()
        {
            EventOutputs.Clear();
            UseAsyncAdd = true;
            ReturnException = false;
        }
    }
}
