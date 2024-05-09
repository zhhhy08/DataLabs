namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.SourceOfTruthChannel.SubTasks
{
    using global::Azure;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using static Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.CacheTaskContext;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;

    internal class OutputBlobUploadTaskFactory : ISubTaskFactory<IOEventTaskContext<ARNSingleInputMessage>>
    {
        private static readonly ActivityMonitorFactory OutputBlobUploadTaskFactoryDeleteCacheAsync =
            new("OutputBlobUploadTaskFactory.DeleteCacheAsync");

        private static readonly ActivityMonitorFactory OutputBlobUploadTaskFactoryETagConflict = 
            new ("OutputBlobUploadTaskFactory.ETagConflict");

        private static readonly ILogger<OutputBlobUploadTaskFactory> Logger = 
            DataLabLoggerFactory.CreateLogger<OutputBlobUploadTaskFactory>();

        private static readonly OutputCacheDeleteFailException _outputCacheDeleteFail = new("Output Cache Delete Fail");

        public string SubTaskName => "OutputBlobUpload";
        public bool CanContinueToNextTaskOnException => false;

        private readonly OutputBlobUploadTask _outputBlobUploadTask; // singleton

        private int _conflictRetryDelayInMsec; // msecs
        private bool _useOutputCache;
        private bool _useSyncOutputCache;
        private bool _useOutputTimeTagCondition;
        private bool _deleteCacheAfterETagConflict;

        public OutputBlobUploadTaskFactory()
        {

            _conflictRetryDelayInMsec =
                ConfigMapUtil.Configuration.GetValueWithCallBack<int>(InputOutputConstants.SourceOfTruthConflictRetryDelayInMsec, UpdateConflictRetryDelay, 100);

            _useOutputTimeTagCondition =
                ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(InputOutputConstants.SourceOfTruthUseOutputTimeTagCondition, UpdateUseOutputTimeTag, false);

            _useOutputCache = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(SolutionConstants.UseOutputCache, UpdateUseOutputCache, false, allowMultiCallBacks: true);
            _useSyncOutputCache = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(InputOutputConstants.UseSyncOutputCache, UpdateUseSyncOutputCache, false);
            _deleteCacheAfterETagConflict = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(InputOutputConstants.DeleteCacheAfterETagConflict, UpdateDeleteCacheAfterETagConflict, true);

            _outputBlobUploadTask = new OutputBlobUploadTask(this);
        }

        public ISubTask<IOEventTaskContext<ARNSingleInputMessage>> CreateSubTask(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            var ioTaskContext = eventTaskContext.TaskContext;

            if (!SolutionInputOutputService.UseSourceOfTruth || !IsValidContextForBlobUpload(ioTaskContext))
            {
                return null;
            }
            return _outputBlobUploadTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidContextForBlobUpload(IOEventTaskContext<ARNSingleInputMessage> eventTaskContext)
        {
            if (eventTaskContext.OutputMessage == null || eventTaskContext.OutputMessage.GetOutputMessageSize() == 0)
            {
                // Move to Success
                var ioTaskContext = eventTaskContext.TaskContext;
                eventTaskContext.EventTaskActivity.SetTag(SolutionConstants.EmptyOutput, true);
                ioTaskContext.TaskSuccess(Stopwatch.GetTimestamp());
                return false;
            }
            return true;
        }

        public void Dispose()
        {
        }

        private Task UpdateDeleteCacheAfterETagConflict(bool newValue)
        {
            var oldValue = _deleteCacheAfterETagConflict;
            if (oldValue != newValue)
            {
                _deleteCacheAfterETagConflict = newValue;
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    InputOutputConstants.DeleteCacheAfterETagConflict, oldValue, newValue);
            }
            return Task.CompletedTask;
        }

        private Task UpdateUseOutputCache(bool newValue)
        {
            var oldValue = _useOutputCache;
            if (oldValue != newValue)
            {
                _useOutputCache = newValue;
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.UseOutputCache, oldValue, newValue);
            }
            return Task.CompletedTask;
        }

        private Task UpdateUseSyncOutputCache(bool newValue)
        {
            var oldValue = _useSyncOutputCache;
            if (oldValue != newValue)
            {
                _useSyncOutputCache = newValue;
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    InputOutputConstants.UseSyncOutputCache, oldValue, newValue);
            }
            return Task.CompletedTask;
        }

        private Task UpdateUseOutputTimeTag(bool newValue)
        {
            var oldValue = _useOutputTimeTagCondition;
            if (oldValue != newValue)
            {
                _useOutputTimeTagCondition = newValue;
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    InputOutputConstants.SourceOfTruthUseOutputTimeTagCondition, oldValue, newValue);
            }
            return Task.CompletedTask;
        }

        private Task UpdateConflictRetryDelay(int newDelayInMs)
        {
            if (newDelayInMs < 0)
            {
                Logger.LogError("{config} must be equal and larger than 0", InputOutputConstants.SourceOfTruthConflictRetryDelayInMsec);
                return Task.CompletedTask;
            }

            var oldDelay = _conflictRetryDelayInMsec;
            if (Interlocked.CompareExchange(ref _conflictRetryDelayInMsec, newDelayInMs, oldDelay) == oldDelay)
            {
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    InputOutputConstants.SourceOfTruthConflictRetryDelayInMsec, oldDelay, newDelayInMs);
            }

            return Task.CompletedTask;
        }

        private class OutputBlobUploadTask : ISubTask<IOEventTaskContext<ARNSingleInputMessage>>
        {
            public bool UseValueTask => false;

            private readonly OutputBlobUploadTaskFactory _outputBlobUploadTaskFactory;

            public OutputBlobUploadTask(OutputBlobUploadTaskFactory outputBlobUploadTaskFactory)
            {
                _outputBlobUploadTaskFactory = outputBlobUploadTaskFactory;
            }

            public async Task ProcessEventTaskContextAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                var ioTaskContext = eventTaskContext.TaskContext;
                var outputBlobClient = ioTaskContext.RegionConfigData.outputBlobClient;
                var outputMessage = ioTaskContext.OutputMessage;
                var taskActivity = eventTaskContext.EventTaskActivity;
                OpenTelemetryActivityWrapper.Current = taskActivity;

                taskActivity.SetTag("OutputBlobUploadProcessingRegion", ioTaskContext.RegionConfigData.RegionLocationName);

                var resourceId = outputMessage.ResourceId;
                var tenantId = outputMessage.TenantId;
                ETag? outEtag = string.IsNullOrEmpty(outputMessage.ETag) ? null : new ETag(outputMessage.ETag);
                var outputTimeStamp = outputMessage.OutputTimeStamp;
                var cancellationToken = ioTaskContext.TaskCancellationToken;

                (ETag? etag, bool conflict) result;
                try
                {
                    result = await outputBlobClient.UploadContentAsync(
                        resourceId, 
                        tenantId, 
                        outputMessage.Data, 
                        outEtag, 
                        outputTimeStamp,
                        _outputBlobUploadTaskFactory._useOutputTimeTagCondition,
                        retryFlowCount: ioTaskContext.RetryCount,
                        cancellationToken).ConfigureAwait(false);

                }catch (Exception)
                {
                    IOServiceOpenTelemetry.BlobSourceOfTruthUploadCounter.Add(1,
                        new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, taskActivity.ActivityName),
                        new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, ioTaskContext.RetryCount > 0),
                        MonitoringConstants.GetSuccessDimension(false));
                    throw;
                }

                if (result.conflict)
                {
                    IOServiceOpenTelemetry.BlobSourceOfTruthUploadCounter.Add(1,
                        new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, taskActivity.ActivityName),
                        new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, ioTaskContext.RetryCount > 0),
                        MonitoringConstants.GetSuccessDimension(false));

                    // Logging ETagConflict as activityFail showed too noisy. 
                    // In order to track easily ETag failure, log it as separate activity monitor with success
                    LogConflictEvent(eventTaskContext);

                    taskActivity.AddEvent(outEtag != null ? 
                        InputOutputConstants.EventName_BlobSourceOfTruthEtagConflict : 
                        InputOutputConstants.EventName_BlobSourceOfTruthOutputTimeStampConflict);

                    if (outEtag != null)
                    {
                        taskActivity.SetTag(SolutionConstants.SourceOfTruthETagConflict, true);
                        ioTaskContext.IOEventTaskFlags |= IOEventTaskFlag.SourceOfTruthEtagConflict;

                        // ETag conflict happens. Let's delete cache entry
                        // We thought retry eventually will overwrite the cache entry but keeping the stale cache entry might be not good idea. 
                        if (_outputBlobUploadTaskFactory._useOutputCache && 
                            _outputBlobUploadTaskFactory._deleteCacheAfterETagConflict)
                        {
                            await DeleteCacheAsync(eventTaskContext).ConfigureAwait(false);
                        }

                        // Clear Output
                        ioTaskContext.AddOutputMessage(null);

                        // Move to Retry Channel
                        var retryDelayInMsec = _outputBlobUploadTaskFactory._conflictRetryDelayInMsec;
                        ioTaskContext.TaskMovingToRetry(RetryReason.SourceOfTruthEtagConflict.FastEnumToString(), null, retryDelayInMsec, IOComponent.SourceOfTruthChannel.FastEnumToString(), null);
                        return;
                    }
                    else
                    {
                        taskActivity.SetTag(SolutionConstants.OutputTimeStampConflict, true);
                        ioTaskContext.IOEventTaskFlags |= IOEventTaskFlag.SourceOfTruthOutputTimeConflict;

                        // It means, output is older message, we don't need to send it to output, Need to drop
                        ioTaskContext.TaskDrop(DropReason.OlderOutputMessage.FastEnumToString(), null, IOComponent.SourceOfTruthChannel.FastEnumToString());
                        return;
                    }
                }
                else
                {
                    // Successful blob Upload
                    IOServiceOpenTelemetry.BlobSourceOfTruthUploadCounter.Add(1,
                        new KeyValuePair<string, object>(MonitoringConstants.EventTaskTypeDimension, taskActivity.ActivityName),
                        new KeyValuePair<string, object>(MonitoringConstants.IsRetryDimension, ioTaskContext.RetryCount > 0),
                        MonitoringConstants.GetSuccessDimension(true));

                    taskActivity.AddEvent(InputOutputConstants.EventName_BlobSourceOfTruthUploaded);
                    ioTaskContext.IOEventTaskFlags |= IOEventTaskFlag.SuccessUploadToSourceOfTruth;

                    // Update Etag and Move to OutputCache
                    outputMessage.ETag = result.etag?.ToString();

                    if (_outputBlobUploadTaskFactory._useOutputCache)
                    {
                        // Need to Create CacheContext first so that we can use it even after IOEventTaskContext is disposed
                        var cacheTaskContext = new CacheTaskContext(
                            tenantId: outputMessage.TenantId,
                            resourceId: outputMessage.ResourceId,
                            resourceType: outputMessage.ResourceType,
                            correlationId: outputMessage.CorrelationId,
                            data: outputMessage.Data,
                            cacheCommand: CacheCommand.SET,
                            timeStamp: outputMessage.OutputTimeStamp,
                            etag: outputMessage.ETag,
                            retryFlowCount: ioTaskContext.RetryCount,
                            parentActivityContext: ioTaskContext.EventTaskActivity.Context,
                            topActivityStartTime: ioTaskContext.EventTaskActivity.TopActivityStartTime,
                            taskCancellationToken: ioTaskContext.TaskCancellationToken,
                            regionConfigData: ioTaskContext.RegionConfigData);

                        // Send To Output Cache
                        // TODO
                        // Handle OutputCache error with wait here?
                        // Currently OutputCache fail is tolerant because we will compare blob Etag eventually
                        // When we migrate to Pacific SourceOfTruth Layer, we need to revisit this one
                        // Cache Insert will be executed asynchrously(in background)

                        if (_outputBlobUploadTaskFactory._useSyncOutputCache)
                        {
                            await cacheTaskContext.StartEventTaskAsync(SolutionInputOutputService.CacheChannels.OutputCacheChannelManager, true, null).ConfigureAwait(false);
                        }
                        else
                        {
                            _ = Task.Run(() => cacheTaskContext.StartEventTaskAsync(SolutionInputOutputService.CacheChannels.OutputCacheChannelManager, false, null));
                        }
                    }

                    // Internal Response check
                    if (SolutionUtils.IsInternalResponse(outputMessage?.RespProperties))
                    {
                        // Move to final Task Success directly
                        eventTaskContext.EventTaskActivity.SetTag(InputOutputConstants.InternalResponse, true);
                        eventTaskContext.TaskContext.TaskSuccess(Stopwatch.GetTimestamp());
                    }else
                    {
                        // For eventTaskContext, Set OutputChannel to next channel
                        SolutionInputOutputService.SetNextChannelToOutputChannel(eventTaskContext);
                    }
                    return;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void LogConflictEvent(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                try
                {
                    var ioTaskContext = eventTaskContext.TaskContext;
                    var outputMessage = ioTaskContext.OutputMessage;
                    var taskActivity = eventTaskContext.EventTaskActivity;
                    var etag = outputMessage.ETag;
                    var resourceId = outputMessage.ResourceId;
                    var tenantId = outputMessage.TenantId;
                    var outputTimeStamp = outputMessage.OutputTimeStamp;
                    var isEtagConflict = !string.IsNullOrEmpty(etag);
                    var resourceType = ArmUtils.GetResourceType(resourceId);
                    var conflictReason = isEtagConflict ? SolutionConstants.EtagConflict : SolutionConstants.TimeStampConflict;

                    using var etagMonitor = OutputBlobUploadTaskFactoryETagConflict.ToMonitor();


                    etagMonitor.Activity[SolutionConstants.ResourceType] = resourceType;
                    etagMonitor.Activity[SolutionConstants.RetryCount] = ioTaskContext.RetryCount;
                    etagMonitor.Activity[SolutionConstants.ConflictReason] = conflictReason;

                    etagMonitor.Activity[SolutionConstants.ResourceId] = resourceId;
                    etagMonitor.Activity[SolutionConstants.TenantId] = tenantId;
                    etagMonitor.Activity[SolutionConstants.ETag] = etag;
                    etagMonitor.Activity[SolutionConstants.RetryCount] = ioTaskContext.RetryCount;
                    etagMonitor.Activity[SolutionConstants.OutputTimeStamp] = outputTimeStamp;

                    IOServiceOpenTelemetry.BlobSourceOfTruthUploadConflictCounter.Add(1,
                        new KeyValuePair<string, object>(SolutionConstants.ResourceType, resourceType),
                        new KeyValuePair<string, object>(SolutionConstants.ConflictReason, conflictReason),
                        new KeyValuePair<string, object>(MonitoringConstants.RetryCountDimension, ioTaskContext.RetryCount));
                
                    etagMonitor.OnCompleted(logging: true, recordDurationMetric: false);
                }
                catch (Exception)
                {
                }
            }

            private static async Task DeleteCacheAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                using var monitor = OutputBlobUploadTaskFactoryDeleteCacheAsync.ToMonitor();

                var ioTaskContext = eventTaskContext.TaskContext;
                var taskActivity = eventTaskContext.EventTaskActivity;
                var outputMessage = ioTaskContext.OutputMessage;
                var cancellationToken = ioTaskContext.TaskCancellationToken;
                var resourceId = outputMessage.ResourceId;
                var resourceType = outputMessage.ResourceType;
                var tenantId = outputMessage.TenantId;
                var correlationId = outputMessage.CorrelationId;

                Exception errorException = null;

                try
                {
                    // Delete Stale Cache Entry from OutputCache
                    var cacheTaskContext = new CacheTaskContext(
                        tenantId: tenantId,
                        resourceId: resourceId,
                        resourceType: resourceType,
                        correlationId: correlationId,
                        data: null,
                        cacheCommand: CacheTaskContext.CacheCommand.DELETE,
                        timeStamp: default,
                        etag: null,
                        retryFlowCount: eventTaskContext.RetryCount,
                        parentActivityContext: outputMessage.ParentIOEventTaskContext.EventTaskActivity.Context,
                        topActivityStartTime: outputMessage.ParentIOEventTaskContext.EventTaskActivity.TopActivityStartTime,
                        taskCancellationToken: cancellationToken,
                        regionConfigData: ioTaskContext.RegionConfigData);

                    // Send To Output Cache Channel and Wait all completion. Notice waitForTaskFinish:true 
                    await cacheTaskContext.StartEventTaskAsync(
                        SolutionInputOutputService.CacheChannels.OutputCacheChannelManager, 
                        waitForTaskFinish: true, 
                        null).ConfigureAwait(false);

                    // Don't set tag to CacheTaskContext after this line because cacheTaskContext will already be disposed()
                    if (cacheTaskContext.EventFinalStage == EventTaskFinalStage.SUCCESS)
                    {
                        ioTaskContext.IOEventTaskFlags |= IOEventTaskFlag.DeleteCacheAfterSourceOfTruthETagConflict;

                        monitor.Activity["CacheDeleteAfterFailureFromETagConflict"] = true;
                        taskActivity.SetTag("CacheDeleteAfterFailureFromETagConflict", true);

                        monitor.OnCompleted();
                        return;
                    }
                    else
                    {
                        errorException = _outputCacheDeleteFail;
                    }
                }
                catch (Exception ex)
                {
                    errorException = ex;
                }

                if (errorException != null)
                {
                    taskActivity.SetTag("CacheDeleteAfterFailureFromETagConflict", false);
                    monitor.Activity["CacheDeleteAfterFailureFromETagConflict"] = false;
                    monitor.OnError(errorException);
                }
            }

            public ValueTask ProcessEventTaskContextValueAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
