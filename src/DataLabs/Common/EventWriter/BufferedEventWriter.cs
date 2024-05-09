namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class BufferedEventWriter<TOutput, TEvent, TBatch> : IBufferedEventWriter<TOutput, TEvent>
        where TOutput : class
        where TEvent : class
        where TBatch : class, IDisposable
    {
        private static readonly ILogger<BufferedEventWriter<TOutput, TEvent, TBatch>> Logger =
            DataLabLoggerFactory.CreateLogger<BufferedEventWriter<TOutput, TEvent, TBatch>>();

        public IEventWriterCallBack<TOutput, TEvent>? EventWriterCallBack { get; set; }

        private const int INIT_BATCH_LIST_SIZE = 4;
        private readonly string _configPrefix;

        private object _updateLock = new object();

        private ChannelWriter<IEventOutputContext<TOutput>>[] _channelWriters;
        private readonly IEventWriter<TEvent, TBatch> _eventWriter;
        
        private int _maxBatchWriters;
        private int _maxBatchSize;
        private TimeSpan _batchWriteTimeout;

        private int _nextChannelIndex = -1;

        private volatile bool _disposed;

        public BufferedEventWriter(IEventWriter<TEvent,TBatch> eventWriter, string configPrefix)
        {
            _configPrefix = configPrefix;

            var configName = configPrefix + SolutionConstants.EventBatchWriterConcurrencySuffix;
            _maxBatchWriters = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                configName, UpdateEventBatchWriterConcurrency, 10, allowMultiCallBacks: true);
            GuardHelper.ArgumentConstraintCheck(_maxBatchWriters > 0, configName);

            configName = configPrefix + SolutionConstants.EventBatchMaxSizeSuffix;
            _maxBatchSize = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                configName, UpdateEventBatchMaxSize, 300, allowMultiCallBacks: true);
            GuardHelper.ArgumentConstraintCheck(_maxBatchSize > 0, configName);

            configName = configPrefix + SolutionConstants.EventBatchWriterTimeOutInSecSuffix;
            var batchWriteTimeoutInSec = ConfigMapUtil.Configuration.GetValueWithCallBack<int>(
                configName, UpdateEventBatchWriterTimeOutInSec, 10, allowMultiCallBacks: true);
            GuardHelper.ArgumentConstraintCheck(batchWriteTimeoutInSec > 0, configName);
            _batchWriteTimeout = TimeSpan.FromSeconds(batchWriteTimeoutInSec);

            _eventWriter = eventWriter;
            _channelWriters = new ChannelWriter<IEventOutputContext<TOutput>>[_maxBatchWriters];

            _nextChannelIndex = -1;

            for (int i = 0; i < _maxBatchWriters; i++)
            {
                var channel = Channel.CreateUnbounded<IEventOutputContext<TOutput>>(
                    new UnboundedChannelOptions
                    {
                        SingleWriter = false,
                        SingleReader = true,
                        AllowSynchronousContinuations = false
                    });

                _channelWriters[i] = channel.Writer;

                _ = Task.Run(() => StartReaderTaskAsync(channel.Reader)); // background
            }
        }

        private Task UpdateEventBatchWriterConcurrency(int newVal)
        {
            if (newVal <= 0)
            {
                Logger.LogError("{prefix}BatchWriterConcurrency must be larger than 0", _configPrefix);
                return Task.CompletedTask;
            }

            lock(_updateLock)
            {
                if (_disposed)
                {
                    return Task.CompletedTask;
                }

                var oldMaxSize = _maxBatchWriters;
                if (newVal == oldMaxSize)
                {
                    return Task.CompletedTask;
                }

                var oldChannelWriters = _channelWriters;
                if (newVal <= oldChannelWriters.Length)
                {
                    // Just change _maxBatchWriters, to avoid race condition, we don't decrease array size
                    if (Interlocked.CompareExchange(ref _maxBatchWriters, newVal, oldMaxSize) == oldMaxSize)
                    {
                        Logger.LogWarning("{prefix}BatchWriterConcurrency is changed, Old: {oldVal}, New: {newVal}",
                            _configPrefix, oldMaxSize, newVal);
                    }
                }
                else
                {
                    // newVal > oldChannelWriters.Length
                    // We need to increase channelWriter Array
                    // To avoid race condition,
                    // 1. first create ChannelArray
                    // 2. Reaplce _maxBatchWriters
                    var newChannelWriters = new ChannelWriter<IEventOutputContext<TOutput>>[newVal];
                    for (int i = 0; i < newVal; i++)
                    {
                        if (i < oldChannelWriters.Length)
                        {
                            newChannelWriters[i] = oldChannelWriters[i];
                        }else
                        {
                            // Create new channel
                            var channel = Channel.CreateUnbounded<IEventOutputContext<TOutput>>(
                            new UnboundedChannelOptions
                            {
                                SingleWriter = false,
                                SingleReader = true,
                                AllowSynchronousContinuations = false
                            });

                            newChannelWriters[i] = channel.Writer;

                            _ = Task.Run(() => StartReaderTaskAsync(channel.Reader)); // background
                        }
                    }

                    // Replace writers
                    Interlocked.Exchange(ref _channelWriters, newChannelWriters);

                    if (Interlocked.CompareExchange(ref _maxBatchWriters, newVal, oldMaxSize) == oldMaxSize)
                    {
                        Logger.LogWarning("{prefix}BatchWriterConcurrency is changed, Old: {oldVal}, New: {newVal}",
                            _configPrefix, oldMaxSize, newVal);
                    }
                }
            }
            return Task.CompletedTask;
        }

        private Task UpdateEventBatchMaxSize(int newVal)
        {
            if (newVal <= 0)
            {
                Logger.LogError("{prefix}EventBatchMaxSize must be larger than 0", _configPrefix);
                return Task.CompletedTask;
            }

            var oldMaxSize = _maxBatchSize;
            if (Interlocked.CompareExchange(ref _maxBatchSize, newVal, oldMaxSize) == oldMaxSize)
            {
                Logger.LogWarning("{prefix}EventBatchMaxSize is changed, Old: {oldVal}, New: {newVal}",
                    _configPrefix, oldMaxSize, newVal);
            }

            return Task.CompletedTask;
        }

        private Task UpdateEventBatchWriterTimeOutInSec(int newVal)
        {
            if (newVal <= 0)
            {
                Logger.LogError("{prefix}BatchWriterTimeOutInSec must be larger than 0", _configPrefix);
                return Task.CompletedTask;
            }

            var oldTimeout = _batchWriteTimeout;
            if (newVal == oldTimeout.TotalSeconds)
            {
                return Task.CompletedTask;
            }

            _batchWriteTimeout = TimeSpan.FromSeconds(newVal);

            Logger.LogWarning("{prefix}BatchWriterTimeOutInSec is changed, Old: {oldVal}, New: {newVal}",
                    _configPrefix, oldTimeout.TotalSeconds, newVal);

            return Task.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TEvent CreateEventData(IEventOutputContext<TOutput> eventOutputContext)
        {
            var eventData = _eventWriter.CreateEventData(eventOutputContext.GetOutputMessage());
            EventWriterCallBack?.EventDataCreationCallBack(eventOutputContext, eventData);
            return eventData;
        }

        private async Task ReadAndSendBatchAsync(ChannelReader<IEventOutputContext<TOutput>> channelReader, List<IEventOutputContext<TOutput>> eventOutputContexts)
        {
            try
            {
                TBatch? eventDataBatch = null;
                IEventOutputContext<TOutput>? eventOutputContext = null;

                while (channelReader.TryRead(out eventOutputContext))
                {
                    if (eventOutputContext == null)
                    {
                        continue;
                    }

                    if (eventDataBatch == null)
                    {
                        eventDataBatch = await _eventWriter.CreateEventDataBatch().ConfigureAwait(false);
                        eventOutputContexts.Clear();
                    }

                    var eventData = CreateEventData(eventOutputContext);
                    var batchSize = _eventWriter.GetBatchCount(eventDataBatch);
                    if (batchSize >= _maxBatchSize || !_eventWriter.TryAddToBatch(eventDataBatch, eventData))
                    {
                        // flush
                        var hasPrevData = batchSize > 0;
                        if (hasPrevData)
                        {
                            await SendEventDataBatchAndDisposeAsync(eventDataBatch, eventOutputContexts).ConfigureAwait(false);

                            eventDataBatch = await _eventWriter.CreateEventDataBatch().ConfigureAwait(false);
                            eventOutputContexts.Clear();
                        }

                        if (!hasPrevData || !_eventWriter.TryAddToBatch(eventDataBatch, eventData))
                        {
                            // Too large single item
                            // It should not happen because it should have been taken care in previous Task

                            int dataSize = eventOutputContext.GetOutputMessageSize();
                            Logger.LogError("OutputMessage is too large. OutputMessage Size: {osize}, MaximumSizeInBytes: {MaximumSizeInBytes}",
                                dataSize, _eventWriter.GetMaxSizeInBytes(eventDataBatch));

                            if (EventWriterCallBack != null)
                            {
                                var tooLargeMessageTask = eventOutputContext;
                                _ = Task.Run(() => EventWriterCallBack.EventTooLargeMessageCallBackAsync(tooLargeMessageTask, dataSize)); //background job
                            }
                        }
                        else
                        {
                            eventOutputContexts.Add(eventOutputContext);
                        }
                    }
                    else
                    {
                        eventOutputContexts.Add(eventOutputContext);
                    }
                }

                if (eventDataBatch != null && _eventWriter.GetBatchCount(eventDataBatch) > 0)
                {
                    // flush
                    await SendEventDataBatchAndDisposeAsync(eventDataBatch, eventOutputContexts).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                if (!_disposed)
                {
                    // Should not happen
                    Logger.LogCritical(ex, "ReadAndSendBatchAsync got exception. {exception}", ex.ToString());
                }
            }
        }

        private async Task StartReaderTaskAsync(ChannelReader<IEventOutputContext<TOutput>> channelReader)
        {
            List<IEventOutputContext<TOutput>> eventOutputContexts = new(INIT_BATCH_LIST_SIZE);

            while(!_disposed)
            {
                try
                {
                    while (await channelReader.WaitToReadAsync().ConfigureAwait(false))
                    {
                        await ReadAndSendBatchAsync(channelReader, eventOutputContexts).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (!_disposed)
                    {
                        // Should not happen
                        Logger.LogCritical(ex, "StartReaderTaskAsync got exception. {exception}", ex.ToString());
                    }
                }
            }
            
        }

        private async Task SendEventDataBatchAndDisposeAsync(TBatch eventDataBatch, List<IEventOutputContext<TOutput>> eventOutputContexts)
        {
            var startTime = Stopwatch.GetTimestamp();

            try
            {
                using var cancellationTokenSource = new CancellationTokenSource(_batchWriteTimeout);
                var cancellationToken = cancellationTokenSource.Token;

                await _eventWriter.SendBatchAsync(eventDataBatch, cancellationToken).ConfigureAwait(false);

                /*
                 * When published, the result is atomic; 
                 * either all events that belong to the batch were successful or all have failed. 
                 * Partial success is not possible.
                 */

                var writeDurationInMilli = (long)Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
                if (EventWriterCallBack != null)
                {
                    await EventWriterCallBack.EventBatchWriteSuccessCallBackAsync(eventOutputContexts, writeDurationInMilli).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                var writeDurationInMilli = (long)Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;

                Logger.LogError(ex, 
                    "SendEventDataBatch failed. Count: {count}, MaximumSizeInBytes: {MaximumSizeInBytes}, SizeInBytes: {SizeInBytes}. {exception}",
                    _eventWriter.GetBatchCount(eventDataBatch),
                    _eventWriter.GetMaxSizeInBytes(eventDataBatch),
                    _eventWriter.GetSizeInBytes(eventDataBatch), 
                    ex.ToString());

                if (EventWriterCallBack != null)
                {
                    // Since this is background task, we need to copy the list
                    var copiedContexts = new List<IEventOutputContext<TOutput>>(eventOutputContexts);
                    _ = Task.Run(() => EventWriterCallBack.EventBatchWriteFailCallBackAsync(copiedContexts, ex, writeDurationInMilli)); //background task
                }
            }
            finally
            {
                eventDataBatch.Dispose();
                eventOutputContexts.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NextWriteId()
        {
            var id = Interlocked.Increment(ref _nextChannelIndex) % _maxBatchWriters;
            return Math.Abs(id);
        }

        public async ValueTask<bool> AddEventMessageAsync(IEventOutputContext<TOutput> eventOutputContext)
        {
            // Handle Empty Resposne
            var hasData = eventOutputContext.GetOutputMessage() != null;
            if (!hasData)
            {
                return true;
            }

            int nextWriteId = NextWriteId();
            await _channelWriters[nextWriteId].WriteAsync(eventOutputContext).ConfigureAwait(false);
            return false;
        }

        public void Dispose()
        {
            lock (_updateLock)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _eventWriter.Dispose();

                    var channelWriters = _channelWriters;
                    for (int i = 0; i < channelWriters.Length; i++)
                    {
                        try
                        {
                            channelWriters[i].Complete();
                        }
                        catch (Exception)
                        {
                            // ignore
                        }
                    }
                }
            }
        }
    }
}
