namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.InputChannel.SubTasks
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;

    internal class InputResourceCheckTaskFactory : ISubTaskFactory<IOEventTaskContext<ARNSingleInputMessage>>
    {
        private static readonly ILogger<InputResourceCheckTaskFactory> Logger = 
            DataLabLoggerFactory.CreateLogger<InputResourceCheckTaskFactory>();

        public string SubTaskName => "InputResourceCheck";
        public bool CanContinueToNextTaskOnException => false;

        private readonly InputResourceCheckTask _inputResourceCheckTask; // singleton
        private static HashSet<string> _dependentResourceTypes;
        private static HashSet<string> _inputResourceCacheTypes;

        static InputResourceCheckTaskFactory()
        {
            _dependentResourceTypes = ConfigMapUtil.Configuration
                .GetValueWithCallBack<string>(InputOutputConstants.DependentResourceTypes, UpdateDependentResourceTypes, null)
                .ConvertToSet(false);

            _inputResourceCacheTypes = ConfigMapUtil.Configuration
                .GetValueWithCallBack<string>(InputOutputConstants.InputCacheTypes, UpdateInputResourceCacheTypes, null)
                .ConvertToSet(false);
        }

        public InputResourceCheckTaskFactory()
        {
            _inputResourceCheckTask = new InputResourceCheckTask();
        }

        public ISubTask<IOEventTaskContext<ARNSingleInputMessage>> CreateSubTask(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            return _inputResourceCheckTask;
        }

        private class InputResourceCheckTask : ISubTask<IOEventTaskContext<ARNSingleInputMessage>>
        {
            public bool UseValueTask => true;

            public Task ProcessEventTaskContextAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                throw new System.NotImplementedException();
            }

            public ValueTask ProcessEventTaskContextValueAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
            {
                var ioTaskContext = eventTaskContext.TaskContext;
                var taskActivity = ioTaskContext.EventTaskActivity;

                (TrafficTunerResult result, TrafficTunerNotAllowedReason reason) trafficTunerResult;
                if (RegionConfigManager.IsBackupRegionPairName(ioTaskContext.RegionConfigData.RegionLocationName))
                {
                    trafficTunerResult = SolutionInputOutputService.BackupEventhubsTrafficTuner.InputTrafficTuner.EvaluateTunerResult(ioTaskContext.InputMessage, ioTaskContext.RetryCount);
                }
                else
                {
                   trafficTunerResult = SolutionInputOutputService.InputEventhubsTrafficTuner.InputTrafficTuner.EvaluateTunerResult(ioTaskContext.InputMessage, ioTaskContext.RetryCount);
                }

                if (trafficTunerResult.result != TrafficTunerResult.Allowed)
                {
                    // Mostly input filter will be applied early stage but here we do one more filtering
                    taskActivity.SetTag(SolutionConstants.TaskFiltered, true);
                    taskActivity.SetTag(SolutionConstants.TrafficTunerResult, trafficTunerResult.reason.FastEnumToString());
                    ioTaskContext.TaskDrop(DropReason.TaskFiltered.FastEnumToString(), "TrafficTuner", IOComponent.InputChannel.FastEnumToString());
                    return ValueTask.CompletedTask;
                }

                AddEventTaskInputFlags(ioTaskContext);
                
                if (IOEventTaskFlagHelper.NeedAddToInputCache(ioTaskContext.IOEventTaskFlags))
                {
                    // Set To InputCacheChannel
                    SolutionInputOutputService.SetNextChannelToInputCacheChannel(eventTaskContext);
                }else
                {
                    // Set to PartnerChannel
                    return SolutionInputOutputService.SetNextChannelToPartnerChannelAsync(eventTaskContext);
                }
                return ValueTask.CompletedTask;
            }
        }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddEventTaskInputFlags(IOEventTaskContext<ARNSingleInputMessage> eventTaskContext)
        {
            var dependentResourceTypes = _dependentResourceTypes;
            var isDepedentResource = false;
            if (dependentResourceTypes?.Count > 0)
            {
                var resourceType = eventTaskContext.InputMessage?.ResourceType;
                if (resourceType != null && dependentResourceTypes.Contains(resourceType))
                {
                    isDepedentResource = true;
                    eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.DependentResource;
                    eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.NeedToAddInputCache;
                }
            }

            if (!isDepedentResource)
            {
                // check inputResourceCacheTypes
                var inputResourceCacheTypes = _inputResourceCacheTypes;
                if (inputResourceCacheTypes?.Count > 0)
                {
                    var resourceType = eventTaskContext.InputMessage?.ResourceType;
                    if (resourceType != null && inputResourceCacheTypes.Contains(resourceType))
                    {
                        eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.NeedToAddInputCache;
                    }
                }
            }
        }

        private static Task UpdateDependentResourceTypes(string newValue)
        {
            var oldDependentTypes = _dependentResourceTypes;
            var newDependentTypes = newValue.ConvertToSet(false);

            if (Interlocked.CompareExchange(ref _dependentResourceTypes, newDependentTypes, oldDependentTypes) == oldDependentTypes)
            {
                Logger.LogWarning("DependentResourceTypes is changed, Old: {oldVal}, New: {newVal}",
                    oldDependentTypes.ToString(), newDependentTypes.ToString());
            }
            return Task.CompletedTask;
        }

        private static Task UpdateInputResourceCacheTypes(string newValue)
        {
            var oldResourceCacheTypes = _inputResourceCacheTypes;
            var newResourceCacheTypes = newValue.ConvertToSet(false);

            if (Interlocked.CompareExchange(ref _inputResourceCacheTypes, newResourceCacheTypes, oldResourceCacheTypes) == oldResourceCacheTypes)
            {
                Logger.LogWarning("InputResourceCacheTypes is changed, Old: {oldVal}, New: {newVal}",
                    oldResourceCacheTypes.ToString(), newResourceCacheTypes.ToString());
            }
            return Task.CompletedTask;
        }
    }
}
