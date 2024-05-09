namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Boost.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.PartnerChannel.SubTasks;
    using Newtonsoft.Json;

    public class PartnerChannelRoutingManager : IPartnerChannelRoutingManager
    {
        private static readonly ActivityMonitorFactory PartnerChannelRoutingManagerSetNextChannelAsync =
         new("PartnerChannelRoutingManager.SetNextChannelAsync");

        private static readonly ActivityMonitorFactory PartnerChannelRoutingManagerCreateChildTasksAndSendToPartnerChannel =
           new("PartnerChannelRoutingManager.createChildTasksAndSendToPartnerChannel");

        private static readonly ResourceTypeNotOnboardedException resourceTypeNotOnboardedException = new ("Resource type is not onboarded");

        private static readonly ILogger<PartnerChannelRoutingManager> Logger = DataLabLoggerFactory.CreateLogger<PartnerChannelRoutingManager>();

        public Dictionary<string, IPartnerChannelManager<ARNSingleInputMessage>> PartnerChannelsDictionary { get; }
        public Dictionary<string, ConfigurableConcurrencyManager> PartnerChannelsConcurrencyDictionary { get; }
        public Dictionary<string, List<IPartnerChannelManager<ARNSingleInputMessage>>> ResourceTypePartnerChannelsDictionary { get; }
        public Dictionary<string, List<IPartnerChannelManager<ARNSingleInputMessage>>> EventTypePartnerChannelsDictionary { get; }
        public Dictionary<string, List<IPartnerChannelManager<ARNSingleInputMessage>>> WildCardResourceTypePartnerChannelsDictionary { get; }
        public Dictionary<string, List<IPartnerChannelManager<ARNSingleInputMessage>>> CachedResourceTypePartnerChannelsDictionary { get; }

        private List<IPartnerChannelManager<ARNSingleInputMessage>> _allTypeMatchChannels;
        private IPartnerChannelManager<ARNSingleInputMessage> _singlePartnerChannel;
        private bool _hasWildCardResourceTypeSpecified;
        private bool _hasAllResourcesSpecified;
        private bool _hasEventTypesSpecified;

        private readonly object lockObj = new object();

        private static ISubTaskFactory<IOEventTaskContext<ARNSingleInputMessage>> testPartnerDispatcherTaskFactory;
        private static bool unitTestMode;

        private static readonly StringSplitOptions trimRemoveEmptyOptions = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;

        public PartnerChannelRoutingManager()
        {
            // All Dictionary is case-insensitive key */
            PartnerChannelsDictionary = new(StringComparer.OrdinalIgnoreCase);
            PartnerChannelsConcurrencyDictionary = new(StringComparer.OrdinalIgnoreCase);
            ResourceTypePartnerChannelsDictionary = new(StringComparer.OrdinalIgnoreCase);
            EventTypePartnerChannelsDictionary = new(StringComparer.OrdinalIgnoreCase);
            WildCardResourceTypePartnerChannelsDictionary = new(StringComparer.OrdinalIgnoreCase);
            CachedResourceTypePartnerChannelsDictionary = new(StringComparer.OrdinalIgnoreCase);
            _allTypeMatchChannels = new();

            var partnerSingleResponseResourcesRoutingConfigs =
                ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.PartnerSingleResponseResourcesRouting)?.ConvertToList(stringSplitOptions: trimRemoveEmptyOptions);

            var partnerMultiResponseResourcesRoutingConfigs =
                ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.PartnerMultiResponseResourcesRouting)?.ConvertToList(stringSplitOptions: trimRemoveEmptyOptions);

            var partnerChannelsConcurrencyConfigs =
                ConfigMapUtil.Configuration.GetValueWithCallBack<string>(InputOutputConstants.PartnerChannelConcurrency, UpdateConcurrencyAsync, null)?.ConvertToList(stringSplitOptions: trimRemoveEmptyOptions);

            if (partnerChannelsConcurrencyConfigs?.Count > 0)
            {
                CreatePartnerChannelConcurrencyManagers(partnerChannelsConcurrencyConfigs);
            }

            if (partnerSingleResponseResourcesRoutingConfigs?.Count > 0)
            {
                CreatePartnerChannels(partnerSingleResponseResourcesRoutingConfigs);
            }

            if (partnerMultiResponseResourcesRoutingConfigs?.Count > 0)
            {
                CreatePartnerChannels(partnerMultiResponseResourcesRoutingConfigs, true);
            }

            if (ResourceTypePartnerChannelsDictionary.Count + WildCardResourceTypePartnerChannelsDictionary.Count + _allTypeMatchChannels.Count + EventTypePartnerChannelsDictionary.Count == 0)
            {
                throw new ArgumentException("Resource Types are not available in the configuration");
            }

            if (_allTypeMatchChannels.Count > 0)
            {
                // Just in case (like not-well prepared local configuration file), let's distinct it
                _allTypeMatchChannels = new HashSet<IPartnerChannelManager<ARNSingleInputMessage>>(_allTypeMatchChannels).ToList();
            }

            // All possible configuration (not supported) error situation

            // Single Partner Channel checking
            if (PartnerChannelsDictionary.Count == 1)
            {
                if (!_hasAllResourcesSpecified)
                {
                    // At this time, we don't support single Partner service with explicit resource Types
                    // If we want to filter some resource Types, we have to use TrafficTunner (instead of using explict resource Type in Partner Channel config)
                    throw new ArgumentException("Single Partner Channel should have all resource wild card(*)");
                }
                else
                {
                    _singlePartnerChannel = PartnerChannelsDictionary.Values.First();
                }
            }
        }

        private void CreatePartnerChannelConcurrencyManagers(List<string> partnerChannelsConcurrencyConfigs)
        {
            if (partnerChannelsConcurrencyConfigs == null || partnerChannelsConcurrencyConfigs.Count == 0)
            {
                return;
            }

            /*
             * This builds concurrency Map for Partner Channels
             * Key: channelName
             * Value: concurrency in IOService
             * e.g) channelName1:concurrency1;channelName2:concurrency2
             */
            foreach (var config in partnerChannelsConcurrencyConfigs)
            {
                var concurrencyConfig = config.Split(':', trimRemoveEmptyOptions);
                var channelName = concurrencyConfig[0];
                var concurrency = int.Parse(concurrencyConfig[1]);
                var partnerChannelConcurrencyManager = new ConfigurableConcurrencyManager($"{channelName}-concurrency", concurrency, false);
                PartnerChannelsConcurrencyDictionary.Add(channelName, partnerChannelConcurrencyManager);
            }
        }

        private void CreatePartnerChannels(
           List<string> partnerResourcesRoutingConfigs,
           bool useMultiResponses = false)
        {
            /*
             * This builds  ResourceTypePartnerChannelsDictionary
             * But resourceType could contain wildCard
             * e.g.) { "resourceTypes": "type1,type2", "eventTypes": "type3/write,type4/write|delete", partnerChannelAddress: "partnerAddr", "PartnerChannelName":"channelName"}
             * channelName consists of serviceName-containerPort
             */
            foreach (var config in partnerResourcesRoutingConfigs)
            {
                var partnerResourcesRoutingConfig = JsonConvert.DeserializeObject<PartnerResourcesRoutingConfig>(config);
                GuardHelper.ArgumentNotNullOrEmpty(partnerResourcesRoutingConfig.PartnerChannelName);
                GuardHelper.ArgumentNotNullOrEmpty(partnerResourcesRoutingConfig.PartnerChannelAddress);

                foreach (var resourceType in partnerResourcesRoutingConfig.ResourceTypes?.Split(',', trimRemoveEmptyOptions) ?? Array.Empty<string>())
                {
                    var isAllTypeMatch = resourceType.Equals("*");
                    _hasAllResourcesSpecified |= isAllTypeMatch;

                    // skip if only "*" is specified
                    var isWildCardSpecified = !isAllTypeMatch && resourceType.EndsWith('*');
                    _hasWildCardResourceTypeSpecified |= isWildCardSpecified;

                    var partnerChannelManager = GetPartnerChannelManager(partnerAddr: partnerResourcesRoutingConfig.PartnerChannelAddress, channelName: partnerResourcesRoutingConfig.PartnerChannelName, useMultiResponses: useMultiResponses);

                    if (isAllTypeMatch)
                    {
                        _allTypeMatchChannels.Add(partnerChannelManager);
                        continue;
                    }

                    // To handle the case efficiently where there are many resource types in configMap
                    // Let's create separate map for exact resource type matching and wildCard resource type
                    // So later we can just iterate the wildCard resource type with string startWith. 
                    // For exact resourcetype map, we just need to lookup instead of string startWith

                    if (isWildCardSpecified)
                    {
                        // At this time, we only support trailing wildCard
                        // Let's build Map with key without the trailing wildCard
                        var mapKey = resourceType.TrimEnd('*');

                        if (!WildCardResourceTypePartnerChannelsDictionary.TryGetValue(mapKey, out var partnerChannelManagerList))
                        {
                            partnerChannelManagerList = new List<IPartnerChannelManager<ARNSingleInputMessage>>(2);
                            WildCardResourceTypePartnerChannelsDictionary.Add(mapKey, partnerChannelManagerList);
                        }
                        partnerChannelManagerList.Add(partnerChannelManager);
                    }
                    else
                    {
                        if (!ResourceTypePartnerChannelsDictionary.TryGetValue(resourceType, out var partnerChannelManagerList))
                        {
                            partnerChannelManagerList = new List<IPartnerChannelManager<ARNSingleInputMessage>>(2);
                            ResourceTypePartnerChannelsDictionary.Add(resourceType, partnerChannelManagerList);
                        }
                        partnerChannelManagerList.Add(partnerChannelManager);
                    }
                }

                foreach(var eventType in partnerResourcesRoutingConfig.EventTypes?.Split(',', trimRemoveEmptyOptions) ??Array.Empty<string>())
                {

                    var partnerChannelManager = GetPartnerChannelManager(partnerAddr: partnerResourcesRoutingConfig.PartnerChannelAddress, channelName: partnerResourcesRoutingConfig.PartnerChannelName, useMultiResponses: useMultiResponses);
                    var actions = ArmUtils.GetAction(eventType);
                    if (actions == null)
                    {
                        throw new ArgumentException("Invalid event type specified in the configuration");
                    }
                    int lastIndexOfActions = eventType.LastIndexOf(actions, StringComparison.OrdinalIgnoreCase);
                    // Get actions for "eventTypes": "resourceType/write|delete"
                    var eventActions = actions.Split('|', trimRemoveEmptyOptions);
                    var resourceType = eventType.Substring(0, lastIndexOfActions);
                    eventActions.ForEach(action =>
                    {
                        var eventTypeWithSingleAction = resourceType + action;
                        if (!EventTypePartnerChannelsDictionary.TryGetValue(eventTypeWithSingleAction, out var partnerChannelManagerList))
                        {
                            partnerChannelManagerList = new List<IPartnerChannelManager<ARNSingleInputMessage>>();
                            EventTypePartnerChannelsDictionary.Add(eventTypeWithSingleAction, partnerChannelManagerList);
                        }
                        partnerChannelManagerList.Add(partnerChannelManager);
                    });
                    _hasEventTypesSpecified = true;
                }
            }
        }

        private IPartnerChannelManager<ARNSingleInputMessage> GetPartnerChannelManager(string partnerAddr, string channelName, bool useMultiResponses)
        {
            var channelMapKey = $"{channelName}-{(useMultiResponses ? "multi" : "single")}";

            if (!PartnerChannelsDictionary.TryGetValue(channelMapKey, out var partnerChannelManager))
            {
                partnerChannelManager = new PartnerChannelManager<ARNSingleInputMessage>(channelMapKey);

                if (!unitTestMode)
                {
                    partnerChannelManager.AddSubTaskFactory(new PartnerDispatcherTaskFactory(partnerAddr, useMultiResponses));
                }
                else
                {
                    partnerChannelManager.AddSubTaskFactory(testPartnerDispatcherTaskFactory);
                }
                PartnerChannelsDictionary.Add(channelMapKey, partnerChannelManager);

                // For now, we will use same concurrency for both multi/single response
                // So we expect channelName appears in the PartnerChannelConcurrency's key
                if (PartnerChannelsConcurrencyDictionary.TryGetValue(channelName, out var partnerChannelConcurrencyManager))
                {
                    partnerChannelConcurrencyManager.RegisterObject(partnerChannelManager.SetExternalConcurrencyManager);
                }
            }
            return partnerChannelManager;
        }

        private List<IPartnerChannelManager<ARNSingleInputMessage>> GetCachedPartnerChannelsForWildCardTypes(string resourceType)
        {
            lock (lockObj)
            {
                // First check if cached type already exists
                if (CachedResourceTypePartnerChannelsDictionary.TryGetValue(resourceType, out var partnerChannelList))
                {
                    return partnerChannelList;
                }

                // Now cache doesn't have this type. Let's buid cacheEntry 
                partnerChannelList = new List<IPartnerChannelManager<ARNSingleInputMessage>>(2);

                // Let's lookup Non wildcard Map
                if (ResourceTypePartnerChannelsDictionary.TryGetValue(resourceType, out var resourceTypePartnerChannels))
                {
                    partnerChannelList.AddRange(resourceTypePartnerChannels);
                }

                // Let's lookup wildcard Map
                if (WildCardResourceTypePartnerChannelsDictionary.Count > 0)
                {
                    var hasWildCardMatched = false;
                    foreach (var kvp in WildCardResourceTypePartnerChannelsDictionary)
                    {
                        //Handle wildcard case
                        if (resourceType.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            partnerChannelList.AddRange(kvp.Value);
                            hasWildCardMatched = true;
                        }
                    }

                    // Due to wild card, some channel might be added multiple (one matched with resource type, one mathed with wild card) in some not-well prepared configuration file
                    // Just in case, let's distinct it here (one time)
                    if (hasWildCardMatched && partnerChannelList.Count > 1)
                    {
                        partnerChannelList = new HashSet<IPartnerChannelManager<ARNSingleInputMessage>>(partnerChannelList).ToList();
                    }
                }

                // PartnerChannelList migth be empty when nothing matches.
                // we will still add the empty list so that line 242 will return the empty list
                // Which indicates we already did checking for the resource type. we don't need to do it again

                CachedResourceTypePartnerChannelsDictionary.Add(resourceType, partnerChannelList);
                return partnerChannelList;
            }
        }

        private async Task UpdateConcurrencyAsync(string newPartnerChannelsConcurrencyConfig)
        {
            List<Task> tasks = new();
            foreach (var config in newPartnerChannelsConcurrencyConfig.ConvertToList(stringSplitOptions: trimRemoveEmptyOptions))
            {
                var concurrencyConfig = config.Split(':', trimRemoveEmptyOptions);
                var channelName = concurrencyConfig[0];
                var concurrency = int.Parse(concurrencyConfig[1]);

                if (PartnerChannelsConcurrencyDictionary.TryGetValue(channelName, out var concurrencyManager))
                {
                    tasks.Add(concurrencyManager.UpdateConcurrencyAsync(concurrency));
                }
                else
                {
                    Logger.LogError($"{concurrencyConfig[0]} concurrency manager doesn't exists");
                }
            }
            await Task.WhenAll(tasks);
        }

        private async Task CreateChildTasksAndSendToPartnerChannel(
            IPartnerChannelManager<ARNSingleInputMessage> partnerChannel,
            IOEventTaskContext<ARNSingleInputMessage> parentEventTaskContext,
            PartnerRoutingChildEventTaskCallBack childEventTaskCallBack,
            int childTaskId)
        {
            using var childMonitor = PartnerChannelRoutingManagerCreateChildTasksAndSendToPartnerChannel.ToMonitor();
            {
                try
                {
                    childMonitor.OnStart(false);

                    var childEventTaskContext = new IOEventTaskContext<ARNSingleInputMessage>(
                        eventTaskType: InputOutputConstants.PartnerInputChildEventTask,
                        dataSourceType: parentEventTaskContext.DataSourceType,
                        dataSourceName: parentEventTaskContext.DataSourceName,
                        firstEnqueuedTime: parentEventTaskContext.FirstEnqueuedTime,
                        firstPickedUpTime: parentEventTaskContext.FirstPickedUpTime,
                        dataEnqueuedTime: parentEventTaskContext.DataEnqueuedTime,
                        eventTime: parentEventTaskContext.EventTime,
                        inputMessage: parentEventTaskContext.InputMessage,
                        eventTaskCallBack: childEventTaskCallBack,
                        retryCount: parentEventTaskContext.RetryCount,
                        retryStrategy: SolutionInputOutputService.RetryStrategy,
                        parentActivityContext: parentEventTaskContext.EventTaskActivity.Context,
                        topActivityStartTime: parentEventTaskContext.EventTaskActivity.TopActivityStartTime,
                        createNewTraceId: true,
                        regionConfigData: parentEventTaskContext.RegionConfigData,
                        parentCancellationToken:  parentEventTaskContext.TaskCancellationToken,
                        retryChannelManager: SolutionInputOutputService.ARNMessageChannels.RetryChannelManager,
                        poisonChannelManager: SolutionInputOutputService.ARNMessageChannels.PoisonChannelManager,
                        finalChannelManager: SolutionInputOutputService.ARNMessageChannels.FinalChannelManager,
                        globalConcurrencyManager: SolutionInputOutputService.GlobalConcurrencyManager);

                    childEventTaskCallBack.IncreaseChildEventCount();

                    var childTaskActivity = childEventTaskContext.EventTaskActivity;
                    childTaskActivity.SetTag(InputOutputConstants.ChildTaskId, childTaskId);

                    var parentTraceId = parentEventTaskContext.EventTaskActivity.TraceId;
                    var childTraceId = childTaskActivity.TraceId;

                    childMonitor.Activity[InputOutputConstants.ChildTaskId] = childTaskId;
                    childMonitor.Activity[InputOutputConstants.ChildTaskTraceId] = childTraceId;
                    childMonitor.Activity[SolutionConstants.ParentTraceId] = parentTraceId;

                    parentEventTaskContext.EventTaskActivity.AddChildTraceId(childTraceId);

                    childMonitor.Activity[SolutionConstants.NextChannel] = partnerChannel.ChannelName;

                    // Start Child Task
                    childEventTaskContext.SetTaskTimeout(parentEventTaskContext.TaskTimeout);
                    await childEventTaskContext.StartEventTaskAsync(partnerChannel, false, childMonitor.Activity);
                    childMonitor.OnCompleted();
                }
                catch (Exception childException)
                {
                    childMonitor.OnError(childException);
                    throw;
                }
            }
        }

        public async ValueTask SetNextChannelAsync(AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>> eventTaskContext)
        {
            var ioTaskContext = eventTaskContext.TaskContext;
            var taskActivity = ioTaskContext.EventTaskActivity;

            (TrafficTunerResult result, TrafficTunerNotAllowedReason reason) trafficTunerResult;
            if (RegionConfigManager.IsBackupRegionPairName(ioTaskContext.RegionConfigData.RegionLocationName))
            {
                trafficTunerResult = SolutionInputOutputService.BackupEventhubsTrafficTuner.PartnerTrafficTuner.EvaluateTunerResult(
                ioTaskContext.InputMessage, ioTaskContext.RetryCount);
            }
            else
            {
                trafficTunerResult = SolutionInputOutputService.InputEventhubsTrafficTuner.PartnerTrafficTuner.EvaluateTunerResult(
                ioTaskContext.InputMessage, ioTaskContext.RetryCount);
            }

            if (trafficTunerResult.result != TrafficTunerResult.Allowed)
            {
                taskActivity.SetTag(SolutionConstants.TaskFiltered, true);
                taskActivity.SetTag(SolutionConstants.PartnerTrafficTunerResult, trafficTunerResult.reason.FastEnumToString());
                ioTaskContext.TaskDrop(DropReason.TaskFiltered.FastEnumToString(), "PartnerTrafficTuner", IOComponent.PartnerChannel.FastEnumToString());
                return;
            }

            // Shortcut for Single Partner Service scenario
            if (_singlePartnerChannel != null)
            {
                eventTaskContext.SetNextChannel(_singlePartnerChannel);
                return;
            }

            using var parentMonitor = PartnerChannelRoutingManagerSetNextChannelAsync.ToMonitor();
            parentMonitor.OnStart(false);

            var resourceType = eventTaskContext.TaskContext.InputMessage.ResourceType;
            parentMonitor.Activity[SolutionConstants.ResourceType] = resourceType;
            var eventType = eventTaskContext.TaskContext.InputMessage.EventType;
            parentMonitor.Activity[SolutionConstants.EventType] = eventType;
            var action = ArmUtils.GetAction(eventType);
            parentMonitor.Activity[SolutionConstants.Action] = action;

            IReadOnlyList<IPartnerChannelManager<ARNSingleInputMessage>> resourceTypePartnerChannelList = null;
            if (_hasWildCardResourceTypeSpecified)
            {
                // cache partner channel for each resource type
                // any operation cacheResourceType should be called with lock because multiple thread might add and it will modify internal data structure
                // even ContainsKey might throw concurrent modification exception because data structures contains key is also growing(modified) when new key is added
                resourceTypePartnerChannelList = GetCachedPartnerChannelsForWildCardTypes(resourceType);
            }
            else
            {
                // Let's lookup Non wildcard Map
                if (ResourceTypePartnerChannelsDictionary.TryGetValue(resourceType, out var resourceTypePartnerChannels))
                {
                    resourceTypePartnerChannelList = resourceTypePartnerChannels;
                }
            }

            // Let's lookup event type
            IReadOnlyList<IPartnerChannelManager<ARNSingleInputMessage>> eventTypePartnerChannelList = null;
            if (_hasEventTypesSpecified && EventTypePartnerChannelsDictionary.TryGetValue(eventType, out var eventTypePartnerChannels))
            {
                eventTypePartnerChannelList = eventTypePartnerChannels;
            }

            // Check if nothing matches
            var hasAllTypeMatch = _allTypeMatchChannels?.Count > 0;
            var hasResourceTypeMatch = resourceTypePartnerChannelList?.Count > 0;
            var hasEventTypeMatch = eventTypePartnerChannelList?.Count > 0;

            if (!hasAllTypeMatch && !hasResourceTypeMatch && !hasEventTypeMatch)
            {
                taskActivity.SetTag(SolutionConstants.TaskFiltered, true);
                taskActivity.SetTag(SolutionConstants.ResourceType, resourceType);
                taskActivity.SetTag(SolutionConstants.Action, action);
                ioTaskContext.TaskDrop(DropReason.TaskFiltered.FastEnumToString(), "No Matched ResourceType in PartnerChannel", IOComponent.PartnerRoutingManager.FastEnumToString());
                parentMonitor.OnError(resourceTypeNotOnboardedException);
                return;
            }

            // Now Let's send to list of partner Channels
            IReadOnlyList<IPartnerChannelManager<ARNSingleInputMessage>> partnerChannelList = null;
            if (hasResourceTypeMatch && hasEventTypeMatch)
            {
                // get distinct channels if resources are misconfigured for both resource types and event types in configuration file
                var hashSet = new HashSet<IPartnerChannelManager<ARNSingleInputMessage>>(resourceTypePartnerChannelList);
                hashSet.UnionWith(eventTypePartnerChannelList);
                partnerChannelList = hashSet.ToList();
            }
            else if (hasResourceTypeMatch)
            {
                partnerChannelList = resourceTypePartnerChannelList;
            }
            else if (hasEventTypeMatch)
            {
                partnerChannelList = eventTypePartnerChannelList;
            }

            int childTaskId = 0;
            var childEventTaskCallBack = new PartnerRoutingChildEventTaskCallBack(ioTaskContext);

            try
            {
                childEventTaskCallBack.StartAddChildEvent();

                if (partnerChannelList?.Count > 0)
                {
                    for (int i = 0; i < partnerChannelList.Count; i++)
                    {
                        ++childTaskId; // 1-Based Id
                        var partnerChannel = partnerChannelList[i];
                        await CreateChildTasksAndSendToPartnerChannel(partnerChannel, ioTaskContext, childEventTaskCallBack, childTaskId);
                    }
                }

                if (_allTypeMatchChannels?.Count > 0)
                {
                    for (int i = 0; i < _allTypeMatchChannels.Count; i++)
                    {
                        ++childTaskId; // 1-Based Id
                        var partnerChannel = _allTypeMatchChannels[i];
                        await CreateChildTasksAndSendToPartnerChannel(partnerChannel, ioTaskContext, childEventTaskCallBack, childTaskId);
                    }
                }

                taskActivity.AddEvent($"In multi-partnerservices routing, Total {childTaskId} created");

                childEventTaskCallBack.FinishAddChildEvent();

                parentMonitor.OnCompleted();
            }
            catch (Exception ex)
            {
                taskActivity.AddEvent($"In multi-partnerservices routing, Total {childTaskId} created and threw exception");

                childEventTaskCallBack.CancelAllChildTasks("ChildTask CreationAndStart Failed", null);

                parentMonitor.OnError(ex);
                throw;
            }
        }

        public void Dispose()
        {
            PartnerChannelsDictionary.Values.ForEach(partnerChannels => partnerChannels.Dispose());
            PartnerChannelsConcurrencyDictionary.Values.ForEach(concurrencyManager => concurrencyManager.Dispose());
        }

        /// <summary>
        /// Used only for Unit Tests
        /// </summary>
        /// <param name="_partnerDispatcherTaskFactory"></param>
        public static void SetUnitTestsProperties(ISubTaskFactory<IOEventTaskContext<ARNSingleInputMessage>> _testPartnerDispatcherTaskFactory)
        {
            testPartnerDispatcherTaskFactory = _testPartnerDispatcherTaskFactory;
            unitTestMode = true;
        }
    }
}