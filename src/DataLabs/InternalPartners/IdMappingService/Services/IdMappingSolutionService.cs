namespace Microsoft.WindowsAzure.IdMappingService.Services
{
    using global::IdMappingService.CustomResourceTypeHandling;
    using global::IdMappingService.CustomResourceTypeHandling.Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.IdMappingService.Services.Constants;
    using Microsoft.WindowsAzure.IdMappingService.Services.Contracts;
    using Microsoft.WindowsAzure.IdMappingService.Services.Telemetry;
    using Microsoft.WindowsAzure.IdMappingService.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading.Tasks;

    public class IdMappingSolutionService : IDataLabsInterface
    {
        #region constants

        private const string microsoftTenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47"; // TODO get tenantId from actual resourceId

        public const string IdMappingLogTable = "IdMappingLogs";
        public const string IdMappingLogTableKey = "IdMappingLogTable";

        #endregion

        #region Fields

        private ILogger logger;
        private readonly ConfigMappingSpecificationService configMappingSpecificationService;
        private readonly IServiceCollection serviceCollection;

        #endregion

        #region Tracing

        private static readonly ActivityMonitorFactory GetResponseAsyncMonitorFactory = new("IdMappingSolutionService.GetResponseAsync");
        private static readonly ActivityMonitorFactory IdMappingSolutionServiceCreateIdentifierResources = new ("IdMappingSolutionService.CreateIdentifierResources");
        private static readonly ActivityMonitorFactory IdMappingSolutionServiceValidateResourceData = new ("IdMappingSolutionService.ValidateResourceData");

        #endregion

        #region Constructor

        public IdMappingSolutionService()
        {
            configMappingSpecificationService = new ConfigMappingSpecificationService();
            serviceCollection = new ServiceCollection();
        }

        #endregion

        #region IDataLabs interface implementation

        public Task<DataLabsARNV3Response> GetResponseAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recvTime = DateTimeOffset.UtcNow;

            // For subscription scoped datasets
            using var monitor = GetResponseAsyncMonitorFactory.ToMonitor();
            monitor.Activity.Properties["RequestTime"] = request.RequestTime;
            monitor.Activity.Properties["RetryCount"] = request.RetryCount;
            monitor.Activity.Properties["CorrelationId"] = request.CorrelationId;
            monitor.Activity.Properties["EventType"] = request.InputResource.EventType;
            monitor.Activity.Properties["EventTime"] = request.InputResource.EventTime;
            monitor.Activity.Properties["EventGridId"] = request.InputResource.Id;
            monitor.OnStart();

            try
            {
                var inputNotificationResource = request.InputResource.Data.Resources?.First();
                monitor.Activity.Properties["ResourceId"] = inputNotificationResource?.ResourceId;

                var outputNotificationData = CreateIdentifierResources(request.InputResource.Data, request.InputResource.EventType, monitor.Activity);
                var newlyCreatedArmId = outputNotificationData.Resources.First().ArmResource.Id;
                var outputEventGridNotification = CreateOutputEventGridNotification(outputNotificationData, request.InputResource.EventType);

                var response = new DataLabsARNV3Response(
                    DateTimeOffset.UtcNow,
                    request.CorrelationId,
                    new DataLabsARNV3SuccessResponse(outputEventGridNotification, DateTimeOffset.UtcNow, null),
                    null,
                    null);

                var resourceType = inputNotificationResource.ArmResource?.Type;
                IdMappingMetricProvider.ReportSuccessResponseMetric(resourceType, request.InputResource.EventType);
                IdMappingMetricProvider.ReportSuccessfulResponseRequestDurationMetric((long)(DateTimeOffset.UtcNow - recvTime).TotalMilliseconds, resourceType, request.InputResource.EventType);

                monitor.OnCompleted();
                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                var resourceType = request.InputResource.Data.Resources?.First()?.ArmResource?.Type;
                IdMappingMetricProvider.ReportErrorResponseMetric(resourceType, request.InputResource.EventType);
                IdMappingMetricProvider.ReportErrorResponseRequestDurationMetric((long)(DateTimeOffset.UtcNow - recvTime).TotalMilliseconds, resourceType, request.InputResource.EventType);

                var errorResponse = IdMappingUtils.BuildIdMappingErrorResponse(request, ex);
                monitor.OnError(ex);
                return Task.FromResult(errorResponse);
            }
        }

        // IdMapping will not implement thisfunction since it outouts only 1 notification per input notification
        IAsyncEnumerable<DataLabsARNV3Response> IDataLabsInterface.GetResponsesAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        List<string> IDataLabsInterface.GetTraceSourceNames()
        {
            return new List<string>();
        }

        [ExcludeFromCodeCoverage]
        List<string> IDataLabsInterface.GetMeterNames()
        {
            return new List<string> { IdMappingMetricProvider.IdMappingServiceMeter };
        }

        [ExcludeFromCodeCoverage]
        List<string> IDataLabsInterface.GetCustomerMeterNames()
        {
            return null;
        }

        [ExcludeFromCodeCoverage]
        Dictionary<string, string> IDataLabsInterface.GetLoggerTableNames()
        {
            return new Dictionary<string, string>
            {
                { IdMappingLogTableKey, IdMappingLogTable }
            };
        }

        [ExcludeFromCodeCoverage]
        void IDataLabsInterface.SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            serviceCollection.AddSingleton(loggerFactory);
            logger = loggerFactory.CreateLogger(IdMappingLogTableKey);
        }

        [ExcludeFromCodeCoverage]
        void IDataLabsInterface.SetConfiguration(IConfigurationWithCallBack configurationWithCallBack)
        {
            //throw new NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        void IDataLabsInterface.SetResourceProxyClient(IResourceProxyClient resourceProxyClient)
        {
            //throw new NotImplementedException();
        }

        #endregion

        #region Private/Internal methods

        //replacement for the "PropertyExtractionJob"
        internal NotificationDataV3<GenericResource> CreateIdentifierResources(NotificationDataV3<GenericResource> notificationData, string eventType, IActivity parentActivity)
        {
            using var monitor = IdMappingSolutionServiceCreateIdentifierResources.ToMonitor(parentActivity, false);
            monitor.Activity.Properties["resourceCount"] = notificationData.Resources.Count;
            monitor.Activity.Properties["publisherInfo"] = notificationData.PublisherInfo;
            monitor.Activity.Properties["containerType"] = notificationData.ResourcesContainer;
            monitor.OnStart();

            try
            {
                if (notificationData != null && notificationData.Resources.Count > 0)
                {
                    //assuming there is only one resource here. From Jae right now ProcessMessage will only send single resource ARN notifications, another function will be used for batched notifications
                    var resourceData = notificationData.Resources.First();
                    ResourceIdentifiers resourceIdentifiers;

                    //skip trying to extract properties for delete actions
                    if (eventType.EndsWith(IdMappingConstants.DeleteEventSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        var resourceType = ArmNotificationUtils.GetResourceTypeFromResourceData(resourceData, eventType);
                        resourceIdentifiers = new ResourceIdentifiers(resourceType, resourceData.ResourceId, resourceData.ResourceEventTime?.UtcDateTime, null);
                    }
                    else
                    {
                        ValidateResourceData(resourceData, monitor.Activity, validateProperties: true);
                        resourceIdentifiers = this.ExtractResourceProperties(resourceData, eventType, configMappingSpecificationService, monitor.Activity);
                    }

                    var result = new IdentifierNotificationInfo(
                        notificationData.ResourceLocation,
                        resourceData.CorrelationId,
                        eventType,
                        notificationData.HomeTenantId,
                        notificationData.ResourceHomeTenantId,
                        resourceIdentifiers,
                        resourceData?.ResourceSystemProperties?.CreatedTime,
                        resourceData?.ResourceSystemProperties?.ModifiedTime
                    ).ToArnNotification(monitor.Activity);
                    
                    monitor.OnCompleted();
                    return result;
                }
                else
                {
                    throw new ArgumentException("Input to CreateIdentifierResources contains no resources");
                }
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        internal void ValidateResourceData(NotificationResourceDataV3<GenericResource> resourceData, IActivity parentActivity, bool validateProperties = false)
        {
            using var monitor = IdMappingSolutionServiceValidateResourceData.ToMonitor(parentActivity, false);
            var armResource = resourceData.ArmResource;

            monitor.Activity.Properties["resourceId"] = resourceData.ResourceId;
            monitor.Activity.Properties["correlationId"] = resourceData.CorrelationId;
            monitor.Activity.Properties["resourceData.ResourceId"] = resourceData.ResourceId ?? "null";
            monitor.Activity.Properties["armResource.Id"] = armResource?.Id ?? "null";
            monitor.Activity.Properties["armResource.Type"] = armResource?.Type ?? "null";
            monitor.OnStart();

            if (armResource == null || (resourceData.ResourceId == null && armResource.Id == null) || (validateProperties && armResource.Properties == null))
            {
                object provisioningState = null;
                object armLinkedNotificationSource = null;
                resourceData?.AdditionalResourceProperties?.TryGetValue("provisioningState", out provisioningState);
                resourceData?.AdditionalResourceProperties?.TryGetValue("armLinkedNotificationSource", out armLinkedNotificationSource);

                monitor.Activity.Properties["statusCode"] = resourceData.StatusCode;
                
                monitor.Activity.Properties["provisioningState"] = provisioningState;
                monitor.Activity.Properties["armLinkedNotificationSource"] = armLinkedNotificationSource;

                IdMappingMetricProvider.ReportUnproccessableNotificationMetric(armResource?.Type, provisioningState?.ToString());
                var ex = new ArgumentException("Incoming notification is missing required resourcedata for creating mapping");
                monitor.OnError(ex);
                throw ex;
            }
            monitor.OnCompleted();
        }

        private ResourceIdentifiers ExtractResourceProperties(NotificationResourceDataV3<GenericResource> resourceData, string eventType, ConfigMappingSpecificationService specService, IActivity parentActivity)
        {
            var resource = resourceData.ArmResource;
            var resourceType = ArmNotificationUtils.GetResourceTypeFromResourceData(resourceData, eventType);
            var mappingSpecification = specService.GetInternalIdSpecification(resourceType);

            // check if customHandler is registered for this type
            var hasCustomHandler = CustomHandlerRegistration.Handlers.TryGetValue(resourceType, out var customHandler);

            // extract identifiers from config if no custom handler exists or if the handler is additive
            var identifiers = hasCustomHandler && customHandler.HandlerType == IdentifierCreationHandlerType.Overwrite
                ? new List<Identifier>()
                : PropertyExtractionService.ExtractProperties(resource, mappingSpecification, parentActivity);

            // append custom identifiers if needed
            if (hasCustomHandler)
            {
                identifiers.AddRange(customHandler.CreateIdentifiers(resourceData, mappingSpecification, parentActivity));
            }

            return new ResourceIdentifiers(
                resourceType: resource.Type,
                resourceId: resource.Id,
                resourceUpdateTimestamp: resourceData.ResourceEventTime?.UtcDateTime,
                identifiers
            );
        }

        private EventGridNotification<NotificationDataV3<GenericResource>> CreateOutputEventGridNotification(NotificationDataV3<GenericResource> notificationData, string eventType)
        {
            var resourceId = notificationData.Resources.First().ArmResource.Id;
            return new EventGridNotification<NotificationDataV3<GenericResource>>(
                id: Guid.NewGuid().ToString(),
                topic: "Mock Topic to be overwritten",
                subject: resourceId,
                eventType: IdentifierNotificationInfo.GetIdentifierEventType(eventType),
                eventTime: DateTimeOffset.UtcNow,
                data: notificationData);
        }

        #endregion
    }
}
