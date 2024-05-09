namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.InputResourceCacheChannel.SubTasks
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;

    internal class InputCacheTaskFactory : ISubTaskFactory<IOEventTaskContext<ARNSingleInputMessage>>
    {
        private static readonly ILogger<InputCacheTaskFactory> Logger =
            DataLabLoggerFactory.CreateLogger<InputCacheTaskFactory>();

        public string SubTaskName => "InputCache";
        public bool CanContinueToNextTaskOnException => false;

        private readonly InputCacheTask _inputCacheTask; // singleton
        private readonly IResourceCacheClient _cacheClient;

        private bool _tolerateInputCacheFailure;

        private readonly object _updateLock = new ();

        public InputCacheTaskFactory(IResourceCacheClient cacheClient)
        {
            _cacheClient = cacheClient;
            _inputCacheTask = new InputCacheTask(this);

            _tolerateInputCacheFailure = ConfigMapUtil.Configuration.GetValueWithCallBack<bool>(SolutionConstants.TolerateInputCacheFailure, UpdateTolerateInputCacheFailure, true);
        }

        public ISubTask<IOEventTaskContext<ARNSingleInputMessage>> CreateSubTask(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            return _inputCacheTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidContextForCache(IOEventTaskContext<ARNSingleInputMessage> ioTaskContext)
        {
            if (ioTaskContext.InputMessage == null)
            {
                return false;
            }

            var resourceId = ioTaskContext.InputMessage.ResourceId;
            var data = ioTaskContext.InputMessage.SerializedData;
            return !string.IsNullOrEmpty(resourceId) && data?.ToMemory().Length > 0;
        }

        public void Dispose()
        {
        }

        private Task UpdateTolerateInputCacheFailure(bool newValue)
        {
            var oldValue = _tolerateInputCacheFailure;
            if (oldValue != newValue)
            {
                lock (_updateLock)
                {
                    _tolerateInputCacheFailure = newValue;
                }
                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    SolutionConstants.TolerateInputCacheFailure, oldValue, newValue);
            }
            return Task.CompletedTask;
        }

        private static ValueTask SetNextChannelAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            var ioTaskContext = eventTaskContext.TaskContext;
            if (!IOEventTaskFlagHelper.IsDependentResource(ioTaskContext.IOEventTaskFlags))
            {
                // Set to PartnerChannel
                return SolutionInputOutputService.SetNextChannelToPartnerChannelAsync(eventTaskContext);
            }
            else
            {
                // For DependentResource, there remains no other action
                ioTaskContext.TaskSuccess(Stopwatch.GetTimestamp());
                return ValueTask.CompletedTask;
            }
        }

        private class InputCacheTask : ISubTask<IOEventTaskContext<ARNSingleInputMessage>>
        {
            private static readonly ActivityMonitorFactory InputCacheTaskProcessEventTaskContextAsync =
                new ("InputCacheTask.ProcessEventTaskContextAsync");

            public bool UseValueTask => false;

            private readonly InputCacheTaskFactory _inputCacheTaskFactory;
            private readonly IResourceCacheClient _cacheClient;

            public InputCacheTask(InputCacheTaskFactory inputCacheTaskFactory)
            {
                _inputCacheTaskFactory = inputCacheTaskFactory;
                _cacheClient = inputCacheTaskFactory._cacheClient;
            }

            public async Task ProcessEventTaskContextAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                var ioTaskContext = eventTaskContext.TaskContext;

                if (!_cacheClient.CacheEnabled ||
                    !IsValidContextForCache(ioTaskContext))
                {
                    eventTaskContext.EventTaskActivity.SetTag(SolutionConstants.InvalidInInputCache, true);
                    await SetNextChannelAsync(eventTaskContext).ConfigureAwait(false);
                    return;
                }

                using var monitor = InputCacheTaskProcessEventTaskContextAsync.ToMonitor();
                monitor.OnStart();

                Exception cacheClientException = null;

                var taskActivity = eventTaskContext.EventTaskActivity;
                var resourceId = ioTaskContext.InputMessage.ResourceId;
                var resourceType = ioTaskContext.InputMessage.ResourceType;
                var data = ioTaskContext.InputMessage.SerializedData;
                var tenantId = ioTaskContext.InputMessage.TenantId; // could be null for global notification
                var notificationTime = ioTaskContext.InputMessage.EventTime != default ? ioTaskContext.InputMessage.EventTime : DateTimeOffset.UtcNow;
                var cacheValue = data.ToMemory();
                var cacheTTL = _inputCacheTaskFactory._cacheClient.CacheTTLManager.GetCacheTTL(resourceType: resourceType, inputType: true);
                var hasTTL = cacheTTL != TimeSpan.Zero;

                try
                {
                    // Send InputMessage to Cache
                    var cacheResult = await _inputCacheTaskFactory._cacheClient.SetResourceIfGreaterThanAsync(
                        resourceId: resourceId,
                        tenantId: tenantId,
                        dataFormat: ResourceCacheDataFormat.ARN,
                        resource: cacheValue,
                        timeStamp: notificationTime.ToUnixTimeMilliseconds(),
                        etag: null,
                        expiry: !hasTTL ? null : cacheTTL,
                        eventTaskContext.TaskCancellationToken).ConfigureAwait(false);

                    eventTaskContext.EventTaskActivity.SetTag(SolutionConstants.AddedToInputCache, cacheResult);

                    eventTaskContext.TaskContext.IOEventTaskFlags |= IOEventTaskFlag.SuccessInputCacheWrite;

                    TagList tagList = default;
                    tagList.Add(SolutionConstants.InputResourceId, resourceId);
                    tagList.Add(SolutionConstants.TenantId, tenantId);
                    tagList.Add(SolutionConstants.Success, cacheResult);

                    taskActivity.AddEvent(InputOutputConstants.EventName_InputCacheInsert, tagList);

                    monitor.OnCompleted();
                    await SetNextChannelAsync(eventTaskContext).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    cacheClientException = ex;
                }

                if (cacheClientException != null)
                {
                    eventTaskContext.EventTaskActivity.SetTag(SolutionConstants.HasInputCacheException, true);
                    monitor.OnError(cacheClientException);

                    TagList tagList = default;
                    tagList.Add(SolutionConstants.InputResourceId, resourceId);
                    tagList.Add(SolutionConstants.TenantId, tenantId);
                    taskActivity.AddEvent(InputOutputConstants.EventName_FailedInputCacheInsert, tagList);

                    if (!_inputCacheTaskFactory._tolerateInputCacheFailure)
                    {
                        // Cache Fail is not tolerated
                        // Let's retry the input task
                        throw cacheClientException;
                    }

                    // Cache Fail is tolerated
                    // Let's move to the next channel
                    // If _retryInputCacheFailure is set, let's create separate tasks for cache retry

                    var cacheClientWriteException = cacheClientException as CacheClientWriteException<bool>;
                    if (cacheClientWriteException != null)
                    {
                        eventTaskContext.EventTaskActivity.SetTag(SolutionConstants.HasCacheClientWriteException, true);
                        // Handle partial write exception
                        // TODO in next PR
                    }
                }

                await SetNextChannelAsync(eventTaskContext).ConfigureAwait(false);
                return;
            }

            public ValueTask ProcessEventTaskContextValueAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
