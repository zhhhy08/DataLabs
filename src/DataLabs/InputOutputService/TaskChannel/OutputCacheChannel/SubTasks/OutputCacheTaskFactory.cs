namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputCacheChannel.SubTasks
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;

    internal class OutputCacheTaskFactory : ISubTaskFactory<CacheTaskContext>
    {
        private static readonly ILogger<OutputCacheTaskFactory> Logger =
            DataLabLoggerFactory.CreateLogger<OutputCacheTaskFactory>();

        public string SubTaskName => "OutputCache";
        public bool CanContinueToNextTaskOnException => false;

        private readonly OutputCacheTask _outputCacheTask; // singleton
        private readonly IResourceCacheClient _cacheClient;

        private bool _useOutputCache;
        private bool _useOutputTimeStampInCache;

        public OutputCacheTaskFactory(IResourceCacheClient cacheClient)
        {
            _cacheClient = cacheClient;
            _outputCacheTask = new OutputCacheTask(this);

            _useOutputCache = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(SolutionConstants.UseOutputCache, UpdateUseOutputCache, false, allowMultiCallBacks: true);
            _useOutputTimeStampInCache = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(InputOutputConstants.UseOutputTimeStampInCache, UpdateUseOutputTimeStampInCache, false);
        }

        public ISubTask<CacheTaskContext> CreateSubTask(AbstractEventTaskContext<CacheTaskContext> eventTaskContext)
        {
            var cacheTaskContext = eventTaskContext.TaskContext;
            if (!_useOutputCache || 
                !_cacheClient.CacheEnabled ||
                !IsValidContextForCache(cacheTaskContext))
            {
                return null;
            }
            return _outputCacheTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidContextForCache(CacheTaskContext cacheTaskContext)
        {
            if (string.IsNullOrEmpty(cacheTaskContext.ResourceId))
            {
                return false;
            }
            return true;
        }

        public void Dispose()
        {
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

        private Task UpdateUseOutputTimeStampInCache(bool newValue)
        {
            var oldValue = _useOutputTimeStampInCache;
            if (oldValue != newValue)
            {
                _useOutputTimeStampInCache = newValue;
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    InputOutputConstants.UseOutputTimeStampInCache, oldValue, newValue);
            }
            return Task.CompletedTask;
        }

        private class OutputCacheTask : ISubTask<CacheTaskContext>
        {
            private readonly OutputCacheTaskFactory _outputCacheTaskFactory;

            public OutputCacheTask(OutputCacheTaskFactory outputCacheTaskFactory)
            {
                _outputCacheTaskFactory = outputCacheTaskFactory;
            }

            public bool UseValueTask => false;

            public async Task ProcessEventTaskContextAsync(AbstractEventTaskContext<CacheTaskContext> eventTaskContext)
            {
                var taskActivity = eventTaskContext.EventTaskActivity;
                var cacheContext = eventTaskContext.TaskContext;

                var resourceId = cacheContext.ResourceId;
                var tenantId = cacheContext.TenantId;
                var resourceType = cacheContext.ResourceType;

                TagList tagList = default;
                tagList.Add(SolutionConstants.InputResourceId, resourceId);
                tagList.Add(SolutionConstants.TenantId, tenantId);

                EventTaskFinalStage cacheFinalStage = EventTaskFinalStage.NONE;

                if (cacheContext.Command == CacheTaskContext.CacheCommand.DELETE)
                {
                    bool cacheResult = await _outputCacheTaskFactory._cacheClient.DeleteResourceAsync(resourceId: resourceId, tenantId: tenantId, cacheContext.TaskCancellationToken).ConfigureAwait(false);

                    tagList.Add(InputOutputConstants.OutputCacheCommand, "DeleteResource");
                    tagList.Add(SolutionConstants.Success, cacheResult);
                    taskActivity.AddEvent(InputOutputConstants.EventName_OutputCacheDelete, tagList);

                    cacheFinalStage = cacheResult ? EventTaskFinalStage.SUCCESS : EventTaskFinalStage.FAIL;
                }
                else if (cacheContext.Command == CacheTaskContext.CacheCommand.SET)
                {
                    if (cacheContext.Data == null)
                    {
                        return;
                    }

                    var cacheValue = cacheContext.Data.ToMemory();
                    if (cacheValue.Length == 0)
                    {
                        return;
                    }

                    var cacheTTL = _outputCacheTaskFactory._cacheClient.CacheTTLManager.GetCacheTTL(resourceType: resourceType, inputType: false);
                    var hasTTL = cacheTTL != TimeSpan.Zero;

                    // Set command

                    bool cacheResult = false;

                    if (_outputCacheTaskFactory._useOutputTimeStampInCache && cacheContext.TimeStamp > 0)
                    {
                        cacheResult = await _outputCacheTaskFactory._cacheClient.SetResourceIfGreaterThanAsync(
                            resourceId: resourceId,
                            tenantId: tenantId,
                            dataFormat: ResourceCacheDataFormat.ARN,
                            resource: cacheValue,
                            timeStamp: cacheContext.TimeStamp,
                            etag: cacheContext.ETag,
                            expiry: !hasTTL ? null : cacheTTL,
                            cacheContext.TaskCancellationToken).ConfigureAwait(false);

                        tagList.Add(InputOutputConstants.OutputTimeStamp, cacheContext.TimeStamp);
                        tagList.Add(InputOutputConstants.OutputCacheCommand, "SetResourceIfGreaterThan");
                        tagList.Add(SolutionConstants.Success, cacheResult);
                        taskActivity.AddEvent(InputOutputConstants.EventName_OutputCacheInsert, tagList);
                    }
                    else
                    {
                        cacheResult = await _outputCacheTaskFactory._cacheClient.SetResourceAsync(
                            resourceId: resourceId,
                            tenantId: tenantId,
                            dataFormat: ResourceCacheDataFormat.ARN,
                            resource: cacheValue,
                            timeStamp: cacheContext.TimeStamp,
                            etag: cacheContext.ETag,
                            expiry: !hasTTL ? null : cacheTTL,
                            cacheContext.TaskCancellationToken).ConfigureAwait(false);

                        tagList.Add(InputOutputConstants.OutputCacheCommand, "SetResource");
                        tagList.Add(SolutionConstants.Success, cacheResult);
                        taskActivity.AddEvent(InputOutputConstants.EventName_OutputCacheInsert, tagList);
                    }

                    cacheFinalStage = cacheResult ? EventTaskFinalStage.SUCCESS : EventTaskFinalStage.FAIL;
                }

                taskActivity.SetTag(SolutionConstants.TaskFinalStage, cacheFinalStage.FastEnumToString());

                eventTaskContext.EventFinalStage = cacheFinalStage;
            }

            public ValueTask ProcessEventTaskContextValueAsync(AbstractEventTaskContext<CacheTaskContext> eventTaskContext)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
