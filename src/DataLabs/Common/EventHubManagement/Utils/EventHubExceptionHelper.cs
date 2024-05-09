namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventHubManagement
{
    using global::Azure.Messaging.EventHubs;
    using System;

    public class EventHubExceptionHelper
    {
        public static bool IsServerBusyException(Exception ex)
        {
            var eventHubsException = ex as EventHubsException ?? ex?.InnerException as EventHubsException;
            return eventHubsException != null &&
                eventHubsException.Reason == EventHubsException.FailureReason.ServiceBusy;
        }

        public static bool IsServerTimeoutException(Exception ex)
        {
            var eventHubsException = ex as EventHubsException ?? ex?.InnerException as EventHubsException;
            return eventHubsException != null &&
                eventHubsException.Reason == EventHubsException.FailureReason.ServiceTimeout;
        }

        public static bool IsQuotaExceeded(Exception ex)
        {
            var eventHubsException = ex as EventHubsException ?? ex?.InnerException as EventHubsException;
            return eventHubsException != null &&
                eventHubsException.Reason == EventHubsException.FailureReason.QuotaExceeded;
        }

        public static bool IsTransientError(Exception ex)
        {
            var eventHubsException = ex as EventHubsException ?? ex?.InnerException as EventHubsException;
            return eventHubsException != null && eventHubsException.IsTransient;
        }

        public static bool IsQueueMessageTooLargeException(Exception ex)
        {
            var eventHubsException = ex as EventHubsException ?? ex?.InnerException as EventHubsException;
            return eventHubsException != null &&
                eventHubsException.Reason == EventHubsException.FailureReason.MessageSizeExceeded;
        }
    }
}
