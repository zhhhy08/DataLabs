namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputChannel.SubTasks
{
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Clients.Contracts;
    using System.Collections.Generic;

    public class BlobPayloadRoutingTaskProcessorFactory : AbstractArnPublishTaskProcessorFactory<ARNSingleInputMessage>
    {
        #region Fields

        private static readonly ILogger<BlobPayloadRoutingTaskProcessorFactory> Logger =
            DataLabLoggerFactory.CreateLogger<BlobPayloadRoutingTaskProcessorFactory>();

        #endregion

        #region Constructors

        public BlobPayloadRoutingTaskProcessorFactory(IArnNotificationClient arnNotificationClient)
            : base(IOComponent.BlobPayloadRoutingChannel, arnNotificationClient, Logger, AdditionalGroupingProperties.Arn_Routing_Location)
        {
        }

        #endregion

        #region Public Methods

        public override IBufferedTaskProcessor<IOEventTaskContext<ARNSingleInputMessage>> CreateBufferedTaskProcessor()
        {
            return new BlobPayloadRoutingTaskProcessor(this);
        }

        #endregion

        #region Wrapping Clases

        private class BlobPayloadRoutingTaskProcessor : AbstractArnPublishTaskProcessor
        {
            public BlobPayloadRoutingTaskProcessor(BlobPayloadRoutingTaskProcessorFactory taskProcessorFactory)
                : base(taskProcessorFactory, new ActivityMonitorFactory("BlobPayloadRoutingTaskProcessor"))
            {
            }

            #region Protected Methods

            protected override void ExtractEventGridNotifications(
                IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<ARNSingleInputMessage>>> eventTaskContexts,
                in List<EventGridNotification<NotificationDataV3<GenericResource>>> eventGridNotifications)
            {
                for (int i = 0, n = eventTaskContexts.Count; i < n; i++)
                {
                    var eventTaskContext = eventTaskContexts[i];

                    // Check if eventTaskContext already has next Channel
                    if (eventTaskContext.NextTaskChannel != null)
                    {
                        continue;
                    }

                    var notification = eventTaskContext.TaskContext.InputMessage?.DeserializedObject?.NotificationDataV3;
                    if (notification == null)
                    {
                        // Empty Response
                        HandleEmptyData(eventTaskContext);
                        continue;
                    }

                    eventGridNotifications.Add(notification);
                }
            }

            #endregion
        }

        #endregion
    }
}
