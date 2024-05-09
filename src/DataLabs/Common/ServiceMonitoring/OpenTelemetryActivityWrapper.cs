// <copyright file="OpenTelemetryActivityWrapper.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    
    /*
     * Here is the reason why we need this wrapper:
     * 1. .net Acitvity uses List for tags.
     *     When there are many tags, setTag iterates all tags in the list. 
     *     This Wrapper improves the setTag performance when there is large number of tags expected.
     * 2. .net Activity support Add (allow duplicated key) but Gevena doesn't support duplicated key
     * 3. Also Geneva exporter doesn't support ActivityEvent. In this wrapper ActivityEvent is converted as Tag
     */
    /* This is NOT Thread Safe */
    public class OpenTelemetryActivityWrapper : IDisposable
    {
        private static readonly AsyncLocal<OpenTelemetryActivityWrapper?> s_current = new();
        public static OpenTelemetryActivityWrapper? Current
        {
            get { return s_current.Value; }
            set {
                s_current.Value = value;
                Activity.Current = value?._activity;
            }
        }

        public static readonly bool USE_PROPERTY_COLUMN = true;
        public const int INIT_STRING_BUILDER_SIZE = 256;
        public const int INIT_CHILD_TRACE_ID_LIST_SIZE = 8;
        public const int INIT_EXCEPTION_LIST_SIZE = 4;
        public const int INIT_KEY_VALUE_MAP_SIZE = 32;

        // Time Related (All TimeStamps are returned from StopWatch, not milliseconds)
        public long CreatedStopWatchTimeStamp { get; }
        public DateTimeOffset TopActivityStartTime { get; }

        public TimeSpan Elapsed => Stopwatch.GetElapsedTime(CreatedStopWatchTimeStamp);
        public TimeSpan DurationFromTopActivity => (DateTimeOffset.UtcNow - TopActivityStartTime);

        public string? ActivityName => _activity?.OperationName;
        public string? ActivityId => _activity?.Id;
        public ActivityContext Context => _activityContext;
        public string? TraceId => _traceId;
        public string? ParentDifferentTraceId { get; set; }

        public string? InputCorrelationId { get; set; }
        public string? OutputCorrelationId { get; set; }
        public string? InputResourceId { get; set; }
        public string? OutputResourceId { get; set; }
        public string? EventType { get; set; }
        public string? EventAction
        {
            get
            {
                if (_eventAction != null)
                {
                    return _eventAction;
                }
                var eventType = EventType;
                _eventAction = ArmUtils.GetAction(eventType);
                return _eventAction;
            }
            set
            {
                if (value != null)
                {
                    _eventAction = value;
                }
            }
        }

        private StringBuilder? _eventListStringBuilder;
        private readonly SimpleOrderedDictionary<string, object> _keyValueMap = new(INIT_KEY_VALUE_MAP_SIZE, null);

        private readonly Activity? _activity;
        private readonly ActivityContext _activityContext;
        private readonly string? _traceId;
        private List<string>? _childTraceIds;

        private List<Exception>? _exceptions;
        private List<string?>? _exceptionEventNames;

        private List<Exception>? _continuableExceptions;
        private List<string?>? _continuableExceptionEventNames;

        private string? _eventAction;
        private int _numActivityEvents;
        private int _disposed;

        /*
         * this will create activity and start it
         */
        public OpenTelemetryActivityWrapper(ActivitySource source, string name,
            ActivityKind kind, ActivityContext parentContext, bool createNewTraceId, DateTimeOffset topActivityStartTime)
        {
            CreatedStopWatchTimeStamp = Stopwatch.GetTimestamp();

            var activityContext = parentContext;
            if (parentContext != default)
            {
                if (createNewTraceId)
                {
                    TopActivityStartTime = DateTimeOffset.UtcNow;

                    // Create new traceId but reuse SpanId of parent
                    ParentDifferentTraceId = parentContext.TraceId.ToString();

                    var traceId = Tracer.CreateActivityTraceId();
                    var spandId = parentContext.SpanId;
                    activityContext = new ActivityContext(traceId, spandId, ActivityTraceFlags.Recorded);
                }
                else
                {
                    // ParentContext is given and use same traceId
                    // Use given topActivityStartTime if it is valid number
                    TopActivityStartTime = topActivityStartTime != default ? topActivityStartTime : DateTimeOffset.UtcNow;
                }
            }
            else
            {
                // ParentContext is not given
                // Set this as topActivity
                TopActivityStartTime = DateTimeOffset.UtcNow;
            }

            _activity = source.StartActivity(name, kind, activityContext);
            _activityContext = _activity == null ? default : _activity.Context;
            _traceId = _activityContext == default ? null : _activityContext.TraceId.ToString();

            if (ParentDifferentTraceId != null)
            {
                // Add Parent Trace ID
                SetTag(SolutionConstants.ParentTraceId, ParentDifferentTraceId);
            }
        }

        public OpenTelemetryActivityWrapper(ActivitySource source, string name,
            ActivityKind kind, string? parentId)
        {
            CreatedStopWatchTimeStamp = Stopwatch.GetTimestamp();
            TopActivityStartTime = DateTimeOffset.UtcNow;
            _activity = source.StartActivity(name, kind, parentId);
            _activityContext = _activity == null ? default : _activity.Context;
        }

        public void AddChildTraceId(string childTraceId)
        {
            _childTraceIds ??= new(INIT_CHILD_TRACE_ID_LIST_SIZE);
            _childTraceIds.Add(childTraceId);
        }

        public void SetStopTime(DateTime endTime = default, bool overwrite=false)
        {
            if (_activity == null || _disposed > 1)
            {
                return;
            }

            if (_activity.Duration == TimeSpan.Zero || overwrite)
            {
                _activity.SetEndTime(endTime == default ? DateTime.UtcNow : endTime);
            }
        }

        public void SetStartTime(DateTime startTime)
        {
            if (_activity == null || startTime == default)
            {
                return;
            }

            _activity.SetStartTime(startTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordException(string? eventName, Exception ex, bool allowMultiples = true)
        {
            if (_disposed > 1 || ex == null)
            {
                return;
            }

            if (_exceptions == null)
            {
                _exceptions = new(INIT_EXCEPTION_LIST_SIZE);
                _exceptionEventNames = new(INIT_EXCEPTION_LIST_SIZE);
            }

            if (_exceptions.Count > 0 && _exceptions[_exceptions.Count-1] == ex)
            {
                // Check if it is same as previous reported exception
                return;
            }

            if (_exceptions.Count > 0 && !allowMultiples)
            {
                // Exception already exists
                return;
            }

            _exceptions.Add(ex);
            _exceptionEventNames!.Add(eventName);

            if (!Tracer.USING_EVENTLIST_COLUMN && !USE_PROPERTY_COLUMN)
            {
                if (_activity == null)
                {
                    return;
                }

                // Use regular OpenTelemetry Event
                // Record Exception as Event
                var exMessage = ex.Message;
                var exStackTrace = ex.ToString();

                var columnName = _exceptions.Count == 1 ? SolutionConstants.ExceptionColumn :
                                SolutionConstants.OtherExceptionColumnPrefix + _exceptions.Count;

                TagList tagList = default;
                tagList.Add(SolutionConstants.AttributeExceptionType, ex.GetType().FullName);
                if (!string.IsNullOrWhiteSpace(exMessage))
                {
                    tagList.Add(SolutionConstants.AttributeExceptionMessage, exMessage);
                }
                tagList.Add(SolutionConstants.AttributeExceptionStacktrace, exStackTrace);
                tagList.Add(SolutionConstants.AttributeExceptionEventName, eventName);

                var tagsCollection = new ActivityTagsCollection(tagList);
                var activityEvent = new ActivityEvent(columnName, default, tagsCollection);
                _activity.AddEvent(activityEvent);
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordContinuableException(string eventName, Exception ex)
        {
            if (_disposed > 1 || ex == null)
            {
                return;
            }

            if (_continuableExceptions == null)
            {
                _continuableExceptions = new(INIT_EXCEPTION_LIST_SIZE);
                _continuableExceptionEventNames = new(INIT_EXCEPTION_LIST_SIZE);
            }

            if (_continuableExceptions.Count > 0 && _continuableExceptions[_continuableExceptions.Count - 1] == ex)
            {
                // Check if it is same as previous reported exception
                return;
            }

            _continuableExceptions.Add(ex);
            _continuableExceptionEventNames!.Add(eventName);

            if (!Tracer.USING_EVENTLIST_COLUMN && !USE_PROPERTY_COLUMN)
            {
                if (_activity == null)
                {
                    return;
                }

                // Use regular OpenTelemetry Event
                // Record Exception as Event
                var exMessage = ex.Message;
                var exStackTrace = ex.ToString();

                var columnName = SolutionConstants.ContinuableExceptionColumn + _continuableExceptions.Count;

                TagList tagList = default;
                tagList.Add(SolutionConstants.AttributeExceptionType, ex.GetType().FullName);
                if (!string.IsNullOrWhiteSpace(exMessage))
                {
                    tagList.Add(SolutionConstants.AttributeExceptionMessage, exMessage);
                }
                tagList.Add(SolutionConstants.AttributeExceptionStacktrace, exStackTrace);
                tagList.Add(SolutionConstants.AttributeExceptionEventName, eventName);

                var tagsCollection = new ActivityTagsCollection(tagList);
                var activityEvent = new ActivityEvent(columnName, default, tagsCollection);
                _activity.AddEvent(activityEvent);
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEvent(string eventName)
        {
            AddEvent(eventName, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEvent(string eventName, in TagList tagList)
        {
            if (_disposed > 1)
            {
                return;
            }

            _numActivityEvents++;

            if (Tracer.USING_EVENTLIST_COLUMN)
            {
                _eventListStringBuilder ??= new StringBuilder(INIT_STRING_BUILDER_SIZE);

                var durationSinceCreated = Elapsed.TotalMilliseconds;

                if (_numActivityEvents > 1)
                {
                    _eventListStringBuilder.Append('\n');
                }

                // Add EventName
                _eventListStringBuilder.Append("* ").Append(eventName);
                // Add TimeSinceCreation
                _eventListStringBuilder.Append('\n').Append(SolutionConstants.TimeSinceCreation).Append('=').Append(durationSinceCreated);

                // Add TagList
                int tagCount = tagList.Count;
                for (int i = 0; i < tagCount; i++)
                {
                    var tag = tagList[i];
                    var key = tag.Key;
                    var value = tag.Value ?? "null";
                    _eventListStringBuilder.Append('\n').Append(key).Append('=').Append(value);
                }
            }
            else
            {
                var tagsCollection = tagList.Count > 0 ? new ActivityTagsCollection(tagList) : null;
                var activityEvent = new ActivityEvent(eventName, default, tagsCollection);
                _activity?.AddEvent(activityEvent);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTag(string key, object? value)
        {
            if (value == null)
            {
                // This is against Original .Net OpenTelemetry contract because null value means delete
                // But it makes our code more efficiently because we don't need to add if null check and we don't need to iterate to delete 
                // For deletion case, new method (RemoveTag is added);
                return;
            }

            if (_disposed > 1)
            {
                return;
            }

            _keyValueMap[key] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveTag(string key)
        {
            if (_disposed > 1)
            {
                return;
            }

            _keyValueMap.Remove(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object? GetTag(string key)
        {
            if (_disposed > 1)
            {
                return null;
            }

            if (_keyValueMap.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }

        public void SetStatus(ActivityStatusCode code, string? description = null)
        {
            if (_disposed > 1)
            {
                return;
            }

            if (GetStatus() != ActivityStatusCode.Error)
            {
                // If status is alredy set to Error. Don't overwrite
                var statusDescription = description;
                if (code != ActivityStatusCode.Ok)
                {
                    if (_exceptions?.Count > 0 && string.IsNullOrWhiteSpace(statusDescription))
                    {
                        statusDescription = _exceptions[0].Message;
                    }
                }
                _activity?.SetStatus(code, statusDescription);
            }
        }

        public void ExportToActivityMonitor(IActivity activity)
        {
            var kvpairs = _keyValueMap.GetInternalKeyValueList;
            for (int i = 0, size = kvpairs.Count; i < size; i++)
            {
                var keyValue = kvpairs[i];
                activity[keyValue.Key] = keyValue.Value;
            }
        }

        private ActivityStatusCode GetStatus()
        {
            if (_disposed > 1)
            {
                return ActivityStatusCode.Ok;
            }

            return _activity != null ? _activity.Status : ActivityStatusCode.Ok;
        }

        public void Dispose()
        {
            if (_disposed > 1 || Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                // Already disposed
                return;
            }

            if (_activity == null)
            {
                return;
            }

            if (!USE_PROPERTY_COLUMN)
            {
                FlushWithIndividualTags();
            }
            else
            {
                FlushWithPropertyColumn();
            }

            _activity.Stop();
            _activity.Dispose();
        }

        private void FlushWithIndividualTags()
        {
            if (_activity == null)
            {
                return;
            }

            // Dont' call SetTag here because it is not working when dispose flag is set
            // Use same column name with ARG ActivityMonitor
            if (!string.IsNullOrWhiteSpace(InputCorrelationId))
            {
                _keyValueMap[BasicActivityMonitor.CorrelationId] = InputCorrelationId;
            }

            if (!string.IsNullOrWhiteSpace(InputResourceId))
            {
                _keyValueMap[BasicActivityMonitor.InputResourceId] = InputResourceId;
                _keyValueMap[BasicActivityMonitor.ResourceType] = ArmUtils.GetResourceType(InputResourceId);
            }

            if (!string.IsNullOrWhiteSpace(EventType))
            {
                _keyValueMap[BasicActivityMonitor.EventType] = EventType;
            }

            if (!string.IsNullOrWhiteSpace(OutputCorrelationId))
            {
                _keyValueMap[SolutionConstants.OutputCorrelationId] = OutputCorrelationId;
            }

            if (!string.IsNullOrWhiteSpace(OutputResourceId))
            {
                _keyValueMap[BasicActivityMonitor.OutputResourceId] = OutputResourceId;
            }

            // Add EventList as Tag
            if (_eventListStringBuilder?.Length > 0)
            {
                _keyValueMap[SolutionConstants.ActivityEventListColumn] = _eventListStringBuilder.ToString();
                _eventListStringBuilder.Clear();
            }

            // Add ChildTags
            if (_childTraceIds?.Count > 0)
            {
                var numChilds = _childTraceIds.Count;
                var needCapacity = (32 + 3) * numChilds;

                if (_eventListStringBuilder == null)
                {
                    _eventListStringBuilder = new StringBuilder(needCapacity);
                }
                else
                {
                    _eventListStringBuilder.Clear();
                    _eventListStringBuilder.EnsureCapacity(needCapacity);
                }
                
                for (int i = 0; i < _childTraceIds.Count; i++)
                {
                    _eventListStringBuilder.Append('[').Append(_childTraceIds[i]).Append(']');
                    if (i > 0)
                    {
                        _eventListStringBuilder.Append('\n');
                    }
                }
                _keyValueMap[SolutionConstants.ChildTraceIds] = _eventListStringBuilder.ToString();
                _eventListStringBuilder.Clear();
            }

            // Add Exception as Tag
            if (_exceptions?.Count > 0 && Tracer.USING_EVENTLIST_COLUMN)
            {
                int numExceptions = _exceptions.Count;
                for (int i = 0; i < numExceptions; i++)
                {
                    var exception = _exceptions[i];
                    var tagName = i == 0 ? SolutionConstants.ExceptionColumn :
                                 SolutionConstants.OtherExceptionColumnPrefix + i;

                    _keyValueMap[tagName] = exception.ToString();
                }
            }

            // Add ContinuableExceptions as Tag
            if (_continuableExceptions?.Count > 0 && Tracer.USING_EVENTLIST_COLUMN)
            {
                int numExceptions = _continuableExceptions.Count;
                for (int i = 0; i < numExceptions; i++)
                {
                    var exception = _continuableExceptions[i];
                    var tagName = SolutionConstants.ContinuableExceptionColumn + (i+1);
                    _keyValueMap[tagName] = exception.ToString();
                }
            }

            // Move tag to activity
            if (_keyValueMap.Count > 0)
            {
                var kvpairs = _keyValueMap.GetInternalKeyValueList;
                for (int i = 0; i < kvpairs.Count; i++)
                {
                    var keyValue = kvpairs[i];
                    _activity.AddTag(keyValue.Key, keyValue.Value);
                }

                _keyValueMap.Clear();
            }
        }

        private static readonly HashSet<string> ExcludeKeysInPropertyBag = new()
        {
            SolutionConstants.TaskChannelBeforeSuccess,
            SolutionConstants.TaskFinalStage,
            SolutionConstants.RetryCount,
            SolutionConstants.PartnerResponseFlags,
            SolutionConstants.TaskFinalStatus,
            SolutionConstants.ParentTraceId,
            SolutionConstants.ChildTraceIds,
            SolutionConstants.ActivityEventListColumn,
            SolutionConstants.TaskFailedComponent,
            SolutionConstants.TaskFailedReason,
            SolutionConstants.TaskFailedDescription,
            SolutionConstants.ExceptionColumn,
            SolutionConstants.ExceptionEventNameColumn,
            SolutionConstants.OtherExceptionColumnPrefix,
            SolutionConstants.ContinuableExceptionColumn,
            SolutionConstants.ContinuableExceptionEventNameColumn
        };

        private void FlushWithPropertyColumn()
        {
            if (_activity == null)
            {
                return;
            }

            var rawInputMessage = false;
            var numRawNotifications = 0;
            var hasMultiResources = false;
            var hasBlobURI = false;

            if (_keyValueMap.TryGetValue(SolutionConstants.HasEventHubARNRawInputMessage, out var outVal))
            {
                rawInputMessage = true;

                if (_keyValueMap.TryGetValue(SolutionConstants.NumOfEventGridNotifications, out outVal) && outVal is int)
                {
                    numRawNotifications = (int)outVal;
                    int totalResources = 0;

                    for (int i = 0; i < numRawNotifications; i++)
                    {
                        string? suffix = numRawNotifications > 1 ? (i+1).ToString() : null;
                        var numResourcesKey = SolutionConstants.NumResources + suffix;

                        int numResources = 0;
                        if (_keyValueMap.TryGetValue(numResourcesKey, out outVal) && outVal is int)
                        {
                            numResources = (int)outVal;
                            totalResources += numResources;
                        }

                        if (numResources == 0)
                        {
                            // Check if blobURIKey exists
                            var blobURIKey = SolutionConstants.BlobURI + suffix;
                            if (_keyValueMap.TryGetValue(blobURIKey, out outVal) && outVal != null)
                            {   // BlobURI exists
                                hasBlobURI = true;
                            }
                        }
                    }

                    hasMultiResources = totalResources > 0;
                }
            }

            // Input Correlation Id
            _activity.AddTag(BasicActivityMonitor.CorrelationId, GetNotEmptyValue(InputCorrelationId));

            // Input Resource Id
            if (string.IsNullOrWhiteSpace(InputResourceId))
            {
                // empty InputResourceId
                // Let's put info about batched reuqest or blob URL
                var rawInputMessageInfo = string.Empty;
                if (rawInputMessage)
                {
                    if (numRawNotifications > 1)
                    {
                        rawInputMessageInfo += SolutionConstants.MultiNotifications;
                    }

                    if (hasMultiResources)
                    {
                        if (rawInputMessageInfo.Length > 0)
                        {
                            rawInputMessageInfo += ',';
                        }
                        rawInputMessageInfo += SolutionConstants.MultiResources;
                    }

                    if (hasBlobURI)
                    {
                        if (rawInputMessageInfo.Length > 0)
                        {
                            rawInputMessageInfo += ',';
                        }
                        rawInputMessageInfo += SolutionConstants.HasBlobURI;
                    }
                }

                _activity.AddTag(BasicActivityMonitor.InputResourceId, GetNotEmptyValue(rawInputMessageInfo));
                _activity.AddTag(BasicActivityMonitor.ResourceType, BasicActivity.NoneString);
            }
            else
            {
                _activity.AddTag(BasicActivityMonitor.InputResourceId, InputResourceId);
                _activity.AddTag(BasicActivityMonitor.ResourceType, GetNotEmptyValue(ArmUtils.GetResourceType(InputResourceId)));
            }

            // EventType
            _activity.AddTag(BasicActivityMonitor.EventType, GetNotEmptyValue(EventType));

            // Output Correlation Id
            _activity.AddTag(BasicActivityMonitor.OutputCorrelationId, GetNotEmptyValue(OutputCorrelationId));

            // Output Resource Id
            _activity.AddTag(BasicActivityMonitor.OutputResourceId, GetNotEmptyValue(OutputResourceId));

            // Elasped Time
            _activity.AddTag(BasicActivityMonitor.ElapsedMilliseconds, Elapsed.TotalMilliseconds);

            // Duration From Top Activity
            _activity.AddTag(BasicActivityMonitor.DurationMilliseconds, DurationFromTopActivity.TotalMilliseconds);

            // Final Stage
            var hasFiltered = false;
            if (_keyValueMap.TryGetValue(SolutionConstants.TaskFiltered, out var taskFiltered) && taskFiltered != null)
            {
                if (taskFiltered is bool v)
                {
                    hasFiltered = v;
                } else
                {
                    _ = bool.TryParse(taskFiltered.ToString(), out hasFiltered);
                }
            }

            if (hasFiltered)
            {
                if (_keyValueMap.TryGetValue(SolutionConstants.TrafficTunerResult, out var tunerResult))
                {
                    _activity.AddTag(SolutionConstants.TaskFinalStage, SolutionConstants.TrafficTunerResult);
                    _activity.AddTag(SolutionConstants.TaskFailedComponent, SolutionConstants.TrafficTunerResult);
                    _activity.AddTag(SolutionConstants.TaskFailedReason, tunerResult);
                }
                else
                {
                    _activity.AddTag(SolutionConstants.TaskFinalStage, SolutionConstants.TaskFiltered);
                }
            }else
            {
                if (_keyValueMap.TryGetValue(SolutionConstants.TaskChannelBeforeSuccess, out var channelBeforeSuccess))
                {
                    _activity.AddTag(SolutionConstants.TaskFinalStage, channelBeforeSuccess);
                }
                else
                {
                    if (_keyValueMap.TryGetValue(SolutionConstants.TaskFinalStage, out var finalStage))
                    {
                        _activity.AddTag(SolutionConstants.TaskFinalStage, finalStage);
                    }
                    else
                    {
                        _activity.AddTag(SolutionConstants.TaskFinalStage, BasicActivity.NoneString);
                    }
                }
            }

            // Partner Response Flags
            if (_keyValueMap.TryGetValue(SolutionConstants.PartnerResponseFlags, out var partnerResponseFlags))
            {
                _activity.AddTag(SolutionConstants.PartnerResponseFlags, partnerResponseFlags);
            }
            else
            {
                _activity.AddTag(SolutionConstants.PartnerResponseFlags, BasicActivity.NoneString);
            }

            // Final Status
            if (hasFiltered)
            {
                _activity.AddTag(SolutionConstants.TaskFinalStatus, SolutionConstants.TaskFiltered);
            }
            else
            {
                if (_keyValueMap.TryGetValue(SolutionConstants.TaskFinalStatus, out var finalStatus))
                {
                    _activity.AddTag(SolutionConstants.TaskFinalStatus, finalStatus);
                }
                else
                {
                    _activity.AddTag(SolutionConstants.TaskFinalStatus, BasicActivity.NoneString);
                }
            }

            // RetryCount
            if (_keyValueMap.TryGetValue(SolutionConstants.RetryCount, out var retryCount))
            {
                _activity.AddTag(SolutionConstants.RetryCount, retryCount);
            }
            else
            {
                _activity.AddTag(SolutionConstants.RetryCount, 0);
            }

            // Parent TraceId
            _activity.AddTag(SolutionConstants.ParentTraceId, GetNotEmptyValue(ParentDifferentTraceId));

            // Child TraceIds
            string? childTraceIds = null;
            if (_childTraceIds?.Count > 0)
            {
                var numChilds = _childTraceIds.Count;
                var needCapacity = (32 + 3) * numChilds;

                if (_eventListStringBuilder == null)
                {
                    _eventListStringBuilder = new StringBuilder(needCapacity);
                }
                else
                {
                    _eventListStringBuilder.Clear();
                    _eventListStringBuilder.EnsureCapacity(needCapacity);
                }

                for (int i = 0; i < _childTraceIds.Count; i++)
                {
                    _eventListStringBuilder.Append('[').Append(_childTraceIds[i]).Append(']');
                    if (i > 0)
                    {
                        _eventListStringBuilder.Append('\n');
                    }
                }

                childTraceIds = _eventListStringBuilder.ToString();
                _eventListStringBuilder.Clear();
            }
            _activity.AddTag(SolutionConstants.ChildTraceIds, GetNotEmptyValue(childTraceIds));

            // Add EventList
            if (_eventListStringBuilder?.Length > 0)
            {
                _activity.AddTag(SolutionConstants.ActivityEventListColumn, _eventListStringBuilder.ToString());
                _eventListStringBuilder.Clear();
            }

            // Add FailedComponent
            if (_keyValueMap.TryGetValue(SolutionConstants.TaskFailedComponent, out var failedComponent))
            {
                _activity.AddTag(SolutionConstants.TaskFailedComponent, failedComponent);
            }

            // Add FailedReason
            if (_keyValueMap.TryGetValue(SolutionConstants.TaskFailedReason, out var failedReason))
            {
                _activity.AddTag(SolutionConstants.TaskFailedReason, failedReason);
            }

            // Add FailedDescription
            if (_keyValueMap.TryGetValue(SolutionConstants.TaskFailedDescription, out var failedDescription))
            {
                _activity.AddTag(SolutionConstants.TaskFailedDescription, failedDescription);
            }

            // Add Exceptions
            if (_exceptions?.Count > 0)
            {
                _eventListStringBuilder ??= new StringBuilder(INIT_STRING_BUILDER_SIZE);
                _eventListStringBuilder.Clear();

                int numExceptions = _exceptions.Count;
                for (int i = 0; i < numExceptions; i++)
                {
                    var exception = _exceptions[i];
                    var exceptionEventName = _exceptionEventNames![i];
                    var columnName = i == 0 ? SolutionConstants.ExceptionColumn :
                                 SolutionConstants.OtherExceptionColumnPrefix + i;

                    _activity.AddTag(columnName, exception.ToString());

                    _eventListStringBuilder.Append('[').Append(exceptionEventName).Append(']');
                    if (i > 0)
                    {
                        _eventListStringBuilder.Append('\n');
                    }
                }

                // Add Exception EventNames as separate column
                _activity.AddTag(SolutionConstants.ExceptionEventNameColumn, _eventListStringBuilder.ToString());
                _eventListStringBuilder.Clear();
            }

            // Add Continuable Exception
            if (_continuableExceptions?.Count > 0)
            {
                _eventListStringBuilder ??= new StringBuilder(INIT_STRING_BUILDER_SIZE);
                _eventListStringBuilder.Clear();

                int numExceptions = _continuableExceptions.Count;
                for (int i = 0; i < numExceptions; i++)
                {
                    var exception = _continuableExceptions[i];
                    var exceptionEventName = _continuableExceptionEventNames![i];
                    var columnName = SolutionConstants.ContinuableExceptionColumn + (i + 1);

                    _activity.AddTag(columnName, exception.ToString());

                    _eventListStringBuilder.Append('[').Append(exceptionEventName).Append(']');
                    if (i > 0)
                    {
                        _eventListStringBuilder.Append('\n');
                    }
                }

                // Add Continuable Exception EventNames as separate column
                _activity.AddTag(SolutionConstants.ContinuableExceptionEventNameColumn, _eventListStringBuilder.ToString());
                _eventListStringBuilder.Clear();
            }

            // Property Bags
            var propertiesSpan = BasicActivity.ToPropertyStringSpan(_keyValueMap.GetInternalKeyValueList, ExcludeKeysInPropertyBag, BasicActivityMonitor.ReservedCompletedEventProperiesSize);
            _keyValueMap.Clear();
            _activity.AddTag(BasicActivityMonitor.Properties, propertiesSpan.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetNotEmptyValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? BasicActivity.NoneString : value;
        }
    }
}
