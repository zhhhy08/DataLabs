namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TestEmulator
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter;

    public class TestEventWriter : IEventWriter<TestEventData, TestEventBatchData>
    {
        public string Name => "TestEventWriter";

        public int MaxBatchSize;
        public int NumEventDataCreated;
        public int NumEventBatchCreated;

        public bool ReturnException;
        public bool ReturnExceptionOnCreateBatch;
        public bool SetLargeMessage;

        public List<TestEventData> testEventDataList = new List<TestEventData>();
        public List<TestEventBatchData> testEventBatchDataList = new List<TestEventBatchData>();

        public bool ResetNextWriteException;
        public TestEventWriter? NextTestEventWriter;

        public TestEventWriter(int maxBatchSize)
        {
            MaxBatchSize = maxBatchSize;
        }

        public TestEventWriter()
        {
        }

        public List<KeyValuePair<string, object>> PropertyList = new();

        public void AddProperty(TestEventData eventData, string key, object value)
        {
            lock(this)
            {
                PropertyList.Add(new KeyValuePair<string, object>(key, value));
            }
        }

        public void AddDelayMessageTime(TestEventData eventData, TimeSpan delay)
        {
        }

        public TestEventData CreateEventData(BinaryData binaryData)
        {
            lock(this)
            {
                NumEventDataCreated++;
                return new TestEventData(binaryData);
            }
        }

        public ValueTask<TestEventBatchData> CreateEventDataBatch(CancellationToken cancellationToken = default)
        {
            lock (this)
            {
                if (ReturnExceptionOnCreateBatch)
                {
                    if (ResetNextWriteException)
                    {
                        NextTestEventWriter!.ReturnExceptionOnCreateBatch = false;
                        NextTestEventWriter!.ReturnException = false;
                    }
                    throw new Exception();
                }

                NumEventBatchCreated++;
                return ValueTask.FromResult(new TestEventBatchData(MaxBatchSize));
            }

        }

        public void Clear()
        {
            testEventDataList.Clear();
            testEventBatchDataList.Clear();

            NumEventDataCreated = 0;
            NumEventBatchCreated = 0;

            ReturnException = false;
            SetLargeMessage = false;
            NextTestEventWriter = null;
        }

        public void Dispose()
        {
            Clear();
        }

        public int GetBatchCount(TestEventBatchData eventDataBatch)
        {
            return eventDataBatch.Count;
        }

        public long GetMaxSizeInBytes(TestEventBatchData eventDataBatch)
        {
            return eventDataBatch.MAXSIZE;
        }

        public long GetSizeInBytes(TestEventData eventData)
        {
            return eventData.SIZE;
        }

        public long GetSizeInBytes(TestEventBatchData eventDataBatch)
        {
            return eventDataBatch.TOTALSIZE;
        }

        public Task SendAsync(TestEventData eventData, CancellationToken cancellationToken = default)
        {
            lock(this)
            {
                if (ReturnException)
                {
                    if (ResetNextWriteException)
                    {
                        NextTestEventWriter!.ReturnExceptionOnCreateBatch = false;
                        NextTestEventWriter!.ReturnException = false;
                    }
                    throw new Exception();
                }

                testEventDataList.Add(eventData);
            }
            
            return Task.CompletedTask;
        }

        public Task SendAsync(IEnumerable<TestEventData> eventBatch, CancellationToken cancellationToken = default)
        {
            lock(this)
            {
                if (ReturnException)
                {

                    if (ResetNextWriteException)
                    {
                        NextTestEventWriter!.ReturnExceptionOnCreateBatch = false;
                        NextTestEventWriter!.ReturnException = false;
                    }
                    throw new Exception();
                }

                lock (this)
                {
                    foreach (var testEventData in eventBatch)
                    {
                        testEventDataList.Add(testEventData);
                    }
                }

            }

            return Task.CompletedTask;
        }

        public Task SendBatchAsync(TestEventBatchData eventDataBatch, CancellationToken cancellationToken = default)
        {
            lock(this)
            {
                if (ReturnException)
                {

                    if (ResetNextWriteException)
                    {
                        NextTestEventWriter!.ReturnExceptionOnCreateBatch = false;
                        NextTestEventWriter!.ReturnException = false;
                    }
                    throw new Exception();
                }

                lock (this)
                {
                    testEventBatchDataList.Add(eventDataBatch);
                }
            }

            return Task.CompletedTask;
        }

        public bool TryAddToBatch(TestEventBatchData eventDataBatch, TestEventData eventData)
        {
            lock(this)
            {
                if (SetLargeMessage)
                {
                    return false;
                }

                return eventDataBatch.TryAddToBatch(eventData);
            }
            
        }
    }

    public class TestEventData
    {
        public int SIZE => Data.ToMemory().Length;
        public BinaryData Data { get; set; }

        public TestEventData(BinaryData binaryData)
        {
            Data = binaryData;
        }
    }

    public class TestEventBatchData : IDisposable
    {
        public long MAXSIZE { get; set; }
        public long TOTALSIZE { get; set; }
        
        public List<TestEventData> EventDataList = new();
        public int Count => EventDataList.Count;

        public TestEventBatchData(int maxSize)
        {
            MAXSIZE = maxSize;
        }

        public bool TryAddToBatch(TestEventData eventData)
        {
            if (MAXSIZE > 0 && (TOTALSIZE + eventData.SIZE) > MAXSIZE)
            {
                return false;
            }

            lock(this)
            {
                EventDataList.Add(eventData);
                TOTALSIZE += eventData.SIZE;
            }

            return true;
        }

        public void Dispose()
        {
            lock (this)
            {
                EventDataList.Clear();
                TOTALSIZE = 0;
            }
        }
    }
}
