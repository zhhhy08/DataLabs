namespace ResourceAliasService
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class ResourceAliasSolutionService : IDataLabsInterface
    {
        /* OpenTelemetry Trace */
        public const string PartnerActivitySourceName = "ResourceAlias";
        public static readonly ActivitySource PartnerActivitySource = new ActivitySource(PartnerActivitySourceName);

        /* OpenTelemetry Metric */
        public const string PartnerMeterName = "ResourceAlias";
        public static readonly Meter PartnerMeter = new(PartnerMeterName, "1.0");
        public static readonly Histogram<int> DurationMetric = PartnerMeter.CreateHistogram<int>("Duration");
        public static readonly Counter<long> RequestCounter = PartnerMeter.CreateCounter<long>("Request");
        public static readonly Counter<long> SuccessCounter = PartnerMeter.CreateCounter<long>("Success");
        public static readonly Counter<long> RetryCounter = PartnerMeter.CreateCounter<long>("Retry");
        public static readonly Counter<long> FailureCounter = PartnerMeter.CreateCounter<long>("Failure");
        public static readonly Counter<long> IdMappingCallErrorCounter = PartnerMeter.CreateCounter<long>("IdMappingCallError");

        public const string CustomerMeterName = "ResourceAliasCustomer";
        public static readonly Meter CustomerMeter = new(CustomerMeterName);
        public const string ResourceAliasLogTable = "ResourceAliasLogTable";
        private static readonly ActivityMonitorFactory GetResponseAsyncMonitorFactory = new("ResourceAliasSolutionService.GetResponseAsync");
        public const string MappingNotExistsErrorMessage = "Mapping not exists in IdMapping response.";
        public int MaxRetryCount;
        public const string MaxRetryCountKey = "MaxRetryCount";
        public const string ResourceAliasPattern = @"^/?providers/microsoft\.idmapping/aliases/default/namespaces/([^/]+)/types/([^/]+)/identifiers/([^/]+)/values/([^/]+)";

        private readonly IServiceCollection serviceCollection;
        private IServiceProvider serviceProvider;

        public ResourceAliasSolutionService()
        {
            serviceCollection = new ServiceCollection();
        }

        public async Task<DataLabsARNV3Response> GetResponseAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            GuardHelper.ArgumentNotNull(request, nameof(request));

            using var monitor = GetResponseAsyncMonitorFactory.ToMonitor();
            monitor.Activity.Properties["RequestTime"] = request.RequestTime;
            monitor.Activity.Properties["RetryCount"] = request.RetryCount;
            monitor.Activity.Properties["CorrelationId"] = request.CorrelationId;
            monitor.Activity.Properties["PayloadId"] = request.InputResource.Id;
            monitor.Activity.Properties["EventType"] = request.InputResource.EventType;
            monitor.Activity.Properties["EventTime"] = request.InputResource.EventTime;
            monitor.Activity[SolutionConstants.PartnerTraceId] = request.TraceId;
            monitor.OnStart();

            RequestCounter.Add(1);
            var stopWatchStartTime = Stopwatch.GetTimestamp();

            var allResolutionsSuccess = true;
            var resourceAliasesToResolve = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resourceAliasRegex = new Regex(ResourceAliasPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var subject = request.InputResource.Subject.Trim();
            monitor.Activity.Properties["Subject"] = subject;
            var (aliasForResolutionInSubject, aliasSuffixSubject) = ExtractAliasForResolution(subject, resourceAliasRegex);
            var isResolutionRequiredForSubject = aliasForResolutionInSubject != null;

            if (isResolutionRequiredForSubject)
            {
                resourceAliasesToResolve.Add(aliasForResolutionInSubject);
            }

            var resource = request.InputResource.Data.Resources?.First();
            GuardHelper.ArgumentNotNull(resource, nameof(resource));
            var resourceId = resource.ResourceId.Trim();
            monitor.Activity.Properties["ResourceId"] = resourceId;

            object systemMetaDataObj = null;
            resource.AdditionalResourceProperties?.TryGetValue("system", out systemMetaDataObj);
            var systemMetadata = systemMetaDataObj != null ? JsonConvert.DeserializeObject<SystemMetadata>(systemMetaDataObj.ToString()) : null;
            var resourceAliasResolution = systemMetadata?.Aliases?.ResourceId;
            var isResolutionRequiredForResourceId = resourceAliasResolution?.State == null || resourceAliasResolution.State == ResourceAliasResolutionState.Original || resourceAliasResolution.State == ResourceAliasResolutionState.Unresolved;
            var (aliasForResolutionInResource, aliasSuffixInResource) = ExtractAliasForResolution(resourceId, resourceAliasRegex);

            if (isResolutionRequiredForResourceId)
            {
                if (aliasForResolutionInResource == null) // alias for resolution cannot be extracted from resource id, send failure response
                {
                    resourceAliasResolution = new ResourceAliasResolution
                    {
                        State = ResourceAliasResolutionState.Unresolved,
                        ErrorMessage = "Resource Id does not contain correctly formatted alias."
                    };
                    systemMetadata = UpdateSystemMetadataObject(systemMetadata, resourceAliasResolution);

                    UpdateMonitorOnFailure();
                    return GenerateFailureResponse(request, systemMetadata);
                }

                resourceAliasesToResolve.Add(aliasForResolutionInResource);
            }

            var idMappingServiceAgent = serviceProvider.GetService<IIdMappingServiceAgent>();
            var idMappingResult = await idMappingServiceAgent.GetArmIdsFromIdMapping(resourceAliasesToResolve, resource.ArmResource.Type, resource.CorrelationId, request.RetryCount, monitor.Activity, true, cancellationToken).IgnoreContext(); // result from idmapping service
            var idMappingDict = idMappingResult.IdMappings.ToDictionary(x => x.AliasResourceId, x => (x.AliasResourceId, x.ArmIds, x.StatusCode, x.ErrorMessage), StringComparer.OrdinalIgnoreCase);
            string subjectResolutionErrorMessage = null;

            if (isResolutionRequiredForSubject)
            {
                var mappingExists = idMappingDict.TryGetValue(aliasForResolutionInSubject, out var mappingResult);
                monitor.Activity.Properties["SubjectMappingStatusCode"] = mappingExists ? mappingResult.StatusCode : "MissingFromResponse";
                monitor.Activity.Properties["SubjectMappingResultCount"] = mappingExists ? mappingResult.ArmIds.Count : 0;

                if (mappingExists && mappingResult.StatusCode.Equals(ActivityStatusCode.Ok.ToString(), StringComparison.OrdinalIgnoreCase) && mappingResult.ArmIds.Count > 0)
                {
                    var resolvedSubject = mappingResult.ArmIds.Order().First(); // alias can be resolved to multiple armIds, here picking the first sorted ascendingly
                    subject = resolvedSubject + aliasSuffixSubject; // resolved subject

                    monitor.Activity.Properties["ResolvedSubject"] = subject;
                    monitor.Activity.Properties["MultipleMappingForSubject"] = mappingResult.ArmIds.Count > 1 ? "true" : "false";
                }
                else
                {
                    allResolutionsSuccess = false;
                    subjectResolutionErrorMessage = "SubjectResolutionError: " + (mappingExists && mappingResult.ErrorMessage != null ? mappingResult.ErrorMessage : MappingNotExistsErrorMessage);
                    monitor.Activity.Properties["ResolutionError"] = subjectResolutionErrorMessage;
                }
            }

            if (isResolutionRequiredForResourceId)
            {
                var mappingExists = idMappingDict.TryGetValue(aliasForResolutionInResource, out var mappingResult);
                monitor.Activity.Properties["ResourceIdMappingStatusCode"] = mappingExists ? mappingResult.StatusCode : "MissingFromResponse";
                monitor.Activity.Properties["ResourceIdMappingResultCount"] = mappingExists ? mappingResult.ArmIds.Count : 0;

                if (allResolutionsSuccess && // only mark resolution as successful and modify the resource id when subject resolution is also successful
                    mappingExists && mappingResult.StatusCode.Equals(ActivityStatusCode.Ok.ToString(), StringComparison.OrdinalIgnoreCase) && mappingResult.ArmIds.Count > 0)
                {
                    resourceAliasResolution = new ResourceAliasResolution
                    {
                        State = mappingResult.ArmIds.Count > 1 ? ResourceAliasResolutionState.Multiple : ResourceAliasResolutionState.Resolved,
                        Id = resourceId, // set the original resource id in metadata
                        ErrorMessage = null
                    };
                    systemMetadata = UpdateSystemMetadataObject(systemMetadata, resourceAliasResolution);

                    var resolvedArmResourceId = mappingResult.ArmIds.Order().First();
                    resourceId = resolvedArmResourceId + aliasSuffixInResource; // resolved resourceId

                    monitor.Activity.Properties["ResolvedResourceId"] = resourceId;
                    monitor.Activity.Properties["MultipleMappingForResourceId"] = mappingResult.ArmIds.Count > 1 ? "true" : "false";
                }
                else
                {
                    var errorMessage = allResolutionsSuccess ? (mappingExists && mappingResult.ErrorMessage != null ? mappingResult.ErrorMessage : MappingNotExistsErrorMessage) : subjectResolutionErrorMessage;
                    resourceAliasResolution = new ResourceAliasResolution
                    {
                        State = ResourceAliasResolutionState.Unresolved,
                        ErrorMessage = errorMessage
                    };
                    systemMetadata = UpdateSystemMetadataObject(systemMetadata, resourceAliasResolution);

                    allResolutionsSuccess = false;
                    monitor.Activity.Properties["ResolutionError"] = errorMessage;
                }
            }

            if (allResolutionsSuccess) // resolution success, emit success notification
            {
                UpdateMonitorOnSuccess();
                return GenerateSuccessResponse(request, resourceId, subject, systemMetadata);
            }
            else
            {
                if (request.RetryCount < MaxRetryCount) // fail with retry, return error response
                {
                    var errorDescription = $"Not all resource alias been resolved for payload with id {request.InputResource.Id} on retry number {request.RetryCount}. Retry triggered.";
                    UpdateMonitorOnError(errorDescription);
                    return GenerateErrorResponse(request, errorDescription);
                }
                else // resolution failure, emit failure notification
                {
                    UpdateMonitorOnFailure();
                    return GenerateFailureResponse(request, systemMetadata);
                }
            }

            void UpdateMonitorOnSuccess()
            {
                var duration = Stopwatch.GetElapsedTime(stopWatchStartTime).TotalMilliseconds;
                DurationMetric.Record((int)duration);
                SuccessCounter.Add(1, new KeyValuePair<string, object?>("retryCount", request.RetryCount));
                monitor.OnCompleted();
            }

            void UpdateMonitorOnFailure()
            {
                FailureCounter.Add(1);
                monitor.Activity.Properties["ErrorType"] = "FAILURE";
                monitor.OnCompleted();
            }

            void UpdateMonitorOnError(string errorDescription)
            {
                RetryCounter.Add(1, new KeyValuePair<string, object?>("retryCount", request.RetryCount));
                monitor.Activity.Properties["ErrorType"] = DataLabsErrorType.RETRY.ToString();
                monitor.OnError(new Exception(errorDescription));
            }
        }

        public async IAsyncEnumerable<DataLabsARNV3Response> GetResponsesAsync(DataLabsARNV3Request request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var response = await GetResponseAsync(request, cancellationToken).IgnoreContext();
            yield return response;
        }

        public void SetConfiguration(IConfigurationWithCallBack configurationWithCallBack)
        {
            GuardHelper.ArgumentNotNull(configurationWithCallBack);
            serviceCollection.AddSingleton(configurationWithCallBack);
            MaxRetryCount = configurationWithCallBack.GetValueWithCallBack<int>(MaxRetryCountKey, UpdateMaxRetryCount, 3);
        }

        private Task UpdateMaxRetryCount(int newMaxRetryCount)
        {
            var oldMaxRetryCount = MaxRetryCount;
            if (newMaxRetryCount == oldMaxRetryCount)
            {
                return Task.CompletedTask;
            }

            Interlocked.Exchange(ref MaxRetryCount, newMaxRetryCount);

            return Task.CompletedTask;
        }

        public void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            GuardHelper.ArgumentNotNull(loggerFactory);
            serviceCollection.AddSingleton(loggerFactory);
        }

        public List<string> GetTraceSourceNames()
        {
            var list = new List<string>(1)
            {
                PartnerActivitySourceName
            };
            return list;
        }

        public List<string> GetMeterNames()
        {
            var list = new List<string>(1)
            {
                PartnerMeterName
            };
            return list;
        }

        public List<string> GetCustomerMeterNames()
        {
            return new List<string> { CustomerMeterName };
        }

        public Dictionary<string, string> GetLoggerTableNames()
        {
            return new Dictionary<string, string>
            {
                [ResourceAliasLogTable] = ResourceAliasLogTable
            };
        }

        public void SetCacheClient(ICacheClient cacheClient)
        {
            GuardHelper.ArgumentNotNull(cacheClient);
            serviceCollection.AddSingleton(cacheClient);
        }

        public void SetResourceProxyClient(IResourceProxyClient resourceProxyClient)
        {
            GuardHelper.ArgumentNotNull(resourceProxyClient);
            serviceCollection.AddSingleton(resourceProxyClient);
            serviceCollection.AddSingleton<IIdMappingServiceAgent, IdMappingServiceAgent>();

            InitializeServiceProvider(serviceCollection);
        }

        public void InitializeServiceProvider(IServiceCollection serviceCollection)
        {
            GuardHelper.ArgumentNotNull(serviceCollection);
            serviceProvider = serviceCollection.BuildServiceProvider();
        }

        private (string, string) ExtractAliasForResolution(string resourceId, Regex resourceAliasRegex)
        {
            var resourceAliasMatch = resourceAliasRegex.Match(resourceId);
            if (resourceAliasMatch.Success)
            {
                var aliasToResolve = resourceAliasMatch.Value;

                var remainderStartIndex = resourceAliasMatch.Index + resourceAliasMatch.Length;
                var aliasSuffix = resourceId.Substring(remainderStartIndex);

                return (aliasToResolve, aliasSuffix);
            }
            else
            {
                return (null, null);
            }
        }

        private SystemMetadata UpdateSystemMetadataObject(SystemMetadata systemMetadata, ResourceAliasResolution resourceAliasResolution)
        {
            systemMetadata = systemMetadata ?? new SystemMetadata();
            systemMetadata.Aliases = systemMetadata.Aliases ?? new AliasMetadata();
            systemMetadata.Aliases.ResourceId = resourceAliasResolution;

            return systemMetadata;
        }

        private DataLabsARNV3Response GenerateSuccessResponse(DataLabsARNV3Request request, string resolvedResourceId, string subject, SystemMetadata systemMetadataWithSuccessState)
        {
            var resource = request.InputResource.Data.Resources.First();
            var resolvedResource = ResourceAliasUtils.CloneNotificationResourceDataV3WithNewResourceId(resource, resolvedResourceId, systemMetadataWithSuccessState); // create new NotificationResourceData with arm id from idmapping
            var updatedNotificationDataV3 = ResourceAliasUtils.CloneNotificationDataV3WithNewResources(request.InputResource.Data, new List<NotificationResourceDataV3<GenericResource>> { resolvedResource });
            var successNotification = new EventGridNotification<NotificationDataV3<GenericResource>>(
                id: request.InputResource.Id,
                topic: request.InputResource.Topic,
                subject: subject,
                eventType: request.InputResource.EventType,
                eventTime: request.InputResource.EventTime,
                data: updatedNotificationDataV3);

            var successResponse = new DataLabsARNV3SuccessResponse(
                successNotification,
                DateTimeOffset.UtcNow,
                null);

            return new DataLabsARNV3Response(
                DateTimeOffset.UtcNow,
                request.CorrelationId,
                successResponse,
                null,
                null);
        }

        private DataLabsARNV3Response GenerateFailureResponse(DataLabsARNV3Request request, SystemMetadata systemMetadataWithFailureState)
        {
            var resource = request.InputResource.Data.Resources.First();
            var unresolvedResource = ResourceAliasUtils.CloneNotificationResourceDataV3WithNewResourceId(resource, resource.ResourceId, systemMetadataWithFailureState); // create new NotificationResourceData with original resource id and new resourceAliasResolutionState
            var failureNotificationDataV3 = ResourceAliasUtils.CloneNotificationDataV3WithNewResources(request.InputResource.Data, new List<NotificationResourceDataV3<GenericResource>> { unresolvedResource });
            var failureNotification = new EventGridNotification<NotificationDataV3<GenericResource>>(
                id: request.InputResource.Id,
                topic: request.InputResource.Topic,
                subject: request.InputResource.Subject,
                eventType: request.InputResource.EventType,
                eventTime: request.InputResource.EventTime,
                data: failureNotificationDataV3);

            var failureResponse = new DataLabsARNV3SuccessResponse(
                failureNotification,
                DateTimeOffset.UtcNow,
            null);

            return new DataLabsARNV3Response(
                DateTimeOffset.UtcNow,
                request.CorrelationId,
                failureResponse,
                null,
                null);
        }


        private DataLabsARNV3Response GenerateErrorResponse(DataLabsARNV3Request request, string errorDescription)
        {
            var errorResponse = new DataLabsErrorResponse(
                DataLabsErrorType.RETRY,
                request.RetryCount * 5000,
                HttpStatusCode.InternalServerError.ToString(),
                errorDescription,
                "GetArmIdsFromIdMapping");

            return new DataLabsARNV3Response(
                DateTimeOffset.UtcNow,
                request.CorrelationId,
                null,
                errorResponse,
                null);
        }

        public class SystemMetadata
        {
            public AliasMetadata Aliases;
        }

        public class AliasMetadata
        {
            public ResourceAliasResolution ResourceId;
        }

        public class ResourceAliasResolution
        {
            public ResourceAliasResolutionState State;
            public string Id;
            public string ErrorMessage;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum ResourceAliasResolutionState
        {
            Original, // resource alias requires resolution
            Resolved, // resource alias resolved to arm id
            Multiple, // resource alias resolved to arm id and has multiple resolutions
            Unresolved, // resource alias resolution attempted but the resolution failed
        }
    }
}
