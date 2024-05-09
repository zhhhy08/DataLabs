namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IEventWriter<TEvent, TBatch> : IDisposable
    {
        public string Name { get; }

        public ValueTask<TBatch> CreateEventDataBatch(CancellationToken cancellationToken = default);

        public bool TryAddToBatch(TBatch eventDataBatch, TEvent eventData);

        public int GetBatchCount(TBatch eventDataBatch);

        public long GetMaxSizeInBytes(TBatch eventDataBatch);

        public long GetSizeInBytes(TEvent eventData);

        public long GetSizeInBytes(TBatch eventDataBatch);

        public TEvent CreateEventData(BinaryData binaryData);

        public void AddProperty(TEvent eventData, string key, object value);

        public void AddDelayMessageTime(TEvent eventData, TimeSpan delay);

        public Task SendAsync(TEvent eventData, CancellationToken cancellationToken = default);

        public Task SendAsync(IEnumerable<TEvent> eventBatch, CancellationToken cancellationToken = default);

        public Task SendBatchAsync(TBatch eventDataBatch, CancellationToken cancellationToken = default);
    }
}
