namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel.OutputChannel.SubTasks
{
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Clients.Contracts;
    using System.Collections.Generic;

    public class ArnPublishTaskProcessorFactory<TInput> : AbstractArnPublishTaskProcessorFactory<TInput>
        where TInput : IInputMessage
    {
        #region Fields

        private static readonly ILogger<ArnPublishTaskProcessorFactory<TInput>> Logger =
            DataLabLoggerFactory.CreateLogger<ArnPublishTaskProcessorFactory<TInput>>();

        #endregion

        #region Constructors

        public ArnPublishTaskProcessorFactory(IArnNotificationClient arnNotificationClient)
            : base(IOComponent.ArnPublish, arnNotificationClient, Logger, AdditionalGroupingProperties.None)
        {
        }

        #endregion

        #region Public Methods

        public override IBufferedTaskProcessor<IOEventTaskContext<TInput>> CreateBufferedTaskProcessor()
        {
            return new ArnPublishTaskProcessor(this);
        }

        #endregion

        #region Wrapping Clases

        private class ArnPublishTaskProcessor : AbstractArnPublishTaskProcessor
        {
            public ArnPublishTaskProcessor(ArnPublishTaskProcessorFactory<TInput> taskProcessorFactory)
                : base(taskProcessorFactory, new ActivityMonitorFactory("ArnPublishTaskProcessor"))
            {
            }

            #region Protected Methods

            protected override void ExtractEventGridNotifications(
                IReadOnlyList<AbstractEventTaskContext<IOEventTaskContext<TInput>>> eventTaskContexts,
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

                    var outputMessageBinary = eventTaskContext.TaskContext?.OutputMessage?.GetOutputMessage();
                    if (outputMessageBinary == null || outputMessageBinary.ToMemory().Length == 0)
                    {
                        // Empty Response
                        HandleEmptyData(eventTaskContext);
                        continue;
                    }

                    // Create EventData
                    eventGridNotifications.AddRange(SerializationHelper.DeserializeArnV3Notification(outputMessageBinary, false));
                }
            }

            #endregion
        }

        #endregion
    }
}
