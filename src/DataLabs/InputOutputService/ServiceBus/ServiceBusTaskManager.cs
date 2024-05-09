namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.ServiceBus
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.EventHub;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;

    [ExcludeFromCodeCoverage]
    internal class ServiceBusTaskManager : IServiceBusTaskManager
    {
        private static readonly ILogger<ServiceBusTaskManager> Logger = DataLabLoggerFactory.CreateLogger<ServiceBusTaskManager>();

        private readonly ActivityMonitorFactory ServiceBusTaskManagerCriticalError;
        private readonly ActivityMonitorFactory ServiceBusTaskManagerParseProperties;
        private readonly ActivityMonitorFactory ServiceBusTaskManagerProcessMessageAsync;

        public const string ComponentName = "ServiceBusTaskManager";

        internal string NameSpaceAndQueueName { get; }

        private readonly string _logicalQueueName;
        private readonly string _activityName;
        private readonly string _timeoutConfigName;
        private readonly Counter<long> _readMessageCounter;
        private TimeSpan _taskTimeOut;
        private object _updateLock = new();

        public ServiceBusTaskManager(string nameSpace, string queueName, string timeoutConfigName, TimeSpan initTimeOut, Counter<long> readMessageCounter, string logicalQueueName)
        {
            NameSpaceAndQueueName = nameSpace + '/' + queueName;

            _logicalQueueName = logicalQueueName;
            _activityName = logicalQueueName + nameof(ServiceBusTaskManager);
            ServiceBusTaskManagerCriticalError = new(_activityName + ".CriticalError", LogLevel.Critical);
            ServiceBusTaskManagerParseProperties = new(_activityName + ".ParseProperties");
            ServiceBusTaskManagerProcessMessageAsync = new(_activityName + ".ProcessMessageAsync");

            _timeoutConfigName = timeoutConfigName;
            _readMessageCounter = readMessageCounter;

            _taskTimeOut = ConfigMapUtil.Configuration.GetValueWithCallBack<TimeSpan>(
                timeoutConfigName, UpdateTaskTimeOutDuration, initTimeOut, allowMultiCallBacks: true);
        }

        public async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            if (args.Message == null)
            {
                return;
            }

            DataLabServiceBusProperties dataLabServiceBusProperties;

            try
            {
                _readMessageCounter.Add(1,
                    new KeyValuePair<string, object>(MonitoringConstants.QueueNameDimension, NameSpaceAndQueueName));

                dataLabServiceBusProperties = DataLabServiceBusProperties.Create(args.Message);
            }
            catch (Exception ex)
            {
                // Move to Poison Queue
                using var parseMonitor = ServiceBusTaskManagerParseProperties.ToMonitor(
                    parentActivity: BasicActivity.Null,
                    component: ComponentName);

                parseMonitor.Activity[SolutionConstants.QueueName] = NameSpaceAndQueueName;
                parseMonitor.Activity[SolutionConstants.ServiceBusMessageId] = args.Message.MessageId;

                var poisonReason = "Invalid Format Message";
                var poisonDescription = ex.Message;
                if (poisonDescription?.Length > 128)
                {
                    poisonDescription = poisonDescription[..128];
                }

                parseMonitor.OnError(ex);

                await MoveToDeadLetter(args, args.Message, poisonReason, poisonDescription).ConfigureAwait(false);
                return;
            }

            ActivityContext parentContext = dataLabServiceBusProperties.ActivityId != null ?
                Tracer.ConvertToActivityContext(dataLabServiceBusProperties.ActivityId) : default;

            using var activity = new OpenTelemetryActivityWrapper(
                source: IOServiceOpenTelemetry.IOActivitySource,
                name: _activityName,
                kind: ActivityKind.Consumer,
                parentContext: parentContext,
                createNewTraceId: false,
                topActivityStartTime: dataLabServiceBusProperties.TopActivityStartTime);

            dataLabServiceBusProperties.ToLog(activity);

            activity.ParentDifferentTraceId = dataLabServiceBusProperties.ParentDifferentTraceId;
            activity.SetTag(SolutionConstants.ParentTraceId, dataLabServiceBusProperties.ParentDifferentTraceId);

            // ActivityMonitor should be created after OpenTelemetry Activity so that it will get TraceId
            using var methodMonitor = ServiceBusTaskManagerProcessMessageAsync.ToMonitor(
                parentActivity: BasicActivity.Null,
                component: ComponentName);

            try
            {
                methodMonitor.OnStart(false);

                var inputCorrelationId = dataLabServiceBusProperties.CorrelationId;
                var inputResourceId = dataLabServiceBusProperties.ResourceId;
                var inputEventType = dataLabServiceBusProperties.EventType;

                activity.InputCorrelationId = inputCorrelationId;
                activity.InputResourceId = inputResourceId;
                activity.EventType = inputEventType;

                methodMonitor.Activity.CorrelationId = inputCorrelationId;
                methodMonitor.Activity.InputResourceId = inputResourceId;

                var hasInput = dataLabServiceBusProperties.HasInput ?? true;
                var hasOutput = dataLabServiceBusProperties.HasOutput ?? false;
                var hadSourceOfTruthConflict = dataLabServiceBusProperties.HasSourceOfTruthConflict ?? false;
                var hadSuccessInputCacheWrite = dataLabServiceBusProperties.HasSuccessInputCacheWrite ?? false;

                activity.SetTag(SolutionConstants.QueueName, NameSpaceAndQueueName);
                activity.SetTag(SolutionConstants.ServiceBusMessageId, args.Message.MessageId);
                activity.SetTag(SolutionConstants.DataEnqueuedTime, args.Message.EnqueuedTime);

                activity.SetTag(SolutionConstants.ServiceBusSequenceNumber, args.Message.SequenceNumber);
                activity.SetTag(SolutionConstants.DeliveryCount, args.Message.DeliveryCount);
                activity.SetTag(SolutionConstants.LockedUntil, args.Message.LockedUntil);

                activity.SetTag(InputOutputConstants.ServiceBusHasInputTag, hasInput);
                activity.SetTag(InputOutputConstants.ServiceBusHasOutputTag, hasOutput);
                activity.SetTag(InputOutputConstants.RetrySourceOfTruthConflictTag, hadSourceOfTruthConflict);
                activity.SetTag(InputOutputConstants.RetrySuccessInputCacheWriteTag, hadSuccessInputCacheWrite);

                var message = args.Message;

                // First check if this message need to go to Poison Queue
                if (!hasInput && !hasOutput)
                {
                    // Wrong format
                    // Explicitly input and output is set to false
                    // Move to Poison Queue
                    await MoveToDeadLetter(args, message, "NoInputOutputInRetry", null).ConfigureAwait(false);
                    activity.SetTag("MovedToDeadLetter", true);
                    activity.SetStatus(ActivityStatusCode.Error, "NoInputOutputInRetry");

                    activity.ExportToActivityMonitor(methodMonitor.Activity);
                    methodMonitor.OnError(new Exception("No input or output in retry message"));
                    return;
                }

                var hasDeadMark = message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_DeadLetterMark, out object deadMarkVal) && deadMarkVal is bool;
                if (hasDeadMark && (bool)deadMarkVal)
                {
                    string reason = null;
                    string description = null;

                    if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_FailedReason, out object value))
                    {
                        reason = value as string;

                        if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_FailedDescription, out value))
                        {
                            description = value as string;
                        }
                    }

                    // Move to Poison Queue
                    await MoveToDeadLetter(args, args.Message, reason, description).ConfigureAwait(false);
                    activity.SetTag("HasDeadMark", true);
                    activity.SetTag("MovedToDeadLetter", true);

                    activity.ExportToActivityMonitor(methodMonitor.Activity);
                    methodMonitor.OnCompleted();
                    return;
                }

                var singleInlineResource = dataLabServiceBusProperties.SingleInlineResource ?? false;

                if (hasInput)
                {
                    // 1. Check Data Format
                    SolutionDataFormat dataFormat = SolutionDataFormat.ARN;

                    if (dataLabServiceBusProperties.DataFormat != null)
                    {
                        if (!StringEnumCache.TryGetEnumIgnoreCase(dataLabServiceBusProperties.DataFormat, out dataFormat))
                        {
                            using var criticalLogMonitor = ServiceBusTaskManagerCriticalError.ToMonitor();
                            criticalLogMonitor.Activity[SolutionConstants.MethodName] = "ProcessMessageAsync";
                            criticalLogMonitor.Activity[SolutionConstants.QueueName] = NameSpaceAndQueueName;
                            criticalLogMonitor.Activity[SolutionConstants.ServiceBusMessageId] = args.Message.MessageId;
                            criticalLogMonitor.Activity[SolutionConstants.DataEnqueuedTime] = args.Message.EnqueuedTime;
                            criticalLogMonitor.Activity[SolutionConstants.DataFormat] = dataLabServiceBusProperties.DataFormat;
                            var exception = new Exception("Not supported Data Format: " + dataLabServiceBusProperties.DataFormat + ". Try to use ARN Format");
                            criticalLogMonitor.OnError(exception);
                        }
                    }

                    if (dataFormat != SolutionDataFormat.ARN)
                    {
                        using var criticalLogMonitor = ServiceBusTaskManagerCriticalError.ToMonitor();
                        var outStr = dataFormat.FastEnumToString();
                        criticalLogMonitor.Activity[SolutionConstants.MethodName] = "ProcessMessageAsync";
                        criticalLogMonitor.Activity[SolutionConstants.QueueName] = NameSpaceAndQueueName;
                        criticalLogMonitor.Activity[SolutionConstants.ServiceBusMessageId] = args.Message.MessageId;
                        criticalLogMonitor.Activity[SolutionConstants.DataEnqueuedTime] = args.Message.EnqueuedTime;
                        criticalLogMonitor.Activity[SolutionConstants.DataFormat] = outStr;
                        var exception = new NotImplementedException("Not Implemented Data Format: " + outStr + ". Try to use ARN Format");
                        criticalLogMonitor.OnError(exception);
                    }
                }

                var serviceBusTaskInfo = new ServiceBusTaskInfo(args);
                var dataSource = hasDeadMark ? DataSourceType.DeadLetterQueue : DataSourceType.ServiceBus;

                // Retry Count
                int appRetryCount = dataLabServiceBusProperties.RetryCount ?? 0;
                activity.SetTag(SolutionConstants.RetryPropertyCount, appRetryCount);

                int retryCount = appRetryCount < message.DeliveryCount ? message.DeliveryCount : appRetryCount;
                activity.SetTag(SolutionConstants.RetryCount, retryCount);

                var firstEnqueuedTime = dataLabServiceBusProperties.FirstEnqueuedTime == default ? message.EnqueuedTime : dataLabServiceBusProperties.FirstEnqueuedTime;
                var firstPickedUpTime = dataLabServiceBusProperties.FirstPickedUpTime == default ? DateTimeOffset.UtcNow : dataLabServiceBusProperties.FirstPickedUpTime;

                if (hasInput && !singleInlineResource)
                {
                    var inputMessage = ARNRawInputMessage.CreateRawInputMessage(message);
                    activity.SetTag(InputOutputConstants.ServiceBusHasRawInput, true);

                    var iotaskName = _logicalQueueName + InputOutputConstants.ServiceBusRawInputEventTaskSuffix;

                    var eventTaskContext = new IOEventTaskContext<ARNRawInputMessage>(
                        iotaskName,
                        dataSource,
                        NameSpaceAndQueueName,
                        firstEnqueuedTime,
                        firstPickedUpTime,
                        message.EnqueuedTime,
                        inputMessage?.EventTime ?? dataLabServiceBusProperties.EventTime,
                        inputMessage,
                        serviceBusTaskInfo,
                        retryCount,
                        SolutionInputOutputService.RetryStrategy,
                        activity.Context,
                        activity.TopActivityStartTime,
                        createNewTraceId: false,
                        regionConfigData: RegionConfigManager.GetRegionConfig(dataLabServiceBusProperties.RegionName),
                        CancellationToken.None,
                        SolutionInputOutputService.ARNMessageChannels.RawInputRetryChannelManager,
                        SolutionInputOutputService.ARNMessageChannels.RawInputPoisonChannelManager,
                        SolutionInputOutputService.ARNMessageChannels.RawInputFinalChannelManager,
                        SolutionInputOutputService.GlobalConcurrencyManager);

                    // Fill information from ServiceBus Message
                    eventTaskContext.EventTaskActivity.InputCorrelationId ??= inputCorrelationId;
                    eventTaskContext.EventTaskActivity.InputResourceId ??= inputResourceId;
                    eventTaskContext.EventTaskActivity.EventType ??= dataLabServiceBusProperties.EventType;
                    eventTaskContext.PartnerTotalSpentTime = dataLabServiceBusProperties.PartnerSpentTime ?? 0;

                    if (hadSourceOfTruthConflict)
                    {
                        eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.RetrySourceOfTruthConflict;
                    }

                    // Set Correlation to propogate through channels
                    // InputResource Id is still unknown in raw Input Message
                    methodMonitor.Activity.CorrelationId = inputMessage.CorrelationId;

                    // Add to RawInputChannelManager
                    activity.SetTag(SolutionConstants.NextChannel, IOTaskChannelType.RawInputChannel.FastEnumToString());

                    // Export to ActivityMonitor before adding to TaskChannel
                    activity.ExportToActivityMonitor(methodMonitor.Activity);

                    // Start Task
                    eventTaskContext.SetTaskTimeout(_taskTimeOut);
                    await eventTaskContext.StartEventTaskAsync(SolutionInputOutputService.ARNMessageChannels.RawInputChannelManager, waitForTaskFinish: true, methodMonitor.Activity).ConfigureAwait(false);

                    // Do not put any code after StartTaskAsync because the task might already finish inside it.
                    // so EventTaskActivity might already be disposed
                    methodMonitor.OnCompleted();

                    if (serviceBusTaskInfo.NeedToMoveDeadLetter)
                    {
                        await MoveToDeadLetter(args, args.Message, serviceBusTaskInfo.FailedReason, serviceBusTaskInfo.FailedDescription).ConfigureAwait(false);
                    }
                    else
                    {
                        await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
                    }

                    return;
                }
                else
                {
                    // Find Entry Channel
                    activity.SetTag(InputOutputConstants.ServiceBusHasSingleInput, true);

                    if (_logicalQueueName == InputOutputConstants.ServiceBusRetryQueueLogicalName)
                    {
                        // Retry Queue
                        var inputAction = ArmUtils.GetAction(inputEventType);
                        IOServiceOpenTelemetry.ReportRetryingIndividualResourceCounter(inputAction);
                    }

                    IOTaskChannelType entryChannelType = IOTaskChannelType.InputChannel;
                    string partnerChannelName = null;

                    if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_ChannelType, out object value) && value != null)
                    {
                        entryChannelType = StringEnumCache.GetEnum<IOTaskChannelType>(value.ToString());
                        if (entryChannelType == IOTaskChannelType.PartnerChannel)
                        {
                            activity.SetTag(InputOutputConstants.ServiceBusHasPartnerChannel, true);

                            // Get PartnerChannel Name
                            if (message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_PartnerChannelName, out value))
                            {
                                partnerChannelName = value?.ToString();
                                activity.SetTag(InputOutputConstants.ServiceBusPartnerChannelName, partnerChannelName);
                            }

                            if (string.IsNullOrEmpty(partnerChannelName))
                            {
                                // Should not happen but just in case
                                entryChannelType = IOTaskChannelType.InputChannel; // fallback
                            }
                        }
                    }

                    ARNSingleInputMessage inputMessage = hasInput ? ARNSingleInputMessage.CreateSingleInputMessage(message, activity, dataLabServiceBusProperties) : null;

                    var iotaskName = _logicalQueueName + InputOutputConstants.ServiceBusSingleInputEventTaskSuffix;
                    var eventTaskContext = new IOEventTaskContext<ARNSingleInputMessage>(
                        iotaskName,
                        dataSource,
                        NameSpaceAndQueueName,
                        firstEnqueuedTime,
                        firstPickedUpTime,
                        message.EnqueuedTime,
                        inputMessage?.EventTime ?? dataLabServiceBusProperties.EventTime,
                        inputMessage,
                        serviceBusTaskInfo,
                        retryCount,
                        SolutionInputOutputService.RetryStrategy,
                        activity.Context,
                        activity.TopActivityStartTime,
                        createNewTraceId: false,
                        regionConfigData: RegionConfigManager.GetRegionConfig(dataLabServiceBusProperties.RegionName),
                        CancellationToken.None,
                        SolutionInputOutputService.ARNMessageChannels.RetryChannelManager,
                        SolutionInputOutputService.ARNMessageChannels.PoisonChannelManager,
                        SolutionInputOutputService.ARNMessageChannels.FinalChannelManager,
                        SolutionInputOutputService.GlobalConcurrencyManager);

                    // Fill information from ServiceBus Message
                    eventTaskContext.EventTaskActivity.InputCorrelationId ??= inputCorrelationId;
                    eventTaskContext.EventTaskActivity.InputResourceId ??= inputResourceId;
                    eventTaskContext.EventTaskActivity.EventType ??= dataLabServiceBusProperties.EventType;
                    eventTaskContext.PartnerTotalSpentTime = dataLabServiceBusProperties.PartnerSpentTime ?? 0;

                    if (hadSourceOfTruthConflict)
                    {
                        eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.RetrySourceOfTruthConflict;
                    }

                    if (hadSuccessInputCacheWrite)
                    {
                        eventTaskContext.IOEventTaskFlags |= IOEventTaskFlag.RetrySuccessInputCacheWrite;
                    }

                    if (hasOutput)
                    {
                        var outputMessage = OutputMessage.CreateOutputMessage(message, eventTaskContext);
                        eventTaskContext.AddOutputMessage(outputMessage);
                        eventTaskContext.EventTaskActivity.SetTag(InputOutputConstants.ServiceBusHasOutput, true);
                    }

                    var entryChannelString = entryChannelType.FastEnumToString();
                    eventTaskContext.EventTaskActivity.SetTag(InputOutputConstants.ServiceBusEntryChannel, entryChannelString);

                    // Shortcut for IO and Output Channel first
                    // retryEntryChannel
                    ITaskChannelManager<IOEventTaskContext<ARNSingleInputMessage>> nextChannel = null;

                    if (entryChannelType == IOTaskChannelType.InputChannel && inputMessage != null)
                    {
                        nextChannel = SolutionInputOutputService.ARNMessageChannels.InputChannelManager;
                    }
                    else if (entryChannelType == IOTaskChannelType.OutputChannel && eventTaskContext.OutputMessage != null)
                    {
                        nextChannel = SolutionInputOutputService.ARNMessageChannels.OutputChannelManager;
                    }
                    else if (entryChannelType == IOTaskChannelType.BlobPayloadRoutingChannel && inputMessage != null)
                    {
                        nextChannel = SolutionInputOutputService.ARNMessageChannels.BlobPayloadRoutingChannelManager;
                    }
                    else if (entryChannelType == IOTaskChannelType.PartnerChannel && partnerChannelName != null && inputMessage != null)
                    {
                        // Find partner channel
                        var partnerChannelsDictionary = SolutionInputOutputService.ARNMessageChannels.PartnerChannelRoutingManager.PartnerChannelsDictionary;
                        if (partnerChannelsDictionary.TryGetValue(partnerChannelName, out var partnerChannelManager))
                        {
                            nextChannel = partnerChannelManager;
                        }
                        else
                        {
                            nextChannel = SolutionInputOutputService.ARNMessageChannels.InputChannelManager;
                        }
                    }
                    else
                    {
                        // Export to ActivityMonitor before adding to TaskChannel
                        activity.ExportToActivityMonitor(methodMonitor.Activity);
                        throw new InvalidOperationException("Invalid Retry Start Channel is set");
                    }

                    // Add to NextChannel
                    activity.SetTag(SolutionConstants.NextChannel, nextChannel.ChannelName);

                    // Export to ActivityMonitor before adding to TaskChannel
                    activity.ExportToActivityMonitor(methodMonitor.Activity);

                    // Start Task
                    // Notice we are waiting for below Task to be completed
                    eventTaskContext.SetTaskTimeout(_taskTimeOut);
                    await eventTaskContext.StartEventTaskAsync(nextChannel, waitForTaskFinish: true, methodMonitor.Activity).ConfigureAwait(false);

                    // Do not put any code after StartTaskAsync because the task might already finish inside it.
                    // so EventTaskActivity might already be disposed
                    methodMonitor.OnCompleted();

                    if (serviceBusTaskInfo.NeedToMoveDeadLetter)
                    {
                        await MoveToDeadLetter(args, args.Message, serviceBusTaskInfo.FailedReason, serviceBusTaskInfo.FailedDescription).ConfigureAwait(false);
                    }
                    else
                    {
                        await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                // Move to Poison Queue
                var poisonReason = "Invalid Format Message";
                var poisonDescription = ex.Message;
                if (poisonDescription?.Length > 128)
                {
                    poisonDescription = poisonDescription[..128];
                }

                await MoveToDeadLetter(args, args.Message, poisonReason, poisonDescription).ConfigureAwait(false);

                activity.SetTag("MovedToDeadLetter", true);
                activity.RecordException("ProcessMessageAsync", ex);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);

                activity.ExportToActivityMonitor(methodMonitor.Activity);
                methodMonitor.OnError(ex);
            }
        }

        public static Task MoveToDeadLetter(ProcessMessageEventArgs args, ServiceBusReceivedMessage message,
            string reason, string description)
        {
            return args.DeadLetterMessageAsync(message,
                InputOutputConstants.DisableDeadLetterMarkDictionary,
                reason, description,
                cancellationToken: args.CancellationToken);
        }

        private Task UpdateTaskTimeOutDuration(TimeSpan newTimeOut)
        {
            if (newTimeOut.TotalMilliseconds <= 0)
            {
                Logger.LogError("{config} must be larger than 0", _timeoutConfigName);
                return Task.CompletedTask;
            }

            lock (_updateLock)
            {
                var oldTimeOut = _taskTimeOut;
                if (newTimeOut.TotalMilliseconds == oldTimeOut.TotalMilliseconds)
                {
                    return Task.CompletedTask;
                }

                _taskTimeOut = newTimeOut;

                Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                    _timeoutConfigName,
                    oldTimeOut, newTimeOut);
            }

            return Task.CompletedTask;
        }
    }
}
